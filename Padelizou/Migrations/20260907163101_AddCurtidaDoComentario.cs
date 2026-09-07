using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddCurtidaDoComentario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurtidasDoComentario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ComentarioId = table.Column<int>(type: "integer", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurtidasDoComentario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurtidasDoComentario_ComentariosPerfil_ComentarioId",
                        column: x => x.ComentarioId,
                        principalTable: "ComentariosPerfil",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurtidasDoComentario_Jogador_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Jogador",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurtidasDoComentario_ComentarioId_JogadorId",
                table: "CurtidasDoComentario",
                columns: new[] { "ComentarioId", "JogadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurtidasDoComentario_JogadorId",
                table: "CurtidasDoComentario",
                column: "JogadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurtidasDoComentario");
        }
    }
}
