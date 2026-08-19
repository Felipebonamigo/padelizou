using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AssinaturaDoClube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssinaturaClubePagaAte",
                table: "Clubes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanoDoClube",
                table: "Clubes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TesteDoClubeInicio",
                table: "Clubes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UltimoLembreteDoPlano",
                table: "Clubes",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssinaturaClubePagaAte",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "PlanoDoClube",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "TesteDoClubeInicio",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "UltimoLembreteDoPlano",
                table: "Clubes");
        }
    }
}
