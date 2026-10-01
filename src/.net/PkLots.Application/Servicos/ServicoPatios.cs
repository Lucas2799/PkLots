using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Pátios da empresa: seletor do app (gestão) e cadastro (admin)
    public class ServicoPatios(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        // Pátios que o usuário pode usar, para o seletor do topo do painel
        public async Task<IReadOnlyList<PatioResumoDto>> ListarPermitidosAsync(CancellationToken ct = default)
        {
            var permitidos = await contextoPatio.ObterPatiosPermitidosAsync(ct);
            var nomes = await db.Patios.Where(p => permitidos.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Nome, ct);
            return permitidos.Select(id => new PatioResumoDto(id, nomes[id])).ToList();
        }

        // Todos os pátios da empresa, inclusive desativados (administração)
        public async Task<IReadOnlyList<PatioAdminDto>> ListarDaEmpresaAsync(CancellationToken ct = default)
        {
            var empresaId = ObterEmpresaId();
            return await db.Patios
                .Where(p => p.EmpresaId == empresaId)
                .OrderByDescending(p => p.Ativo).ThenBy(p => p.CriadoEm)
                .Select(p => new PatioAdminDto(
                    p.Id,
                    p.Nome,
                    p.Ativo,
                    p.Vagas.Count(v => v.Ativa),
                    p.ValorHora,
                    p.Entradas.Count(e => e.Situacao == SituacaoEntrada.Ativa)))
                .ToListAsync(ct);
        }

        public async Task<PatioAdminDto> CriarAsync(Guid adminId, CriarPatioRequisicao requisicao, CancellationToken ct = default)
        {
            var nome = ValidarNome(requisicao.Nome);
            ValidarQuantidadeVagas(requisicao.TotalVagas);
            if (requisicao.ValorHora <= 0 || requisicao.ValorHora > ServicoAdministracao.ValorHoraMaximo)
                throw new ExcecaoNegocio("O valor por hora deve ser maior que zero e até R$ 1.000,00.");
            if (requisicao.MinutosTolerancia is < 0 or > ServicoAdministracao.ToleranciaMaxima)
                throw new ExcecaoNegocio($"A tolerância deve ficar entre 0 e {ServicoAdministracao.ToleranciaMaxima} minutos.");

            var empresaId = ObterEmpresaId();
            await GarantirNomeUnicoAsync(empresaId, nome, null, ct);

            var agora = relogio.GetUtcNow().UtcDateTime;
            var patio = NovoPatio(empresaId, nome, requisicao.TotalVagas, agora);
            patio.ValorHora = requisicao.ValorHora;
            patio.MinutosTolerancia = requisicao.MinutosTolerancia;
            db.Patios.Add(patio);

            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.PatioCriado, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["adminId"] = adminId.ToString(), ["nome"] = nome, ["vagas"] = requisicao.TotalVagas });

            await db.SaveChangesAsync(ct);
            return new PatioAdminDto(patio.Id, patio.Nome, true, requisicao.TotalVagas, patio.ValorHora, 0);
        }

        public async Task<PatioAdminDto> AtualizarAsync(Guid adminId, Guid patioId, AtualizarPatioRequisicao requisicao, CancellationToken ct = default)
        {
            var nome = ValidarNome(requisicao.Nome);
            var empresaId = ObterEmpresaId();
            var patio = await db.Patios.FirstOrDefaultAsync(p => p.Id == patioId && p.EmpresaId == empresaId, ct)
                ?? throw new ExcecaoNaoEncontrado("Pátio não encontrado.");
            await GarantirNomeUnicoAsync(empresaId, nome, patioId, ct);

            var estacionados = await db.EntradasVeiculos.CountAsync(e => e.PatioId == patioId && e.Situacao == SituacaoEntrada.Ativa, ct);
            if (patio.Ativo && !requisicao.Ativo)
            {
                if (estacionados > 0)
                    throw new ExcecaoConflito($"Não é possível desativar: há {estacionados} veículo(s) estacionado(s) neste pátio.");
                if (!await db.Patios.AnyAsync(p => p.EmpresaId == empresaId && p.Id != patioId && p.Ativo, ct))
                    throw new ExcecaoConflito("A empresa precisa ter pelo menos um pátio ativo.");
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.PatioAlterado, SituacaoAuditoria.Sucesso, null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["nomeAnterior"] = patio.Nome,
                    ["nome"] = nome,
                    ["ativoAnterior"] = patio.Ativo,
                    ["ativo"] = requisicao.Ativo
                });

            patio.Nome = nome;
            patio.Ativo = requisicao.Ativo;
            patio.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);

            var vagas = await db.Vagas.CountAsync(v => v.PatioId == patioId && v.Ativa, ct);
            return new PatioAdminDto(patio.Id, patio.Nome, patio.Ativo, vagas, patio.ValorHora, estacionados);
        }

        // Pátio novo já com as vagas criadas (usado também na criação de empresas)
        public static Patio NovoPatio(Guid empresaId, string nome, int totalVagas, DateTime agora)
        {
            var patio = new Patio
            {
                EmpresaId = empresaId,
                Nome = nome,
                MaximoVagas = totalVagas,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            foreach (var vaga in VagasDoPatio.Criar(patio.Id, totalVagas, agora))
                patio.Vagas.Add(vaga);
            return patio;
        }

        public static void ValidarQuantidadeVagas(int total)
        {
            if (total < 1 || total > LayoutPatio.MaximoVagas)
                throw new ExcecaoNegocio($"A quantidade de vagas deve ficar entre 1 e {LayoutPatio.MaximoVagas}.");
        }

        private static string ValidarNome(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ExcecaoNegocio("Informe o nome do pátio.");
            return nome.Trim();
        }

        private async Task GarantirNomeUnicoAsync(Guid empresaId, string nome, Guid? exceto, CancellationToken ct)
        {
            var existe = await db.Patios.AnyAsync(p => p.EmpresaId == empresaId && p.Nome.ToLower() == nome.ToLower() && p.Id != exceto, ct);
            if (existe)
                throw new ExcecaoConflito($"Já existe um pátio chamado \"{nome}\".");
        }

        private Guid ObterEmpresaId() =>
            contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Esta função é exclusiva de usuários de uma empresa.");
    }
}
