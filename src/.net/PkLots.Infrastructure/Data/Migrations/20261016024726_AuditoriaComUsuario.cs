using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuditoriaComUsuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "LogsAuditoria",
                type: "uuid",
                nullable: true);

            // Registros antigos: quem fez estava só nos detalhes. Nas ações de gestão de usuários, "usuarioId" é o alvo
            // e "adminId" quem agiu, por isso a ordem: adminId, superAdminId e por último usuarioId
            migrationBuilder.Sql("""
                UPDATE "LogsAuditoria"
                SET "UsuarioId" = (CASE
                    WHEN "Detalhes"->>'adminId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN "Detalhes"->>'adminId'
                    WHEN "Detalhes"->>'superAdminId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' THEN "Detalhes"->>'superAdminId'
                    ELSE "Detalhes"->>'usuarioId'
                END)::uuid
                WHERE "Detalhes"->>'adminId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                   OR "Detalhes"->>'superAdminId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                   OR "Detalhes"->>'usuarioId' ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_CriadoEm",
                table: "LogsAuditoria",
                column: "CriadoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_CriadoEm",
                table: "LogsAuditoria");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "LogsAuditoria");
        }
    }
}
