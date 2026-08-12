using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class QuadraPreferidaDaCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuadraDaCategoria",
                columns: table => new
                {
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    QuadraId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuadraDaCategoria", x => new { x.CategoriaId, x.QuadraId });
                    table.ForeignKey(
                        name: "FK_QuadraDaCategoria_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuadraDaCategoria_Quadra_QuadraId",
                        column: x => x.QuadraId,
                        principalTable: "Quadra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuadraDaCategoria_QuadraId",
                table: "QuadraDaCategoria",
                column: "QuadraId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuadraDaCategoria");
        }
    }
}
