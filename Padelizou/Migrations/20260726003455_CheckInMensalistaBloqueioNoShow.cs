using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CheckInMensalistaBloqueioNoShow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladaEm",
                table: "MarcacaoJogo",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanceladaPor",
                table: "MarcacaoJogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CobrarMesmoAssim",
                table: "MarcacaoJogo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EhBloqueio",
                table: "MarcacaoJogo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "MensalidadeId",
                table: "MarcacaoJogo",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoBloqueio",
                table: "MarcacaoJogo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInEm",
                table: "Dupla",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CobraNoShow",
                table: "Clubes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HorasMinimasCancelamento",
                table: "Clubes",
                type: "integer",
                nullable: false,
                // 12h pra bater com o default do modelo — clube antigo e novo com a mesma
                // regra. Sem risco: CobraNoShow nasce false, nada e cobrado.
                defaultValue: 12);

            migrationBuilder.AddColumn<string>(
                name: "PoliticaCancelamentoTexto",
                table: "Clubes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanceladaEm",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "CanceladaPor",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "CobrarMesmoAssim",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "EhBloqueio",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "MensalidadeId",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "MotivoBloqueio",
                table: "MarcacaoJogo");

            migrationBuilder.DropColumn(
                name: "CheckInEm",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "CobraNoShow",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "HorasMinimasCancelamento",
                table: "Clubes");

            migrationBuilder.DropColumn(
                name: "PoliticaCancelamentoTexto",
                table: "Clubes");
        }
    }
}
