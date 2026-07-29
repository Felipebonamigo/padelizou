using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TaxaExternoDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TaxaExternoNegociadaEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxaExternoNegociadaObs",
                table: "Torneio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxaExternoPagaEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxaExternoNegociadaEm",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TaxaExternoNegociadaObs",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "TaxaExternoPagaEm",
                table: "Torneio");
        }
    }
}
