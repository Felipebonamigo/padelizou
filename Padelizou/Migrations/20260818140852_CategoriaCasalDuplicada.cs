using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <inheritdoc />
    public partial class CategoriaCasalDuplicada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Categoria Casais" e "Categoria Casal" apareciam LADO A LADO na criação de
            // torneio — a mesma coisa escrita de dois jeitos, e quem organiza tinha que
            // adivinhar em qual inscrever a dupla.
            //
            // É a sobra da renomeação de 08/08/2026: o seed do Program.cs cria por NOME, então
            // ao trocar o nome pro plural ele criou a linha nova e deixou a antiga de pé, ligada.
            //
            // Desligar, e não APAGAR: torneio, preferência de jogador e aviso guardam o Id da
            // categoria: apagar a linha levaria junto o histórico de quem já jogou nela. Com
            // `Ativa = false` ela vale onde já está e some do catálogo que as telas oferecem —
            // o mesmo caminho das duas "Iniciantes" (ver CategoriasIniciantesDesligadas).
            //
            // ⚠️ Casa por NOME, nunca por `Codigo`: as duas linhas têm o mesmo código "CASAL",
            // e filtrar por ele desligaria a categoria de casal inteira.
            //
            // O EXISTS é o seguro: num banco onde a linha nova ainda não nasceu (start que não
            // rodou o seed), desligar a antiga deixaria o sistema sem NENHUMA categoria de casal.
            migrationBuilder.Sql(@"
                UPDATE ""CategoriaPadrao"" SET ""Ativa"" = false
                WHERE ""Nome"" = 'Categoria Casal'
                  AND EXISTS (SELECT 1 FROM ""CategoriaPadrao"" c2 WHERE c2.""Nome"" = 'Categoria Casais');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta a duplicata pro catálogo — é o estado que esta migração desfez.
            migrationBuilder.Sql(@"
                UPDATE ""CategoriaPadrao"" SET ""Ativa"" = true
                WHERE ""Nome"" = 'Categoria Casal';");
        }
    }
}
