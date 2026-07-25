using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class AddRecebimentoJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AsaasWalletId",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModoComissao",
                table: "Jogador",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceberPagamentoOnline",
                table: "Jogador",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsaasWalletId",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "ModoComissao",
                table: "Jogador");

            migrationBuilder.DropColumn(
                name: "ReceberPagamentoOnline",
                table: "Jogador");
        }
    }
}
