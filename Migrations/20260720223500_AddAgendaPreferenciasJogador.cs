using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaPreferenciasJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AgendaMostrarAlunos",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AgendaMostrarAulas",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AgendaMostrarJogosSemanais",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AgendaMostrarTorneios",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgendaMostrarAlunos",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "AgendaMostrarAulas",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "AgendaMostrarJogosSemanais",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "AgendaMostrarTorneios",
                table: "Jogador");
        }
    }
}
