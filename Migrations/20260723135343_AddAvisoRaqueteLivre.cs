using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAvisoRaqueteLivre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisoRaqueteLivre",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClubeId = table.Column<int>(type: "int", nullable: false),
                    CriadorId = table.Column<int>(type: "int", nullable: false),
                    DataHoraInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LimiteVagas = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoRaqueteLivre", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisoRaqueteLivre_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoRaqueteLivre_Jogador_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InscricaoRaqueteLivre",
                columns: table => new
                {
                    AvisoRaqueteLivreId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    EmListaDeEspera = table.Column<bool>(type: "bit", nullable: false),
                    InscritoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricaoRaqueteLivre", x => new { x.AvisoRaqueteLivreId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_InscricaoRaqueteLivre_AvisoRaqueteLivre_AvisoRaqueteLivreId",
                        column: x => x.AvisoRaqueteLivreId,
                        principalTable: "AvisoRaqueteLivre",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InscricaoRaqueteLivre_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisoRaqueteLivre_ClubeId",
                table: "AvisoRaqueteLivre",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoRaqueteLivre_CriadorId",
                table: "AvisoRaqueteLivre",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoRaqueteLivre_JogadorId",
                table: "InscricaoRaqueteLivre",
                column: "JogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InscricaoRaqueteLivre");

            migrationBuilder.DropTable(
                name: "AvisoRaqueteLivre");
        }
    }
}
