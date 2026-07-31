using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PixRecadoEDatasPrevistasDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChavePixOrganizador",
                table: "Torneio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrevisaoChaveamento",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrevisaoEncerramentoInscricoes",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecadoAosInscritos",
                table: "Torneio",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChavePixOrganizador",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PrevisaoChaveamento",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PrevisaoEncerramentoInscricoes",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "RecadoAosInscritos",
                table: "Torneio");
        }
    }
}
