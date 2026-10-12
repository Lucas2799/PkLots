using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Emails;
using PkLots.Application.Excecoes;
using PkLots.Application.Impressao;
using PkLots.Application.Interfaces;
using PkLots.Application.Pagamentos;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Faturas do aluguel do PkLots. A mensalidade é emitida sozinha alguns dias antes do vencimento
    // (pátios ativos × preço por pátio); o superadmin lança avulsas, dá baixa e cancela.
    // O PIX usa a chave do PkLots e é montado na leitura, então trocar a chave vale para todas as faturas abertas.
    public class ServicoFaturas(IPkLotsDbContext db, IContextoPatio contextoPatio, LinksPublicos links, TimeProvider relogio)
    {
        public const decimal ValorMaximo = 100_000m;

        // ---------- Emissão ----------

        // Emite as mensalidades que já estão na janela de emissão; seguro para rodar várias vezes
        public async Task<GeracaoFaturasDto> EmitirMensalidadesAsync(CancellationToken ct = default)
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var hoje = ServicoMensalistas.Hoje(agora);
            var assinaturas = await db.Assinaturas.IgnoreQueryFilters()
                .Include(a => a.Plano).Include(a => a.Empresa)
                .Where(a => a.Empresa.Ativa)
                .ToListAsync(ct);
            var config = await ObterConfiguracaoEntidadeAsync(ct);

            var emitidas = 0;
            foreach (var assinatura in assinaturas)
            {
                var inicio = DataLocal(assinatura.CriadoEm);
                // O mês atual e o próximo: perto da virada a emissão antecipada já cai no mês seguinte
                foreach (var competencia in new[] { new DateOnly(hoje.Year, hoje.Month, 1), new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1) })
                {
                    var vencimento = new DateOnly(competencia.Year, competencia.Month, assinatura.DiaVencimento);
                    if (hoje < vencimento.AddDays(-TipoFatura.DiasAntecedenciaEmissao)
                        || vencimento <= inicio                                  // antes de virar cliente
                        || assinatura.TesteAte is { } teste && teste >= vencimento) // dentro do período de teste
                        continue;
                    if (await db.Faturas.IgnoreQueryFilters().AnyAsync(f => f.EmpresaId == assinatura.EmpresaId
                            && f.Tipo == TipoFatura.Mensalidade && f.Competencia == competencia, ct))
                        continue;

                    var patios = await db.Patios.IgnoreQueryFilters().CountAsync(p => p.EmpresaId == assinatura.EmpresaId && p.Ativo, ct);
                    var preco = ServicoPlanos.PrecoEfetivo(assinatura);
                    if (patios * preco <= 0)
                        continue; // plano gratuito: nada a cobrar

                    var fatura = new Fatura
                    {
                        EmpresaId = assinatura.EmpresaId,
                        Tipo = TipoFatura.Mensalidade,
                        Competencia = competencia,
                        Descricao = $"Mensalidade {competencia:MM/yyyy} · plano {assinatura.Plano.Nome}",
                        Valor = patios * preco,
                        Vencimento = vencimento,
                        PatiosCobrados = patios,
                        PrecoPorPatio = preco,
                        NomePlano = assinatura.Plano.Nome,
                        CriadoEm = agora
                    };
                    db.Faturas.Add(fatura);
                    await AvisarAdminsAsync(fatura, assinatura.Empresa.Nome, config, agora, ct);
                    emitidas++;
                }
            }

            await db.SaveChangesAsync(ct);
            return new GeracaoFaturasDto(emitidas);
        }

        // ---------- Avisos de cobrança ----------

        // Lembrete antes do vencimento, aviso de vencida (com os dias até o bloqueio) e aviso de bloqueio.
        // Cada aviso sai uma vez; se o processamento atrasou, envia só o da etapa atual.
        public async Task<int> EnviarAvisosAsync(CancellationToken ct = default)
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var hoje = ServicoMensalistas.Hoje(agora);
            var limiteLembrete = hoje.AddDays(SituacaoAssinatura.DiasLembrete);
            var faturas = await db.Faturas.IgnoreQueryFilters()
                .Include(f => f.Empresa)
                .Where(f => f.Situacao == SituacaoFatura.Aberta && f.Vencimento <= limiteLembrete && f.Empresa.Ativa)
                .ToListAsync(ct);
            if (faturas.Count == 0)
                return 0;

            var situacoes = await Inadimplencia.ObterTodasAsync(db, hoje, ct);
            var config = await ObterConfiguracaoEntidadeAsync(ct);
            var enviados = 0;
            foreach (var fatura in faturas)
            {
                var situacao = situacoes.GetValueOrDefault(fatura.EmpresaId);
                var atraso = hoje.DayNumber - fatura.Vencimento.DayNumber;
                var (etapa, assunto, texto) = true switch
                {
                    _ when situacao?.Situacao == SituacaoAssinatura.Bloqueada && atraso > 0 => (SituacaoAssinatura.AvisoBloqueio,
                        "PkLots: acesso parcialmente bloqueado",
                        $"A fatura está vencida há {atraso} dias e o acesso foi parcialmente bloqueado: novas entradas e mudanças na administração voltam após o pagamento. Saídas, caixa e mensalidades continuam funcionando."),
                    _ when atraso >= 0 => (SituacaoAssinatura.AvisoVencimento,
                        "PkLots: fatura vencida",
                        situacao is { DiasParaBloqueio: > 0 } s
                            ? $"A fatura venceu e ainda não identificamos o pagamento. Se não for paga em {s.DiasParaBloqueio} dia(s), novas entradas serão bloqueadas até a regularização."
                            : "A fatura venceu e ainda não identificamos o pagamento."),
                    _ => (SituacaoAssinatura.AvisoLembrete,
                        "PkLots: fatura vence em breve",
                        $"Lembrete: a fatura vence em {-atraso} dia(s)."),
                };
                if (fatura.AvisosEnviados.Contains(etapa))
                    continue;

                var admins = await db.Usuarios.IgnoreQueryFilters()
                    .Where(u => u.EmpresaId == fatura.EmpresaId && u.Ativo && u.Perfil == Perfis.Admin)
                    .Select(u => u.Email)
                    .ToListAsync(ct);
                var mensagem = ModelosEmail.AvisoFatura(assunto, fatura.Empresa.Nome, texto, fatura.Descricao,
                    GeradorTicketEscPos.Moeda(fatura.Valor), fatura.Vencimento, Pix(fatura, config), links.UrlBase);
                foreach (var email in admins)
                    FilaEmails.Enfileirar(db, email, mensagem, agora);

                // Marca a etapa e as anteriores (não manda lembrete depois do vencimento)
                var etapas = new[] { SituacaoAssinatura.AvisoLembrete, SituacaoAssinatura.AvisoVencimento, SituacaoAssinatura.AvisoBloqueio };
                fatura.AvisosEnviados = [.. fatura.AvisosEnviados.Union(etapas.Take(Array.IndexOf(etapas, etapa) + 1))];
                enviados++;
            }

            await db.SaveChangesAsync(ct);
            return enviados;
        }

        // Para o aviso no app (gestão) e o bloqueio parcial
        public async Task<SituacaoCobrancaDto> SituacaoDaMinhaEmpresaAsync(CancellationToken ct = default)
        {
            var empresaId = contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Disponível só para usuários de uma empresa.");
            return await Inadimplencia.ObterAsync(db, empresaId, ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime), ct);
        }

        public async Task<FaturaDto> LancarAvulsaAsync(Guid empresaId, LancarFaturaRequisicao r, CancellationToken ct = default)
        {
            var empresa = await db.Empresas.FirstOrDefaultAsync(e => e.Id == empresaId, ct) ?? throw new ExcecaoNaoEncontrado("Empresa não encontrada.");
            if (string.IsNullOrWhiteSpace(r.Descricao) || r.Descricao.Trim().Length > TipoFatura.TamanhoMaximoDescricao)
                throw new ExcecaoNegocio($"Informe a descrição (até {TipoFatura.TamanhoMaximoDescricao} caracteres).");
            ValidarValor(r.Valor, "valor");
            var agora = relogio.GetUtcNow().UtcDateTime;
            if (r.Vencimento < ServicoMensalistas.Hoje(agora))
                throw new ExcecaoNegocio("O vencimento não pode ser no passado.");

            var fatura = new Fatura
            {
                EmpresaId = empresaId,
                Tipo = TipoFatura.Avulsa,
                Descricao = r.Descricao.Trim(),
                Valor = r.Valor,
                Vencimento = r.Vencimento,
                CriadoEm = agora
            };
            db.Faturas.Add(fatura);
            var config = await ObterConfiguracaoEntidadeAsync(ct);
            await AvisarAdminsAsync(fatura, empresa.Nome, config, agora, ct);
            await db.SaveChangesAsync(ct);
            return ParaDto(fatura, empresa.Nome, config, ServicoMensalistas.Hoje(agora));
        }

        // ---------- Baixa e cancelamento (superadmin) ----------

        public async Task<FaturaDto> BaixarAsync(Guid superAdminId, Guid id, BaixarFaturaRequisicao r, CancellationToken ct = default)
        {
            var fatura = await ObterAbertaAsync(id, ct);
            var forma = (r.FormaPagamento ?? "").Trim().ToLowerInvariant();
            if (!FormasPagamento.Todas.Contains(forma))
                throw new ExcecaoNegocio($"Forma de pagamento inválida. Use: {string.Join(", ", FormasPagamento.Todas)}.");
            var agora = relogio.GetUtcNow().UtcDateTime;
            var hoje = ServicoMensalistas.Hoje(agora);
            if (r.PagaEm > hoje)
                throw new ExcecaoNegocio("A data do pagamento não pode ser no futuro.");
            var valorPago = r.ValorPago ?? fatura.Valor;
            ValidarValor(valorPago, "valor pago");

            fatura.Situacao = SituacaoFatura.Paga;
            // Data informada: meio-dia local, para não mudar de dia ao converter o fuso
            fatura.PagaEm = r.PagaEm is { } data && data != hoje
                ? TimeZoneInfo.ConvertTimeToUtc(data.ToDateTime(new TimeOnly(12, 0)), FusoHorario.Brasil)
                : agora;
            fatura.FormaPagamento = forma;
            fatura.ValorPago = valorPago;
            fatura.Observacao = string.IsNullOrWhiteSpace(r.Observacao) ? null : r.Observacao.Trim();
            fatura.BaixadaPorId = superAdminId;
            await AuditarAsync(fatura, AcoesAuditoria.FaturaPaga, superAdminId, agora,
                new() { ["valorPago"] = valorPago, ["forma"] = forma }, ct);

            await db.SaveChangesAsync(ct);
            return await ObterDtoAsync(fatura, ct);
        }

        public async Task<FaturaDto> CancelarAsync(Guid superAdminId, Guid id, CancelarFaturaRequisicao r, CancellationToken ct = default)
        {
            var fatura = await ObterAbertaAsync(id, ct);
            var motivo = string.IsNullOrWhiteSpace(r.Motivo) ? null : r.Motivo.Trim();
            if (motivo is null)
                throw new ExcecaoNegocio("Informe o motivo do cancelamento.");
            var agora = relogio.GetUtcNow().UtcDateTime;

            fatura.Situacao = SituacaoFatura.Cancelada;
            fatura.CanceladaEm = agora;
            fatura.MotivoCancelamento = motivo.Length > 300 ? motivo[..300] : motivo;
            await AuditarAsync(fatura, AcoesAuditoria.FaturaCancelada, superAdminId, agora, new() { ["motivo"] = fatura.MotivoCancelamento }, ct);

            await db.SaveChangesAsync(ct);
            return await ObterDtoAsync(fatura, ct);
        }

        private async Task<Fatura> ObterAbertaAsync(Guid id, CancellationToken ct)
        {
            var fatura = await db.Faturas.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new ExcecaoNaoEncontrado("Fatura não encontrada.");
            if (fatura.Situacao != SituacaoFatura.Aberta)
                throw new ExcecaoConflito($"Esta fatura já está {fatura.Situacao}.");
            return fatura;
        }

        // ---------- Consulta ----------

        // Superadmin: todas, ou de uma empresa; "vencida" filtra as abertas com vencimento passado
        public async Task<IReadOnlyList<FaturaDto>> ListarAsync(Guid? empresaId, string? situacao, CancellationToken ct = default)
        {
            var hoje = ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime);
            var consulta = db.Faturas.Where(f => empresaId == null || f.EmpresaId == empresaId);
            consulta = situacao switch
            {
                null or "" => consulta,
                SituacaoFatura.Vencida => consulta.Where(f => f.Situacao == SituacaoFatura.Aberta && f.Vencimento < hoje),
                _ => consulta.Where(f => f.Situacao == situacao)
            };
            return await MontarAsync(consulta, hoje, ct);
        }

        // Admin: as faturas da própria empresa
        public async Task<IReadOnlyList<FaturaDto>> ListarDaMinhaEmpresaAsync(CancellationToken ct = default)
        {
            var empresaId = contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Disponível só para usuários de uma empresa.");
            var hoje = ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime);
            return await MontarAsync(db.Faturas.Where(f => f.EmpresaId == empresaId && f.Situacao != SituacaoFatura.Cancelada), hoje, ct);
        }

        private async Task<IReadOnlyList<FaturaDto>> MontarAsync(IQueryable<Fatura> consulta, DateOnly hoje, CancellationToken ct)
        {
            var faturas = await consulta
                .OrderByDescending(f => f.Vencimento).ThenByDescending(f => f.CriadoEm)
                .Select(f => new { Fatura = f, NomeEmpresa = f.Empresa.Nome })
                .ToListAsync(ct);
            var config = await ObterConfiguracaoEntidadeAsync(ct);
            return faturas.Select(f => ParaDto(f.Fatura, f.NomeEmpresa, config, hoje)).ToList();
        }

        private async Task<FaturaDto> ObterDtoAsync(Fatura fatura, CancellationToken ct)
        {
            var nome = await db.Empresas.Where(e => e.Id == fatura.EmpresaId).Select(e => e.Nome).FirstAsync(ct);
            return ParaDto(fatura, nome, await ObterConfiguracaoEntidadeAsync(ct), ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime));
        }

        // ---------- Dados de recebimento do PkLots ----------

        public async Task<ConfiguracaoPlataformaDto> ObterConfiguracaoAsync(CancellationToken ct = default)
        {
            var c = await ObterConfiguracaoEntidadeAsync(ct);
            return new ConfiguracaoPlataformaDto(c?.ChavePix, c?.NomeRecebedor, c?.CidadeRecebedor);
        }

        public async Task<ConfiguracaoPlataformaDto> SalvarConfiguracaoAsync(ConfiguracaoPlataformaDto r, CancellationToken ct = default)
        {
            string? chave = null, nome = null, cidade = null;
            if (!string.IsNullOrWhiteSpace(r.ChavePix))
            {
                chave = PixCopiaECola.ValidarChave(r.ChavePix)
                    ?? throw new ExcecaoNegocio("Chave PIX inválida. Use CPF, CNPJ, e-mail, telefone (+55...) ou chave aleatória.");
                nome = string.IsNullOrWhiteSpace(r.NomeRecebedor) ? throw new ExcecaoNegocio("Informe o nome do recebedor.") : r.NomeRecebedor.Trim();
                cidade = string.IsNullOrWhiteSpace(r.CidadeRecebedor) ? throw new ExcecaoNegocio("Informe a cidade do recebedor.") : r.CidadeRecebedor.Trim();
                if (nome.Length > 25 || cidade.Length > 15)
                    throw new ExcecaoNegocio("Nome do recebedor até 25 caracteres e cidade até 15 (padrão do PIX).");
            }

            var config = await db.ConfiguracoesPlataforma.FirstOrDefaultAsync(ct);
            if (config is null)
            {
                config = new ConfiguracaoPlataforma();
                db.ConfiguracoesPlataforma.Add(config);
            }
            config.ChavePix = chave;
            config.NomeRecebedor = nome;
            config.CidadeRecebedor = cidade;
            config.AtualizadoEm = relogio.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return new ConfiguracaoPlataformaDto(chave, nome, cidade);
        }

        private Task<ConfiguracaoPlataforma?> ObterConfiguracaoEntidadeAsync(CancellationToken ct) =>
            db.ConfiguracoesPlataforma.FirstOrDefaultAsync(ct);

        // ---------- Apoio ----------

        public static string? Pix(Fatura f, ConfiguracaoPlataforma? config) =>
            f.Situacao != SituacaoFatura.Aberta || config?.ChavePix is null
                ? null
                : PixCopiaECola.Gerar(config.ChavePix, config.NomeRecebedor!, config.CidadeRecebedor!, f.Valor, TxId(f));

        // Identifica a fatura no extrato do PIX (até 25 caracteres alfanuméricos)
        public static string TxId(Fatura f) => "PKL" + f.Id.ToString("N")[..22].ToUpperInvariant();

        public static FaturaDto ParaDto(Fatura f, string nomeEmpresa, ConfiguracaoPlataforma? config, DateOnly hoje)
        {
            var vencida = f.Situacao == SituacaoFatura.Aberta && f.Vencimento < hoje;
            return new FaturaDto(
                f.Id, f.EmpresaId, nomeEmpresa, f.Tipo, f.Competencia, f.Descricao, f.Valor, f.Vencimento,
                vencida ? SituacaoFatura.Vencida : f.Situacao,
                vencida ? hoje.DayNumber - f.Vencimento.DayNumber : 0,
                f.PatiosCobrados, f.PrecoPorPatio, f.NomePlano, f.PagaEm, f.FormaPagamento, f.ValorPago, f.Observacao,
                f.CanceladaEm, f.MotivoCancelamento, Pix(f, config), f.CriadoEm);
        }

        private async Task AvisarAdminsAsync(Fatura fatura, string nomeEmpresa, ConfiguracaoPlataforma? config, DateTime agora, CancellationToken ct)
        {
            var admins = await db.Usuarios.IgnoreQueryFilters()
                .Where(u => u.EmpresaId == fatura.EmpresaId && u.Ativo && u.Perfil == Perfis.Admin)
                .Select(u => u.Email)
                .ToListAsync(ct);
            var mensagem = ModelosEmail.FaturaEmitida(nomeEmpresa, fatura.Descricao, GeradorTicketEscPos.Moeda(fatura.Valor), fatura.Vencimento,
                Pix(fatura, config), links.UrlBase);
            foreach (var email in admins)
                FilaEmails.Enfileirar(db, email, mensagem, agora);
        }

        private async Task AuditarAsync(Fatura fatura, string acao, Guid superAdminId, DateTime agora, Dictionary<string, object> dados, CancellationToken ct)
        {
            var patio = await db.Patios.IgnoreQueryFilters().Where(p => p.EmpresaId == fatura.EmpresaId).OrderBy(p => p.CriadoEm)
                .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            if (patio is null)
                return;
            dados["superAdminId"] = superAdminId.ToString();
            dados["faturaId"] = fatura.Id.ToString();
            dados["descricao"] = fatura.Descricao;
            dados["valor"] = fatura.Valor;
            Auditoria.Registrar(db, patio.Value, acao, SituacaoAuditoria.Sucesso, null, agora, dados);
        }

        private static void ValidarValor(decimal valor, string nome)
        {
            if (valor <= 0 || valor > ValorMaximo || decimal.Round(valor, 2) != valor)
                throw new ExcecaoNegocio($"Informe um {nome} válido (maior que zero, até R$ 100.000,00).");
        }

        private static DateOnly DataLocal(DateTime utc) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FusoHorario.Brasil));
    }
}
