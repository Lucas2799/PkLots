using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PkLots.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlanosComerciais : Migration
    {
        // Planos de lançamento (o superadmin edita tudo depois). Entrada/saída, caixa, PIX, ticket em PDF,
        // painel e auditoria valem em todos; os planos se diferenciam pelos limites e recursos.
        // Período de teste e desconto de piloto ficam na assinatura (TesteAte e preço negociado).
        private const string Ids = """
            '0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0002', '0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0003', '0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0004'
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ON CONFLICT: não mexe num plano de mesmo nome já criado pelo superadmin
            migrationBuilder.Sql("""
                INSERT INTO "Planos" ("Id", "Nome", "Descricao", "PrecoPorPatio", "MaximoPatios", "MaximoVagasPorPatio", "MaximoUsuarios",
                                      "Recursos", "DiasCarencia", "Ativo", "CriadoEm", "AtualizadoEm")
                VALUES
                    ('0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0002', 'Essencial',
                     'Para o pátio pequeno: entrada e saída, caixa, PIX, ticket em PDF e mensalistas',
                     89.90, 1, 50, 3, ARRAY['mensalistas'], 7, TRUE, now(), now()),
                    ('0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0003', 'Profissional',
                     'Até 3 pátios, relatórios gerenciais, app do mensalista e impressora térmica',
                     159.90, 3, 150, 10, ARRAY['mensalistas','app_mensalista','relatorios','impressora_rede'], 7, TRUE, now(), now()),
                    ('0b7e9a51-4c1f-4f4e-9a3a-6c1d2b8e0004', 'Rede',
                     'Sem limites e com leitura automática de placa, para redes de estacionamentos',
                     239.90, NULL, NULL, NULL, ARRAY['lpr','mensalistas','app_mensalista','relatorios','impressora_rede'], 10, TRUE, now(), now())
                ON CONFLICT ("Nome") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Plano já assinado por alguma empresa fica (a FK impediria a remoção)
            migrationBuilder.Sql($"""
                DELETE FROM "Planos" p
                WHERE p."Id" IN ({Ids})
                  AND NOT EXISTS (SELECT 1 FROM "Assinaturas" a WHERE a."PlanoId" = p."Id");
                """);
        }
    }
}
