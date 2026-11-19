using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class DuracaoPorFase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TempoFinalMinutos",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TempoMataMataMinutos",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TempoSemifinalMinutos",
                table: "Torneio",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TempoFinalMinutos",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TempoMataMataMinutos",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TempoSemifinalMinutos",
                table: "Torneio");
        }
    }
}
