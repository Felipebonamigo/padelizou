using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PedidoDePerfilDeOrganizador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoDoPedidoDeOrganizador",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PedidoDeOrganizadorRecusadoEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SolicitouOrganizadorEm",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoDoPedidoDeOrganizador",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "PedidoDeOrganizadorRecusadoEm",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "SolicitouOrganizadorEm",
                table: "Jogador");
        }
    }
}
