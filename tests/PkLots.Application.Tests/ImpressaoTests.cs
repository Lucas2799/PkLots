using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PkLots.Application.DTOs;
using PkLots.Application.Excecoes;
using PkLots.Application.Impressao;
using PkLots.Core.Constantes;
using PkLots.Infrastructure.Servicos;

namespace PkLots.Application.Tests
{
    public class GeradorTicketEscPosTests
    {
        private static TicketEntradaDto Ticket(int largura = 80, int tolerancia = 15) => new(
            Guid.Parse("1a2b3c4d-0000-0000-0000-000000000000"), "1A2B3C4D", "Pátio Padrão", "ABC1234", "A1",
            new DateTime(2026, 9, 30, 13, 30, 45, DateTimeKind.Utc), 15m, tolerancia, largura, "PKLOTS:teste");

        private static string Texto(byte[] dados) => Encoding.ASCII.GetString(dados);

        [Fact]
        public void Ticket_tem_placa_vaga_horario_local_preco_e_codigo()
        {
            var texto = Texto(GeradorTicketEscPos.TicketEntrada(Ticket()));

            Assert.Contains("ABC-1234", texto);
            Assert.Contains("VAGA A1", texto);
            Assert.Contains("30/09/2026 10:30", texto); // 13:30 UTC = 10:30 em Brasília
            Assert.Contains("R$ 15,00", texto);
            Assert.Contains("15 min", texto);
            Assert.Contains("Codigo: 1A2B3C4D", texto);
        }

        [Fact]
        public void Remove_acentos_para_compatibilidade_com_as_impressoras()
        {
            var texto = Texto(GeradorTicketEscPos.TicketEntrada(Ticket()));

            Assert.Contains("Patio Padrao", texto);
            Assert.DoesNotContain("?", texto.Replace("??", "")); // nenhum caractere não convertido
        }

        [Fact]
        public void Comeca_inicializando_e_termina_avancando_e_cortando()
        {
            var dados = GeradorTicketEscPos.TicketEntrada(Ticket());

            Assert.Equal(new byte[] { 0x1B, 0x40 }, dados[..2]);
            Assert.Equal(new byte[] { 0x1B, 0x64, 0x04, 0x1D, 0x56, 0x42, 0x03 }, dados[^7..]);
        }

        [Fact]
        public void QrCode_informa_o_tamanho_correto_dos_dados()
        {
            var dados = GeradorTicketEscPos.TicketEntrada(Ticket());
            var armazenar = new byte[] { 0x1D, 0x28, 0x6B };
            var inicio = Enumerable.Range(0, dados.Length - 8)
                .First(i => dados.AsSpan(i, 3).SequenceEqual(armazenar) && dados[i + 5] == 0x31 && dados[i + 6] == 0x50);

            var tamanho = dados[inicio + 3] + dados[inicio + 4] * 256;
            Assert.Equal("PKLOTS:teste".Length + 3, tamanho);
            Assert.Equal("PKLOTS:teste", Encoding.ASCII.GetString(dados, inicio + 8, "PKLOTS:teste".Length));
        }

        [Theory]
        [InlineData(58, 32)]
        [InlineData(80, 48)]
        public void Linhas_respeitam_a_largura_do_papel(int largura, int colunas)
        {
            var texto = Texto(GeradorTicketEscPos.TicketEntrada(Ticket(largura)));

            Assert.Contains(new string('-', colunas) + "\n", texto);
            Assert.DoesNotContain(new string('-', colunas + 1), texto);
        }

        [Fact]
        public void Sem_tolerancia_nao_mostra_a_linha()
        {
            Assert.DoesNotContain("Tolerancia", Texto(GeradorTicketEscPos.TicketEntrada(Ticket(tolerancia: 0))));
        }

        [Theory]
        [InlineData(15, "R$ 15,00")]
        [InlineData(1234.5, "R$ 1.234,50")]
        public void Formata_moeda_brasileira(double valor, string esperado) =>
            Assert.Equal(esperado, GeradorTicketEscPos.Moeda((decimal)valor));
    }

    public class ServicoImpressaoTests
    {
        private static readonly Guid Admin = Guid.NewGuid();

        private static async Task<Cenario> CenarioComEntrada(string modo, bool automatica = true)
        {
            var cenario = new Cenario();
            await cenario.Impressao.AtualizarConfiguracaoAsync(Admin,
                new ConfiguracaoImpressaoDto(modo, modo == ModoImpressao.Rede ? "192.168.0.50" : null, 9100, 80, automatica));
            return cenario;
        }

