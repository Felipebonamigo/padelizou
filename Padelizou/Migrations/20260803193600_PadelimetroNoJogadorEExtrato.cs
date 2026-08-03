using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PadelimetroNoJogadorEExtrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JogosDePadelimetro",
                table: "Jogador",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Padelimetro",
                table: "Jogador",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HistoricoDePadelimetro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    PartidaId = table.Column<int>(type: "integer", nullable: true),
                    NivelAntes = table.Column<int>(type: "integer", nullable: false),
                    Delta = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoDePadelimetro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoDePadelimetro_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HistoricoDePadelimetro_Partida_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "Partida",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDePadelimetro_JogadorId_CriadoEm",
                table: "HistoricoDePadelimetro",
                columns: new[] { "JogadorId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDePadelimetro_PartidaId",
                table: "HistoricoDePadelimetro",
                column: "PartidaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoDePadelimetro");

            migrationBuilder.DropColumn(
                name: "JogosDePadelimetro",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "Padelimetro",
                table: "Jogador");
        }
    }
}
