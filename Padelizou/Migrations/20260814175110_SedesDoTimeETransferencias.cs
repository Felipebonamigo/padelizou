using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class SedesDoTimeETransferencias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ATENÇÃO à ordem, pelo mesmo motivo do AdministradoresDoTime (e o EF errou igual
            // de novo): ele gerou o DropColumn do "ClubeId" ANTES de criar a tabela nova, o que
            // jogaria fora a sede de todos os times. Aqui é: cria a tabela, copia as sedes pra
            // dentro dela, e só então derruba a coluna.

            migrationBuilder.CreateTable(
                name: "TimeSedes",
                columns: table => new
                {
                    TimeId = table.Column<int>(type: "integer", nullable: false),
                    ClubeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeSedes", x => new { x.TimeId, x.ClubeId });
                    table.ForeignKey(
                        name: "FK_TimeSedes_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeSedes_Times_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeSedes_ClubeId",
                table: "TimeSedes",
                column: "ClubeId");

            // A sede que cada time já tinha vira a primeira linha da lista.
            //
            // O JOIN com "Clubes" não é decoração: a FK antiga permitia nulo e nada garante que
            // toda linha aponte pra clube existente (base velha, clube apagado à mão). Sem o
            // filtro, um id órfão faria a migração estourar — e ela roda no start do app.
            migrationBuilder.Sql(@"
                INSERT INTO ""TimeSedes"" (""TimeId"", ""ClubeId"")
                SELECT t.""Id"", t.""ClubeId""
                  FROM ""Times"" t
                  JOIN ""Clubes"" c ON c.""Id"" = t.""ClubeId""
                 WHERE t.""ClubeId"" IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Times_Clubes_ClubeId",
                table: "Times");

            migrationBuilder.DropIndex(
                name: "IX_Times_ClubeId",
                table: "Times");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "Times");

            migrationBuilder.CreateTable(
                name: "TransferenciasDeTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    TimeAnteriorId = table.Column<int>(type: "integer", nullable: true),
                    TimeNovoId = table.Column<int>(type: "integer", nullable: true),
                    Em = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferenciasDeTime", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransferenciasDeTime_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferenciasDeTime_Times_TimeAnteriorId",
                        column: x => x.TimeAnteriorId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TransferenciasDeTime_Times_TimeNovoId",
                        column: x => x.TimeNovoId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // A tabela NASCE VAZIA de propósito, e não com uma linha por jogador que já tem
            // time: `Jogador.TimeId` sobrescrevia a camisa anterior sem guardar nada, então
            // não existe data nem time de origem pra inventar. Uma "transferência" carimbada
            // com a data do deploy diria que a base inteira trocou de time hoje. O histórico
            // começa na primeira troca real depois deste deploy — a aba explica isso.
            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasDeTime_Em",
                table: "TransferenciasDeTime",
                column: "Em");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasDeTime_JogadorId",
                table: "TransferenciasDeTime",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasDeTime_TimeAnteriorId_Em",
                table: "TransferenciasDeTime",
                columns: new[] { "TimeAnteriorId", "Em" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciasDeTime_TimeNovoId_Em",
                table: "TransferenciasDeTime",
                columns: new[] { "TimeNovoId", "Em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "Times",
                type: "integer",
                nullable: true);

            // Volta UMA sede de cada time pra coluna de sede única. Se o time tinha várias, as
            // demais se perdem — é o preço de voltar pra um modelo que só cabe uma, e por isso
            // o Down existe só pra emergência. O DISTINCT ON ordenado deixa a escolha
            // determinística (a de menor ClubeId), em vez de depender do que o banco devolver.
            migrationBuilder.Sql(@"
                UPDATE ""Times"" t SET ""ClubeId"" = s.""ClubeId""
                  FROM (SELECT DISTINCT ON (""TimeId"") ""TimeId"", ""ClubeId""
                          FROM ""TimeSedes""
                         ORDER BY ""TimeId"", ""ClubeId"") s
                 WHERE s.""TimeId"" = t.""Id"";");

            migrationBuilder.CreateIndex(
                name: "IX_Times_ClubeId",
                table: "Times",
                column: "ClubeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Times_Clubes_ClubeId",
                table: "Times",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id");

            migrationBuilder.DropTable(
                name: "TimeSedes");

            migrationBuilder.DropTable(
                name: "TransferenciasDeTime");
        }
    }
}
