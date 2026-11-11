using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoManualDoJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PagamentoManualDoJogador",
                columns: table => new
                {
                    DuplaId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    MarcadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentoManualDoJogador", x => new { x.DuplaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_PagamentoManualDoJogador_Dupla_DuplaId",
                        column: x => x.DuplaId,
                        principalTable: "Dupla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagamentoManualDoJogador");
        }
    }
}
