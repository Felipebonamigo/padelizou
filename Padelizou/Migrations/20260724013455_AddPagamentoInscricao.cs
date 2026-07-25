using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddPagamentoInscricao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DadosInscricao",
                table: "Pagamento",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JogoAulaId",
                table: "Pagamento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecebedorId",
                table: "Pagamento",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TorneioId",
                table: "Pagamento",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DadosInscricao",
                table: "Pagamento");

            migrationBuilder.DropColumn(
                name: "JogoAulaId",
                table: "Pagamento");

            migrationBuilder.DropColumn(
                name: "RecebedorId",
                table: "Pagamento");

            migrationBuilder.DropColumn(
                name: "TorneioId",
                table: "Pagamento");
        }
    }
}
