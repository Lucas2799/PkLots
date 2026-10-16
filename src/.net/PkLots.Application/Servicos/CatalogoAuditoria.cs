using System.Globalization;
using System.Text.Json;
using PkLots.Application.DTOs;
using PkLots.Application.Impressao;
using PkLots.Core.Constantes;

namespace PkLots.Application.Servicos
{
    // Nomes legíveis das ações e dos detalhes gravados na auditoria
    public static class CatalogoAuditoria
    {
        public const string Movimento = "movimento";
        public const string Caixa = "caixa";
        public const string Mensalistas = "mensalistas";
        public const string Configuracao = "configuracao";
        public const string Usuarios = "usuarios";
        public const string Plataforma = "plataforma";

        public static readonly IReadOnlyList<(string Codigo, string Nome)> Categorias =
        [
            (Movimento, "Entradas e saídas"),
            (Caixa, "Caixa e pagamentos"),
            (Mensalistas, "Mensalistas"),
            (Configuracao, "Configurações"),
            (Usuarios, "Usuários e acessos"),
            (Plataforma, "Assinatura do PkLots")
        ];

        private static readonly Dictionary<string, (string Descricao, string Categoria)> Acoes = new()
        {
            [AcoesAuditoria.EntradaSucesso] = ("Entrada registrada", Movimento),
            [AcoesAuditoria.EntradaRecusada] = ("Entrada recusada", Movimento),
            [AcoesAuditoria.EntradaCancelada] = ("Entrada cancelada", Movimento),
            [AcoesAuditoria.SaidaSucesso] = ("Saída registrada", Movimento),
            [AcoesAuditoria.SaidaSemEntrada] = ("Saída sem entrada", Movimento),
            [AcoesAuditoria.LprFalhou] = ("Leitura de placa falhou", Movimento),
            [AcoesAuditoria.ImpressaoFalhou] = ("Impressão do ticket falhou", Movimento),
            [AcoesAuditoria.VagaManutencao] = ("Vaga em manutenção", Configuracao),
            [AcoesAuditoria.VagaLiberada] = ("Vaga liberada", Configuracao),
            [AcoesAuditoria.CaixaAberto] = ("Caixa aberto", Caixa),
            [AcoesAuditoria.CaixaFechado] = ("Caixa fechado", Caixa),
            [AcoesAuditoria.MovimentoCaixa] = ("Sangria ou suprimento", Caixa),
            [AcoesAuditoria.MensalidadePaga] = ("Mensalidade recebida", Caixa),
            [AcoesAuditoria.MensalistaCriado] = ("Mensalista cadastrado", Mensalistas),
            [AcoesAuditoria.MensalistaAlterado] = ("Mensalista alterado", Mensalistas),
            [AcoesAuditoria.ConviteMensalistaEnviado] = ("Convite do app enviado ao mensalista", Mensalistas),
            [AcoesAuditoria.ConviteMensalistaAceito] = ("Mensalista ativou o app", Mensalistas),
            [AcoesAuditoria.AcessoMensalistaRevogado] = ("Acesso do mensalista ao app revogado", Mensalistas),
            [AcoesAuditoria.ConfiguracaoAlterada] = ("Configuração do pátio alterada", Configuracao),
            [AcoesAuditoria.QuantidadeVagasAlterada] = ("Quantidade de vagas alterada", Configuracao),
            [AcoesAuditoria.ConfiguracaoPagamentoAlterada] = ("Formas de pagamento alteradas", Configuracao),
            [AcoesAuditoria.ConfiguracaoImpressaoAlterada] = ("Impressora alterada", Configuracao),
            [AcoesAuditoria.PatioCriado] = ("Pátio criado", Configuracao),
            [AcoesAuditoria.PatioAlterado] = ("Pátio alterado", Configuracao),
            [AcoesAuditoria.UsuarioCriado] = ("Usuário criado", Usuarios),
            [AcoesAuditoria.UsuarioAlterado] = ("Usuário alterado", Usuarios),
            [AcoesAuditoria.ConviteUsuarioEnviado] = ("Convite de usuário enviado", Usuarios),
            [AcoesAuditoria.SenhaRedefinida] = ("Link de nova senha enviado", Usuarios),
            [AcoesAuditoria.EmpresaCriada] = ("Empresa criada", Plataforma),
            [AcoesAuditoria.EmpresaAlterada] = ("Empresa alterada", Plataforma),
            [AcoesAuditoria.AssinaturaAlterada] = ("Plano da assinatura alterado", Plataforma),
            [AcoesAuditoria.FaturaPaga] = ("Fatura paga", Plataforma),
            [AcoesAuditoria.FaturaCancelada] = ("Fatura cancelada", Plataforma),
        };

