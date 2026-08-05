using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CarenciaParaTrocarNomeEApelido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApelidoAlteradoEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NomeAlteradoEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApelidoAlteradoEm",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "NomeAlteradoEm",
                table: "Jogador");
        }
    }
}
