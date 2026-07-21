using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddJogoSemanal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JogoSemanal",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrupoId = table.Column<int>(type: "int", nullable: false),
                    DataJogo = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Dupla1Jogador1Id = table.Column<int>(type: "int", nullable: false),
                    Dupla1Jogador2Id = table.Column<int>(type: "int", nullable: false),
                    Dupla2Jogador1Id = table.Column<int>(type: "int", nullable: false),
                    Dupla2Jogador2Id = table.Column<int>(type: "int", nullable: false),
                    GamesDupla1 = table.Column<int>(type: "int", nullable: false),
                    GamesDupla2 = table.Column<int>(type: "int", nullable: false),
                    RegistradoPorId = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JogoSemanal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_GrupoPrivado_GrupoId",
                        column: x => x.GrupoId,
                        principalTable: "GrupoPrivado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla1Jogador1Id",
                        column: x => x.Dupla1Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla1Jogador2Id",
                        column: x => x.Dupla1Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla2Jogador1Id",
                        column: x => x.Dupla2Jogador1Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_Dupla2Jogador2Id",
                        column: x => x.Dupla2Jogador2Id,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JogoSemanal_Jogador_RegistradoPorId",
                        column: x => x.RegistradoPorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla1Jogador1Id",
                table: "JogoSemanal",
                column: "Dupla1Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla1Jogador2Id",
                table: "JogoSemanal",
                column: "Dupla1Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla2Jogador1Id",
                table: "JogoSemanal",
                column: "Dupla2Jogador1Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_Dupla2Jogador2Id",
                table: "JogoSemanal",
                column: "Dupla2Jogador2Id");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_GrupoId",
                table: "JogoSemanal",
                column: "GrupoId");

            migrationBuilder.CreateIndex(
                name: "IX_JogoSemanal_RegistradoPorId",
                table: "JogoSemanal",
                column: "RegistradoPorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JogoSemanal");
        }
    }
}
