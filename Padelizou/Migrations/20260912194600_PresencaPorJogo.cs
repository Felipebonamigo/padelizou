using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PresencaPorJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ O EF GEROU O `DropTable` ANTES DO `CreateTable`, e assim a tabela velha morreria
            // levando os checks do dia junto. A ordem foi trocada NA MÃO: criar → copiar →
            // apagar. É o mesmo tombo do `PresencaPorJogador`, de hoje de manhã.
            migrationBuilder.CreateTable(
                name: "PresencaNoJogo",
                columns: table => new
                {
                    PartidaId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    ChegouEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresencaNoJogo", x => new { x.PartidaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_PresencaNoJogo_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PresencaNoJogo_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresencaNoJogo_JogadorId",
                table: "PresencaNoJogo",
                column: "JogadorId");

            // ── OS CHECKS DE HOJE ATRAVESSAM ────────────────────────────────────────────────
            //
            // Decisão do Felipe: copiar SÓ pros jogos ainda não finalizados. No instante da
            // publicação a tela do dia fica como estava — ninguém perde check no meio de um
            // torneio em andamento —, e o passado não ganha carimbo que ninguém deu.
            //
            // ⚠️ `MIN` + `GROUP BY`: a mesma pessoa pode aparecer pelas DUAS duplas do mesmo jogo
            // (não deveria, e a chave antiga não impedia). Sem o agrupamento isso vira
            // duplicate-key na PK nova e a migration morre no meio — foi exatamente o que
            // aconteceu de manhã, e só apareceu contra um Postgres de verdade.
            //
            // ⚠️ `LATERAL (VALUES ...)`: é o jeito de virar as duas colunas de jogador da dupla em
            // duas LINHAS. O `Jogador2Id` é anulável (inscrição sem parceiro) e o `WHERE` de
            // igualdade já descarta o nulo.
            migrationBuilder.Sql("""
                INSERT INTO "PresencaNoJogo" ("PartidaId", "JogadorId", "ChegouEm")
                SELECT p."Id", x."JogadorId", MIN(pt."ChegouEm")
                FROM "PresencaNoTorneio" pt
                JOIN "Categoria" c ON c."TorneioId" = pt."TorneioId"
                JOIN "Partida" p ON p."CategoriaId" = c."Id" AND p."Status" <> 'Finalizada'
                JOIN "Dupla" d ON d."Id" IN (p."Dupla1Id", p."Dupla2Id")
                CROSS JOIN LATERAL (VALUES (d."Jogador1Id"), (d."Jogador2Id")) AS x("JogadorId")
                WHERE x."JogadorId" = pt."JogadorId"
                GROUP BY p."Id", x."JogadorId";
                """);

            migrationBuilder.DropTable(
                name: "PresencaNoTorneio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PresencaNoJogo");

            migrationBuilder.CreateTable(
                name: "PresencaNoTorneio",
                columns: table => new
                {
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    ChegouEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresencaNoTorneio", x => new { x.TorneioId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_PresencaNoTorneio_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PresencaNoTorneio_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PresencaNoTorneio_JogadorId",
                table: "PresencaNoTorneio",
                column: "JogadorId");
        }
    }
}
