using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TokenAcesso",
                table: "EntradasVeiculos",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_TokenAcesso",
                table: "EntradasVeiculos",
                column: "TokenAcesso",
                unique: true,
                filter: "\"TokenAcesso\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntradasVeiculos_TokenAcesso",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "TokenAcesso",
                table: "EntradasVeiculos");
        }
    }
}
