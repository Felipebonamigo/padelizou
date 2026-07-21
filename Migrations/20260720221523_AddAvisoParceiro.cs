using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAvisoParceiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvisoParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriadorId = table.Column<int>(type: "int", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NomeTorneio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvisoParceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvisoParceiro_Jogador_CriadorId",
                        column: x => x.CriadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CandidaturaParceiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AvisoParceiroId = table.Column<int>(type: "int", nullable: false),
                    CandidatoId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidaturaParceiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidaturaParceiro_AvisoParceiro_AvisoParceiroId",
                        column: x => x.AvisoParceiroId,
                        principalTable: "AvisoParceiro",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CandidaturaParceiro_Jogador_CandidatoId",
                        column: x => x.CandidatoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvisoParceiro_CriadorId",
                table: "AvisoParceiro",
                column: "CriadorId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidaturaParceiro_AvisoParceiroId",
                table: "CandidaturaParceiro",
                column: "AvisoParceiroId");

            migrationBuilder.CreateIndex(
                name: "IX_CandidaturaParceiro_CandidatoId",
                table: "CandidaturaParceiro",
                column: "CandidatoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidaturaParceiro");

            migrationBuilder.DropTable(
                name: "AvisoParceiro");
        }
    }
}
