using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Funcionalidades exclusivas do perfil admin: preço, tolerância, quantidade de vagas e usuários
    public class ServicoAdministracao(
        IPkLotsDbContext db,
        IContextoPatio contextoPatio,
        ICacheVagas cache,
        IHashSenha hashSenha,
        TimeProvider relogio)
    {
        public const decimal ValorHoraMaximo = 1000m;
        public const int ToleranciaMaxima = 240;

        // ---------- Configuração do pátio ----------

        public async Task<ConfiguracaoPatioDto> ObterConfiguracaoAsync(CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            var totalVagas = await db.Vagas.CountAsync(v => v.PatioId == patio.Id && v.Ativa, ct);
            return ParaDto(patio, totalVagas);
        }

        public async Task<ConfiguracaoPatioDto> AtualizarConfiguracaoAsync(Guid adminId, AtualizarConfiguracaoRequisicao requisicao, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(requisicao.Nome))
                throw new ExcecaoNegocio("Informe o nome do pátio.");
            if (requisicao.ValorHora <= 0 || requisicao.ValorHora > ValorHoraMaximo)
                throw new ExcecaoNegocio("O valor por hora deve ser maior que zero e até R$ 1.000,00.");
            if (decimal.Round(requisicao.ValorHora, 2) != requisicao.ValorHora)
                throw new ExcecaoNegocio("O valor por hora deve ter no máximo duas casas decimais.");
            if (requisicao.MinutosTolerancia < 0 || requisicao.MinutosTolerancia > ToleranciaMaxima)
                throw new ExcecaoNegocio($"A tolerância deve ficar entre 0 e {ToleranciaMaxima} minutos.");

            if (requisicao.ValorMensalidade is < 0 or > 100_000m || (requisicao.ValorMensalidade is { } mensalidade && decimal.Round(mensalidade, 2) != mensalidade))
                throw new ExcecaoNegocio("Valor da mensalidade inválido.");

            var patio = await ObterPatioAsync(ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            var reservadas = requisicao.VagasReservadasMensalistas ?? patio.VagasReservadasMensalistas;
            var totalVagas = await db.Vagas.CountAsync(v => v.PatioId == patio.Id && v.Ativa, ct);
            if (reservadas < 0 || reservadas > totalVagas)
                throw new ExcecaoNegocio($"As vagas reservadas a mensalistas devem ficar entre 0 e {totalVagas} (total do pátio).");

            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.ConfiguracaoAlterada, SituacaoAuditoria.Sucesso, null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["nomeAnterior"] = patio.Nome,
                    ["nome"] = requisicao.Nome.Trim(),
                    ["valorHoraAnterior"] = patio.ValorHora,
                    ["valorHora"] = requisicao.ValorHora,
                    ["toleranciaAnterior"] = patio.MinutosTolerancia,
                    ["tolerancia"] = requisicao.MinutosTolerancia,
                    ["mensalidadeAnterior"] = patio.ValorMensalidade,
                    ["mensalidade"] = requisicao.ValorMensalidade ?? patio.ValorMensalidade,
                    ["reservadasAnterior"] = patio.VagasReservadasMensalistas,
                    ["reservadas"] = reservadas
                });

            patio.Nome = requisicao.Nome.Trim();
            patio.ValorHora = requisicao.ValorHora;
            patio.MinutosTolerancia = requisicao.MinutosTolerancia;
            patio.ValorMensalidade = requisicao.ValorMensalidade ?? patio.ValorMensalidade;
            patio.VagasReservadasMensalistas = reservadas;
            patio.AtualizadoEm = agora;

            await db.SaveChangesAsync(ct);
            await cache.InvalidarAsync(patio.Id); // vagas reservadas mudam o resumo
            return await ObterConfiguracaoAsync(ct);
        }

        // ---------- Quantidade de vagas ----------

        // As vagas ocupam as posições 0..total-1 (A1, A2... A10, B1...). Aumentar reativa ou cria vagas;
        // diminuir desativa as últimas (histórico preservado) e é recusado se alguma delas estiver ocupada.
        public async Task<ConfiguracaoPatioDto> AlterarQuantidadeVagasAsync(Guid adminId, int total, CancellationToken ct = default)
        {
            if (total < 1 || total > LayoutPatio.MaximoVagas)
                throw new ExcecaoNegocio($"A quantidade de vagas deve ficar entre 1 e {LayoutPatio.MaximoVagas}.");

            var patio = await ObterPatioAsync(ct);
            if (total < patio.VagasReservadasMensalistas)
                throw new ExcecaoConflito($"O pátio tem {patio.VagasReservadasMensalistas} vagas reservadas a mensalistas: reduza a reserva antes.");
            var agora = relogio.GetUtcNow().UtcDateTime;
            var vagas = await db.Vagas.Where(v => v.PatioId == patio.Id).ToDictionaryAsync(v => v.Numero, ct);
            var totalAnterior = vagas.Values.Count(v => v.Ativa);

            // Vagas que ficariam fora da nova quantidade
            var numerosMantidos = Enumerable.Range(0, total).Select(LayoutPatio.Numero).ToHashSet();
            var removidas = vagas.Values.Where(v => v.Ativa && !numerosMantidos.Contains(v.Numero)).ToList();

            var ocupadas = removidas.Where(v => v.Ocupada).Select(v => v.Numero).OrderBy(n => n).ToList();
            if (ocupadas.Count > 0)
                throw new ExcecaoConflito($"Não é possível remover vagas ocupadas: {string.Join(", ", ocupadas)}. Registre a saída antes.");

            foreach (var vaga in removidas)
            {
                vaga.Ativa = false;
                vaga.Disponivel = false;
                vaga.Situacao = SituacaoVaga.Disponivel;
                vaga.Observacoes = null;
                vaga.AtualizadoEm = agora;
            }

            for (var posicao = 0; posicao < total; posicao++)
            {
                var numero = LayoutPatio.Numero(posicao);
                if (vagas.TryGetValue(numero, out var existente))
                {
                    if (existente.Ativa)
                        continue;

                    existente.Ativa = true;
                    existente.Disponivel = true;
                    existente.Ocupada = false;
                    existente.Situacao = SituacaoVaga.Disponivel;
                    existente.VeiculoAtualId = null;
                    existente.OcupadaDesde = null;
                    existente.AtualizadoEm = agora;
                }
                else
                {
                    db.Vagas.Add(VagasDoPatio.Nova(patio.Id, posicao, agora));
                }
            }

            patio.MaximoVagas = total;
            patio.AtualizadoEm = agora;

            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.QuantidadeVagasAlterada, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["adminId"] = adminId.ToString(), ["totalAnterior"] = totalAnterior, ["total"] = total });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ExcecaoConflito("Uma vaga foi ocupada durante a alteração. Tente novamente.");
            }

            await cache.InvalidarAsync(patio.Id);
            return ParaDto(patio, total);
        }

        // ---------- Usuários da empresa (admins e operadores) ----------

        public async Task<IReadOnlyList<UsuarioDto>> ListarUsuariosAsync(CancellationToken ct = default)
        {
            var empresaId = ObterEmpresaId();
            return await db.Usuarios
                .Where(u => u.EmpresaId == empresaId)
                .OrderByDescending(u => u.Ativo).ThenBy(u => u.NomeCompleto)
                .Select(u => new UsuarioDto(u.Id, u.Email, u.NomeCompleto, u.Perfil, u.Ativo, u.UltimoLogin, u.CriadoEm,
                    u.Patios.Select(up => up.PatioId).ToList()))
                .ToListAsync(ct);
        }

        public async Task<UsuarioDto> CriarUsuarioAsync(Guid adminId, CriarUsuarioRequisicao requisicao, CancellationToken ct = default)
        {
            var email = RegrasUsuario.NormalizarEmail(requisicao.Email);
            var nome = RegrasUsuario.ValidarNome(requisicao.NomeCompleto);
            var perfil = RegrasUsuario.ValidarPerfilDeEmpresa(requisicao.Perfil);
            RegrasUsuario.ValidarSenha(requisicao.Senha);
            var patiosIds = await ValidarPatiosAsync(perfil, requisicao.PatiosIds, ct);

            if (await db.Usuarios.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
                throw new ExcecaoConflito("E-mail já cadastrado.");

            var agora = relogio.GetUtcNow().UtcDateTime;
            var usuario = new Usuario
            {
                EmpresaId = ObterEmpresaId(),
                Email = email,
                NomeCompleto = nome,
                Perfil = perfil,
                SenhaHash = hashSenha.Gerar(requisicao.Senha),
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            foreach (var patioId in patiosIds)
                usuario.Patios.Add(new UsuarioPatio { PatioId = patioId });
            db.Usuarios.Add(usuario);

            Auditoria.Registrar(db, await contextoPatio.ObterPatioIdAsync(ct), AcoesAuditoria.UsuarioCriado, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["adminId"] = adminId.ToString(), ["usuarioId"] = usuario.Id.ToString(), ["email"] = email, ["perfil"] = perfil });

            await db.SaveChangesAsync(ct);
            return ParaDto(usuario);
        }

        public async Task<UsuarioDto> AtualizarUsuarioAsync(Guid adminId, Guid usuarioId, AtualizarUsuarioRequisicao requisicao, CancellationToken ct = default)
        {
            var nome = RegrasUsuario.ValidarNome(requisicao.NomeCompleto);
            var perfil = RegrasUsuario.ValidarPerfilDeEmpresa(requisicao.Perfil);
            var patiosIds = await ValidarPatiosAsync(perfil, requisicao.PatiosIds, ct);
            var usuario = await ObterUsuarioAsync(usuarioId, ct);

            var perdeAdmin = usuario.Perfil == Perfis.Admin && usuario.Ativo && (perfil != Perfis.Admin || !requisicao.Ativo);
            if (perdeAdmin && usuario.Id == adminId)
                throw new ExcecaoNegocio("Você não pode remover o seu próprio acesso de administrador.");
            if (perdeAdmin)
            {
                var outrosAdmins = await db.Usuarios.CountAsync(
                    u => u.EmpresaId == usuario.EmpresaId && u.Id != usuario.Id && u.Ativo && u.Perfil == Perfis.Admin, ct);
                if (outrosAdmins == 0)
                    throw new ExcecaoConflito("A empresa precisa ter pelo menos um administrador ativo.");
            }

            var agora = relogio.GetUtcNow().UtcDateTime;
            Auditoria.Registrar(db, await contextoPatio.ObterPatioIdAsync(ct), AcoesAuditoria.UsuarioAlterado, SituacaoAuditoria.Sucesso, null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["usuarioId"] = usuario.Id.ToString(),
                    ["perfilAnterior"] = usuario.Perfil,
                    ["perfil"] = perfil,
                    ["ativoAnterior"] = usuario.Ativo,
                    ["ativo"] = requisicao.Ativo,
                    ["patios"] = string.Join(",", patiosIds)
                });

            usuario.NomeCompleto = nome;
            usuario.Perfil = perfil;
            usuario.Ativo = requisicao.Ativo;
            usuario.AtualizadoEm = agora;

            // Substitui os pátios vinculados
            usuario.Patios.Clear();
            foreach (var patioId in patiosIds)
                usuario.Patios.Add(new UsuarioPatio { UsuarioId = usuario.Id, PatioId = patioId });

            await db.SaveChangesAsync(ct);
            return ParaDto(usuario);
        }

        public async Task RedefinirSenhaAsync(Guid adminId, Guid usuarioId, string senha, CancellationToken ct = default)
        {
            RegrasUsuario.ValidarSenha(senha);
            var usuario = await ObterUsuarioAsync(usuarioId, ct);
            var agora = relogio.GetUtcNow().UtcDateTime;

            usuario.SenhaHash = hashSenha.Gerar(senha);
            usuario.AtualizadoEm = agora;

            Auditoria.Registrar(db, await contextoPatio.ObterPatioIdAsync(ct), AcoesAuditoria.SenhaRedefinida, SituacaoAuditoria.Sucesso, null, agora,
                new() { ["adminId"] = adminId.ToString(), ["usuarioId"] = usuario.Id.ToString() });

            await db.SaveChangesAsync(ct);
        }

        // Operador precisa de pelo menos um pátio, todos da empresa; admin acessa todos (não guarda vínculos)
        private async Task<List<Guid>> ValidarPatiosAsync(string perfil, IReadOnlyList<Guid>? patiosIds, CancellationToken ct)
        {
            if (perfil != Perfis.Operador)
                return [];

            var solicitados = (patiosIds ?? []).Distinct().ToList();
            if (solicitados.Count == 0)
                throw new ExcecaoNegocio("Selecione pelo menos um pátio para o operador.");

            var empresaId = ObterEmpresaId();
            var validos = await db.Patios.CountAsync(p => p.EmpresaId == empresaId && solicitados.Contains(p.Id), ct);
            if (validos != solicitados.Count)
                throw new ExcecaoNegocio("Um ou mais pátios selecionados não pertencem à empresa.");
            return solicitados;
        }

        // ---------- Auxiliares ----------

        private async Task<Patio> ObterPatioAsync(CancellationToken ct)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            return await db.Patios.FirstOrDefaultAsync(p => p.Id == patioId, ct)
                ?? throw new ExcecaoNaoEncontrado("Pátio não encontrado.");
        }

        private Guid ObterEmpresaId() =>
            contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Esta função é exclusiva de usuários de uma empresa.");

        private async Task<Usuario> ObterUsuarioAsync(Guid usuarioId, CancellationToken ct)
        {
            var empresaId = ObterEmpresaId();
            return await db.Usuarios.Include(u => u.Patios).FirstOrDefaultAsync(u => u.Id == usuarioId && u.EmpresaId == empresaId, ct)
                ?? throw new ExcecaoNaoEncontrado("Usuário não encontrado.");
        }

        private static ConfiguracaoPatioDto ParaDto(Patio patio, int totalVagas) =>
            new(patio.Nome, patio.ValorHora, patio.MinutosTolerancia, totalVagas, LayoutPatio.VagasPorFileira, LayoutPatio.MaximoVagas,
                patio.ValorMensalidade, patio.VagasReservadasMensalistas);

        private static UsuarioDto ParaDto(Usuario u) =>
            new(u.Id, u.Email, u.NomeCompleto, u.Perfil, u.Ativo, u.UltimoLogin, u.CriadoEm, u.Patios.Select(up => up.PatioId).ToList());
    }
}
