using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class UmaCampanhaPorJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId",
                table: "HistoricoDePadelimetro");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId_JogadorId",
                table: "HistoricoDePadelimetro",
                columns: new[] { "CategoriaId", "JogadorId" },
                unique: true,
                filter: "\"CategoriaId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId_JogadorId",
                table: "HistoricoDePadelimetro");

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId",
                table: "HistoricoDePadelimetro",
                column: "CategoriaId");
        }
    }
}
