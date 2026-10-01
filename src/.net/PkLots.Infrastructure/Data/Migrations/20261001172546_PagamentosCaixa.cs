using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PagamentosCaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CaixaId",
                table: "SaidasVeiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CobrancaId",
                table: "SaidasVeiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormaPagamento",
                table: "SaidasVeiculos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegistradoPorId",
                table: "SaidasVeiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Troco",
                table: "SaidasVeiculos",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorRecebido",
                table: "SaidasVeiculos",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CaixaId",
                table: "PagamentosMensalidade",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Caixas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AbertoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TrocoInicial = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    FechadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DinheiroEsperado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DinheiroContado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ObservacaoFechamento = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caixas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caixas_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Caixas_Usuarios_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CobrancasSaida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntradaVeiculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DuracaoSegundos = table.Column<int>(type: "integer", nullable: false),
                    DuracaoFormatada = table.Column<string>(type: "text", nullable: false),
                    SituacaoPagamento = table.Column<string>(type: "text", nullable: false),
                    CalculadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TxId = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    PixCopiaECola = table.Column<string>(type: "text", nullable: true),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    Origem = table.Column<string>(type: "text", nullable: true),
                    Mensalista = table.Column<string>(type: "text", nullable: true),
                    Aviso = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrancasSaida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CobrancasSaida_EntradasVeiculos_EntradaVeiculoId",
                        column: x => x.EntradaVeiculoId,
                        principalTable: "EntradasVeiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CobrancasSaida_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesPagamento",
                columns: table => new
                {
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provedor = table.Column<string>(type: "text", nullable: false),
                    ChavePix = table.Column<string>(type: "text", nullable: true),
                    NomeRecebedor = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    CidadeRecebedor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    AceitaDinheiro = table.Column<bool>(type: "boolean", nullable: false),
                    AceitaPix = table.Column<bool>(type: "boolean", nullable: false),
                    AceitaCartao = table.Column<bool>(type: "boolean", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesPagamento", x => x.EmpresaId);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesPagamento_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimentosCaixa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaixaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosCaixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosCaixa_Caixas_CaixaId",
                        column: x => x.CaixaId,
                        principalTable: "Caixas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaidasVeiculos_CaixaId",
                table: "SaidasVeiculos",
                column: "CaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosMensalidade_CaixaId",
                table: "PagamentosMensalidade",
                column: "CaixaId");

            migrationBuilder.CreateIndex(
                name: "IX_Caixas_OperadorId",
                table: "Caixas",
                column: "OperadorId",
                unique: true,
                filter: "\"FechadoEm\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Caixas_PatioId_AbertoEm",
                table: "Caixas",
                columns: new[] { "PatioId", "AbertoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasSaida_EntradaVeiculoId_Situacao",
                table: "CobrancasSaida",
                columns: new[] { "EntradaVeiculoId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_CobrancasSaida_PatioId",
                table: "CobrancasSaida",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosCaixa_CaixaId",
                table: "MovimentosCaixa",
                column: "CaixaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CobrancasSaida");

            migrationBuilder.DropTable(
                name: "ConfiguracoesPagamento");

            migrationBuilder.DropTable(
                name: "MovimentosCaixa");

            migrationBuilder.DropTable(
                name: "Caixas");

            migrationBuilder.DropIndex(
                name: "IX_SaidasVeiculos_CaixaId",
                table: "SaidasVeiculos");

            migrationBuilder.DropIndex(
                name: "IX_PagamentosMensalidade_CaixaId",
                table: "PagamentosMensalidade");

            migrationBuilder.DropColumn(
                name: "CaixaId",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "CobrancaId",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "FormaPagamento",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "RegistradoPorId",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "Troco",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "ValorRecebido",
                table: "SaidasVeiculos");

            migrationBuilder.DropColumn(
                name: "CaixaId",
                table: "PagamentosMensalidade");
        }
    }
}
