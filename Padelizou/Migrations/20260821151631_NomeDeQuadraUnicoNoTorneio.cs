using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Padelizou.Migrations
{
    /// <summary>
    /// O nome da quadra vira identidade dentro do torneio (21/08/2026).
    ///
    /// ⚠️ ESCRITA À MÃO DE PROPÓSITO. O EF gerou este arquivo VAZIO porque `HasIndex().IsUnique()`
    /// não serviria aqui, e a razão é a tela de editar torneio: TorneiosController.Criacao grava as
    /// quadras renomeando UMA A UMA, num laço. Trocar "Quadra A" por "Quadra B" e vice-versa
    /// (coisa que o organizador faz) passa por um instante em que as duas se chamam "Quadra B" —
    /// e um índice único comum recusaria o UPDATE no meio, com erro de banco na cara do usuário,
    /// numa edição perfeitamente legítima.
    ///
    /// DEFERRABLE INITIALLY DEFERRED resolve: a checagem acontece no COMMIT, não a cada linha. No
    /// meio da transação as duas podem se chamar igual; no fim, não.
    /// </summary>
    public partial class NomeDeQuadraUnicoNoTorneio : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ⚠️ ANTES DE TRANCAR A PORTA, ARRUMAR QUEM JÁ ESTÁ DENTRO. Conferi prod e dev em
            // 21/08/2026: zero repetido nos dois. Mas base local de desenvolvimento pode ter, e
            // migration que estoura no `Update-Database` trava a subida do app inteiro.
            //
            // Renomeia só a repetição, mantendo a MAIS ANTIGA com o nome original — as partidas já
            // marcadas apontam pro nome como texto (`Partida.NomeQuadra`), e a primeira é a que
            // tem mais chance de ser a que elas queriam dizer.
            migrationBuilder.Sql(@"
                UPDATE ""Quadra"" q
                SET ""Nome"" = q.""Nome"" || ' (' || d.ordem || ')'
                FROM (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""TorneioId"", lower(trim(""Nome""))
                                              ORDER BY ""Id"") AS ordem
                    FROM ""Quadra""
                ) d
                WHERE d.""Id"" = q.""Id"" AND d.ordem > 1;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""Quadra""
                ADD CONSTRAINT ""UQ_Quadra_Torneio_Nome"" UNIQUE (""TorneioId"", ""Nome"")
                DEFERRABLE INITIALLY DEFERRED;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Quadra"" DROP CONSTRAINT IF EXISTS ""UQ_Quadra_Torneio_Nome"";");
        }
    }
}
