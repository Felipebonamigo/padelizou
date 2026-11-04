using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CamisetaDeBrinde : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CamisetaEscolhaAte",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OfereceCamiseta",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TamanhosDeCamiseta",
                table: "Torneio",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CamisetaDoAtleta",
                columns: table => new
                {
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Tamanho = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EscolhidaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamisetaDoAtleta", x => new { x.TorneioId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_CamisetaDoAtleta_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CamisetaDoAtleta_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamisetaDoAtleta_JogadorId",
                table: "CamisetaDoAtleta",
                column: "JogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CamisetaDoAtleta");

            migrationBuilder.DropColumn(
                name: "CamisetaEscolhaAte",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "OfereceCamiseta",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TamanhosDeCamiseta",
                table: "Torneio");
        }
    }
}
