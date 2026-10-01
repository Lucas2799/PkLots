using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultiEmpresa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migração escrita à mão para preservar os dados: primeiro cria a empresa e copia os vínculos,
            // só depois remove as colunas antigas (Usuarios.PatioId e Veiculos.UsuarioId).

            // 1) Empresas
            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Documento = table.Column<string>(type: "text", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            // Banco existente: tudo passa a pertencer à "Empresa Padrão" (banco vazio: o semeador cria depois)
            migrationBuilder.Sql($"""
                INSERT INTO "Empresas" ("Id", "Nome", "Documento", "Ativa", "CriadoEm", "AtualizadoEm")
                SELECT '{EmpresaPadraoId}', 'Empresa Padrão', NULL, TRUE, now(), now()
                WHERE EXISTS (SELECT 1 FROM "Patios");
                """);

            // 2) Pátios → empresa
            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Patios",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql($"""UPDATE "Patios" SET "EmpresaId" = '{EmpresaPadraoId}';""");
            migrationBuilder.AlterColumn<Guid>(
                name: "EmpresaId",
                table: "Patios",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 3) Usuários: admin/operador ficam na empresa; motoristas viram contas da plataforma (sem empresa)
            migrationBuilder.AddColumn<Guid>(
                name: "EmpresaId",
                table: "Usuarios",
                type: "uuid",
                nullable: true);
            migrationBuilder.Sql($"""UPDATE "Usuarios" SET "EmpresaId" = '{EmpresaPadraoId}' WHERE "Perfil" IN ('admin', 'operador');""");

            // 4) Operadores ficam vinculados ao pátio em que estavam
            migrationBuilder.CreateTable(
                name: "UsuariosPatios",
                columns: table => new
                {
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuariosPatios", x => new { x.UsuarioId, x.PatioId });
                    table.ForeignKey(
                        name: "FK_UsuariosPatios_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuariosPatios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql("""
                INSERT INTO "UsuariosPatios" ("UsuarioId", "PatioId")
                SELECT "Id", "PatioId" FROM "Usuarios" WHERE "Perfil" = 'operador';
                """);

            // 5) Placas dos motoristas passam a ser da conta (uma linha por placa, a mais recente)
            migrationBuilder.CreateTable(
                name: "VeiculosMotoristas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placa = table.Column<string>(type: "text", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: true),
                    Cor = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VeiculosMotoristas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VeiculosMotoristas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.Sql("""
                INSERT INTO "VeiculosMotoristas" ("Id", "UsuarioId", "Placa", "Modelo", "Cor", "CriadoEm", "AtualizadoEm")
                SELECT DISTINCT ON ("Placa") gen_random_uuid(), "UsuarioId", "Placa", "Modelo", "Cor", "CriadoEm", "AtualizadoEm"
                FROM "Veiculos"
                WHERE "UsuarioId" IS NOT NULL
                ORDER BY "Placa", "AtualizadoEm" DESC;
                """);

            // 6) Remove o modelo antigo (dados já copiados)
            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Patios_PatioId",
                table: "Usuarios");
            migrationBuilder.DropForeignKey(
                name: "FK_Veiculos_Usuarios_UsuarioId",
                table: "Veiculos");
            migrationBuilder.DropIndex(
                name: "IX_Veiculos_UsuarioId",
                table: "Veiculos");
            migrationBuilder.DropIndex(
                name: "IX_Usuarios_PatioId",
                table: "Usuarios");
            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Veiculos");
            migrationBuilder.DropColumn(
                name: "PatioId",
                table: "Usuarios");

            // 7) Índices e chaves do modelo novo
            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId");
            migrationBuilder.CreateIndex(
                name: "IX_Patios_EmpresaId",
                table: "Patios",
                column: "EmpresaId");
            migrationBuilder.CreateIndex(
                name: "IX_UsuariosPatios_PatioId",
                table: "UsuariosPatios",
                column: "PatioId");
            migrationBuilder.CreateIndex(
                name: "IX_VeiculosMotoristas_Placa",
                table: "VeiculosMotoristas",
                column: "Placa",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_VeiculosMotoristas_UsuarioId",
                table: "VeiculosMotoristas",
                column: "UsuarioId");
            migrationBuilder.AddForeignKey(
                name: "FK_Patios_Empresas_EmpresaId",
                table: "Patios",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Empresas_EmpresaId",
                table: "Usuarios",
                column: "EmpresaId",
                principalTable: "Empresas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private const string EmpresaPadraoId = "6b1f3a52-9c1e-4c3a-8a8e-2f5d0c0e0001";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patios_Empresas_EmpresaId",
                table: "Patios");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Empresas_EmpresaId",
                table: "Usuarios");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropTable(
                name: "UsuariosPatios");

            migrationBuilder.DropTable(
                name: "VeiculosMotoristas");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_EmpresaId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Patios_EmpresaId",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "EmpresaId",
                table: "Patios");

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "Veiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatioId",
                table: "Usuarios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_UsuarioId",
                table: "Veiculos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PatioId",
                table: "Usuarios",
                column: "PatioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Patios_PatioId",
                table: "Usuarios",
                column: "PatioId",
                principalTable: "Patios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Veiculos_Usuarios_UsuarioId",
                table: "Veiculos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
