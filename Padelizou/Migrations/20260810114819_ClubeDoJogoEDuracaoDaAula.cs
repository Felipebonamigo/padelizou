using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ClubeDoJogoEDuracaoDaAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "JogoSemanal",
                type: "integer",
                nullable: true);

            // ⚠️ defaultValue 60 escrito à mão: o EF gera 0 aqui, e 0 é o valor que TODA aula
            // já marcada receberia — aula de duração zero, terminando na hora em que começa.
            // O `= 60` da propriedade em C# só vale pra objeto novo; quem preenche a linha
            // antiga é este DEFAULT. 60 era a duração implícita de tudo antes de 10/08/2026.
            migrationBuilder.AddColumn<int>(
                name: "DuracaoMinutos",
                table: "Aula",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "RecorrenciaSemFim",
                table: "Aula",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_ClubeId",
                table: "JogoSemanal",
                column: "ClubeId");

            migrationBuilder.AddForeignKey(
                name: "FK_JogoSemanal_Clubes_ClubeId",
                table: "JogoSemanal",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JogoSemanal_Clubes_ClubeId",
                table: "JogoSemanal");

            migrationBuilder.DropIndex(
                name: "IX_JogoSemanal_ClubeId",
                table: "JogoSemanal");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "JogoSemanal");

            migrationBuilder.DropColumn(
                name: "DuracaoMinutos",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "RecorrenciaSemFim",
                table: "Aula");
        }
    }
}
