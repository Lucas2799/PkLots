using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AcessoMensalistaPorConvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VeiculosMotoristas");

            migrationBuilder.AddColumn<Guid>(
                name: "MensalistaId",
                table: "TokensUsuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Mensalistas",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioId",
                table: "Mensalistas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VinculadoEm",
                table: "Mensalistas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokensUsuario_MensalistaId_Tipo",
                table: "TokensUsuario",
                columns: new[] { "MensalistaId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_Mensalistas_UsuarioId",
                table: "Mensalistas",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_Mensalistas_Usuarios_UsuarioId",
                table: "Mensalistas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TokensUsuario_Mensalistas_MensalistaId",
                table: "TokensUsuario",
                column: "MensalistaId",
                principalTable: "Mensalistas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Mensalistas_Usuarios_UsuarioId",
                table: "Mensalistas");

            migrationBuilder.DropForeignKey(
                name: "FK_TokensUsuario_Mensalistas_MensalistaId",
                table: "TokensUsuario");

            migrationBuilder.DropIndex(
                name: "IX_TokensUsuario_MensalistaId_Tipo",
                table: "TokensUsuario");

            migrationBuilder.DropIndex(
                name: "IX_Mensalistas_UsuarioId",
                table: "Mensalistas");

            migrationBuilder.DropColumn(
                name: "MensalistaId",
                table: "TokensUsuario");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Mensalistas");

            migrationBuilder.DropColumn(
                name: "UsuarioId",
                table: "Mensalistas");

            migrationBuilder.DropColumn(
                name: "VinculadoEm",
                table: "Mensalistas");

            migrationBuilder.CreateTable(
                name: "VeiculosMotoristas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Cor = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Modelo = table.Column<string>(type: "text", nullable: true),
                    Placa = table.Column<string>(type: "text", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_VeiculosMotoristas_Placa",
                table: "VeiculosMotoristas",
                column: "Placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VeiculosMotoristas_UsuarioId",
                table: "VeiculosMotoristas",
                column: "UsuarioId");
        }
    }
}
