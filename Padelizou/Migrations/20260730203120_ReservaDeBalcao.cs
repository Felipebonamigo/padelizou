using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ReservaDeBalcao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CelularClienteBalcao",
                table: "MarcacaoJogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeClienteBalcao",
                table: "MarcacaoJogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PagaNoLocal",
                table: "MarcacaoJogo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PagoEm",
                table: "MarcacaoJogo",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CelularClienteBalcao",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "NomeClienteBalcao",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "PagaNoLocal",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "PagoEm",
                table: "MarcacaoJogo");
        }
    }
}
