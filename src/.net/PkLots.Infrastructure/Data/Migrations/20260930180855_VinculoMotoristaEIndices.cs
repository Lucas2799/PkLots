using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class VinculoMotoristaEIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Veiculos_PatioId",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_SaidasVeiculos_EntradaVeiculoId",
                table: "SaidasVeiculos");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_PatioId",
                table: "LogsAuditoria");

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "Veiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Vagas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_PatioId_Placa",
                table: "Veiculos",
                columns: new[] { "PatioId", "Placa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_UsuarioId",
                table: "Veiculos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Vagas_PatioId_Numero",
                table: "Vagas",
                columns: new[] { "PatioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaidasVeiculos_EntradaVeiculoId",
                table: "SaidasVeiculos",
                column: "EntradaVeiculoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_PatioId_CriadoEm",
                table: "LogsAuditoria",
                columns: new[] { "PatioId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_PatioId_Placa",
                table: "EntradasVeiculos",
                columns: new[] { "PatioId", "Placa" },
                unique: true,
                filter: "\"Situacao\" = 'ativa'");

            migrationBuilder.AddForeignKey(
                name: "FK_Veiculos_Usuarios_UsuarioId",
                table: "Veiculos",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Veiculos_Usuarios_UsuarioId",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_PatioId_Placa",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Veiculos_UsuarioId",
                table: "Veiculos");

            migrationBuilder.DropIndex(
                name: "IX_Vagas_PatioId_Numero",
                table: "Vagas");

            migrationBuilder.DropIndex(
                name: "IX_SaidasVeiculos_EntradaVeiculoId",
                table: "SaidasVeiculos");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_PatioId_CriadoEm",
                table: "LogsAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_EntradasVeiculos_PatioId_Placa",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Veiculos");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Vagas");

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_PatioId",
                table: "Veiculos",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasVeiculos_EntradaVeiculoId",
                table: "SaidasVeiculos",
                column: "EntradaVeiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_PatioId",
                table: "LogsAuditoria",
                column: "PatioId");
        }
    }
}
