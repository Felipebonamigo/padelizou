using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminEnotificacoesToJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAdminGeral",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminRaiz",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarAvisoJogo",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarJogoAula",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarRaqueteLivre",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarSeguidosTorneio",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarTorneiosAbertos",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAdminGeral",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "IsAdminRaiz",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarAvisoJogo",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarJogoAula",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarRaqueteLivre",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarSeguidosTorneio",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarTorneiosAbertos",
                table: "Jogador");
        }
    }
}
