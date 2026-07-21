using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddPalpitePartida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PalpitePartida",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartidaId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    DuplaEscolhidaId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PalpitePartida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Dupla_DuplaEscolhidaId",
                        column: x => x.DuplaEscolhidaId,
                        principalTable: "Dupla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PalpitePartida_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_DuplaEscolhidaId",
                table: "PalpitePartida",
                column: "DuplaEscolhidaId");

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_JogadorId",
                table: "PalpitePartida",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_PalpitePartida_PartidaId_JogadorId",
                table: "PalpitePartida",
                columns: new[] { "PartidaId", "JogadorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PalpitePartida");
        }
    }
}
