using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CampanhaNoPadelimetro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "HistoricoDePadelimetro",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId",
                table: "HistoricoDePadelimetro",
                column: "CategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_HistoricoDePadelimetro_Categoria_CategoriaId",
                table: "HistoricoDePadelimetro",
                column: "CategoriaId",
                principalTable: "Categoria",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HistoricoDePadelimetro_Categoria_CategoriaId",
                table: "HistoricoDePadelimetro");

            migrationBuilder.DropIndex(
                name: "IX_HistoricoDePadelimetro_CategoriaId",
                table: "HistoricoDePadelimetro");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "HistoricoDePadelimetro");
        }
    }
}
