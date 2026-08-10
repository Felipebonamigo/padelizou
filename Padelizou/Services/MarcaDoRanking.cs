namespace Padelizou.Services;

// O NOME DO PARCEIRO NA TELA, num lugar só.
//
// Existe porque em 08/08/2026 eles avisaram que **"Ranking RS" está virando "Ranking Brasil"**,
// e o nome antigo estava digitado à mão em mais de vinte lugares — views, mensagens de recusa,
// avisos de criação de torneio, painel do admin. Trocar marca de terceiro à mão é a receita
// documentada dos piores defeitos deste projeto: uma cópia é corrigida, a outra não, e o site
// passa a chamar o mesmo parceiro de dois jeitos na mesma tela.
//
// ⚠️ É só o RÓTULO. O código (`RankingRsService`, `ValidarPeloRankingRs`, a rota `/Admin/
// RankingRs`, a coluna do banco) continua com o nome antigo de propósito: renomear identificador
// e coluna é migração e risco, e não muda nada pra quem usa. Se um dia isso for feito, é outro
// trabalho — e este arquivo continua sendo o que a pessoa lê.
public static class MarcaDoRanking
{
    // Trocar aqui troca no site inteiro.
    public const string Nome = "Ranking Brasil";

    // Quem calcula e publica. Fica separado do nome porque aparece em frase corrida
    // ("quem atualiza é o ..."), e não como marca.
    public const string Site = "mundodoatleta.com.br";
}
