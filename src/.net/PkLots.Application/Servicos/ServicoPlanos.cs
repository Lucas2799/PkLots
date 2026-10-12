using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Planos de aluguel (superadmin) e a assinatura de cada empresa; o admin consulta a da própria empresa
    public class ServicoPlanos(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        public const int PrecoMaximo = 100_000;
        public const int DiasCarenciaMaximo = 60;

        public static IReadOnlyList<RecursoPlanoDto> Recursos() =>
            RecursosPlano.Catalogo.Select(r => new RecursoPlanoDto(r.Codigo, r.Nome, r.Descricao)).ToList();

        // ---------- Planos ----------

        public async Task<IReadOnlyList<PlanoDto>> ListarAsync(CancellationToken ct = default)
        {
            var planos = await db.Planos.OrderByDescending(p => p.Ativo).ThenBy(p => p.PrecoPorPatio).ThenBy(p => p.Nome).ToListAsync(ct);
            var uso = await db.Assinaturas.GroupBy(a => a.PlanoId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Total, ct);
            return planos.Select(p => ParaDto(p, uso.GetValueOrDefault(p.Id))).ToList();
        }

        public async Task<PlanoDto> CriarAsync(SalvarPlanoRequisicao requisicao, CancellationToken ct = default)
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var plano = new Plano { CriadoEm = agora };
            await AplicarAsync(plano, requisicao, agora, ct);
            db.Planos.Add(plano);
            await db.SaveChangesAsync(ct);
            return ParaDto(plano, 0);
        }

        // Vale na hora para todas as empresas do plano (limites e recursos); o preço, a partir da próxima fatura
        public async Task<PlanoDto> AtualizarAsync(Guid id, SalvarPlanoRequisicao requisicao, CancellationToken ct = default)
        {
            var plano = await db.Planos.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new ExcecaoNaoEncontrado("Plano não encontrado.");
            var agora = relogio.GetUtcNow().UtcDateTime;
            await AplicarAsync(plano, requisicao, agora, ct);
            await db.SaveChangesAsync(ct);
            return ParaDto(plano, await db.Assinaturas.CountAsync(a => a.PlanoId == id, ct));
        }

        private async Task AplicarAsync(Plano plano, SalvarPlanoRequisicao r, DateTime agora, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Nome))
                throw new ExcecaoNegocio("Informe o nome do plano.");
            var nome = r.Nome.Trim();
            if (nome.Length > 60)
                throw new ExcecaoNegocio("O nome do plano deve ter até 60 caracteres.");
            if (r.PrecoPorPatio < 0 || r.PrecoPorPatio > PrecoMaximo || decimal.Round(r.PrecoPorPatio, 2) != r.PrecoPorPatio)
                throw new ExcecaoNegocio("Preço por pátio inválido.");
            ValidarLimite(r.MaximoPatios, "pátios", 1000);
            ValidarLimite(r.MaximoVagasPorPatio, "vagas por pátio", LayoutPatio.MaximoVagas);
            ValidarLimite(r.MaximoUsuarios, "usuários", 1000);
            if (r.DiasCarencia is < 0 or > DiasCarenciaMaximo)
                throw new ExcecaoNegocio($"A carência deve ficar entre 0 e {DiasCarenciaMaximo} dias.");
            var recursos = (r.Recursos ?? []).Select(c => c.Trim().ToLowerInvariant()).Distinct().ToList();
            if (recursos.FirstOrDefault(c => !RecursosPlano.Todos.Contains(c)) is { } desconhecido)
                throw new ExcecaoNegocio($"Recurso desconhecido: {desconhecido}.");
            if (await db.Planos.AnyAsync(p => p.Id != plano.Id && p.Nome.ToLower() == nome.ToLower(), ct))
                throw new ExcecaoConflito($"Já existe um plano chamado \"{nome}\".");

            plano.Nome = nome;
            plano.Descricao = string.IsNullOrWhiteSpace(r.Descricao) ? null : r.Descricao.Trim();
            plano.PrecoPorPatio = r.PrecoPorPatio;
            plano.MaximoPatios = r.MaximoPatios;
            plano.MaximoVagasPorPatio = r.MaximoVagasPorPatio;
            plano.MaximoUsuarios = r.MaximoUsuarios;
            // Mantém a ordem do catálogo, para a tela ficar estável
            plano.Recursos = RecursosPlano.Todos.Where(recursos.Contains).ToList();
            plano.DiasCarencia = r.DiasCarencia;
            plano.Ativo = r.Ativo;
            plano.AtualizadoEm = agora;
        }

        private static void ValidarLimite(int? valor, string nome, int maximo)
        {
            if (valor is { } v && (v < 1 || v > maximo))
                throw new ExcecaoNegocio($"O limite de {nome} deve ficar entre 1 e {maximo} (ou sem limite).");
        }

        // ---------- Assinatura (superadmin) ----------

        public async Task<AssinaturaDto> ObterAssinaturaAsync(Guid empresaId, CancellationToken ct = default)
        {
            var assinatura = await db.Assinaturas.Include(a => a.Plano).FirstOrDefaultAsync(a => a.EmpresaId == empresaId, ct)
                ?? throw new ExcecaoNaoEncontrado("Assinatura não encontrada.");
            return await ParaDtoAsync(assinatura, ct);
        }

        public async Task<AssinaturaDto> SalvarAssinaturaAsync(Guid superAdminId, Guid empresaId, SalvarAssinaturaRequisicao r, CancellationToken ct = default)
        {
            if (!await db.Empresas.AnyAsync(e => e.Id == empresaId, ct))
                throw new ExcecaoNaoEncontrado("Empresa não encontrada.");
            var plano = await db.Planos.FirstOrDefaultAsync(p => p.Id == r.PlanoId, ct) ?? throw new ExcecaoNaoEncontrado("Plano não encontrado.");
            ValidarAssinatura(r.DiaVencimento, r.PrecoPorPatioNegociado);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var assinatura = await db.Assinaturas.FirstOrDefaultAsync(a => a.EmpresaId == empresaId, ct);
            if (assinatura is null)
            {
                assinatura = new Assinatura { EmpresaId = empresaId, CriadoEm = agora };
                db.Assinaturas.Add(assinatura);
            }
            else if (assinatura.PlanoId != plano.Id && !plano.Ativo)
                throw new ExcecaoConflito($"O plano {plano.Nome} está inativo e não aceita novas assinaturas.");

            var patioAuditoria = await db.Patios.Where(p => p.EmpresaId == empresaId).OrderBy(p => p.CriadoEm).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (patioAuditoria is not null)
                Auditoria.Registrar(db, patioAuditoria.Value, AcoesAuditoria.AssinaturaAlterada, SituacaoAuditoria.Sucesso, null, agora,
                    new()
                    {
                        ["superAdminId"] = superAdminId.ToString(),
                        ["planoAnterior"] = assinatura.PlanoId.ToString(),
                        ["plano"] = plano.Nome,
                        ["precoNegociado"] = r.PrecoPorPatioNegociado?.ToString() ?? "",
                        ["diaVencimento"] = r.DiaVencimento
                    });

            assinatura.PlanoId = plano.Id;
            assinatura.Plano = plano;
            assinatura.PrecoPorPatioNegociado = r.PrecoPorPatioNegociado;
            assinatura.DiaVencimento = r.DiaVencimento;
            assinatura.TesteAte = r.TesteAte;
            assinatura.Observacoes = string.IsNullOrWhiteSpace(r.Observacoes) ? null : r.Observacoes.Trim();
            assinatura.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);
            return await ParaDtoAsync(assinatura, ct);
        }

        // Plano de uma empresa nova (sem plano informado: o primeiro plano ativo mais barato)
        internal static async Task<Plano> PlanoParaNovaEmpresaAsync(IPkLotsDbContext db, Guid? planoId, CancellationToken ct) =>
            planoId is { } id
                ? await db.Planos.FirstOrDefaultAsync(p => p.Id == id && p.Ativo, ct) ?? throw new ExcecaoNaoEncontrado("Plano não encontrado ou inativo.")
                : await db.Planos.Where(p => p.Ativo).OrderBy(p => p.PrecoPorPatio).ThenBy(p => p.Nome).FirstOrDefaultAsync(ct)
                    ?? throw new ExcecaoConflito("Cadastre um plano antes de criar empresas.");

        internal static Assinatura NovaAssinatura(Guid empresaId, Plano plano, DateTime agora) => new()
        {
            EmpresaId = empresaId,
            PlanoId = plano.Id,
            DiaVencimento = Math.Min(ServicoMensalistas.Hoje(agora).Day, 28),
            CriadoEm = agora,
            AtualizadoEm = agora
        };

        private static void ValidarAssinatura(int diaVencimento, decimal? precoNegociado)
        {
            if (diaVencimento is < 1 or > 28)
                throw new ExcecaoNegocio("O dia de vencimento deve ficar entre 1 e 28.");
            if (precoNegociado is { } p && (p < 0 || p > PrecoMaximo || decimal.Round(p, 2) != p))
                throw new ExcecaoNegocio("Preço negociado inválido.");
        }

        public static decimal PrecoEfetivo(Assinatura a) => a.PrecoPorPatioNegociado ?? a.Plano.PrecoPorPatio;

        private async Task<AssinaturaDto> ParaDtoAsync(Assinatura a, CancellationToken ct)
        {
            var patios = await db.Patios.CountAsync(p => p.EmpresaId == a.EmpresaId && p.Ativo, ct);
            return new AssinaturaDto(a.EmpresaId, a.PlanoId, a.Plano.Nome, a.Plano.PrecoPorPatio, a.PrecoPorPatioNegociado,
                a.DiaVencimento, a.TesteAte, a.Observacoes, patios, PrecoEfetivo(a) * patios);
        }

        // ---------- Minha assinatura (admin da empresa) ----------

        public async Task<MinhaAssinaturaDto> ObterMinhaAsync(CancellationToken ct = default)
        {
            var empresaId = contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Disponível só para usuários de uma empresa.");
            var assinatura = await db.Assinaturas.Include(a => a.Plano).FirstOrDefaultAsync(a => a.EmpresaId == empresaId, ct)
                ?? throw new ExcecaoNaoEncontrado("A empresa ainda não tem um plano. Fale com o suporte do PkLots.");
            var plano = assinatura.Plano;

            var patios = await db.Patios.Where(p => p.EmpresaId == empresaId && p.Ativo)
                .Select(p => p.Vagas.Count(v => v.Ativa)).ToListAsync(ct);
            var usuarios = await RegrasPlano.ContarUsuariosAsync(db, empresaId, ct);
            var preco = PrecoEfetivo(assinatura);

            return new MinhaAssinaturaDto(
                plano.Nome, plano.Descricao, preco, preco * patios.Count, assinatura.DiaVencimento, assinatura.TesteAte,
                new UsoLimiteDto(patios.Count, plano.MaximoPatios),
                new UsoLimiteDto(usuarios, plano.MaximoUsuarios),
                new UsoLimiteDto(patios.DefaultIfEmpty(0).Max(), plano.MaximoVagasPorPatio),
                RecursosPlano.Catalogo.Select(r => new RecursoLiberadoDto(r.Codigo, r.Nome, plano.Recursos.Contains(r.Codigo))).ToList());
        }

        // Recursos liberados para a empresa do usuário (o app esconde o que o plano não inclui)
        public async Task<IReadOnlyCollection<string>> RecursosDaMinhaEmpresaAsync(CancellationToken ct = default) =>
            contextoPatio.EmpresaId is { } empresaId ? await RegrasPlano.RecursosLiberadosAsync(db, empresaId, ct) : RecursosPlano.Todos;

        private static PlanoDto ParaDto(Plano p, int empresas) =>
            new(p.Id, p.Nome, p.Descricao, p.PrecoPorPatio, p.MaximoPatios, p.MaximoVagasPorPatio, p.MaximoUsuarios,
                p.Recursos, p.DiasCarencia, p.Ativo, empresas);
    }
}
