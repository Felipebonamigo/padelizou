using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PlanoDoProfessorNoJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AssinaturaProfessorPagaAte",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanoProfessor",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TesteProfessorInicio",
                table: "Jogador",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssinaturaProfessorPagaAte",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "PlanoProfessor",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "TesteProfessorInicio",
                table: "Jogador");
        }
    }
}
