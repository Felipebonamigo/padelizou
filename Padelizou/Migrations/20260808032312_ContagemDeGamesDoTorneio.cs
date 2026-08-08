using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ContagemDeGamesDoTorneio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ defaultValue "Ate", e NÃO o "" que o EF gera do default de string: todo
            // torneio que já existe sempre contou games no "até", e é isso que a coluna tem
            // que dizer. String vazia funcionaria por acidente (ContagemDeGamesDoTorneio.Valido
            // devolve "Ate" pro desconhecido), mas deixaria o banco com um valor que não
            // significa nada — e a próxima pessoa a ler a coluna direto não teria como saber.
            // Mesma armadilha do `defaultValue: true` da migração de CategoriaPadrao.Ativa.
            migrationBuilder.AddColumn<string>(
                name: "ContagemDeGames",
                table: "Torneio",
                type: "text",
                nullable: false,
                defaultValue: "Ate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContagemDeGames",
                table: "Torneio");
        }
    }
}
