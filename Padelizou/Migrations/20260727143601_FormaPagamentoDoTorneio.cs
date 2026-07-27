using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class FormaPagamentoDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormaPagamento",
                table: "Torneio",
                type: "text",
                nullable: false,
                defaultValue: "Online");

            migrationBuilder.AddColumn<string>(
                name: "ModoComissao",
                table: "Torneio",
                type: "text",
                nullable: false,
                defaultValue: "Somada");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormaPagamento",
                table: "Torneio");

            migrationBuilder.DropColumn(
                name: "ModoComissao",
                table: "Torneio");
        }
    }
}
