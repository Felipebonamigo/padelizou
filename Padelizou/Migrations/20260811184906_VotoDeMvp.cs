using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class VotoDeMvp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VotoDeMvp",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    VotanteId = table.Column<int>(type: "integer", nullable: false),
                    CandidatoId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VotoDeMvp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VotoDeMvp_Jogador_CandidatoId",
                        column: x => x.CandidatoId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VotoDeMvp_Jogador_VotanteId",
                        column: x => x.VotanteId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VotoDeMvp_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VotoDeMvp_CandidatoId",
                table: "VotoDeMvp",
                column: "CandidatoId");

            migrationBuilder.CreateIndex(
                name: "IX_VotoDeMvp_TorneioId_VotanteId",
                table: "VotoDeMvp",
                columns: new[] { "TorneioId", "VotanteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VotoDeMvp_VotanteId",
                table: "VotoDeMvp",
                column: "VotanteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VotoDeMvp");
        }
    }
}
