using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class TabelaDePreco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TabelaPrecoJson",
                table: "Patios",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalhamento",
                table: "CobrancasSaida",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TabelaPrecoJson",
                table: "Patios");

            migrationBuilder.DropColumn(
                name: "Detalhamento",
                table: "CobrancasSaida");
        }
    }
}
