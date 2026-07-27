using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AdministradoresDoTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ATENÇÃO à ordem: o EF gerou o DropColumn do "DonoId" ANTES de criar a tabela
            // nova, o que jogaria fora quem era dono de cada time. Aqui é: cria a tabela,
            // copia os donos pra dentro dela, e só então derruba a coluna.
            migrationBuilder.CreateTable(
                name: "TimeAdministradores",
                columns: table => new
                {
                    TimeId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    ConcedidoPorId = table.Column<int>(type: "integer", nullable: true),
                    ConcedidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAdministradores", x => new { x.TimeId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_TimeAdministradores_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeAdministradores_Times_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Times",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeAdministradores_JogadorId",
                table: "TimeAdministradores",
                column: "JogadorId");

            // Quem já era dono vira o primeiro administrador do seu time.
            //
            // O JOIN com "Jogador" não é decoração: "DonoId" era coluna solta, sem chave
            // estrangeira ("pra não criar caminho de cascade duplo com Jogador.TimeId"),
            // então nada garantia que aponta pra alguém que ainda existe. Sem o filtro, um
            // dono órfão faria a migração estourar — e ela roda no start do app.
            migrationBuilder.Sql(@"
                INSERT INTO ""TimeAdministradores"" (""TimeId"", ""JogadorId"", ""ConcedidoPorId"", ""ConcedidoEm"")
                SELECT t.""Id"", t.""DonoId"", t.""DonoId"", NOW()
                  FROM ""Times"" t
                  JOIN ""Jogador"" j ON j.""Id"" = t.""DonoId""
                 WHERE t.""DonoId"" IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "DonoId",
                table: "Times");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonoId",
                table: "Times",
                type: "integer",
                nullable: true);

            // Volta o administrador mais antigo de cada time pra coluna de dono único.
            // Se o time tinha vários, os demais se perdem — é o preço de voltar pra um
            // modelo que só cabe um, e por isso o Down existe só pra emergência.
            migrationBuilder.Sql(@"
                UPDATE ""Times"" t SET ""DonoId"" = a.""JogadorId""
                  FROM (SELECT DISTINCT ON (""TimeId"") ""TimeId"", ""JogadorId""
                          FROM ""TimeAdministradores""
                         ORDER BY ""TimeId"", ""ConcedidoEm"") a
                 WHERE a.""TimeId"" = t.""Id"";");

            migrationBuilder.DropTable(
                name: "TimeAdministradores");
        }
    }
}
