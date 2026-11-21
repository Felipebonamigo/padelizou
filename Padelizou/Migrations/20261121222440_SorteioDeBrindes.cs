using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class SorteioDeBrindes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SorteioDoTorneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    Premio = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Quantidade = table.Column<int>(type: "integer", nullable: false),
                    FeitoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    FeitoPorJogadorId = table.Column<int>(type: "integer", nullable: false),
                    SubstituidoPorId = table.Column<int>(type: "integer", nullable: true),
                    SubstituidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteioDoTorneio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SorteioDoTorneio_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GanhadorDoSorteio",
                columns: table => new
                {
                    SorteioId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GanhadorDoSorteio", x => new { x.SorteioId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_GanhadorDoSorteio_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GanhadorDoSorteio_SorteioDoTorneio_SorteioId",
                        column: x => x.SorteioId,
                        principalTable: "SorteioDoTorneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GanhadorDoSorteio_JogadorId",
                table: "GanhadorDoSorteio",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_SorteioDoTorneio_TorneioId",
                table: "SorteioDoTorneio",
                column: "TorneioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GanhadorDoSorteio");

            migrationBuilder.DropTable(
                name: "SorteioDoTorneio");
        }
    }
}
