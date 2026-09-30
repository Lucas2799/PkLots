using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdministracaoPatio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Ativa",
                table: "Vagas",
                type: "boolean",
                nullable: false,
                defaultValue: true); // vagas existentes continuam ativas

            migrationBuilder.AddColumn<int>(
                name: "MinutosTolerancia",
                table: "Patios",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ativa",
                table: "Vagas");

            migrationBuilder.DropColumn(
                name: "MinutosTolerancia",
                table: "Patios");
        }
    }
}
