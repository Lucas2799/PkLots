using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Servicos;
using PkLots.Application.Tests;
using PkLots.Core.Constantes;
using PkLots.Infrastructure.Data;

namespace PkLots.Integracao.Tests
{
    // Duas requisições ao mesmo tempo, cada uma com a sua conexão, como dois balcões do mesmo pátio.
    // Só o PostgreSQL mostra isso: índices únicos e o controle de concorrência (xmin) não existem no banco em memória.
    public class ConcorrenciaTests
    {
        private const int Rodadas = 8;

        // Serviços de um "balcão": conexão própria, mesmo usuário e mesmo pátio do cenário
        private sealed class Balcao(Cenario cenario) : IDisposable
        {
            private readonly PkLotsDbContext _db = cenario.NovoDb();

            private ContextoPatioTeste Contexto => new(_db, cenario.Empresa.Id, cenario.UsuarioAtualId, cenario.PerfilAtual, null);
            private ReconhecedorPlaca Reconhecedor => new(cenario.Lpr, _db, Contexto, cenario.Relogio);
            public ServicoEntrada Entrada => new(_db, Contexto, cenario.Cache, Reconhecedor, cenario.Relogio);
            public ServicoSaida Saida => new(_db, Contexto, cenario.Cache, Reconhecedor, cenario.Relogio);

            public void Dispose() => _db.Dispose();
        }

        // Executa as duas ações juntas e devolve quantas deram certo e os erros das outras
        private static async Task<(int Sucessos, List<Exception> Erros)> AoMesmoTempo(params Func<Task>[] acoes)
        {
            using var largada = new Barrier(acoes.Length);
            var tarefas = acoes.Select(acao => Task.Run(async () =>
            {
                largada.SignalAndWait();
                await acao();
            })).ToArray();
            try
            {
                await Task.WhenAll(tarefas);
            }
            catch (Exception)
            {
                // os erros são lidos de cada tarefa abaixo
            }
            return (tarefas.Count(t => t.IsCompletedSuccessfully), tarefas.Where(t => t.IsFaulted).Select(t => t.Exception!.InnerException!).ToList());
        }

        [Fact]
        public async Task Mesma_placa_em_dois_balcoes_entra_uma_vez_e_o_outro_recebe_conflito()
        {
            using var cenario = new Cenario();
            for (var i = 0; i < Rodadas; i++)
            {
                var placa = $"ABC{1000 + i}";
                using var a = new Balcao(cenario);
                using var b = new Balcao(cenario);

                var (sucessos, erros) = await AoMesmoTempo(
                    () => a.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa)),
                    () => b.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa)));

                Assert.Equal(1, sucessos);
                Assert.IsType<ExcecaoConflito>(Assert.Single(erros)); // 409 com mensagem, nunca erro 500
                await using var db = cenario.NovoDb();
                Assert.Equal(1, await db.EntradasVeiculos.CountAsync(e => e.Placa == placa && e.Situacao == SituacaoEntrada.Ativa));
            }
        }

        [Fact]
        public async Task Placas_diferentes_ao_mesmo_tempo_entram_em_vagas_diferentes()
        {
            using var cenario = new Cenario();
            for (var i = 0; i < Rodadas; i++)
            {
                using var a = new Balcao(cenario);
                using var b = new Balcao(cenario);

                var (sucessos, erros) = await AoMesmoTempo(
                    () => a.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao($"AAA{1000 + i}")),
                    () => b.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao($"BBB{1000 + i}")));

                Assert.Empty(erros);
                Assert.Equal(2, sucessos);
            }

            await using var db = cenario.NovoDb();
            var vagas = await db.EntradasVeiculos.Where(e => e.Situacao == SituacaoEntrada.Ativa).Select(e => e.VagaId).ToListAsync();
            Assert.Equal(2 * Rodadas, vagas.Distinct().Count());
            Assert.Equal(2 * Rodadas, await db.Vagas.CountAsync(v => v.Situacao == SituacaoVaga.Ocupada));
        }

        [Fact]
        public async Task Confirmar_a_mesma_saida_em_dois_balcoes_registra_um_pagamento()
        {
            using var cenario = new Cenario();
            await cenario.Caixa.AbrirAsync(new AbrirCaixaRequisicao(0m));
            for (var i = 0; i < Rodadas; i++)
            {
                var placa = $"CCC{1000 + i}";
                await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao(placa));
                cenario.Relogio.Advance(TimeSpan.FromMinutes(30));
                var cobranca = await cenario.Saida.CobrarAsync(new GerarCobrancaRequisicao(placa, null));
                using var a = new Balcao(cenario);
                using var b = new Balcao(cenario);

                var confirmar = new ConfirmarSaidaRequisicao(FormasPagamento.Dinheiro, null);
                var (sucessos, erros) = await AoMesmoTempo(
                    () => a.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, confirmar),
                    () => b.Saida.ConfirmarAsync(cenario.UsuarioAtualId, cobranca.Id, confirmar));

                Assert.Equal(1, sucessos);
                Assert.IsType<ExcecaoConflito>(Assert.Single(erros));
                await using var db = cenario.NovoDb();
                Assert.Equal(1, await db.SaidasVeiculos.CountAsync(s => s.EntradaVeiculo.Placa == placa));
            }

            await using var banco = cenario.NovoDb();
            Assert.Equal(0, await banco.Vagas.CountAsync(v => v.Situacao == SituacaoVaga.Ocupada));
        }
    }
}
