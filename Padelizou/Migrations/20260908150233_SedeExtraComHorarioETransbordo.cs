using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class SedeExtraComHorarioETransbordo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EvitarDoisJogosNaSedeExtra",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisponivelAte",
                table: "Quadra",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisponivelDe",
                table: "Quadra",
                type: "timestamp without time zone",
                nullable: true);

            // ⚠️ `true`, E NÃO O `false` QUE O `migrations add` GEROU (ele não lê o `= true` do
            // modelo). Este valor BACKFILLA as linhas existentes: com `false`, toda categoria
            // sem clube fixo de todo torneio de duas sedes ficaria presa na sede principal, em
            // silêncio, e a próxima grade sairia diferente sem ninguém ter mexido em nada.
            // Travado pelo gate de GateDoDefaultDasColunasBoolTests.
            migrationBuilder.AddColumn<bool>(
                name: "PodeJogarNaSedeExtra",
                table: "Categoria",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvitarDoisJogosNaSedeExtra",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "DisponivelAte",
                table: "Quadra");

            migrationBuilder.DropColumn(
                name: "DisponivelDe",
                table: "Quadra");

            migrationBuilder.DropColumn(
                name: "PodeJogarNaSedeExtra",
                table: "Categoria");
        }
    }
}
