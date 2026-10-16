using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    public class ServicoEntrada(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        ReconhecedorPlaca reconhecedor,
        TimeProvider relogio)
    {
        public async Task<EntradaDto> RegistrarPorImagemAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default)
        {
            await Inadimplencia.ExigirSemBloqueioAsync(db, contextoPatio.EmpresaId, relogio, "Novas entradas", ct);
            var resultado = await reconhecedor.ReconhecerAsync(imagem, nomeArquivo, "entrada", ct);
            return await RegistrarAsync(
                new RegistrarEntradaRequisicao(resultado.Placa!, resultado.Confianca, resultado.Metodo), ct);
        }

        // Dois balcões ao mesmo tempo podem escolher a mesma vaga (ou a mesma placa): quem perde a corrida
        // tenta de novo com a situação atualizada, e cai na próxima vaga livre ou no "já está estacionado"
        private const int TentativasEntrada = 3;

        public async Task<EntradaDto> RegistrarAsync(RegistrarEntradaRequisicao requisicao, CancellationToken ct = default)
        {
            for (var tentativa = 1; ; tentativa++)
            {
                try
                {
                    return await TentarRegistrarAsync(requisicao, ct);
                }
                catch (Exception e) when (ErrosBanco.Concorrencia(e))
                {
                    if (tentativa == TentativasEntrada)
                        throw new ExcecaoConflito("Muitas entradas ao mesmo tempo neste pátio. Tente novamente.");
                    db.ChangeTracker.Clear(); // descarta o que não foi gravado e relê vagas e entradas
                }
            }
        }

        private async Task<EntradaDto> TentarRegistrarAsync(RegistrarEntradaRequisicao requisicao, CancellationToken ct)
        {
            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");

            await Inadimplencia.ExigirSemBloqueioAsync(db, contextoPatio.EmpresaId, relogio, "Novas entradas", ct);
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var jaEstacionado = await db.EntradasVeiculos
                .AnyAsync(e => e.PatioId == patioId && e.Placa == placa && e.Situacao == SituacaoEntrada.Ativa, ct);
            if (jaEstacionado)
            {
                await RecusarAsync(patioId, placa, agora, "veiculo_ja_estacionado", ct);
                throw new ExcecaoConflito($"O veículo {placa} já está estacionado.");
            }

            // Mensalista em dia usa qualquer vaga livre (inclusive as reservadas);
            // avulso (ou mensalista vencido) só entra se sobrar vaga além das reservadas ainda não usadas
            var mensalista = await ServicoMensalistas.BuscarPorPlacaAsync(db, patioId, placa, ct);
            var hoje = ServicoMensalistas.Hoje(agora);
            var mensalistaEmDia = mensalista is not null && ServicoMensalistas.EmDia(mensalista, hoje);

            var vagasLivres = await db.Vagas.CountAsync(v => v.PatioId == patioId && v.Ativa && v.Situacao == SituacaoVaga.Disponivel, ct);
            if (vagasLivres == 0)
            {
                await RecusarAsync(patioId, placa, agora, "patio_lotado", ct);
                throw new ExcecaoConflito("Pátio lotado: não há vagas disponíveis.");
            }
            if (!mensalistaEmDia)
            {
                var reservadas = await db.Patios.Where(p => p.Id == patioId).Select(p => p.VagasReservadasMensalistas).FirstAsync(ct);
                var reservadasLivres = Math.Max(0, reservadas - await ServicoMensalistas.ContarMensalistasEstacionadosAsync(db, patioId, ct));
                if (vagasLivres <= reservadasLivres)
                {
                    await RecusarAsync(patioId, placa, agora, "lotado_para_avulsos", ct);
                    throw new ExcecaoConflito(mensalista is null
                        ? "Pátio lotado para avulsos: as vagas restantes são reservadas a mensalistas."
                        : $"Mensalidade de {mensalista.Nome} vencida e as vagas restantes são reservadas a mensalistas em dia.");
                }
            }

            var vaga = await db.Vagas
                .Where(v => v.PatioId == patioId && v.Ativa && v.Situacao == SituacaoVaga.Disponivel)
                .OrderBy(v => v.Fileira).ThenBy(v => v.Indice)
                .FirstAsync(ct);

            var veiculo = await db.Veiculos.FirstOrDefaultAsync(v => v.PatioId == patioId && v.Placa == placa, ct);
            if (veiculo is null)
            {
                veiculo = new Veiculo { PatioId = patioId, Placa = placa, CriadoEm = agora, AtualizadoEm = agora };
                db.Veiculos.Add(veiculo);
            }

            var entrada = new EntradaVeiculo
            {
                PatioId = patioId,
                CodigoTicket = await GerarCodigoTicketAsync(patioId, ct),
                TokenAcesso = TokenSeguro.Gerar(TokenSeguro.BytesTicket),
                MensalistaId = mensalistaEmDia ? mensalista!.Id : null,
                VeiculoId = veiculo.Id,
                Placa = placa,
                VagaId = vaga.Id,
                DataHoraEntrada = agora,
                UrlFotoEntrada = requisicao.UrlFoto,
                MetodoReconhecimento = requisicao.MetodoReconhecimento,
                Confianca = requisicao.Confianca,
                Situacao = SituacaoEntrada.Ativa,
                CriadoEm = agora
            };
            db.EntradasVeiculos.Add(entrada);

            vaga.Disponivel = false;
            vaga.Ocupada = true;
            vaga.Situacao = SituacaoVaga.Ocupada;
            vaga.VeiculoAtualId = veiculo.Id;
            vaga.OcupadaDesde = agora;
            vaga.AtualizadoEm = agora;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaSucesso, SituacaoAuditoria.Sucesso, placa, agora,
                new()
                {
                    ["vaga"] = vaga.Numero,
                    ["confianca"] = requisicao.Confianca,
                    ["metodo"] = requisicao.MetodoReconhecimento ?? ""
                });

            await db.SaveChangesAsync(ct);
            await cache.InvalidarAsync(patioId);

            return new EntradaDto(entrada.Id, placa, agora, entrada.UrlFotoEntrada, entrada.MetodoReconhecimento,
                entrada.Confianca, vaga.Id, vaga.Numero, entrada.Situacao,
                Mensalista: mensalista?.Nome,
                SituacaoMensalista: mensalista is null ? null : ServicoMensalistas.Situacao(mensalista, hoje),
                MensalidadeValidaAte: mensalista?.ValidoAte);
        }

        // Cancela uma entrada ativa sem registrar saída nem cobrança: placa digitada errada, entrada em duplicidade,
        // veículo que saiu sem passar pelo sistema. Libera a vaga e fica na auditoria com o motivo e o valor que seria cobrado.
        public async Task CancelarAsync(Guid entradaId, CancelarEntradaRequisicao requisicao, CancellationToken ct = default)
        {
            var motivo = requisicao.Motivo?.Trim();
            if (string.IsNullOrEmpty(motivo))
                throw new ExcecaoNegocio("Informe o motivo do cancelamento.");
            if (motivo.Length > SituacaoEntrada.TamanhoMaximoMotivo)
                throw new ExcecaoNegocio($"O motivo deve ter no máximo {SituacaoEntrada.TamanhoMaximoMotivo} caracteres.");

            var usuarioId = contextoPatio.UsuarioId ?? throw new ExcecaoNaoAutorizado("Usuário não identificado.");
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .Include(e => e.Patio)
                .FirstOrDefaultAsync(e => e.Id == entradaId && e.PatioId == patioId, ct)
                ?? throw new ExcecaoNaoEncontrado("Entrada não encontrada.");
            var agora = relogio.GetUtcNow().UtcDateTime;

            if (entrada.Situacao != SituacaoEntrada.Ativa)
                throw new ExcecaoConflito($"A entrada de {entrada.Placa} não está mais ativa (saída registrada ou já cancelada).");

            var permanencia = agora - entrada.DataHoraEntrada;
            if (contextoPatio.Perfil != Perfis.Admin && permanencia > TimeSpan.FromMinutes(SituacaoEntrada.MinutosCancelamentoOperador))
                throw new ExcecaoProibido(
                    $"Passados {SituacaoEntrada.MinutosCancelamentoOperador} minutos da entrada, só o administrador pode cancelá-la.");

            var pendentes = await db.CobrancasSaida
                .Where(c => c.EntradaVeiculoId == entrada.Id && c.Situacao == SituacaoCobranca.Pendente)
                .ToListAsync(ct);
            foreach (var cobranca in pendentes)
                cobranca.Situacao = SituacaoCobranca.Cancelada;

            entrada.Situacao = SituacaoEntrada.Cancelada;
            entrada.CanceladaEm = agora;
            entrada.CanceladaPorId = usuarioId;
            entrada.MotivoCancelamento = motivo;

            var vaga = entrada.Vaga;
            if (vaga is not null)
            {
                vaga.Disponivel = true;
                vaga.Ocupada = false;
                vaga.Situacao = SituacaoVaga.Disponivel;
                vaga.VeiculoAtualId = null;
                vaga.OcupadaDesde = null;
                vaga.AtualizadoEm = agora;
            }

            var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, TabelaPreco.Do(entrada.Patio), entrada.Patio.MinutosTolerancia);
            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaCancelada, SituacaoAuditoria.Alerta, entrada.Placa, agora,
                new()
                {
                    ["motivo"] = motivo,
                    ["usuarioId"] = usuarioId.ToString(),
                    ["perfil"] = contextoPatio.Perfil ?? "",
                    ["vaga"] = vaga?.Numero ?? "",
                    ["duracao"] = tarifa.DuracaoFormatada,
                    ["valorNaoCobrado"] = entrada.MensalistaId is null ? tarifa.ValorTotal : 0m
                });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ExcecaoConflito("A saída deste veículo está sendo processada ao mesmo tempo. Atualize e tente novamente.");
            }

            await cache.InvalidarAsync(patioId);
        }

        private async Task<string> GerarCodigoTicketAsync(Guid patioId, CancellationToken ct)
        {
            // ~1 trilhão de combinações: colisão é improvável, mas confere mesmo assim
            for (var tentativa = 0; tentativa < 5; tentativa++)
            {
                var codigo = CodigoTicket.Gerar();
                if (!await db.EntradasVeiculos.AnyAsync(e => e.PatioId == patioId && e.CodigoTicket == codigo, ct))
                    return codigo;
            }
            throw new InvalidOperationException("Não foi possível gerar um código de ticket único.");
        }

        private async Task RecusarAsync(Guid patioId, string placa, DateTime agora, string motivo, CancellationToken ct)
        {
            Auditoria.Registrar(db, patioId, AcoesAuditoria.EntradaRecusada, SituacaoAuditoria.Alerta, placa, agora,
                new() { ["motivo"] = motivo });
            await db.SaveChangesAsync(ct);
        }
    }
}
