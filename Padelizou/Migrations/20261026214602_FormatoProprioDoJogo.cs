using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FormatoProprioDoJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GamesDoJogo",
                table: "Partida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PontosTieBreakDoJogo",
                table: "Partida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetsDoJogo",
                table: "Partida",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GamesDoJogo",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "PontosTieBreakDoJogo",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "SetsDoJogo",
                table: "Partida");
        }
    }
}
