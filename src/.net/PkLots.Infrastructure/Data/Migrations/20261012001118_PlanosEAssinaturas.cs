using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlanosEAssinaturas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Planos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PrecoPorPatio = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MaximoPatios = table.Column<int>(type: "integer", nullable: true),
                    MaximoVagasPorPatio = table.Column<int>(type: "integer", nullable: true),
                    MaximoUsuarios = table.Column<int>(type: "integer", nullable: true),
                    Recursos = table.Column<List<string>>(type: "text[]", nullable: false),
                    DiasCarencia = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Assinaturas",
                columns: table => new
                {
                    EmpresaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrecoPorPatioNegociado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DiaVencimento = table.Column<int>(type: "integer", nullable: false),
                    TesteAte = table.Column<DateOnly>(type: "date", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assinaturas", x => x.EmpresaId);
                    table.ForeignKey(
                        name: "FK_Assinaturas_Empresas_EmpresaId",
                        column: x => x.EmpresaId,
                        principalTable: "Empresas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assinaturas_Planos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Planos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assinaturas_PlanoId",
                table: "Assinaturas",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_Planos_Nome",
                table: "Planos",
                column: "Nome",
                unique: true);

            // Plano "Cortesia" (tudo liberado, sem limites, sem cobrança) para as empresas que já existiam:
            // nada muda para elas até o superadmin escolher outro plano
            migrationBuilder.Sql("""
                INSERT INTO "Planos" ("Id", "Nome", "Descricao", "PrecoPorPatio", "Recursos", "DiasCarencia", "Ativo", "CriadoEm", "AtualizadoEm")
                VALUES ('0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0001', 'Cortesia', 'Tudo liberado, sem limites e sem cobrança', 0,
                        ARRAY['lpr','mensalistas','app_mensalista','relatorios','impressora_rede'], 7, TRUE, now(), now());

                INSERT INTO "Assinaturas" ("EmpresaId", "PlanoId", "DiaVencimento", "CriadoEm", "AtualizadoEm")
                SELECT "Id", '0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0001', 10, now(), now() FROM "Empresas";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assinaturas");

            migrationBuilder.DropTable(
                name: "Planos");
        }
    }
}
