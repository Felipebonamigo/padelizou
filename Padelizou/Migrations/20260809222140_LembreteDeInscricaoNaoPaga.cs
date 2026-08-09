using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class LembreteDeInscricaoNaoPaga : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UltimoLembreteDePagamento",
                table: "InscricaoAmericana",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UltimoLembreteDePagamento",
                table: "Dupla",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimoLembreteDePagamento",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "UltimoLembreteDePagamento",
                table: "Dupla");
        }
    }
}
