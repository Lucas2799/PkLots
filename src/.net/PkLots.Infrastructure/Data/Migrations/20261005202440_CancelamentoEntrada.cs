using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CancelamentoEntrada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "EntradasVeiculos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CanceladaPorId",
                table: "EntradasVeiculos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "EntradasVeiculos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "CanceladaPorId",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "EntradasVeiculos");
        }
    }
}
