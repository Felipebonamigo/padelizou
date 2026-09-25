using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoDeSaidasDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SaidaDoTorneio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TorneioId = table.Column<int>(type: "integer", nullable: false),
                    CategoriaId = table.Column<int>(type: "integer", nullable: false),
                    Jogador1Id = table.Column<int>(type: "integer", nullable: false),
                    Jogador2Id = table.Column<int>(type: "integer", nullable: true),
                    QuemPediuId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<int>(type: "integer", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    EstavaPaga = table.Column<bool>(type: "boolean", nullable: false),
                    AbriuVaga = table.Column<bool>(type: "boolean", nullable: false),
                    SaiuEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaidaDoTorneio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaidaDoTorneio_Torneio_TorneioId",
                        column: x => x.TorneioId,
                        principalTable: "Torneio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaidaDoTorneio_TorneioId_SaiuEm",
                table: "SaidaDoTorneio",
                columns: new[] { "TorneioId", "SaiuEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaidaDoTorneio");
        }
    }
}
