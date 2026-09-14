using Padelizou.Services;

namespace Padelizou.ViewModels;

// O BOTÃO "COMPARTILHAR" DE UMA ABA DO RANKING — e, principalmente, o ENDEREÇO da arte dela.
//
// 🗣️ Felipe, 14/09/2026: *"crie um botão para compartilhar o ranking"*, com um botão por aba.
//
// ⚠️ A URL É MONTADA AQUI, EM C#, E NÃO NO RAZOR, e essa é a única decisão de peso deste
// arquivo: ela precisa carregar os MESMOS filtros que a tela está mostrando — estado, cidades,
// torneio e período. Um botão que esquece um deles não falha: ele desenha, com capricho, o
// ranking do Brasil todo debaixo de uma tela que diz "Porto Alegre". Em C# isso é conferível
// por teste; montado à mão em quinze lugares do `.cshtml`, seriam quinze chances de esquecer
// um parâmetro e nenhuma de perceber.
public sealed record BotaoDeCompartilharRanking(
    RankingHubVM Hub,
    AbaDoRanking Aba,
    // A categoria escolhida, nas abas que têm dropdown ou pílula de categoria. Nula nas outras.
    string? Categoria = null)
{
    public const string Acao = "/Cartoes/RankingImagem";

    // ⚠️ O BOTÃO SÓ EXISTE ONDE A ARTE EXISTE, e quem responde é a MESMA régua que desenha (o
    // `RankingParaCard.Montar`, que devolve nulo pra aba sem ninguém naquele recorte). São 17
    // botões na tela, e cada um mora ao lado de uma tabela com a sua própria guarda de vazio —
    // amarrar cada um ao `if` do vizinho seria 17 condições escritas à mão, e a primeira a
    // discordar entregaria um botão que só sabe abrir 404. Botão que não faz nada é pior que
    // botão que não está lá.
    //
    // Custa pouco: a lista já veio inteira do `HubDoRanking`, e isto é um `Take(10)` em memória.
    public bool TemArte => RankingParaCard.Montar(Hub, Aba, Categoria) != null;

    // ⚠️ `cidade` REPETIDO, e não uma lista separada por vírgula: é o formato que o filtro
    // regional já usa na URL da própria página (`?cidade=A&cidade=B`), e é o que o binder do
    // ASP.NET lê como `string[]`. Uma segunda convenção aqui faria o card de duas cidades sair
    // com o ranking de nenhuma.
    public string Url
    {
        get
        {
            var qs = new List<string> { "aba=" + Aba };

            if (!string.IsNullOrWhiteSpace(Categoria))
                qs.Add("categoria=" + Uri.EscapeDataString(Categoria));

            if (!string.IsNullOrWhiteSpace(Hub.Estado))
                qs.Add("estado=" + Uri.EscapeDataString(Hub.Estado));

            foreach (var cidade in Hub.Cidades.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
                qs.Add("cidade=" + Uri.EscapeDataString(cidade));

            if (Hub.TorneioSelecionadoId is int torneioId)
                qs.Add("torneioId=" + torneioId);

            // "sempre" é o padrão do servidor — mandá-lo escrito só deixaria a URL mais longa.
            if (!string.IsNullOrEmpty(Hub.Periodo) && Hub.Periodo != "sempre")
                qs.Add("periodo=" + Uri.EscapeDataString(Hub.Periodo));

            return Acao + "?" + string.Join("&", qs);
        }
    }

    // O nome com que o PNG chega na galeria de quem baixa. Leva a aba porque a pessoa vai
    // compartilhar mais de um: "ranking.png (1)" não diz qual é qual.
    public string Arquivo => $"ranking-{Aba.ToString().ToLowerInvariant()}.png";

    // O título do menu nativo de compartilhamento (o `navigator.share`).
    public string Titulo =>
        "Ranking Padelizou" + (string.IsNullOrWhiteSpace(Categoria) ? "" : $" — {Categoria}");
}
