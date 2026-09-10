using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class OrdemNoHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrdemNoHorario",
                table: "ReservaDeHorario",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrdemNoHorario",
                table: "Partida",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrdemNoHorario",
                table: "ReservaDeHorario");

            migrationBuilder.DropColumn(
                name: "OrdemNoHorario",
                table: "Partida");
        }
    }
}
