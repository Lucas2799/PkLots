using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Application.Pagamentos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Saída em dois passos: a COBRANÇA calcula e congela o valor; a CONFIRMAÇÃO registra o pagamento e libera.
    // Sem pagamento confirmado não há saída (exceto valor zero: mensalista em dia ou tolerância).
    public class ServicoSaida(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        ReconhecedorPlaca reconhecedor,
        TimeProvider relogio)
    {
        public static readonly TimeSpan ValidadeCobranca = TimeSpan.FromMinutes(15);

        // ---------- 1) Cobrança ----------

        public async Task<CobrancaDto> CobrarAsync(GerarCobrancaRequisicao requisicao, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(requisicao.CodigoTicket))
                return await GerarCobrancaAsync(await BuscarPorTicketAsync(requisicao.CodigoTicket, ct), "ticket", ct);

            var placa = ValidadorPlaca.Normalizar(requisicao.Placa)
                ?? throw new ExcecaoNegocio("Placa inválida. Formatos aceitos: ABC1234 ou ABC1D23.");
            return await GerarCobrancaAsync(await BuscarPorPlacaAsync(placa, null, ct), "placa", ct);
        }

        public async Task<CobrancaDto> CobrarPorImagemAsync(Stream imagem, string nomeArquivo, CancellationToken ct = default)
        {
            var resultado = await reconhecedor.ReconhecerAsync(imagem, nomeArquivo, "saida", ct);
            return await GerarCobrancaAsync(await BuscarPorPlacaAsync(resultado.Placa!, resultado.Confianca, ct), "imagem", ct);
        }

        private async Task<CobrancaDto> GerarCobrancaAsync(EntradaVeiculo entrada, string origem, CancellationToken ct)
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var patio = await db.Patios.FirstAsync(p => p.Id == entrada.PatioId, ct);
            var config = await ServicoConfiguracaoPagamento.ObterDaEmpresaAsync(db, patio.EmpresaId, ct);

            // Reaproveita a cobrança pendente ainda válida (mesmo valor e mesmo QR enquanto o cliente paga)
            var pendentes = await db.CobrancasSaida
                .Where(c => c.EntradaVeiculoId == entrada.Id && c.Situacao == SituacaoCobranca.Pendente)
                .ToListAsync(ct);
            var vigente = pendentes.FirstOrDefault(c => c.ExpiraEm > agora);
            if (vigente is not null)
                return ParaDto(vigente, entrada, patio, config);

            foreach (var expirada in pendentes)
                expirada.Situacao = SituacaoCobranca.Cancelada;

            var tarifa = CalculadoraTarifa.Calcular(entrada.DataHoraEntrada, agora, patio.ValorHora, patio.MinutosTolerancia);

            // Mensalista: sem cobrança se entrou em dia ou está em dia agora; vencido paga como avulso
            var mensalista = await ServicoMensalistas.BuscarPorPlacaAsync(db, entrada.PatioId, entrada.Placa, ct);
            var coberto = entrada.MensalistaId is not null
                || (mensalista is not null && ServicoMensalistas.EmDia(mensalista, ServicoMensalistas.Hoje(agora)));
            string? aviso = null;
            if (!coberto && mensalista is not null)
                aviso = mensalista.ValidoAte is { } venceu
                    ? $"Mensalidade de {mensalista.Nome} vencida em {venceu:dd/MM/yyyy}: cobrado como avulso."
                    : $"{mensalista.Nome} ainda não pagou a mensalidade: cobrado como avulso.";

            var valor = coberto ? 0m : tarifa.ValorTotal;
            var cobranca = new CobrancaSaida
            {
                PatioId = entrada.PatioId,
                EntradaVeiculoId = entrada.Id,
                Valor = valor,
                DuracaoSegundos = tarifa.DuracaoSegundos,
                DuracaoFormatada = tarifa.DuracaoFormatada,
                SituacaoPagamento = coberto ? SituacaoPagamento.Mensalista
                    : tarifa.Isento ? SituacaoPagamento.Isento
                    : SituacaoPagamento.Calculado,
                CalculadoEm = agora,
                ExpiraEm = agora + ValidadeCobranca,
                Origem = origem,
                Mensalista = mensalista?.Nome,
                Aviso = aviso
            };
            cobranca.TxId = "PKL" + cobranca.Id.ToString("N")[..22].ToUpperInvariant();
            if (valor > 0 && config.AceitaPix && config.ChavePix is not null)
                cobranca.PixCopiaECola = PixCopiaECola.Gerar(config.ChavePix, config.NomeRecebedor!, config.CidadeRecebedor!, valor, cobranca.TxId);

            db.CobrancasSaida.Add(cobranca);
            await db.SaveChangesAsync(ct);
            return ParaDto(cobranca, entrada, patio, config);
        }

        // ---------- 2) Confirmação ----------

        public async Task<SaidaDto> ConfirmarAsync(Guid usuarioId, Guid cobrancaId, ConfirmarSaidaRequisicao requisicao, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var cobranca = await db.CobrancasSaida
                .Include(c => c.Entrada).ThenInclude(e => e.Vaga)
                .FirstOrDefaultAsync(c => c.Id == cobrancaId && c.PatioId == patioId, ct)
                ?? throw new ExcecaoNaoEncontrado("Cobrança não encontrada.");
            var agora = relogio.GetUtcNow().UtcDateTime;

            if (cobranca.Situacao == SituacaoCobranca.Paga)
                throw new ExcecaoConflito("Esta saída já foi confirmada.");
            if (cobranca.Situacao != SituacaoCobranca.Pendente || cobranca.Entrada.Situacao != SituacaoEntrada.Ativa)
                throw new ExcecaoConflito("Cobrança cancelada ou veículo já saiu. Gere a cobrança novamente.");
            if (cobranca.ExpiraEm <= agora)
                throw new ExcecaoConflito("A cobrança expirou e o valor pode ter mudado. Gere a cobrança novamente.");

            string? forma = null;
            decimal? recebido = null, troco = null;
            Caixa? caixa = null;

            if (cobranca.Valor > 0)
            {
                forma = (requisicao.FormaPagamento ?? "").Trim().ToLowerInvariant();
                var patio = await db.Patios.FirstAsync(p => p.Id == patioId, ct);
                var aceitas = ServicoConfiguracaoPagamento.FormasAceitas(await ServicoConfiguracaoPagamento.ObterDaEmpresaAsync(db, patio.EmpresaId, ct));
                if (!aceitas.Contains(forma))
                    throw new ExcecaoNegocio($"Forma de pagamento não aceita. Use: {string.Join(", ", aceitas)}.");

                caixa = await ServicoCaixa.ExigirCaixaAbertoAsync(db, usuarioId, patioId, ct);

                if (forma == FormasPagamento.Dinheiro)
                {
                    recebido = requisicao.ValorRecebido ?? cobranca.Valor;
                    if (recebido < cobranca.Valor)
                        throw new ExcecaoNegocio($"Valor recebido menor que o cobrado ({cobranca.Valor:0.00}).");
                    if (recebido > ServicoCaixa.ValorMaximo || decimal.Round(recebido.Value, 2) != recebido)
                        throw new ExcecaoNegocio("Valor recebido inválido.");
                    troco = recebido - cobranca.Valor;
                }
            }

            return await FinalizarAsync(cobranca, usuarioId, forma, recebido, troco, caixa, agora, ct);
        }

        // ---------- Identificação do veículo ----------

        private async Task<EntradaVeiculo> BuscarPorPlacaAsync(string placa, decimal? confianca, CancellationToken ct)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .FirstOrDefaultAsync(e => e.PatioId == patioId && e.Placa == placa && e.Situacao == SituacaoEntrada.Ativa, ct);

            if (entrada is null)
            {
                // Placa lida na saída sem entrada correspondente: alerta para o operador
                Auditoria.Registrar(db, patioId, AcoesAuditoria.SaidaSemEntrada, SituacaoAuditoria.Alerta, placa,
                    relogio.GetUtcNow().UtcDateTime, new() { ["confianca"] = confianca ?? 1m });
                await db.SaveChangesAsync(ct);
                throw new ExcecaoNaoEncontrado($"Nenhuma entrada ativa encontrada para a placa {placa}.");
            }
            return entrada;
        }

        private async Task<EntradaVeiculo> BuscarPorTicketAsync(string lido, CancellationToken ct)
        {
            var codigo = CodigoTicket.Normalizar(lido)
                ?? throw new ExcecaoNegocio("Código de ticket inválido. Use o código de 8 caracteres impresso no ticket.");
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);

            var entrada = await db.EntradasVeiculos
                .Include(e => e.Vaga)
                .FirstOrDefaultAsync(e => e.PatioId == patioId && e.CodigoTicket == codigo, ct)
                ?? throw new ExcecaoNaoEncontrado($"Ticket {CodigoTicket.Formatar(codigo)} não encontrado.");

            if (entrada.Situacao != SituacaoEntrada.Ativa)
            {
                var saidaAnterior = await db.SaidasVeiculos
                    .Where(s => s.EntradaVeiculoId == entrada.Id)
                    .Select(s => (DateTime?)s.DataHoraSaida)
                    .FirstOrDefaultAsync(ct);
                var quando = saidaAnterior is null
                    ? ""
                    : $" em {TimeZoneInfo.ConvertTimeFromUtc(saidaAnterior.Value, FusoHorario.Brasil):dd/MM 'às' HH:mm}";
                throw new ExcecaoConflito($"Este ticket já foi usado: a saída de {entrada.Placa} foi registrada{quando}.");
            }
            return entrada;
        }

        // ---------- Registro da saída ----------

        private async Task<SaidaDto> FinalizarAsync(CobrancaSaida cobranca, Guid usuarioId, string? forma, decimal? recebido, decimal? troco,
            Caixa? caixa, DateTime agora, CancellationToken ct)
        {
            var entrada = cobranca.Entrada;
            var patio = await db.Patios.FirstAsync(p => p.Id == entrada.PatioId, ct);

            var saida = new SaidaVeiculo
            {
                PatioId = entrada.PatioId,
                EntradaVeiculoId = entrada.Id,
                Placa = entrada.Placa,
                DataHoraSaida = agora,
                DuracaoSegundos = cobranca.DuracaoSegundos,
                DuracaoFormatada = cobranca.DuracaoFormatada,
                ValorHora = patio.ValorHora,
                ValorTotal = cobranca.Valor,
                Moeda = "BRL",
                SituacaoPagamento = cobranca.Valor > 0 ? SituacaoPagamento.Pago : cobranca.SituacaoPagamento,
                Situacao = SituacaoEntrada.Concluida,
                FormaPagamento = forma,
                ValorRecebido = recebido,
                Troco = troco,
                CaixaId = caixa?.Id,
                RegistradoPorId = usuarioId,
                CobrancaId = cobranca.Id,
                CriadoEm = agora
            };
            db.SaidasVeiculos.Add(saida);

            entrada.Situacao = SituacaoEntrada.Concluida;
            cobranca.Situacao = SituacaoCobranca.Paga;

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

            var detalhes = new Dictionary<string, object>
            {
                ["vaga"] = vaga?.Numero ?? "",
                ["duracao"] = cobranca.DuracaoFormatada,
                ["valor"] = cobranca.Valor,
                ["origem"] = cobranca.Origem ?? "placa",
                ["forma"] = forma ?? "",
                ["usuarioId"] = usuarioId.ToString()
            };
            if (cobranca.Mensalista is not null)
                detalhes["mensalista"] = cobranca.Mensalista;
            if (cobranca.Aviso is not null)
                detalhes["aviso"] = cobranca.Aviso;

            Auditoria.Registrar(db, entrada.PatioId, AcoesAuditoria.SaidaSucesso, SituacaoAuditoria.Sucesso, entrada.Placa, agora, detalhes);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ExcecaoConflito("A saída deste veículo já está sendo processada. Tente novamente.");
            }

            await cache.InvalidarAsync(entrada.PatioId);

            return new SaidaDto(saida.Id, entrada.Id, entrada.Placa, entrada.DataHoraEntrada, agora, null,
                cobranca.DuracaoSegundos, cobranca.DuracaoFormatada, Math.Round(cobranca.DuracaoSegundos / 3600m, 2), patio.ValorHora,
                cobranca.Valor, saida.Moeda, saida.SituacaoPagamento, vaga?.Numero, patio.Nome,
                cobranca.Mensalista, cobranca.Aviso, forma, recebido, troco);
        }

        private static CobrancaDto ParaDto(CobrancaSaida c, EntradaVeiculo entrada, Patio patio, ConfiguracaoPagamento config) =>
            new(c.Id, entrada.Id, entrada.Placa, entrada.Vaga?.Numero, patio.Nome, entrada.DataHoraEntrada, c.CalculadoEm, c.ExpiraEm,
                c.DuracaoFormatada, patio.ValorHora, c.Valor, c.SituacaoPagamento, c.Valor > 0,
                c.Valor > 0 ? ServicoConfiguracaoPagamento.FormasAceitas(config) : [],
                c.PixCopiaECola, c.Mensalista, c.Aviso);
    }
}
