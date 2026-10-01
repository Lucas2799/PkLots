using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImpressaoTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImpressoraHost",
                table: "Patios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImpressoraPorta",
                table: "Patios",
                type: "integer",
                nullable: false,
                defaultValue: 9100);

            migrationBuilder.AddColumn<bool>(
                name: "ImprimirEntradaAutomaticamente",
                table: "Patios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LarguraPapelMm",
                table: "Patios",
                type: "integer",
                nullable: false,
                defaultValue: 80);

            migrationBuilder.AddColumn<string>(
                name: "ModoImpressao",
                table: "Patios",
                type: "text",
                nullable: false,
                defaultValue: "desativada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpressoraHost",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "ImpressoraPorta",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "ImprimirEntradaAutomaticamente",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "LarguraPapelMm",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "ModoImpressao",
                table: "Patios");
        }
    }
}
