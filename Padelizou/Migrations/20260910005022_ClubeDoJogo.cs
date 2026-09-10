using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ClubeDoJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClubeId",
                table: "Partida",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partida_ClubeId",
                table: "Partida",
                column: "ClubeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Partida_Clubes_ClubeId",
                table: "Partida",
                column: "ClubeId",
                principalTable: "Clubes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Partida_Clubes_ClubeId",
                table: "Partida");

            migrationBuilder.DropIndex(
                name: "IX_Partida_ClubeId",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "ClubeId",
                table: "Partida");
        }
    }
}
