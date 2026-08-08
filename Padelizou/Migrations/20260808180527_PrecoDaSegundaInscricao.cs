using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PrecoDaSegundaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecoSegundaInscricao",
                table: "Torneio",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorInscricao",
                table: "InscricaoAmericana",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorInscricao",
                table: "Dupla",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecoSegundaInscricao",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "ValorInscricao",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "ValorInscricao",
                table: "Dupla");
        }
    }
}
