using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvisosDeCobranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "AvisosEnviados",
                table: "Faturas",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'"); // faturas já emitidas: nenhum aviso enviado
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvisosEnviados",
                table: "Faturas");
        }
    }
}
