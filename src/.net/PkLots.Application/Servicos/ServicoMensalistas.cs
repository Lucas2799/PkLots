using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Mensalistas do pátio selecionado: cadastro, situação e pagamentos (registrados manualmente)
    public class ServicoMensalistas(IPkLotsDbContext db, IContextoPatio contextoPatio, LinksPublicos links, TimeProvider relogio)
    {
        public const int MesesMaximoPorPagamento = 12;

        // ---------- Regras de situação (usadas também na entrada/saída) ----------

        public static DateOnly Hoje(DateTime agoraUtc) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, FusoHorario.Brasil));

        public static bool EmDia(Mensalista m, DateOnly hoje) => m.Ativo && m.ValidoAte is { } ate && ate >= hoje;

        public static string Situacao(Mensalista m, DateOnly hoje)
        {
            if (!m.Ativo) return SituacaoMensalista.Inativo;
            if (m.ValidoAte is not { } ate || ate < hoje) return SituacaoMensalista.Vencido;
            return ate.DayNumber - hoje.DayNumber <= SituacaoMensalista.DiasAvisoVencimento
                ? SituacaoMensalista.Vencendo
                : SituacaoMensalista.EmDia;
        }

        // Mensalista ativo dono da placa no pátio (ou null)
        public static Task<Mensalista?> BuscarPorPlacaAsync(IPkLotsDbContext db, Guid patioId, string placa, CancellationToken ct) =>
            db.Mensalistas.FirstOrDefaultAsync(m => m.PatioId == patioId && m.Ativo && m.Placas.Any(p => p.Placa == placa), ct);

        // ---------- Consulta ----------

        public async Task<IReadOnlyList<MensalistaDto>> ListarAsync(string? situacao = null, string? busca = null, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var consulta = db.Mensalistas.Include(m => m.Placas).Where(m => m.PatioId == patioId);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                var placa = ValidadorPlaca.Normalizar(busca) ?? termo.ToUpperInvariant();
                consulta = consulta.Where(m => m.Nome.ToLower().Contains(termo) || m.Placas.Any(p => p.Placa.Contains(placa)));
            }

            var mensalistas = await consulta.OrderBy(m => m.Nome).ToListAsync(ct);
            var estacionadas = await PlacasEstacionadasAsync(patioId, ct);
            var hoje = Hoje(relogio.GetUtcNow().UtcDateTime);

            return mensalistas
                .Select(m => ParaDto(m, hoje, estacionadas))
                .Where(m => situacao is null || m.Situacao == situacao)
                .ToList();
        }

        public async Task<MensalistaDto> ObterAsync(Guid id, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var mensalista = await ObterEntidadeAsync(id, patioId, ct);
            return ParaDto(mensalista, Hoje(relogio.GetUtcNow().UtcDateTime), await PlacasEstacionadasAsync(patioId, ct));
        }

        public async Task<IReadOnlyList<PagamentoDto>> ListarPagamentosAsync(Guid id, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            await ObterEntidadeAsync(id, patioId, ct);
            return await db.PagamentosMensalidade
                .Where(p => p.MensalistaId == id)
                .OrderByDescending(p => p.PagoEm).ThenByDescending(p => p.ValidoAte)
                .Select(p => new PagamentoDto(p.Id, p.Valor, p.Meses, p.ValidoDe, p.ValidoAte, p.FormaPagamento, p.Observacao, p.PagoEm))
                .ToListAsync(ct);
        }

        public async Task<ResumoMensalistasDto> ObterResumoAsync(Guid patioId, CancellationToken ct = default)
        {
            var hoje = Hoje(relogio.GetUtcNow().UtcDateTime);
            var ativos = await db.Mensalistas.Where(m => m.PatioId == patioId && m.Ativo).Select(m => m.ValidoAte).ToListAsync(ct);
            var limite = hoje.AddDays(SituacaoMensalista.DiasAvisoVencimento);
            var patio = await db.Patios.Where(p => p.Id == patioId).Select(p => new { p.VagasReservadasMensalistas, p.ValorMensalidade }).FirstAsync(ct);
            var reservadas = patio.VagasReservadasMensalistas;
            var estacionados = await ContarMensalistasEstacionadosAsync(db, patioId, ct);

            return new ResumoMensalistasDto(
                ativos.Count,
                ativos.Count(v => v > limite),
                ativos.Count(v => v >= hoje && v <= limite),
                ativos.Count(v => v is null || v < hoje),
                estacionados,
                reservadas,
                Math.Max(0, reservadas - estacionados),
                patio.ValorMensalidade);
        }

        // Entradas ativas de mensalistas em dia (ocupam as vagas reservadas)
        public static Task<int> ContarMensalistasEstacionadosAsync(IPkLotsDbContext db, Guid patioId, CancellationToken ct) =>
            db.EntradasVeiculos.CountAsync(e => e.PatioId == patioId && e.Situacao == SituacaoEntrada.Ativa && e.MensalistaId != null, ct);

        // ---------- Cadastro ----------

        public async Task<MensalistaDto> CriarAsync(Guid usuarioId, SalvarMensalistaRequisicao requisicao, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var (nome, placas) = Validar(requisicao);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var mensalista = new Mensalista
            {
                PatioId = patioId,
                Nome = nome,
                Telefone = Limpar(requisicao.Telefone),
                Documento = Limpar(requisicao.Documento),
                Observacoes = Limpar(requisicao.Observacoes),
                Ativo = true,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            await AtribuirPlacasAsync(mensalista, placas, ct);
            db.Mensalistas.Add(mensalista);
            ServicoAcessoMensalista.AplicarEmail(db, links, mensalista, requisicao.Email, await NomePatioAsync(patioId, ct), usuarioId, agora);

            Auditoria.Registrar(db, patioId, AcoesAuditoria.MensalistaCriado, SituacaoAuditoria.Sucesso, placas[0], agora,
                new() { ["usuarioId"] = usuarioId.ToString(), ["mensalista"] = nome, ["placas"] = string.Join(",", placas) });

            await db.SaveChangesAsync(ct);
            return ParaDto(mensalista, Hoje(agora), await PlacasEstacionadasAsync(patioId, ct));
        }

        public async Task<MensalistaDto> AtualizarAsync(Guid usuarioId, Guid id, SalvarMensalistaRequisicao requisicao, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var mensalista = await ObterEntidadeAsync(id, patioId, ct);
            var (nome, placas) = Validar(requisicao);
            var agora = relogio.GetUtcNow().UtcDateTime;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.MensalistaAlterado, SituacaoAuditoria.Sucesso, placas[0], agora,
                new()
                {
                    ["usuarioId"] = usuarioId.ToString(),
                    ["mensalista"] = nome,
                    ["placas"] = string.Join(",", placas),
                    ["ativoAnterior"] = mensalista.Ativo,
                    ["ativo"] = requisicao.Ativo
                });

            mensalista.Nome = nome;
            mensalista.Telefone = Limpar(requisicao.Telefone);
            mensalista.Documento = Limpar(requisicao.Documento);
            mensalista.Observacoes = Limpar(requisicao.Observacoes);
            mensalista.Ativo = requisicao.Ativo;
            mensalista.AtualizadoEm = agora;
            await AtribuirPlacasAsync(mensalista, placas, ct);
            ServicoAcessoMensalista.AplicarEmail(db, links, mensalista, requisicao.Email, await NomePatioAsync(patioId, ct), usuarioId, agora);

            await db.SaveChangesAsync(ct);
            return ParaDto(mensalista, Hoje(agora), await PlacasEstacionadasAsync(patioId, ct));
        }

        // Novo convite para o mesmo e-mail (o anterior expirou ou se perdeu)
        public async Task<MensalistaDto> ReenviarConviteAsync(Guid usuarioId, Guid id, CancellationToken ct = default)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var mensalista = await ObterEntidadeAsync(id, patioId, ct);
            if (!mensalista.Ativo)
                throw new ExcecaoConflito("Mensalista inativo: reative o cadastro antes de enviar o convite.");
            if (mensalista.Email is null)
                throw new ExcecaoNegocio("Informe o e-mail do mensalista no cadastro para enviar o convite.");
            if (mensalista.UsuarioId is not null)
                throw new ExcecaoConflito("Este mensalista já ativou o acesso ao app.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            ServicoAcessoMensalista.EnviarConvite(db, links, mensalista, await NomePatioAsync(patioId, ct), usuarioId, agora);
            await db.SaveChangesAsync(ct);
            return ParaDto(mensalista, Hoje(agora), await PlacasEstacionadasAsync(patioId, ct));
        }

        // ---------- Pagamentos ----------

        // Em dia: a validade continua de onde parou. Vencido/nunca pagou: recomeça hoje.
        public async Task<MensalistaDto> RegistrarPagamentoAsync(Guid usuarioId, Guid id, RegistrarPagamentoRequisicao requisicao, CancellationToken ct = default)
        {
            if (requisicao.Meses is < 1 or > MesesMaximoPorPagamento)
                throw new ExcecaoNegocio($"Informe de 1 a {MesesMaximoPorPagamento} meses.");
            var forma = (requisicao.FormaPagamento ?? "").Trim().ToLowerInvariant();
            if (!FormasPagamento.Todas.Contains(forma))
                throw new ExcecaoNegocio($"Forma de pagamento inválida. Use: {string.Join(", ", FormasPagamento.Todas)}.");

            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            var mensalista = await ObterEntidadeAsync(id, patioId, ct);
            if (!mensalista.Ativo)
                throw new ExcecaoConflito("Mensalista inativo: reative o cadastro antes de registrar o pagamento.");

            var patio = await db.Patios.FirstAsync(p => p.Id == patioId, ct);
            var valor = requisicao.Valor ?? patio.ValorMensalidade * requisicao.Meses;
            if (valor <= 0)
                throw new ExcecaoNegocio("Informe o valor pago (a mensalidade do pátio não está configurada).");
            if (decimal.Round(valor, 2) != valor || valor > 100_000m)
                throw new ExcecaoNegocio("Valor inválido.");

            // Dinheiro exige caixa aberto (entra na conferência); outras formas entram no caixa aberto, se houver
            var caixa = forma == FormasPagamento.Dinheiro
                ? await ServicoCaixa.ExigirCaixaAbertoAsync(db, usuarioId, patioId, ct)
                : await db.Caixas.FirstOrDefaultAsync(c => c.OperadorId == usuarioId && c.FechadoEm == null && c.PatioId == patioId, ct);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var hoje = Hoje(agora);
            var de = mensalista.ValidoAte is { } ate && ate >= hoje ? ate.AddDays(1) : hoje;
            var ateNovo = de.AddMonths(requisicao.Meses).AddDays(-1);

            db.PagamentosMensalidade.Add(new PagamentoMensalidade
            {
                MensalistaId = mensalista.Id,
                PatioId = patioId,
                Valor = valor,
                Meses = requisicao.Meses,
                ValidoDe = de,
                ValidoAte = ateNovo,
                FormaPagamento = forma,
                Observacao = Limpar(requisicao.Observacao),
                RegistradoPorId = usuarioId,
                CaixaId = caixa?.Id,
                PagoEm = agora
            });
            mensalista.ValidoAte = ateNovo;
            mensalista.AtualizadoEm = agora;

            Auditoria.Registrar(db, patioId, AcoesAuditoria.MensalidadePaga, SituacaoAuditoria.Sucesso, mensalista.Placas.FirstOrDefault()?.Placa, agora,
                new()
                {
                    ["usuarioId"] = usuarioId.ToString(),
                    ["mensalista"] = mensalista.Nome,
                    ["valor"] = valor,
                    ["meses"] = requisicao.Meses,
                    ["validoAte"] = ateNovo.ToString("yyyy-MM-dd"),
                    ["forma"] = forma
                });

            await db.SaveChangesAsync(ct);
            return ParaDto(mensalista, hoje, await PlacasEstacionadasAsync(patioId, ct));
        }

        // ---------- Auxiliares ----------

        private async Task<Mensalista> ObterEntidadeAsync(Guid id, Guid patioId, CancellationToken ct) =>
            await db.Mensalistas.Include(m => m.Placas).FirstOrDefaultAsync(m => m.Id == id && m.PatioId == patioId, ct)
            ?? throw new ExcecaoNaoEncontrado("Mensalista não encontrado.");

        private static (string Nome, List<string> Placas) Validar(SalvarMensalistaRequisicao requisicao)
        {
            if (string.IsNullOrWhiteSpace(requisicao.Nome))
                throw new ExcecaoNegocio("Informe o nome do mensalista.");

            var placas = new List<string>();
            foreach (var bruta in requisicao.Placas ?? [])
            {
                var placa = ValidadorPlaca.Normalizar(bruta)
                    ?? throw new ExcecaoNegocio($"Placa inválida: {bruta}. Formatos aceitos: ABC1234 ou ABC1D23.");
                if (!placas.Contains(placa))
                    placas.Add(placa);
            }
            if (placas.Count == 0)
                throw new ExcecaoNegocio("Informe pelo menos uma placa.");
            if (placas.Count > 5)
                throw new ExcecaoNegocio("No máximo 5 placas por mensalista.");

            return (requisicao.Nome.Trim(), placas);
        }

        // Substitui as placas; placa de outro mensalista ativo é conflito, de mensalista inativo é transferida
        private async Task AtribuirPlacasAsync(Mensalista mensalista, List<string> placas, CancellationToken ct)
        {
            var existentes = await db.MensalistasPlacas
                .Include(mp => mp.Mensalista)
                .Where(mp => mp.PatioId == mensalista.PatioId && placas.Contains(mp.Placa) && mp.MensalistaId != mensalista.Id)
                .ToListAsync(ct);

            var emUso = existentes.FirstOrDefault(mp => mp.Mensalista.Ativo);
            if (emUso is not null)
                throw new ExcecaoConflito($"A placa {emUso.Placa} já pertence ao mensalista {emUso.Mensalista.Nome}.");
            db.MensalistasPlacas.RemoveRange(existentes);

            foreach (var antiga in mensalista.Placas.Where(p => !placas.Contains(p.Placa)).ToList())
            {
                mensalista.Placas.Remove(antiga);
                db.MensalistasPlacas.Remove(antiga);
            }
            foreach (var placa in placas.Where(p => mensalista.Placas.All(mp => mp.Placa != p)).ToList())
            {
                var nova = new MensalistaPlaca { MensalistaId = mensalista.Id, PatioId = mensalista.PatioId, Placa = placa };
                mensalista.Placas.Add(nova);
                db.MensalistasPlacas.Add(nova); // explícito: pela coleção o EF trataria o Id preenchido como registro existente
            }
        }

        private async Task<HashSet<string>> PlacasEstacionadasAsync(Guid patioId, CancellationToken ct) =>
            (await db.EntradasVeiculos
                .Where(e => e.PatioId == patioId && e.Situacao == SituacaoEntrada.Ativa)
                .Select(e => e.Placa)
                .ToListAsync(ct)).ToHashSet();

        private static MensalistaDto ParaDto(Mensalista m, DateOnly hoje, HashSet<string> estacionadas)
        {
            var placas = m.Placas.Select(p => p.Placa).OrderBy(p => p).ToList();
            return new MensalistaDto(
                m.Id, m.Nome, m.Telefone, m.Documento, m.Observacoes, placas, m.ValidoAte,
                Situacao(m, hoje),
                m.ValidoAte is { } ate ? ate.DayNumber - hoje.DayNumber : null,
                m.Ativo,
                placas.Any(estacionadas.Contains),
                m.Email,
                ServicoAcessoMensalista.SituacaoAcesso(m));
        }

        private Task<string> NomePatioAsync(Guid patioId, CancellationToken ct) =>
            db.Patios.Where(p => p.Id == patioId).Select(p => p.Nome).FirstAsync(ct);

        private static string? Limpar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }
}
