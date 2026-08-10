using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class EnderecoPeloCep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnderecoPublico",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Logradouro",
                table: "Jogador",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "EnderecoPublico",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "Logradouro",
                table: "Jogador");
        }
    }
}
