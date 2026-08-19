using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PalpiteComPlacar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GamesDupla1",
                table: "PalpitePartida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GamesDupla2",
                table: "PalpitePartida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetsDupla1",
                table: "PalpitePartida",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SetsDupla2",
                table: "PalpitePartida",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GamesDupla1",
                table: "PalpitePartida");

            migrationBuilder.DropColumn(
                name: "GamesDupla2",
                table: "PalpitePartida");

            migrationBuilder.DropColumn(
                name: "SetsDupla1",
                table: "PalpitePartida");

            migrationBuilder.DropColumn(
                name: "SetsDupla2",
                table: "PalpitePartida");
        }
    }
}
