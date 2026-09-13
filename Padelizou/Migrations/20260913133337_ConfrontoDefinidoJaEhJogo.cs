using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ConfrontoDefinidoJaEhJogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Partida_CategoriaId",
                table: "Partida");

            migrationBuilder.AddColumn<int>(
                name: "NumeroNaFase",
                table: "Partida",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partida_Categoria_Fase_Numero",
                table: "Partida",
                columns: new[] { "CategoriaId", "Fase", "NumeroNaFase" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Partida_Categoria_Fase_Numero",
                table: "Partida");

            migrationBuilder.DropColumn(
                name: "NumeroNaFase",
                table: "Partida");

            migrationBuilder.CreateIndex(
                name: "IX_Partida_CategoriaId",
                table: "Partida",
                column: "CategoriaId");
        }
    }
}
