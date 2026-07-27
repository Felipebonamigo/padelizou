using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class StatusPagoDaInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcluirSeNaoPagar",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PagamentoObrigatorioNaInscricao",
                table: "Torneio",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrazoPagamento",
                table: "Torneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Pago",
                table: "InscricaoAmericana",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PagoEm",
                table: "InscricaoAmericana",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Pago",
                table: "Dupla",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PagoEm",
                table: "Dupla",
                type: "timestamp without time zone",
                nullable: true);

            // Inscrição que nasceu de um pagamento confirmado JÁ está paga. Sem isto, todo
            // mundo que pagou apareceria como devedor no dia seguinte ao deploy.
            migrationBuilder.Sql(@"
                UPDATE ""Dupla"" d SET ""Pago"" = true, ""PagoEm"" = p.""ConfirmadoEm""
                FROM ""Pagamento"" p
                WHERE p.""ReferenciaId"" = d.""Id"" AND p.""Status"" = 'Confirmado'
                  AND p.""Tipo"" = 'TorneioDupla';");

            migrationBuilder.Sql(@"
                UPDATE ""InscricaoAmericana"" i SET ""Pago"" = true, ""PagoEm"" = p.""ConfirmadoEm""
                FROM ""Pagamento"" p
                WHERE p.""ReferenciaId"" = i.""Id"" AND p.""Status"" = 'Confirmado'
                  AND p.""Tipo"" = 'TorneioAmericano';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcluirSeNaoPagar",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PagamentoObrigatorioNaInscricao",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "PrazoPagamento",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "Pago",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "PagoEm",
                table: "InscricaoAmericana");

            migrationBuilder.DropColumn(
                name: "Pago",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "PagoEm",
                table: "Dupla");
        }
    }
}
