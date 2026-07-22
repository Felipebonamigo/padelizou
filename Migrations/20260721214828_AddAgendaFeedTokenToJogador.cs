using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaFeedTokenToJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgendaFeedToken",
                table: "Jogador",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.CreateIndex(
                name: "IX_Jogador_AgendaFeedToken",
                table: "Jogador",
                column: "AgendaFeedToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jogador_AgendaFeedToken",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "AgendaFeedToken",
                table: "Jogador");
        }
    }
}
