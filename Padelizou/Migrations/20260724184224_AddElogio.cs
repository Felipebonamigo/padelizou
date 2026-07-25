using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddElogio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Elogios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeJogadorId = table.Column<int>(type: "integer", nullable: false),
                    ParaJogadorId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Elogios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Elogios_Jogador_DeJogadorId",
                        column: x => x.DeJogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Elogios_Jogador_ParaJogadorId",
                        column: x => x.ParaJogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Elogios_DeJogadorId_ParaJogadorId_Tipo",
                table: "Elogios",
                columns: new[] { "DeJogadorId", "ParaJogadorId", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Elogios_ParaJogadorId",
                table: "Elogios",
                column: "ParaJogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Elogios");
        }
    }
}
