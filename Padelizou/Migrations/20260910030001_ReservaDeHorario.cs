using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ReservaDeHorario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservaDeHorario",
                columns: table => new
                {
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Fase = table.Column<string>(type: "text", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Horario = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NomeQuadra = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservaDeHorario", x => new { x.CategoriaId, x.Fase, x.Numero });
                    table.ForeignKey(
                        name: "FK_ReservaDeHorario_Categoria_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categoria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservaDeHorario");
        }
    }
}
