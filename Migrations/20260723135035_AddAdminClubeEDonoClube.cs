using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminClubeEDonoClube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonoId",
                table: "Clubes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClubeAdministrador",
                columns: table => new
                {
                    ClubeId = table.Column<int>(type: "int", nullable: false),
                    JogadorId = table.Column<int>(type: "int", nullable: false),
                    AdicionadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubeAdministrador", x => new { x.ClubeId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_ClubeAdministrador_Clubes_ClubeId",
                        column: x => x.ClubeId,
                        principalTable: "Clubes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClubeAdministrador_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clubes_DonoId",
                table: "Clubes",
                column: "DonoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClubeAdministrador_JogadorId",
                table: "ClubeAdministrador",
                column: "JogadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clubes_Jogador_DonoId",
                table: "Clubes",
                column: "DonoId",
                principalTable: "Jogador",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clubes_Jogador_DonoId",
                table: "Clubes");

            migrationBuilder.DropTable(
                name: "ClubeAdministrador");

            migrationBuilder.DropIndex(
                name: "IX_Clubes_DonoId",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "DonoId",
                table: "Clubes");
        }
    }
}
