using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ComentariosNaAvaliacaoDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Anonimo",
                table: "FeedbacksSite",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TorneioId",
                table: "FeedbacksSite",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Anonimo",
                table: "AvaliacaoDoTorneio",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioClube",
                table: "AvaliacaoDoTorneio",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioOrganizacao",
                table: "AvaliacaoDoTorneio",
                type: "character varying(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotaSistema",
                table: "AvaliacaoDoTorneio",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublicadoEm",
                table: "AvaliacaoDoTorneio",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublicadoPorId",
                table: "AvaliacaoDoTorneio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeedbacksSite_TorneioId",
                table: "FeedbacksSite",
                column: "TorneioId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbacksSite_Torneio_TorneioId",
                table: "FeedbacksSite",
                column: "TorneioId",
                principalTable: "Torneio",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeedbacksSite_Torneio_TorneioId",
                table: "FeedbacksSite");

            migrationBuilder.DropIndex(
                name: "IX_FeedbacksSite_TorneioId",
                table: "FeedbacksSite");

            migrationBuilder.DropColumn(
                name: "Anonimo",
                table: "FeedbacksSite");

            migrationBuilder.DropColumn(
                name: "TorneioId",
                table: "FeedbacksSite");

            migrationBuilder.DropColumn(
                name: "Anonimo",
                table: "AvaliacaoDoTorneio");

            migrationBuilder.DropColumn(
                name: "ComentarioClube",
                table: "AvaliacaoDoTorneio");

            migrationBuilder.DropColumn(
                name: "ComentarioOrganizacao",
                table: "AvaliacaoDoTorneio");

            migrationBuilder.DropColumn(
                name: "NotaSistema",
                table: "AvaliacaoDoTorneio");

            migrationBuilder.DropColumn(
                name: "PublicadoEm",
                table: "AvaliacaoDoTorneio");

            migrationBuilder.DropColumn(
                name: "PublicadoPorId",
                table: "AvaliacaoDoTorneio");
        }
    }
}
