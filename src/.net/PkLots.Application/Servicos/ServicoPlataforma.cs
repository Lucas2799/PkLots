using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Gestão da plataforma (superadmin): empresas clientes e o primeiro administrador de cada uma
    public class ServicoPlataforma(IPkLotsDbContext db, IHashSenha hashSenha, TimeProvider relogio)
    {
        public async Task<IReadOnlyList<EmpresaDto>> ListarEmpresasAsync(CancellationToken ct = default) =>
            await db.Empresas
                .OrderByDescending(e => e.Ativa).ThenBy(e => e.Nome)
                .Select(e => new EmpresaDto(e.Id, e.Nome, e.Documento, e.Ativa, e.Patios.Count(p => p.Ativo), e.Usuarios.Count(u => u.Ativo), e.CriadoEm))
                .ToListAsync(ct);

        // Cria a empresa com o primeiro pátio (vagas já criadas) e o administrador
        public async Task<EmpresaDto> CriarEmpresaAsync(Guid superAdminId, CriarEmpresaRequisicao requisicao, CancellationToken ct = default)
        {
            var nome = ValidarNomeEmpresa(requisicao.Nome);
            if (string.IsNullOrWhiteSpace(requisicao.NomePatio))
                throw new ExcecaoNegocio("Informe o nome do primeiro pátio.");
            ServicoPatios.ValidarQuantidadeVagas(requisicao.TotalVagas);
            var emailAdmin = RegrasUsuario.NormalizarEmail(requisicao.EmailAdmin);
            var nomeAdmin = RegrasUsuario.ValidarNome(requisicao.NomeAdmin);
            RegrasUsuario.ValidarSenha(requisicao.SenhaAdmin);

            if (await db.Empresas.AnyAsync(e => e.Nome.ToLower() == nome.ToLower(), ct))
                throw new ExcecaoConflito($"Já existe uma empresa chamada \"{nome}\".");
            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == emailAdmin, ct))
                throw new ExcecaoConflito("E-mail do administrador já cadastrado.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var empresa = new Empresa
            {
                Nome = nome,
                Documento = string.IsNullOrWhiteSpace(requisicao.Documento) ? null : requisicao.Documento.Trim(),
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            var patio = ServicoPatios.NovoPatio(empresa.Id, requisicao.NomePatio.Trim(), requisicao.TotalVagas, agora);
            var admin = new Usuario
            {
                EmpresaId = empresa.Id,
                Email = emailAdmin,
                NomeCompleto = nomeAdmin,
                Perfil = Perfis.Admin,
                SenhaHash = hashSenha.Gerar(requisicao.SenhaAdmin),
                DeveTrocarSenha = true,
                CriadoEm = agora,
                AtualizadoEm = agora
            };

            db.Empresas.Add(empresa);
            db.Patios.Add(patio);
            db.Usuarios.Add(admin);
            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.EmpresaCriada, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["superAdminId"] = superAdminId.ToString(), ["empresa"] = nome, ["admin"] = emailAdmin });

            await db.SaveChangesAsync(ct);
            return new EmpresaDto(empresa.Id, empresa.Nome, empresa.Documento, true, 1, 1, agora);
        }

        // Desativar a empresa bloqueia na hora o acesso de todos os seus usuários
        public async Task<EmpresaDto> AtualizarEmpresaAsync(Guid superAdminId, Guid empresaId, AtualizarEmpresaRequisicao requisicao, CancellationToken ct = default)
        {
            var nome = ValidarNomeEmpresa(requisicao.Nome);
            var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct)
                ?? throw new ExcecaoNaoEncontrado("Empresa não encontrada.");
            if (await db.Empresas.AnyAsync(e => e.Id != empresaId && e.Nome.ToLower() == nome.ToLower(), ct))
                throw new ExcecaoConflito($"Já existe uma empresa chamada \"{nome}\".");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var patioAuditoria = await db.Patios.Where(p => p.EmpresaId == empresaId).OrderBy(p => p.CriadoEm).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (patioAuditoria is not null)
            {
                Auditoria.Registrar(db, patioAuditoria.Value, AcoesAuditoria.EmpresaAlterada, SituacaoAuditoria.Sucesso, null, agora,
                    new()
                    {
                        ["superAdminId"] = superAdminId.ToString(),
                        ["nomeAnterior"] = empresa.Nome,
                        ["nome"] = nome,
                        ["ativaAnterior"] = empresa.Ativa,
                        ["ativa"] = requisicao.Ativa
                    });
            }

            empresa.Nome = nome;
            empresa.Documento = string.IsNullOrWhiteSpace(requisicao.Documento) ? null : requisicao.Documento.Trim();
            empresa.Ativa = requisicao.Ativa;
            empresa.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);

            return (await ListarEmpresasAsync(ct)).First(e => e.Id == empresaId);
        }

        private static string ValidarNomeEmpresa(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome))
                throw new ExcecaoNegocio("Informe o nome da empresa.");
            return nome.Trim();
        }
    }
}
