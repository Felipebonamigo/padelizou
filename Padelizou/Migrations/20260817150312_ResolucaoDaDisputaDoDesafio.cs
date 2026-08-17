using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ResolucaoDaDisputaDoDesafio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResolucaoDaDisputa",
                table: "Desafio",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResolvidoPorId",
                table: "Desafio",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResolucaoDaDisputa",
                table: "Desafio");

            migrationBuilder.DropColumn(
                name: "ResolvidoPorId",
                table: "Desafio");
        }
    }
}
