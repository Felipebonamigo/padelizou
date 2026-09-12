using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TieBreakEmPontos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreakFinal",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreakGrupos",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreakMataMata",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreak1",
                table: "Partida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreak2",
                table: "Partida",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PontosTieBreakFinal",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PontosTieBreakGrupos",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PontosTieBreakMataMata",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PontosTieBreak1",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "PontosTieBreak2",
                table: "Partida");
        }
    }
}
