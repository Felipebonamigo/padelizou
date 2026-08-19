using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PrecoDeTurmaAteSeisAlunos : Migration
    {
        /// <inheritdoc />
        // ⚠️ A ORDEM AQUI É O QUE SALVA OS PREÇOS JÁ CADASTRADOS. O scaffold do EF gerou os
        // dois DropColumn ANTES do CreateTable — nessa ordem, todo preço de dupla e trio que
        // os professores já tinham cadastrado ia embora antes de existir pra onde copiar.
        // Cria a tabela, COPIA, e só então derruba as colunas.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrecoDeTurma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocalAulaId = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeAlunos = table.Column<int>(type: "integer", nullable: false),
                    Preco = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrecoDeTurma", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrecoDeTurma_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrecoDeTurma_LocalAulaId_QuantidadeAlunos",
                table: "PrecoDeTurma",
                columns: new[] { "LocalAulaId", "QuantidadeAlunos" },
                unique: true);

            // Dupla e trio viram as duas primeiras linhas da tabela nova. O `> 0` é a mesma
            // régua que o cadastro sempre aplicou (zero era "não faço esse tamanho"): copiar
            // um zero criaria aula de graça anunciada ao aluno.
            migrationBuilder.Sql(
                """
                INSERT INTO "PrecoDeTurma" ("LocalAulaId", "QuantidadeAlunos", "Preco")
                SELECT "Id", 2, "PrecoDupla" FROM "LocalAula" WHERE "PrecoDupla" > 0;

                INSERT INTO "PrecoDeTurma" ("LocalAulaId", "QuantidadeAlunos", "Preco")
                SELECT "Id", 3, "PrecoTrio" FROM "LocalAula" WHERE "PrecoTrio" > 0;
                """);

            migrationBuilder.DropColumn(
                name: "PrecoDupla",
                table: "LocalAula");

            migrationBuilder.DropColumn(
                name: "PrecoTrio",
                table: "LocalAula");
        }

        /// <inheritdoc />
        // Volta as colunas e devolve os valores antes de derrubar a tabela — mesma ordem
        // invertida do Up. Quarteto, quinteto e sexteto NÃO têm pra onde voltar: quem
        // desfizer esta migration perde os tamanhos que só existiam na tabela nova, e não há
        // como não perder — as colunas antigas iam até três.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecoDupla",
                table: "LocalAula",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoTrio",
                table: "LocalAula",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "LocalAula" l
                   SET "PrecoDupla" = t."Preco"
                  FROM "PrecoDeTurma" t
                 WHERE t."LocalAulaId" = l."Id" AND t."QuantidadeAlunos" = 2;

                UPDATE "LocalAula" l
                   SET "PrecoTrio" = t."Preco"
                  FROM "PrecoDeTurma" t
                 WHERE t."LocalAulaId" = l."Id" AND t."QuantidadeAlunos" = 3;
                """);

            migrationBuilder.DropTable(
                name: "PrecoDeTurma");
        }
    }
}
