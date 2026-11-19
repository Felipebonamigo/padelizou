using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class HorariosDaDupla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HorarioDaDupla",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DuplaId = table.Column<int>(type: "integer", nullable: false),
                    PodeJogar = table.Column<bool>(type: "boolean", nullable: false),
                    Inicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Fim = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HorarioDaDupla", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HorarioDaDupla_Dupla_DuplaId",
                        column: x => x.DuplaId,
                        principalTable: "Dupla",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HorarioDaDupla_DuplaId",
                table: "HorarioDaDupla",
                column: "DuplaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HorarioDaDupla");
        }
    }
}
