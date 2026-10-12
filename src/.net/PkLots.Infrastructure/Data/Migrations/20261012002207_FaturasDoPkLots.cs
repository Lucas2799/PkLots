using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FaturasDoPkLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesPlataforma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChavePix = table.Column<string>(type: "text", nullable: true),
                    NomeRecebedor = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    CidadeRecebedor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesPlataforma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Faturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Competencia = table.Column<DateOnly>(type: "date", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Vencimento = table.Column<DateOnly>(type: "date", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    PatiosCobrados = table.Column<int>(type: "integer", nullable: true),
                    PrecoPorPatio = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    NomePlano = table.Column<string>(type: "text", nullable: true),
                    PagaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FormaPagamento = table.Column<string>(type: "text", nullable: true),
                    ValorPago = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BaixadaPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CanceladaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faturas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faturas_EmpresaId_Competencia",
                table: "Faturas",
                columns: new[] { "EmpresaId", "Competencia" },
                unique: true,
                filter: "\"Tipo\" = 'mensalidade'");

            migrationBuilder.CreateIndex(
                name: "IX_Faturas_Situacao_Vencimento",
                table: "Faturas",
                columns: new[] { "Situacao", "Vencimento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesPlataforma");

            migrationBuilder.DropTable(
                name: "Faturas");
        }
    }
}
