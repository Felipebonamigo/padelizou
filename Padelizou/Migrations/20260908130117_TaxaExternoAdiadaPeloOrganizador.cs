using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TaxaExternoAdiadaPeloOrganizador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TaxaExternoAdiadaEm",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxaExternoAdiadaEm",
                table: "Torneio");
        }
    }
}
