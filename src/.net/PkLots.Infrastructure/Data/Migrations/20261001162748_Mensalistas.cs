using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Mensalistas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VagasReservadasMensalistas",
                table: "Patios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMensalidade",
                table: "Patios",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "MensalistaId",
                table: "EntradasVeiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Mensalistas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Telefone = table.Column<string>(type: "text", nullable: true),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensalistas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensalistas_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MensalistasPlacas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MensalistaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placa = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensalistasPlacas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MensalistasPlacas_Mensalistas_MensalistaId",
                        column: x => x.MensalistaId,
                        principalTable: "Mensalistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagamentosMensalidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MensalistaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Meses = table.Column<int>(type: "integer", nullable: false),
                    ValidoDe = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidoAte = table.Column<DateOnly>(type: "date", nullable: false),
                    FormaPagamento = table.Column<string>(type: "text", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    RegistradoPorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PagoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosMensalidade", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosMensalidade_Mensalistas_MensalistaId",
                        column: x => x.MensalistaId,
                        principalTable: "Mensalistas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PagamentosMensalidade_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_MensalistaId",
                table: "EntradasVeiculos",
                column: "MensalistaId");

            migrationBuilder.CreateIndex(
                name: "IX_Mensalistas_PatioId_Nome",
                table: "Mensalistas",
                columns: new[] { "PatioId", "Nome" });

            migrationBuilder.CreateIndex(
                name: "IX_MensalistasPlacas_MensalistaId",
                table: "MensalistasPlacas",
                column: "MensalistaId");

            migrationBuilder.CreateIndex(
                name: "IX_MensalistasPlacas_PatioId_Placa",
                table: "MensalistasPlacas",
                columns: new[] { "PatioId", "Placa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosMensalidade_MensalistaId_PagoEm",
                table: "PagamentosMensalidade",
                columns: new[] { "MensalistaId", "PagoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosMensalidade_PatioId",
                table: "PagamentosMensalidade",
                column: "PatioId");

            migrationBuilder.AddForeignKey(
                name: "FK_EntradasVeiculos_Mensalistas_MensalistaId",
                table: "EntradasVeiculos",
                column: "MensalistaId",
                principalTable: "Mensalistas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntradasVeiculos_Mensalistas_MensalistaId",
                table: "EntradasVeiculos");

            migrationBuilder.DropTable(
                name: "MensalistasPlacas");

            migrationBuilder.DropTable(
                name: "PagamentosMensalidade");

            migrationBuilder.DropTable(
                name: "Mensalistas");

            migrationBuilder.DropIndex(
                name: "IX_EntradasVeiculos_MensalistaId",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "VagasReservadasMensalistas",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "ValorMensalidade",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "MensalistaId",
                table: "EntradasVeiculos");
        }
    }
}
