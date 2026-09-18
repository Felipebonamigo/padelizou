using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class LembreteDaAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UltimoLembreteEnviado",
                table: "JogoAula",
                type: "integer",
                nullable: true);

            // ⚠️ `defaultValue: true` TROCADO À MÃO, e é a lição do `= 60` de 10/08/2026: o EF
            // gerou `false`, e `false` é o valor que TODA CONTA JÁ EXISTENTE receberia — a base
            // inteira nasceria com o lembrete DESLIGADO, que é o contrário da decisão ("nasce
            // ligado, e existe pra quem não quer"). O `= true` da propriedade em C# só vale pra
            // objeto novo; quem preenche linha antiga é o default daqui.
            migrationBuilder.AddColumn<bool>(
                name: "NotificarLembreteDeAula",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "UltimoLembreteEnviado",
                table: "Aula",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimoLembreteEnviado",
                table: "JogoAula");

            migrationBuilder.DropColumn(
                name: "NotificarLembreteDeAula",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "UltimoLembreteEnviado",
                table: "Aula");
        }
    }
}
