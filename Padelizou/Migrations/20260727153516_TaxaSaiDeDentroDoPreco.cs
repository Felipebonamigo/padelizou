using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class TaxaSaiDeDentroDoPreco : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A taxa deixou de ser somada por cima do preço: agora o jogador paga exatamente
            // o valor anunciado em todo torneio. Sem alinhar os que já existiam, dois torneios
            // com o mesmo preço na tela cobrariam valores diferentes.
            migrationBuilder.Sql(@"UPDATE ""Torneio"" SET ""ModoComissao"" = 'Descontada';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
