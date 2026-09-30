using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Patios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    MaximoVagas = table.Column<int>(type: "integer", nullable: false),
                    ValorHora = table.Column<decimal>(type: "numeric", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Acao = table.Column<string>(type: "text", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    Placa = table.Column<string>(type: "text", nullable: true),
                    Detalhes = table.Column<string>(type: "jsonb", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LogsAuditoria_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    SenhaHash = table.Column<string>(type: "text", nullable: false),
                    NomeCompleto = table.Column<string>(type: "text", nullable: false),
                    Perfil = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    UltimoLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vagas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "text", nullable: false),
                    Fileira = table.Column<char>(type: "character(1)", nullable: false),
                    Indice = table.Column<int>(type: "integer", nullable: false),
                    Disponivel = table.Column<bool>(type: "boolean", nullable: false),
                    Ocupada = table.Column<bool>(type: "boolean", nullable: false),
                    VeiculoAtualId = table.Column<Guid>(type: "uuid", nullable: true),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    OcupadaDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vagas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vagas_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Veiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placa = table.Column<string>(type: "text", nullable: false),
                    Cor = table.Column<string>(type: "text", nullable: true),
                    Modelo = table.Column<string>(type: "text", nullable: true),
                    NomeProprietario = table.Column<string>(type: "text", nullable: true),
                    Telefone = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Veiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Veiculos_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntradasVeiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    VeiculoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Placa = table.Column<string>(type: "text", nullable: false),
                    VagaId = table.Column<Guid>(type: "uuid", nullable: true),
                    DataHoraEntrada = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UrlFotoEntrada = table.Column<string>(type: "text", nullable: true),
                    MetodoReconhecimento = table.Column<string>(type: "text", nullable: true),
                    Confianca = table.Column<decimal>(type: "numeric", nullable: false),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasVeiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradasVeiculos_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntradasVeiculos_Vagas_VagaId",
                        column: x => x.VagaId,
                        principalTable: "Vagas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntradasVeiculos_Veiculos_VeiculoId",
                        column: x => x.VeiculoId,
                        principalTable: "Veiculos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SaidasVeiculos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatioId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntradaVeiculoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Placa = table.Column<string>(type: "text", nullable: false),
                    DataHoraSaida = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UrlFotoSaida = table.Column<string>(type: "text", nullable: true),
                    DuracaoSegundos = table.Column<int>(type: "integer", nullable: false),
                    DuracaoFormatada = table.Column<string>(type: "text", nullable: false),
                    ValorHora = table.Column<decimal>(type: "numeric", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Moeda = table.Column<string>(type: "text", nullable: false),
                    SituacaoPagamento = table.Column<string>(type: "text", nullable: false),
                    UrlRecibo = table.Column<string>(type: "text", nullable: true),
                    Situacao = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaidasVeiculos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaidasVeiculos_EntradasVeiculos_EntradaVeiculoId",
                        column: x => x.EntradaVeiculoId,
                        principalTable: "EntradasVeiculos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SaidasVeiculos_Patios_PatioId",
                        column: x => x.PatioId,
                        principalTable: "Patios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_PatioId_DataHoraEntrada",
                table: "EntradasVeiculos",
                columns: new[] { "PatioId", "DataHoraEntrada" });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_VagaId",
                table: "EntradasVeiculos",
                column: "VagaId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasVeiculos_VeiculoId",
                table: "EntradasVeiculos",
                column: "VeiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_PatioId",
                table: "LogsAuditoria",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasVeiculos_EntradaVeiculoId",
                table: "SaidasVeiculos",
                column: "EntradaVeiculoId");

            migrationBuilder.CreateIndex(
                name: "IX_SaidasVeiculos_PatioId",
                table: "SaidasVeiculos",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_PatioId",
                table: "Usuarios",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_Vagas_PatioId_Disponivel",
                table: "Vagas",
                columns: new[] { "PatioId", "Disponivel" });

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_PatioId",
                table: "Veiculos",
                column: "PatioId");

            migrationBuilder.CreateIndex(
                name: "IX_Veiculos_Placa",
                table: "Veiculos",
                column: "Placa");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LogsAuditoria");

            migrationBuilder.DropTable(
                name: "SaidasVeiculos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "EntradasVeiculos");

            migrationBuilder.DropTable(
                name: "Vagas");

            migrationBuilder.DropTable(
                name: "Veiculos");

            migrationBuilder.DropTable(
                name: "Patios");
        }
    }
}
