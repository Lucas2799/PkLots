using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CodigoTicket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoTicket",
                table: "EntradasVeiculos",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Entradas existentes: o código é o que os tickets antigos já mostravam (8 primeiros caracteres do Id).
            // Se dois prefixos coincidirem no mesmo pátio, ficam sem código para não violar o índice único.
            migrationBuilder.Sql("""
                UPDATE "EntradasVeiculos" e
                SET "CodigoTicket" = c.codigo
                FROM (
                    SELECT "Id", upper(substr(replace("Id"::text, '-', ''), 1, 8)) AS codigo,
                           count(*) OVER (PARTITION BY "PatioId", upper(substr(replace("Id"::text, '-', ''), 1, 8))) AS repeticoes
                    FROM "EntradasVeiculos"
                ) c
                WHERE e."Id" = c."Id" AND c.repeticoes = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_PatioId_CodigoTicket",
                table: "EntradasVeiculos",
                columns: new[] { "PatioId", "CodigoTicket" },
                unique: true,
                filter: "\"CodigoTicket\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EntradasVeiculos_PatioId_CodigoTicket",
                table: "EntradasVeiculos");

            migrationBuilder.DropColumn(
                name: "CodigoTicket",
                table: "EntradasVeiculos");
        }
    }
}
