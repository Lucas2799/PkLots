using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Interfaces;
using PkLots.Core.Constantes;
using PkLots.Core.Entities;

namespace PkLots.Application.Servicos
{
    // Tabela de preço do pátio escolhido no painel (admin): leitura, gravação e simulação
    public class ServicoTabelaPreco(IPkLotsDbContext db, IContextoPatio contextoPatio, TimeProvider relogio)
    {
        public const int DiasMaximosSimulacao = 31;

        public async Task<TabelaPrecoDto> ObterAsync(CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            return ParaDto(patio, TabelaPreco.Do(patio));
        }

        public async Task<TabelaPrecoDto> SalvarAsync(Guid adminId, SalvarTabelaPrecoRequisicao requisicao, CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            var tabela = Montar(requisicao.Semana, requisicao.FimDeSemana, patio);
            var tolerancia = Tolerancia(requisicao.MinutosTolerancia, patio);
            var agora = relogio.GetUtcNow().UtcDateTime;

            Auditoria.Registrar(db, patio.Id, AcoesAuditoria.ConfiguracaoAlterada, SituacaoAuditoria.Sucesso, null, agora,
                new()
                {
                    ["adminId"] = adminId.ToString(),
                    ["tabelaPrecoAnterior"] = TabelaPreco.Do(patio).Descricao(),
                    ["tabelaPreco"] = tabela.Descricao(),
                    ["toleranciaAnterior"] = patio.MinutosTolerancia,
                    ["tolerancia"] = tolerancia
                });

            // O valor/hora da semana mora no pátio (telas e relatórios antigos usam ele)
            if (tabela.Semana.Modo == ModosCobranca.Hora)
                patio.ValorHora = tabela.Semana.ValorHora;
            patio.TabelaPrecoJson = tabela.ParaJson();
            patio.MinutosTolerancia = tolerancia;
            patio.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);
            return ParaDto(patio, TabelaPreco.Do(patio));
        }

        public async Task<SimulacaoTarifaDto> SimularAsync(SimularTarifaRequisicao requisicao, CancellationToken ct = default)
        {
            var patio = await ObterPatioAsync(ct);
            var tabela = Montar(requisicao.Semana, requisicao.FimDeSemana, patio);
            var entrada = Utc(requisicao.Entrada);
            var saida = Utc(requisicao.Saida);
            if (saida <= entrada)
                throw new ExcecaoNegocio("A saída deve ser depois da entrada.");
            if (saida - entrada > TimeSpan.FromDays(DiasMaximosSimulacao))
                throw new ExcecaoNegocio($"Simule estadias de até {DiasMaximosSimulacao} dias.");

            var resultado = CalculadoraTarifa.Calcular(entrada, saida, tabela, Tolerancia(requisicao.MinutosTolerancia, patio));
            return new SimulacaoTarifaDto(resultado.ValorTotal, resultado.DuracaoFormatada, resultado.Isento,
                !ReferenceEquals(tabela.RegrasPara(entrada), tabela.Semana), resultado.Detalhamento);
        }

        // Valida a tabela; no modo por frações o valor/hora da semana não é editado e continua o do pátio
        private static TabelaPreco Montar(RegrasPreco? semana, RegrasPreco? fimDeSemana, Patio patio)
        {
            if (semana is null)
                throw new ExcecaoNegocio("Informe as regras de preço da semana.");
            if (semana.Modo != ModosCobranca.Hora)
                semana = semana with { ValorHora = patio.ValorHora };
            var tabela = new TabelaPreco(semana, fimDeSemana);
            tabela.Validar();
            return tabela;
        }

        private static int Tolerancia(int? minutos, Patio patio) => minutos switch
        {
            null => patio.MinutosTolerancia,
            < 0 or > ServicoAdministracao.ToleranciaMaxima =>
                throw new ExcecaoNegocio($"A tolerância deve ficar entre 0 e {ServicoAdministracao.ToleranciaMaxima} minutos."),
            _ => minutos.Value
        };

        private static DateTime Utc(DateTime data) => data.Kind switch
        {
            DateTimeKind.Utc => data,
            DateTimeKind.Local => data.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(data, FusoHorario.Brasil) // sem fuso: horário de Brasília
        };

        private static TabelaPrecoDto ParaDto(Patio patio, TabelaPreco tabela) =>
            new(tabela.Semana, tabela.FimDeSemana, patio.MinutosTolerancia, tabela.Descricao());

        private async Task<Patio> ObterPatioAsync(CancellationToken ct)
        {
            var patioId = await contextoPatio.ObterPatioIdAsync(ct);
            return await db.Patios.FirstOrDefaultAsync(p => p.Id == patioId, ct) ?? throw new ExcecaoNaoEncontrado("Pátio não encontrado.");
        }
    }
}
