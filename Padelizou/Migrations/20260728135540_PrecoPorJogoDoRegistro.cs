using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class PrecoPorJogoDoRegistro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JogosPrevistos",
                table: "SolicitacoesRegistroResultados",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoPorJogoCotado",
                table: "SolicitacoesRegistroResultados",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorMinimoCotado",
                table: "SolicitacoesRegistroResultados",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JogosPrevistos",
                table: "SolicitacoesRegistroResultados");

            migrationBuilder.DropColumn(
                name: "PrecoPorJogoCotado",
                table: "SolicitacoesRegistroResultados");

            migrationBuilder.DropColumn(
                name: "ValorMinimoCotado",
                table: "SolicitacoesRegistroResultados");
        }
    }
}
