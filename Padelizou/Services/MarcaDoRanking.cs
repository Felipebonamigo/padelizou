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

    // A página DELES que explica o que o clube/organizador ganha ao habilitar o ranking:
    // atleta fora de categoria pego antes do torneio, divulgação pra base de atletas deles,
    // pontuação no ranking nacional, clube de vantagens e etapas oficiais.
    //
    // Mora aqui pelo mesmo motivo do nome: endereço de terceiro digitado à mão em duas telas é
    // o que vira link quebrado em UMA delas no dia em que eles mudarem a rota. São duas hoje
    // (criar torneio e editar torneio) — a terceira que vier já acha o endereço aqui.
    //
    // ⚠️ É a página de VENDA deles, pro organizador. Nada a ver com a nossa API de torneios
    // (API-TORNEIOS.md), que é conversa entre servidores e não interessa a quem cria torneio.
    public const string ParaClubes = "https://mundodoatleta.com.br/parceria-clubes";

    // A arte que eles usam hoje, servida por nós (nunca por link direto ao site deles: imagem
    // de terceiro no nosso HTML quebra quando eles renomeiam o arquivo, e ainda entrega o IP
    // de quem abriu a nossa tela pro servidor deles).
    //
    // ⚠️ NASCE VAZIO. Enquanto ninguém salvar o arquivo em `wwwroot/image/`, a tela mostra só o
    // texto — que é melhor que o ícone de imagem quebrada ao lado do nome do parceiro. Pra
    // ligar: salve a arte e escreva o caminho aqui ("/image/ranking-brasil.webp").
    public const string Logo = "";

    public static bool TemLogo => !string.IsNullOrWhiteSpace(Logo);
}
