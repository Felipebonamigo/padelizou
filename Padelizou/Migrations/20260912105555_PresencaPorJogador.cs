using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PresencaPorJogador : Migration
    {
        /// <inheritdoc />
        // ⚠️ A ORDEM AQUI FOI TROCADA À MÃO, e é o que salva as presenças de hoje: o EF gerou o
        // `DropColumn` ANTES do `CreateTable`, e nessa ordem a coluna com quem já chegou morre
        // antes de existir pra onde copiar. Cria → copia → derruba.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // AS CHEGADAS DE HOJE NÃO SE PERDEM: cada dupla marcada vira uma linha por jogador.
            // Sem isto, um torneio no meio do sábado voltaria do deploy com a chamada zerada — e
            // o organizador não tem como saber quem já tinha marcado.
            //
            // ⚠️ `GROUP BY` + `MIN` não é enfeite: quem joga DUAS categorias tem duas duplas, e as
            // duas podem estar marcadas. A chave nova é (torneio, pessoa) — sem o agrupamento, o
            // INSERT estoura na chave duplicada e derruba a migration inteira. Fica a chegada
            // mais antiga, que é a hora em que a pessoa de fato apareceu no clube.
            //
            // ⚠️ `Jogador2Id` é anulável (inscrição sem parceiro): o `WHERE` tira o fantasma.
            migrationBuilder.Sql("""
                INSERT INTO "PresencaNoTorneio" ("TorneioId", "JogadorId", "ChegouEm")
                SELECT c."TorneioId", j."JogadorId", MIN(d."CheckInEm")
                FROM "Dupla" d
                JOIN "Categoria" c ON c."Id" = d."CategoriaId"
                CROSS JOIN LATERAL (VALUES (d."Jogador1Id"), (d."Jogador2Id")) AS j("JogadorId")
                WHERE d."CheckInEm" IS NOT NULL AND j."JogadorId" IS NOT NULL
                GROUP BY c."TorneioId", j."JogadorId";
                """);

            migrationBuilder.DropColumn(
                name: "CheckInEm",
                table: "Dupla");
        }

        /// <inheritdoc />
        // A VOLTA TAMBÉM CARREGA O DADO — e perde precisão de propósito, porque a informação nova
        // não cabe na antiga: a dupla volta marcada quando TODOS os jogadores dela estavam
        // presentes, com a chegada mais recente entre eles. Meia dupla presente vira "não chegou",
        // que é exatamente o que a coluna antiga sabia dizer.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInEm",
                table: "Dupla",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Dupla" d
                SET "CheckInEm" = sub."Quando"
                FROM (
                    SELECT d2."Id", MAX(p."ChegouEm") AS "Quando"
                    FROM "Dupla" d2
                    JOIN "Categoria" c ON c."Id" = d2."CategoriaId"
                    JOIN "PresencaNoTorneio" p ON p."TorneioId" = c."TorneioId"
                     AND p."JogadorId" IN (d2."Jogador1Id", d2."Jogador2Id")
                    GROUP BY d2."Id", d2."Jogador2Id"
                    HAVING COUNT(*) = CASE WHEN d2."Jogador2Id" IS NULL THEN 1 ELSE 2 END
                ) AS sub
                WHERE d."Id" = sub."Id";
                """);

            migrationBuilder.DropTable(
                name: "PresencaNoTorneio");
        }
    }
}
