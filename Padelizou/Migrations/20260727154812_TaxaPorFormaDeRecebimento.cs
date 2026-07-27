using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TaxaPorFormaDeRecebimento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Online" virou duas opções, com taxas diferentes. Quem já estava online aceitava
            // todas as formas de pagamento, então vira "OnlineTodas" — mudar pra "OnlinePix"
            // travaria em Pix cobranças de torneios que já estão anunciados.
            migrationBuilder.Sql(@"UPDATE ""Torneio"" SET ""FormaPagamento"" = 'OnlineTodas' WHERE ""FormaPagamento"" = 'Online';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
