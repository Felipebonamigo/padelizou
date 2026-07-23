using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddLimiteVagasEListaDeEspera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LimiteDuplasTotal",
                table: "Torneio",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EmListaDeEspera",
                table: "InscricaoAmericana",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EmListaDeEspera",
                table: "Dupla",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LimiteDuplas",
                table: "Categoria",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimiteDuplasTotal",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "EmListaDeEspera",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "EmListaDeEspera",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "LimiteDuplas",
                table: "Categoria");
        }
    }
}
