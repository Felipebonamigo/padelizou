using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddJogoAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JogoAula",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfessorId = table.Column<int>(type: "int", nullable: false),
                    LocalAulaId = table.Column<int>(type: "int", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "int", nullable: false),
                    Modalidade = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LimiteVagas = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogoAula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JogoAula_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoAula_Jogador_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogoAula_LocalAula_LocalAulaId",
                        column: x => x.LocalAulaId,
                        principalTable: "LocalAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InscricaoJogoAula",
                columns: table => new
                {
                    JogoAulaId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    EmListaDeEspera = table.Column<bool>(type: "bit", nullable: false),
                    InscritoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InscricaoJogoAula", x => new { x.JogoAulaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_InscricaoJogoAula_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InscricaoJogoAula_JogoAula_JogoAulaId",
                        column: x => x.JogoAulaId,
                        principalTable: "JogoAula",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InscricaoJogoAula_JogadorId",
                table: "InscricaoJogoAula",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_CategoriaPadraoId",
                table: "JogoAula",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_LocalAulaId",
                table: "JogoAula",
                column: "LocalAulaId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoAula_ProfessorId",
                table: "JogoAula",
                column: "ProfessorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InscricaoJogoAula");

            migrationBuilder.DropTable(
                name: "JogoAula");
        }
    }
}