        public static (string Descricao, string Categoria) Acao(string acao) =>
            Acoes.TryGetValue(acao, out var a) ? a : (acao.Replace('_', ' '), Movimento);

        public static IReadOnlyList<string> AcoesDaCategoria(string categoria) =>
            Acoes.Where(a => a.Value.Categoria == categoria).Select(a => a.Key).ToList();

        private static readonly Dictionary<string, string> Rotulos = new()
        {
            ["vaga"] = "Vaga", ["duracao"] = "Permanência", ["valor"] = "Valor", ["forma"] = "Forma de pagamento",
            ["formas"] = "Formas aceitas", ["origem"] = "Origem", ["motivo"] = "Motivo", ["confianca"] = "Confiança da leitura",
            ["metodo"] = "Leitura", ["mensalista"] = "Mensalista", ["placas"] = "Placas", ["email"] = "E-mail", ["nome"] = "Nome",
            ["perfil"] = "Perfil", ["ativo"] = "Ativo", ["ativa"] = "Ativa", ["troco"] = "Troco inicial", ["total"] = "Vagas",
            ["vagas"] = "Vagas", ["tolerancia"] = "Tolerância (min)", ["tabelaPreco"] = "Tabela de preço", ["valorHora"] = "Valor/hora",
            ["reservadas"] = "Vagas reservadas", ["mensalidade"] = "Mensalidade", ["valorPago"] = "Valor pago",
            ["valorNaoCobrado"] = "Valor não cobrado", ["validoAte"] = "Válido até", ["totalRecebido"] = "Total recebido",
            ["tipo"] = "Tipo", ["provedor"] = "Provedor", ["precoNegociado"] = "Preço negociado", ["plano"] = "Plano",
            ["patios"] = "Pátios", ["observacoes"] = "Observações", ["modo"] = "Modo", ["meses"] = "Meses",
            ["larguraPapelMm"] = "Papel (mm)", ["impressora"] = "Impressora", ["esperado"] = "Dinheiro esperado",
            ["contado"] = "Dinheiro contado", ["diferenca"] = "Diferença", ["erro"] = "Erro", ["empresa"] = "Empresa",
            ["diaVencimento"] = "Dia de vencimento", ["descricao"] = "Descrição", ["contaCriada"] = "Conta criada",
            ["chavePix"] = "Chave PIX", ["aviso"] = "Aviso", ["automatica"] = "Automática", ["admin"] = "Administrador",
        };

        private static readonly HashSet<string> Monetarios =
            ["valor", "valorPago", "valorNaoCobrado", "valorHora", "troco", "totalRecebido", "precoNegociado", "mensalidade",
             "esperado", "contado", "diferenca"];

        // Detalhes legíveis: identificadores internos ficam de fora; "x" e "xAnterior" viram "antes → depois"
        public static IReadOnlyList<DetalheAuditoriaDto> Detalhes(IReadOnlyDictionary<string, object> detalhes)
        {
            var lista = new List<DetalheAuditoriaDto>();
            foreach (var (chave, valor) in detalhes)
            {
                if (chave.EndsWith("Id", StringComparison.Ordinal) || chave.EndsWith("Anterior", StringComparison.Ordinal))
                    continue;
                var texto = Formatar(chave, valor);
                if (detalhes.TryGetValue(chave + "Anterior", out var anterior))
                {
                    var antes = Formatar(chave, anterior);
                    if (antes != texto)
                        texto = $"{antes} → {texto}";
                }
                if (texto.Length > 0)
                    lista.Add(new DetalheAuditoriaDto(Rotulos.GetValueOrDefault(chave, chave), texto));
            }
            return lista;
        }

        private static string Formatar(string chave, object? valor)
        {
            var elemento = valor switch
            {
                JsonElement e => e,
                null => default,
                _ => JsonSerializer.SerializeToElement(valor)
            };
            return elemento.ValueKind switch
            {
                // A API roda sem dados de cultura (InvariantGlobalization): formata à mão no padrão brasileiro
                JsonValueKind.Number when Monetarios.Contains(chave) && elemento.TryGetDecimal(out var d) => GeradorTicketEscPos.Moeda(d),
                JsonValueKind.Number when elemento.TryGetDecimal(out var d) => d.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ','),
                JsonValueKind.True => "Sim",
                JsonValueKind.False => "Não",
                JsonValueKind.String => elemento.GetString() ?? "",
                JsonValueKind.Array => string.Join(", ", elemento.EnumerateArray().Select(i => Formatar(chave, i))),
                JsonValueKind.Undefined or JsonValueKind.Null => "",
                _ => elemento.ToString()
            };
        }
    }
}
