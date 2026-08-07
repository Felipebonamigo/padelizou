using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class RecebimentoPorPixDireto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConfirmadoPorJogadorId",
                table: "Pagamento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetodoPagamento",
                table: "Pagamento",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmadoPorJogadorId",
                table: "Pagamento");

            migrationBuilder.DropColumn(
                name: "MetodoPagamento",
                table: "Pagamento");
        }
    }
}
