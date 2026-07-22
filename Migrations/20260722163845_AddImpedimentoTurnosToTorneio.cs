using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddImpedimentoTurnosToTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermiteImpedimentoSabadoManha",
                table: "Torneio",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PermiteImpedimentoSabadoTarde",
                table: "Torneio",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PermiteImpedimentoSextaNoite",
                table: "Torneio",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PermiteImpedimentoSabadoManha",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PermiteImpedimentoSabadoTarde",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PermiteImpedimentoSextaNoite",
                table: "Torneio");
        }
    }
}
