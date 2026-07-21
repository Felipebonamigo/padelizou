using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAvisoJogoEPreferenciasJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LadoQuadra",
                table: "Jogador",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarEmail",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificarWhatsApp",
                table: "Jogador",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AvisoJogo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriadorId = table.Column<int>(type: "int", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "int", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoJogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AvisoJogo_Jogador_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorCategoria",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    CategoriaPadraoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorCategoria", x => new { x.JogadorId, x.CategoriaPadraoId });
                    table.ForeignKey(
                        name: "FK_JogadorCategoria_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorCategoria_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorClube",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    ClubeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorClube", x => new { x.JogadorId, x.ClubeId });
                    table.ForeignKey(
                        name: "FK_JogadorClube_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogadorClube_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JogadorDiaHorario",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    DiaSemana = table.Column<int>(type: "int", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogadorDiaHorario", x => new { x.JogadorId, x.DiaSemana, x.Periodo });
                    table.ForeignKey(
                        name: "FK_JogadorDiaHorario_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_CategoriaPadraoId",
                table: "AvisoJogo",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_ClubeId",
                table: "AvisoJogo",
                column: "ClubeId");

            migrationBuilder.CreateIndex(
                name: "IX_AvisoJogo_CriadorId",
                table: "AvisoJogo",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorCategoria_CategoriaPadraoId",
                table: "JogadorCategoria",
                column: "CategoriaPadraoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogadorClube_ClubeId",
                table: "JogadorClube",
                column: "ClubeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvisoJogo");

            migrationBuilder.DropTable(
                name: "JogadorCategoria");

            migrationBuilder.DropTable(
                name: "JogadorClube");

            migrationBuilder.DropTable(
                name: "JogadorDiaHorario");

            migrationBuilder.DropColumn(
                name: "LadoQuadra",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarEmail",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NotificarWhatsApp",
                table: "Jogador");
        }
    }
}
