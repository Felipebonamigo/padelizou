using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddNomeCompletoEAcompanhantesToAula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Acompanhantes",
                table: "Aula",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeCompletoAluno",
                table: "Aula",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acompanhantes",
                table: "Aula");

            migrationBuilder.DropColumn(
                name: "NomeCompletoAluno",
                table: "Aula");
        }
    }
}
