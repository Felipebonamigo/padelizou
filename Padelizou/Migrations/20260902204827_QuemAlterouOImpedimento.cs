using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class QuemAlterouOImpedimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImpedimentoAlteradoEm",
                table: "Dupla",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImpedimentoAlteradoPorId",
                table: "Dupla",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpedimentoAlteradoEm",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "ImpedimentoAlteradoPorId",
                table: "Dupla");
        }
    }
}
