using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Auditoria: o que aconteceu, quando, em qual pátio e quem fez.
    // Admin vê só a própria empresa (filtro multi-tenant do banco + checagem aqui); superadmin vê todas e pode filtrar uma.
    public class ServicoAuditoria(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        public const int DiasMaximos = 366;
        public const int DiasPadrao = 7;
        public const int TamanhoMaximoPagina = 200;
        public const string NomeSuporte = "Equipe PkLots";
        // Sem usuário: registros anteriores à auditoria com usuário ou ações sem login (ex.: rotinas automáticas)
        public const string NomeAutomatico = "Não identificado";

        public async Task<PaginaAuditoriaDto> ListarAsync(FiltroAuditoria filtro, CancellationToken ct = default)
        {
            var empresaId = EmpresaPermitida(filtro.EmpresaId);
            var (de, ate) = Periodo(filtro.De, filtro.Ate);
            var inicio = TimeZoneInfo.ConvertTimeToUtc(de.ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil);
            var fim = TimeZoneInfo.ConvertTimeToUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil);
            var pagina = Math.Max(1, filtro.Pagina);
            var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, TamanhoMaximoPagina);

            var consulta = db.LogsAuditoria.Where(l => l.CriadoEm >= inicio && l.CriadoEm < fim);
            if (empresaId is not null)
                consulta = consulta.Where(l => l.Patio.EmpresaId == empresaId);
            if (filtro.PatioId is { } patioId)
                consulta = consulta.Where(l => l.PatioId == patioId);
            if (!string.IsNullOrWhiteSpace(filtro.Categoria))
            {
                var acoes = CatalogoAuditoria.AcoesDaCategoria(filtro.Categoria.Trim());
                consulta = consulta.Where(l => acoes.Contains(l.Acao));
            }
            if (!string.IsNullOrWhiteSpace(filtro.Situacao))
            {
                var situacao = filtro.Situacao.Trim().ToLowerInvariant();
                consulta = consulta.Where(l => l.Situacao == situacao);
            }
            if (!string.IsNullOrWhiteSpace(filtro.Placa))
            {
                var placa = new string(filtro.Placa.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
                consulta = consulta.Where(l => l.Placa != null && l.Placa.Contains(placa));
            }
            if (filtro.UsuarioId is { } usuarioId)
                consulta = consulta.Where(l => l.UsuarioId == usuarioId);

            var total = await consulta.CountAsync(ct);
            var registros = await consulta
                .OrderByDescending(l => l.CriadoEm).ThenBy(l => l.Id)
                .Skip((pagina - 1) * tamanho).Take(tamanho)
                .Select(l => new
                {
                    l.Id, l.CriadoEm, l.Acao, l.Situacao, l.Placa, l.PatioId, Patio = l.Patio.Nome, Empresa = l.Patio.Empresa.Nome,
                    l.UsuarioId, l.Detalhes
                })
                .ToListAsync(ct);

            // Nomes de quem fez: inclui a equipe do PkLots e mensalistas, que não pertencem à empresa
            var idsUsuarios = registros.Where(r => r.UsuarioId != null).Select(r => r.UsuarioId!.Value).Distinct().ToList();
            var usuarios = await db.Usuarios.IgnoreQueryFilters()
                .Where(u => idsUsuarios.Contains(u.Id))
                .Select(u => new { u.Id, u.NomeCompleto, u.Email, u.Perfil })
                .ToDictionaryAsync(u => u.Id, ct);

            string Quem(Guid? id) => id is null
                ? NomeAutomatico
                : !usuarios.TryGetValue(id.Value, out var u) ? "Usuário removido"
                : u.Perfil == Perfis.SuperAdmin ? (contextoPatio.Perfil == Perfis.SuperAdmin ? $"{u.NomeCompleto} ({NomeSuporte})" : NomeSuporte)
                : u.Perfil == Perfis.Motorista ? $"Mensalista {u.NomeCompleto}"
                : string.IsNullOrWhiteSpace(u.NomeCompleto) ? u.Email : u.NomeCompleto;

            var superAdmin = contextoPatio.Perfil == Perfis.SuperAdmin;
            var itens = registros.Select(r =>
            {
                var (descricao, categoria) = CatalogoAuditoria.Acao(r.Acao);
                return new RegistroAuditoriaDto(r.Id, r.CriadoEm, r.Acao, descricao, categoria, r.Situacao, r.Placa, r.PatioId, r.Patio,
                    superAdmin ? r.Empresa : null, r.UsuarioId, Quem(r.UsuarioId), CatalogoAuditoria.Detalhes(r.Detalhes));
            }).ToList();

            return new PaginaAuditoriaDto(itens, total, pagina, tamanho, de, ate);
        }

        // Listas dos filtros. Superadmin: empresas e, com uma escolhida, os pátios e usuários dela
        public async Task<OpcoesAuditoriaDto> OpcoesAsync(Guid? empresaFiltro, CancellationToken ct = default)
        {
            var empresaId = EmpresaPermitida(empresaFiltro);
            var categorias = CatalogoAuditoria.Categorias.Select(c => new OpcaoFiltroDto(c.Codigo, c.Nome)).ToList();

            List<OpcaoFiltroDto> patios = [], usuarios = [], empresas = [];
            if (empresaId is not null)
            {
                patios = await db.Patios.Where(p => p.EmpresaId == empresaId)
                    .OrderByDescending(p => p.Ativo).ThenBy(p => p.Nome)
                    .Select(p => new OpcaoFiltroDto(p.Id.ToString(), p.Ativo ? p.Nome : p.Nome + " (inativo)"))
                    .ToListAsync(ct);
                usuarios = await db.Usuarios.Where(u => u.EmpresaId == empresaId)
                    .OrderBy(u => u.NomeCompleto)
                    .Select(u => new OpcaoFiltroDto(u.Id.ToString(), u.NomeCompleto == "" ? u.Email : u.NomeCompleto))
                    .ToListAsync(ct);
            }
            if (contextoPatio.Perfil == Perfis.SuperAdmin)
                empresas = await db.Empresas.OrderBy(e => e.Nome).Select(e => new OpcaoFiltroDto(e.Id.ToString(), e.Nome)).ToListAsync(ct);

            return new OpcoesAuditoriaDto(categorias, patios, usuarios, empresas);
        }

        // Admin: sempre a própria empresa (ignora o filtro); superadmin: a escolhida ou todas
        private Guid? EmpresaPermitida(Guid? filtro) => contextoPatio.Perfil switch
        {
            Perfis.SuperAdmin => filtro,
            Perfis.Admin => contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Usuário sem empresa."),
            _ => throw new ExcecaoProibido("A auditoria é restrita a administradores.")
        };

        private (DateOnly De, DateOnly Ate) Periodo(DateOnly? de, DateOnly? ate)
        {
            var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(relogio.GetUtcNow().UtcDateTime, FusoHorario.Brasil));
            var fim = ate ?? hoje;
            var inicio = de ?? fim.AddDays(-(DiasPadrao - 1));
            if (inicio > fim)
                throw new ExcecaoNegocio("A data inicial deve ser antes da final.");
            if (fim.DayNumber - inicio.DayNumber + 1 > DiasMaximos)
                throw new ExcecaoNegocio($"Escolha um período de até {DiasMaximos} dias.");
            return (inicio, fim);
        }
    }
}
