using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TorneioDeUmTimeSo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimeExclusivoId",
                table: "Torneio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Torneio_TimeExclusivoId",
                table: "Torneio",
                column: "TimeExclusivoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Torneio_Times_TimeExclusivoId",
                table: "Torneio",
                column: "TimeExclusivoId",
                principalTable: "Times",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Torneio_Times_TimeExclusivoId",
                table: "Torneio");

            migrationBuilder.DropIndex(
                name: "IX_Torneio_TimeExclusivoId",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TimeExclusivoId",
                table: "Torneio");
        }
    }
}
