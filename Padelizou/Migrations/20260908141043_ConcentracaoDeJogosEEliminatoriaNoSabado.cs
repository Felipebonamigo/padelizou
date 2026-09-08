using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ConcentracaoDeJogosEEliminatoriaNoSabado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConcentrarJogosEm",
                table: "Dupla",
                type: "integer",
                nullable: true);

            // ⚠️ `true`, E NÃO O `false` QUE O `migrations add` GEROU. O gerador não lê o
            // inicializador da propriedade (`= true` em Models/Categoria), e este valor é o que
            // BACKFILLA as linhas que já estão no banco: com `false`, o deploy tiraria em
            // silêncio o sábado à noite de TODAS as categorias de TODOS os torneios existentes,
            // sem ninguém ter apertado botão nenhum. Travado em
            // MigrationDaEliminatoriaNoSabadoTests, porque regenerar a migration traz o `false`
            // de volta.
            migrationBuilder.AddColumn<bool>(
                name: "EliminatoriaNoSabadoANoite",
                table: "Categoria",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConcentrarJogosEm",
                table: "Dupla");

            migrationBuilder.DropColumn(
                name: "EliminatoriaNoSabadoANoite",
                table: "Categoria");
        }
    }
}
