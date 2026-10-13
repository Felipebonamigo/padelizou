using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class VotoDoMvpPorNaipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VotoDeMvp_TorneioId_VotanteId",
                table: "VotoDeMvp");

            migrationBuilder.AddColumn<string>(
                name: "Naipe",
                table: "VotoDeMvp",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_VotoDeMvp_TorneioId_VotanteId_Naipe",
                table: "VotoDeMvp",
                columns: new[] { "TorneioId", "VotanteId", "Naipe" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VotoDeMvp_TorneioId_VotanteId_Naipe",
                table: "VotoDeMvp");

            migrationBuilder.DropColumn(
                name: "Naipe",
                table: "VotoDeMvp");

            migrationBuilder.CreateIndex(
                name: "IX_VotoDeMvp_TorneioId_VotanteId",
                table: "VotoDeMvp",
                columns: new[] { "TorneioId", "VotanteId" },
                unique: true);
        }
    }
}
