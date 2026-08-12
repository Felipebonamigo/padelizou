using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TorneioOrigem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TorneioOrigemId",
                table: "Torneio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Torneio_TorneioOrigemId",
                table: "Torneio",
                column: "TorneioOrigemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Torneio_Torneio_TorneioOrigemId",
                table: "Torneio",
                column: "TorneioOrigemId",
                principalTable: "Torneio",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Torneio_Torneio_TorneioOrigemId",
                table: "Torneio");

            migrationBuilder.DropIndex(
                name: "IX_Torneio_TorneioOrigemId",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TorneioOrigemId",
                table: "Torneio");
        }
    }
}