        [Fact]
        public async Task Desativada_ou_manual_nao_imprime_apos_entrada()
        {
            using var desativada = await CenarioComEntrada(ModoImpressao.Desativada);
            var e1 = await desativada.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            Assert.Null(await desativada.Impressao.AposEntradaAsync(e1.Id));

            using var manual = await CenarioComEntrada(ModoImpressao.Rede, automatica: false);
            var e2 = await manual.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));
            Assert.Null(await manual.Impressao.AposEntradaAsync(e2.Id));
            Assert.Empty(manual.Impressora.Enviados);
        }

        [Fact]
        public async Task Modo_navegador_avisa_o_app_para_imprimir()
        {
            using var cenario = await CenarioComEntrada(ModoImpressao.Navegador);
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            Assert.Equal(ResultadoImpressao.Navegador, await cenario.Impressao.AposEntradaAsync(entrada.Id));
            Assert.Empty(cenario.Impressora.Enviados);
        }

        [Fact]
        public async Task Modo_rede_envia_o_ticket_para_a_impressora()
        {
            using var cenario = await CenarioComEntrada(ModoImpressao.Rede);
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            Assert.Equal(ResultadoImpressao.Impresso, await cenario.Impressao.AposEntradaAsync(entrada.Id));
            var (host, porta, dados) = Assert.Single(cenario.Impressora.Enviados);
            Assert.Equal(("192.168.0.50", 9100), (host, porta));
            Assert.Contains("ABC-1234", Encoding.ASCII.GetString(dados));
        }

        [Fact]
        public async Task Impressora_fora_do_ar_nao_desfaz_a_entrada_e_gera_alerta()
        {
            using var cenario = await CenarioComEntrada(ModoImpressao.Rede);
            cenario.Impressora.Falhar = true;
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            Assert.Equal(ResultadoImpressao.Falhou, await cenario.Impressao.AposEntradaAsync(entrada.Id));
            Assert.Equal(1, await cenario.Db.EntradasVeiculos.CountAsync());
            Assert.True(await cenario.Db.LogsAuditoria.AnyAsync(l => l.Acao == AcoesAuditoria.ImpressaoFalhou));

            // Reimpressão manual informa o erro ao operador
            await Assert.ThrowsAsync<ExcecaoServicoExterno>(() => cenario.Impressao.ImprimirEntradaAsync(entrada.Id));
        }

        [Fact]
        public async Task Reimpressao_exige_impressora_de_rede()
        {
            using var cenario = await CenarioComEntrada(ModoImpressao.Navegador);
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            await Assert.ThrowsAsync<ExcecaoConflito>(() => cenario.Impressao.ImprimirEntradaAsync(entrada.Id));
        }

        [Fact]
        public async Task Ticket_traz_dados_do_patio_e_codigo_curto()
        {
            using var cenario = await CenarioComEntrada(ModoImpressao.Navegador);
            var entrada = await cenario.Entrada.RegistrarAsync(new RegistrarEntradaRequisicao("ABC1234"));

            var ticket = await cenario.Impressao.ObterTicketEntradaAsync(entrada.Id);

            Assert.Equal("Pátio Teste", ticket.NomePatio);
            Assert.Equal("A1", ticket.NumeroVaga);
            Assert.Equal(15m, ticket.ValorHora);
            var registro = await cenario.Db.EntradasVeiculos.SingleAsync();
            var codigo = registro.CodigoTicket!;
            Assert.Equal($"{codigo[..4]}-{codigo[4..]}", ticket.Codigo);
            Assert.Equal($"https://pklots.teste/t/{registro.TokenAcesso}", ticket.ConteudoQrCode);
        }

        [Theory]
        [InlineData("laser", null, 80)]          // modo inválido
        [InlineData("rede", null, 80)]           // rede sem impressora
        [InlineData("rede", "192.168.0.50", 70)] // largura inválida
        public async Task Recusa_configuracao_invalida(string modo, string? host, int largura)
        {
            using var cenario = new Cenario();

            await Assert.ThrowsAsync<ExcecaoNegocio>(() => cenario.Impressao.AtualizarConfiguracaoAsync(Admin,
                new ConfiguracaoImpressaoDto(modo, host, 9100, largura, true)));
        }
    }

    // Envio real por TCP para um "servidor de impressão" local (simula a porta 9100)
    public class ImpressoraTicketsTcpTests
    {
        [Fact]
        public async Task Envia_os_bytes_pela_rede()
        {
            var ouvinte = new TcpListener(IPAddress.Loopback, 0);
            ouvinte.Start();
            var porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
            var recebidos = Task.Run(async () =>
            {
                using var conexao = await ouvinte.AcceptTcpClientAsync();
                using var memoria = new MemoryStream();
                await conexao.GetStream().CopyToAsync(memoria);
                return memoria.ToArray();
            });

            byte[] dados = [0x1B, 0x40, (byte)'O', (byte)'K', 0x0A];
            await new ImpressoraTicketsTcp().EnviarAsync("127.0.0.1", porta, dados);

            Assert.Equal(dados, await recebidos.WaitAsync(TimeSpan.FromSeconds(5)));
            ouvinte.Stop();
        }

        [Fact]
        public async Task Impressora_desligada_gera_erro()
        {
            var ouvinte = new TcpListener(IPAddress.Loopback, 0);
            ouvinte.Start();
            var porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
            ouvinte.Stop(); // porta livre: ninguém escutando

            await Assert.ThrowsAnyAsync<SocketException>(() => new ImpressoraTicketsTcp().EnviarAsync("127.0.0.1", porta, [0x0A]));
        }
    }
}
