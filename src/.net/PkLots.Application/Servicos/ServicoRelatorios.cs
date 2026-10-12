using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Relatórios por período (datas no fuso de Brasília, fim inclusivo):
    // o do dono cobre os pátios da empresa dele; o da plataforma, o uso de cada empresa cliente.
    public class ServicoRelatorios(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        public const int DiasMaximos = 366;
        public const string FormaNaoInformada = "nao_informada";

        public async Task<RelatorioEmpresaDto> ObterDaEmpresaAsync(DateOnly? de, DateOnly? ate, Guid? patioId, CancellationToken ct = default)
        {
            var empresaId = contextoPatio.EmpresaId ?? throw new ExcecaoProibido("Relatório disponível só para usuários de uma empresa.");
            await RegrasPlano.ExigirRecursoAsync(db, empresaId, RecursosPlano.Relatorios, ct);
            var (inicioData, fimData) = Periodo(de, ate);
            var (inicio, fim) = IntervaloUtc(inicioData, fimData);

            // Inclui pátios desativados: o movimento deles no período continua sendo da empresa
            var patios = await db.Patios
                .Where(p => p.EmpresaId == empresaId && (patioId == null || p.Id == patioId))
                .OrderByDescending(p => p.Ativo).ThenBy(p => p.Nome)
                .Select(p => new { p.Id, p.Nome, p.Ativo, Vagas = p.Vagas.Count(v => v.Ativa) })
                .ToListAsync(ct);
            if (patioId is not null && patios.Count == 0)
                throw new ExcecaoNaoEncontrado("Pátio não encontrado.");
            var ids = patios.Select(p => p.Id).ToList();
            var nomeEmpresa = await db.Empresas.Where(e => e.Id == empresaId).Select(e => e.Nome).FirstAsync(ct);

            var entradas = await db.EntradasVeiculos
                .Where(e => ids.Contains(e.PatioId) && e.DataHoraEntrada >= inicio && e.DataHoraEntrada < fim && e.Situacao != SituacaoEntrada.Cancelada)
                .Select(e => new { e.PatioId, e.DataHoraEntrada })
                .ToListAsync(ct);
            var saidas = await db.SaidasVeiculos
                .Where(s => ids.Contains(s.PatioId) && s.DataHoraSaida >= inicio && s.DataHoraSaida < fim)
                .Select(s => new { s.PatioId, s.DataHoraSaida, s.ValorTotal, s.DuracaoSegundos, s.SituacaoPagamento, s.FormaPagamento, s.RegistradoPorId })
                .ToListAsync(ct);
            var mensalidades = await db.PagamentosMensalidade
                .Where(p => ids.Contains(p.PatioId) && p.PagoEm >= inicio && p.PagoEm < fim)
                .Select(p => new { p.PatioId, p.PagoEm, p.Valor, p.FormaPagamento, p.RegistradoPorId })
                .ToListAsync(ct);
            var cancelamentos = await db.EntradasVeiculos
                .Where(e => ids.Contains(e.PatioId) && e.CanceladaEm >= inicio && e.CanceladaEm < fim)
                .OrderByDescending(e => e.CanceladaEm)
                .Select(e => new { e.CanceladaEm, e.Placa, e.PatioId, e.CanceladaPorId, e.MotivoCancelamento })
                .ToListAsync(ct);
            var caixas = await db.Caixas
                .Where(c => ids.Contains(c.PatioId) && c.FechadoEm >= inicio && c.FechadoEm < fim)
                .Select(c => new { c.OperadorId, Diferenca = (c.DinheiroContado ?? 0) - (c.DinheiroEsperado ?? 0) })
                .ToListAsync(ct);

            // Totais
            var cobradas = saidas.Count(s => s.ValorTotal > 0);
            var receitaAvulsos = saidas.Sum(s => s.ValorTotal);
            var receitaMensalidades = mensalidades.Sum(p => p.Valor);
            var totais = new IndicadoresPeriodoDto(
                entradas.Count,
                saidas.Count,
                cobradas,
                saidas.Count(s => s.ValorTotal == 0 && s.SituacaoPagamento == SituacaoPagamento.Isento),
                saidas.Count(s => s.ValorTotal == 0 && s.SituacaoPagamento == SituacaoPagamento.Mensalista),
                cancelamentos.Count,
                receitaAvulsos,
                receitaMensalidades,
                receitaAvulsos + receitaMensalidades,
                cobradas > 0 ? Math.Round(receitaAvulsos / cobradas, 2) : 0,
                CalculadoraTarifa.FormatarDuracao(TimeSpan.FromSeconds(saidas.Count > 0 ? saidas.Average(s => s.DuracaoSegundos) : 0)));

            // Saídas cobradas sem forma registrada (anteriores ao caixa) entram como "não informada": a soma fecha com a receita
            var porForma = saidas.Where(s => s.ValorTotal > 0)
                .Select(s => new { Forma = s.FormaPagamento ?? FormaNaoInformada, Valor = s.ValorTotal })
                .Concat(mensalidades.Select(p => new { Forma = p.FormaPagamento, p.Valor }))
                .GroupBy(r => r.Forma)
                .Select(g => new RecebimentoPorFormaDto(g.Key, g.Count(), g.Sum(r => r.Valor)))
                .OrderByDescending(r => r.Total).ThenBy(r => r.Forma)
                .ToList();

            var porDia = PorDia(inicioData, fimData,
                entradas.Select(e => e.DataHoraEntrada),
                saidas.Select(s => (s.DataHoraSaida, s.ValorTotal)),
                mensalidades.Select(p => (p.PagoEm, p.Valor)));

            var porHora = Enumerable.Range(0, 24)
                .Select(h => new EntradasHoraDto(h, entradas.Count(e => HoraLocal(e.DataHoraEntrada) == h)))
                .ToList();

            var dias = fimData.DayNumber - inicioData.DayNumber + 1;
            var porPatio = patios.Select(p =>
            {
                var saidasPatio = saidas.Count(s => s.PatioId == p.Id);
                return new DesempenhoPatioDto(
                    p.Id, p.Nome, p.Ativo, p.Vagas,
                    entradas.Count(e => e.PatioId == p.Id),
                    saidasPatio,
                    saidas.Where(s => s.PatioId == p.Id).Sum(s => s.ValorTotal),
                    mensalidades.Where(m => m.PatioId == p.Id).Sum(m => m.Valor),
                    p.Vagas > 0 ? Math.Round((decimal)saidasPatio / p.Vagas / dias, 2) : 0);
            }).ToList();

            // Operadores (e admins) que receberam, fecharam caixa ou cancelaram entradas no período
            var pessoas = saidas.Where(s => s.RegistradoPorId != null && s.ValorTotal > 0).Select(s => s.RegistradoPorId!.Value)
                .Concat(mensalidades.Where(p => p.RegistradoPorId != null).Select(p => p.RegistradoPorId!.Value))
                .Concat(caixas.Select(c => c.OperadorId))
                .Concat(cancelamentos.Where(c => c.CanceladaPorId != null).Select(c => c.CanceladaPorId!.Value))
                .Distinct()
                .ToList();
            var nomes = await db.Usuarios
                .Where(u => pessoas.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

            var porOperador = pessoas.Select(id =>
                {
                    var recebidas = saidas.Where(s => s.RegistradoPorId == id && s.ValorTotal > 0).ToList();
                    var mensalidadesOperador = mensalidades.Where(p => p.RegistradoPorId == id).ToList();
                    var caixasOperador = caixas.Where(c => c.OperadorId == id).ToList();
                    return new DesempenhoOperadorDto(
                        id,
                        nomes.GetValueOrDefault(id, "Usuário removido"),
                        recebidas.Count,
                        mensalidadesOperador.Count,
                        recebidas.Sum(s => s.ValorTotal) + mensalidadesOperador.Sum(p => p.Valor),
                        caixasOperador.Count,
                        caixasOperador.Sum(c => c.Diferenca),
                        cancelamentos.Count(c => c.CanceladaPorId == id));
                })
                .OrderByDescending(o => o.Recebido).ThenBy(o => o.Nome)
                .ToList();

            var nomesPatios = patios.ToDictionary(p => p.Id, p => p.Nome);
            var listaCancelamentos = cancelamentos
                .Select(c => new CancelamentoRelatorioDto(
                    c.CanceladaEm!.Value, c.Placa, nomesPatios[c.PatioId],
                    c.CanceladaPorId is { } por ? nomes.GetValueOrDefault(por) : null,
                    c.MotivoCancelamento))
                .ToList();

            var hoje = ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime);
            var carteira = await db.Mensalistas
                .Where(m => ids.Contains(m.PatioId))
                .Select(m => new { m.Ativo, m.ValidoAte, m.CriadoEm })
                .ToListAsync(ct);
            var mensalistas = new MensalistasRelatorioDto(
                carteira.Count(m => m.Ativo),
                carteira.Count(m => m.Ativo && m.ValidoAte >= hoje),
                carteira.Count(m => m.Ativo && !(m.ValidoAte >= hoje)),
                carteira.Count(m => m.CriadoEm >= inicio && m.CriadoEm < fim),
                mensalidades.Count);

            return new RelatorioEmpresaDto(inicioData, fimData, nomeEmpresa, patioId is null ? null : patios[0].Nome,
                totais, porForma, porDia, porHora, porPatio, porOperador, listaCancelamentos, mensalistas);
        }

        // Superadmin: o DbContext não filtra por empresa (sem claim empresa_id)
        public async Task<RelatorioPlataformaDto> ObterDaPlataformaAsync(DateOnly? de, DateOnly? ate, CancellationToken ct = default)
        {
            var (inicioData, fimData) = Periodo(de, ate);
            var (inicio, fim) = IntervaloUtc(inicioData, fimData);

            var empresas = await db.Empresas
                .OrderByDescending(e => e.Ativa).ThenBy(e => e.Nome)
                .Select(e => new
                {
                    e.Id,
                    e.Nome,
                    e.Ativa,
                    e.CriadoEm,
                    Patios = e.Patios.Count(p => p.Ativo),
                    Vagas = e.Patios.Where(p => p.Ativo).SelectMany(p => p.Vagas).Count(v => v.Ativa),
                    Usuarios = e.Usuarios.Count(u => u.Ativo),
                })
                .ToListAsync(ct);

            var entradas = await db.EntradasVeiculos
                .Where(e => e.DataHoraEntrada >= inicio && e.DataHoraEntrada < fim && e.Situacao != SituacaoEntrada.Cancelada)
                .Select(e => new { e.Patio.EmpresaId, e.DataHoraEntrada })
                .ToListAsync(ct);
            var saidas = await db.SaidasVeiculos
                .Where(s => s.DataHoraSaida >= inicio && s.DataHoraSaida < fim)
                .Select(s => new { s.Patio.EmpresaId, s.DataHoraSaida, s.ValorTotal })
                .ToListAsync(ct);
            var mensalidades = await db.PagamentosMensalidade
                .Where(p => p.PagoEm >= inicio && p.PagoEm < fim)
                .Select(p => new { p.Patio.EmpresaId, p.PagoEm, p.Valor })
                .ToListAsync(ct);
            var mensalistasAtivos = await db.Mensalistas
                .Where(m => m.Ativo && m.Patio.Ativo)
                .GroupBy(m => m.Patio.EmpresaId)
                .Select(g => new { EmpresaId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(g => g.EmpresaId, g => g.Total, ct);
            var ultimasEntradas = await db.EntradasVeiculos
                .GroupBy(e => e.Patio.EmpresaId)
                .Select(g => new { EmpresaId = g.Key, Ultima = g.Max(e => e.DataHoraEntrada) })
                .ToDictionaryAsync(g => g.EmpresaId, g => g.Ultima, ct);

            // Cobrança do PkLots: plano, mensalidade atual e situação
            var assinaturas = await db.Assinaturas
                .Select(a => new { a.EmpresaId, a.Plano.Nome, Preco = a.PrecoPorPatioNegociado ?? a.Plano.PrecoPorPatio })
                .ToDictionaryAsync(a => a.EmpresaId, ct);
            var emAberto = await db.Faturas.Where(f => f.Situacao == SituacaoFatura.Aberta).SumAsync(f => f.Valor, ct);
            var cobranca = await Inadimplencia.ObterTodasAsync(db, ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime), ct);

            var uso = empresas.Select(e => new UsoEmpresaDto(
                    e.Id, e.Nome, e.Ativa, e.CriadoEm, e.Patios, e.Vagas, e.Usuarios,
                    mensalistasAtivos.GetValueOrDefault(e.Id),
                    entradas.Count(x => x.EmpresaId == e.Id),
                    saidas.Where(x => x.EmpresaId == e.Id).Sum(x => x.ValorTotal) + mensalidades.Where(x => x.EmpresaId == e.Id).Sum(x => x.Valor),
                    ultimasEntradas.TryGetValue(e.Id, out var ultima) ? ultima : null,
                    assinaturas.GetValueOrDefault(e.Id)?.Nome,
                    (assinaturas.GetValueOrDefault(e.Id)?.Preco ?? 0) * e.Patios,
                    cobranca.TryGetValue(e.Id, out var situacao) ? situacao.Situacao : SituacaoAssinatura.EmDia))
                .ToList();

            var ativas = uso.Where(e => e.Ativa).ToList();
            var totais = new IndicadoresPlataformaDto(
                ativas.Count,
                uso.Count(e => e.CriadoEm >= inicio && e.CriadoEm < fim),
                ativas.Count(e => e.Entradas == 0),
                ativas.Sum(e => e.Patios),
                ativas.Sum(e => e.Vagas),
                ativas.Sum(e => e.Usuarios),
                ativas.Sum(e => e.MensalistasAtivos),
                entradas.Count,
                saidas.Sum(s => s.ValorTotal) + mensalidades.Sum(p => p.Valor),
                ativas.Sum(e => e.MensalidadePkLots),
                emAberto,
                uso.Count(e => e.SituacaoCobranca != SituacaoAssinatura.EmDia));

            var porDia = PorDia(inicioData, fimData,
                entradas.Select(e => e.DataHoraEntrada),
                saidas.Select(s => (s.DataHoraSaida, s.ValorTotal)),
                mensalidades.Select(p => (p.PagoEm, p.Valor)));

            return new RelatorioPlataformaDto(inicioData, fimData, totais, porDia, uso);
        }

        // Sem datas: do dia 1º do mês atual até hoje
        private (DateOnly De, DateOnly Ate) Periodo(DateOnly? de, DateOnly? ate)
        {
            var hoje = ServicoMensalistas.Hoje(relogio.GetUtcNow().UtcDateTime);
            var fimData = ate ?? hoje;
            var inicioData = de ?? new DateOnly(fimData.Year, fimData.Month, 1);
            if (inicioData > fimData)
                throw new ExcecaoNegocio("A data inicial deve ser anterior ou igual à final.");
            if (fimData.DayNumber - inicioData.DayNumber + 1 > DiasMaximos)
                throw new ExcecaoNegocio($"Escolha um período de até {DiasMaximos} dias.");
            return (inicioData, fimData);
        }

        private static (DateTime Inicio, DateTime Fim) IntervaloUtc(DateOnly de, DateOnly ate) => (
            TimeZoneInfo.ConvertTimeToUtc(de.ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil),
            TimeZoneInfo.ConvertTimeToUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue), FusoHorario.Brasil));

        // Uma linha por dia do período, inclusive os sem movimento (o gráfico mostra os buracos)
        private static List<MovimentoDiaDto> PorDia(DateOnly de, DateOnly ate,
            IEnumerable<DateTime> entradas, IEnumerable<(DateTime Quando, decimal Valor)> saidas, IEnumerable<(DateTime Quando, decimal Valor)> mensalidades)
        {
            var entradasDia = entradas.GroupBy(DataLocal).ToDictionary(g => g.Key, g => g.Count());
            var saidasDia = saidas.GroupBy(s => DataLocal(s.Quando)).ToDictionary(g => g.Key, g => (Quantidade: g.Count(), Valor: g.Sum(s => s.Valor)));
            var mensalidadesDia = mensalidades.GroupBy(p => DataLocal(p.Quando)).ToDictionary(g => g.Key, g => g.Sum(p => p.Valor));

            return Enumerable.Range(0, ate.DayNumber - de.DayNumber + 1)
                .Select(de.AddDays)
                .Select(dia =>
                {
                    var saida = saidasDia.GetValueOrDefault(dia);
                    return new MovimentoDiaDto(dia, entradasDia.GetValueOrDefault(dia), saida.Quantidade, saida.Valor,
                        mensalidadesDia.GetValueOrDefault(dia));
                })
                .ToList();
        }

        private static DateTime Local(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), FusoHorario.Brasil);

        private static DateOnly DataLocal(DateTime utc) => DateOnly.FromDateTime(Local(utc));

        private static int HoraLocal(DateTime utc) => Local(utc).Hour;
    }
}
