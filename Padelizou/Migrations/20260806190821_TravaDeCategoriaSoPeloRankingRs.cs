using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <summary>
    /// A trava de categoria passa a ser só a do Ranking RS (decisão do Felipe, 06/08/2026).
    ///
    /// A opção "Restrição de categoria (anti-sandbagging)" saiu das telas de criar e editar
    /// torneio. Sem esta migração, um torneio que já estava com o gatilho ligado continuaria
    /// barrando gente por uma regra que ninguém mais consegue ver nem desligar — o pior tipo
    /// de recusa, a que não tem tela onde ser corrigida.
    ///
    /// Não muda schema: a coluna e o motor continuam existindo (ver Torneio.RestricaoCategoria).
    /// Isto aqui é política, não remoção.
    /// </summary>
    public partial class TravaDeCategoriaSoPeloRankingRs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE \"Torneio\" SET \"RestricaoCategoria\" = 'Livre' WHERE \"RestricaoCategoria\" <> 'Livre';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sem volta: o gatilho que cada torneio tinha antes não fica guardado em lugar
            // nenhum. Reverter devolve a coluna ao que ela já é (livre) — quem quiser a regra
            // de volta escolhe o gatilho na tela, que é justamente o que some aqui.
        }
    }
}
