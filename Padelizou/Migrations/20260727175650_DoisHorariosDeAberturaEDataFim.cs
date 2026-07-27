using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class DoisHorariosDeAberturaEDataFim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataFim",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            // O default que o EF gera aqui é 00:00 — que marcaria jogo à MEIA-NOITE em todo
            // torneio que já existe. 08:00 é o padrão de quem cria de agora em diante.
            migrationBuilder.AddColumn<TimeSpan>(
                name: "HoraInicioDiasSeguintes",
                table: "Torneio",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 8, 0, 0, 0));

            // Pros torneios que JÁ EXISTEM o certo não é 8h: é a hora que eles já usavam.
            // Antes havia uma abertura só, valendo pra todos os dias — copiar preserva
            // exatamente o que o organizador já tinha combinado com os jogadores.
            migrationBuilder.Sql(@"UPDATE ""Torneio"" SET ""HoraInicioDiasSeguintes"" = ""HoraInicioDoDia"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataFim",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "HoraInicioDiasSeguintes",
                table: "Torneio");
        }
    }
}
