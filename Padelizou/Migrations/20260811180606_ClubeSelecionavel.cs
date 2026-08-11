using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class ClubeSelecionavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ `defaultValue: true` ESCRITO À MÃO. O EF gerou `false` aqui, como sempre gera
            // pra bool: o `= true` do modelo C# só vale pra objeto NOVO, e a coluna nova nasce
            // com o zero do tipo pra todas as linhas que já existem.
            //
            // Se isto subisse com `false`, TODOS os clubes da base sumiriam de TODA lista de
            // escolha no primeiro deploy — cadastro sem clube nenhum, criar torneio sem local,
            // e nenhum erro em lugar nenhum pra explicar. O passivo tem que nascer ligado; o
            // que se desliga depois é o lixo, um a um, em /Admin/Clubes.
            migrationBuilder.AddColumn<bool>(
                name: "Selecionavel",
                table: "Clubes",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Selecionavel",
                table: "Clubes");
        }
    }
}
