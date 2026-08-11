using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CinturaoDosDesafios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReinadoNoCinturao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoriaPadraoId = table.Column<int>(type: "integer", nullable: false),
                    Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Jogador2Id = table.Column<int>(type: "integer", nullable: false),
                    ComecouEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TerminouEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Defesas = table.Column<int>(type: "integer", nullable: false),
                    DesafioDeConquistaId = table.Column<int>(type: "integer", nullable: true),
                    DesafioDaPerdaId = table.Column<int>(type: "integer", nullable: true),
                    ComoTerminou = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReinadoNoCinturao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReinadoNoCinturao_CategoriaPadrao_CategoriaPadraoId",
                        column: x => x.CategoriaPadraoId,
                        principalTable: "CategoriaPadrao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReinadoNoCinturao_Jogador_Jogador1Id",
                        column: x => x.Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReinadoNoCinturao_Jogador_Jogador2Id",
                        column: x => x.Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReinadoNoCinturao_CategoriaPadraoId_TerminouEm",
                table: "ReinadoNoCinturao",
                columns: new[] { "CategoriaPadraoId", "TerminouEm" });

            migrationBuilder.CreateIndex(
                name: "IX_ReinadoNoCinturao_Jogador1Id",
                table: "ReinadoNoCinturao",
                column: "Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_ReinadoNoCinturao_Jogador2Id",
                table: "ReinadoNoCinturao",
                column: "Jogador2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReinadoNoCinturao");
        }
    }
}
