using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// QUAL LISTA DO RANKING VIRA ARTE — e como cada uma vira LINHA.
//
// 🗣️ Felipe, 14/09/2026: *"crie um botão para compartilhar o ranking"*. Escolheu arte PNG (e não
// link) com um botão por aba.
//
// ⚠️ AQUI NÃO SE CALCULA RANKING NENHUM, e essa é a régua pra ler o arquivo inteiro. A ordem, os
// pontos e o recorte regional já vêm prontos do `ObterRankingHubAsync` — o mesmo que desenha a
// tela. Uma segunda ordenação aqui publicaria uma arte dizendo que o time A é o primeiro
// enquanto a página diz que é o B, e é o tipo de divergência que só aparece depois de postada
// no grupo. O trabalho deste arquivo é ESCOLHER a lista e TRADUZIR em `1º Nome · valor`.
//
// ⚠️ E É UM SÓ CARD PRA QUATORZE LISTAS, de propósito: todas têm a mesma forma (posição, quem,
// um número). Um desenho por aba seria quatorze lugares pra a próxima mudança de marca passar —
// e treze deles ficariam pra trás.
public static class RankingParaCard
{
    // O TOPO, e não a tabela inteira (escolha do Felipe). É o que cabe legível na faixa de
    // tabela do 1080×1350: com dez linhas o corpo fica em ~32, e é aí que o nome com apelido
    // ainda se lê num story visto de relance.
    //
    // ⚠️ Acima disto a fonte encolheria até o chão do `TamanhoQueCabe` — que devolve o mínimo
    // MESMO quando ele não cabe, e aí o Skia pinta até a borda do canvas e some com o resto do
    // nome, sem erro e sem log. Foi assim que "Marcio Rafael Machado" virou "Marcio Rafae" no
    // pódio (14/09/2026). Quem vigia é o gate de margem do `CartaoDoRankingTests`.
    public const int MaximoDeLinhas = 10;

    public static ListaDoRanking? Montar(RankingHubVM hub, AbaDoRanking aba, string? categoria = null)
    {
        var (titulo, linhas) = Escolher(hub, aba, categoria);
        if (linhas.Count == 0) return null;

        return new ListaDoRanking(
            Titulo: titulo,
            // A pílula diz o recorte MAIS ESPECÍFICO que existe: a categoria quando a aba tem
            // uma, senão o lugar. Arte de ranking sem recorte escrito é arte que diz "sou o
            // 1º" sem dizer de quê — e a de Porto Alegre ficaria idêntica à do Brasil todo.
            Recorte: categoria is { Length: > 0 } ? CategoriaNaTela.Curto(categoria) : Lugar(hub),
            Legenda: Legenda(hub, categoria),
            Linhas: linhas.Take(MaximoDeLinhas).ToList());
    }

    // O lugar por extenso, do jeito que a própria tela do ranking o anuncia.
    public static string Lugar(RankingHubVM hub)
    {
        if (hub.Cidades.Count == 1) return hub.Cidades[0];

        // Várias cidades não cabem numa pílula, e listar três e cortar a quarta mentiria sobre
        // o recorte. A contagem é honesta e curta.
        if (hub.Cidades.Count > 1)
            return $"{hub.Cidades.Count} cidades" + (hub.Estado is { Length: > 0 } uf ? $" do {uf}" : "");

        return hub.Estado is { Length: > 0 } estado ? estado : "Brasil todo";
    }

    // A linha de baixo da arte: lugar + período + torneio, o recorte inteiro escrito. Ela
    // existe porque a pílula só cabe UMA coisa — e num card de troféus "Este mês" é exatamente
    // a diferença entre 2 títulos e 40.
    private static string Legenda(RankingHubVM hub, string? categoria)
    {
        var partes = new List<string>();

        // Quando a pílula levou a categoria, o lugar desceu pra cá — senão ele sumiria.
        if (categoria is { Length: > 0 }) partes.Add(Lugar(hub));

        if (hub.TorneioSelecionadoNome is { Length: > 0 } torneio) partes.Add(torneio);

        partes.Add(hub.Periodo switch
        {
            "mes" => "Este mês",
            "ano" => "Este ano",
            _ => "Todas as épocas",
        });

        return string.Join("  ·  ", partes);
    }

    // O de-para de cada aba: o título da arte e as linhas dela.
    //
    // ⚠️ O `switch` é a lista COMPLETA das tabelas que a tela mostra, e é assim que ele tem que
    // continuar. O `default` é `throw` DE PROPÓSITO: devolvendo lista vazia, uma aba nova sem
    // entrada aqui responderia 404 e o botão sumiria da tela sem ninguém saber por quê — falha
    // calada, que é a família de defeito que este repositório mais persegue. Quem transforma
    // esse esquecimento em suíte vermelha é o `Toda_aba_do_enum_sabe_virar_arte`.
    private static (string Titulo, List<LinhaDoCartaoDeRanking> Linhas) Escolher(
        RankingHubVM hub, AbaDoRanking aba, string? categoria) => aba switch
    {
        AbaDoRanking.PorCategoria => (
            "RANKING",
            DeJogadores(PorCategoria(hub.PorCategoria, categoria)?.Linhas, l => l.Jogador, l => $"{l.Pontos} pts")),

        AbaDoRanking.Padelimetro => (
            "PADELÍMETRO",
            DeJogadores(hub.Padelimetro, l => l.Jogador, l => $"{l.Pdz} PDZ")),

        AbaDoRanking.AmericanoIndividual => (
            "AMERICANO",
            DeJogadores(hub.AmericanoIndividual, l => l.Jogador, l => $"{l.Pontos} pts")),

        AbaDoRanking.AmericanoDuplas => (
            "AMERICANO EM DUPLAS",
            DeJogadores(hub.AmericanoDuplas, l => l.Jogador, l => $"{l.Pontos} pts")),

        AbaDoRanking.Trofeus => (
            "TROFÉUS",
            DeJogadores(PorCategoria(hub.TrofeusPorCategoria, categoria)?.Linhas, l => l.Jogador,
                l => Plural(l.Titulos, "título", "títulos"))),

        AbaDoRanking.TrofeusAmericanoIndividual => (
            "TROFÉUS DO AMERICANO",
            DeJogadores(hub.TrofeusAmericanoIndividual, l => l.Jogador,
                l => Plural(l.Vitorias, "título", "títulos"))),

        AbaDoRanking.TrofeusAmericanoDuplas => (
            "TROFÉUS DO AMERICANO EM DUPLAS",
            DeJogadores(hub.TrofeusAmericanoDuplas, l => l.Jogador,
                l => Plural(l.Vitorias, "título", "títulos"))),

        AbaDoRanking.VitoriasJogadores => (
            "VITÓRIAS",
            DeJogadores(
                categoria is { Length: > 0 }
                    ? hub.VitoriasJogadoresPorCategoria.FirstOrDefault(c => c.Categoria == categoria)?.Jogadores
                    : hub.VitoriasJogadores,
                l => l.Jogador, l => Plural(l.Vitorias, "vitória", "vitórias"))),

        AbaDoRanking.VitoriasDuplas => (
            "VITÓRIAS EM DUPLAS",
            DeDuplas(
                categoria is { Length: > 0 }
                    ? hub.VitoriasDuplasPorCategoria.FirstOrDefault(c => c.Categoria == categoria)?.Duplas
                    : hub.VitoriasDuplas,
                l => Plural(l.Vitorias, "vitória", "vitórias"))),

        AbaDoRanking.InvictosJogadores => (
            "INVENCIBILIDADE",
            DeJogadores(hub.InvictosJogadores, l => l.Jogador,
                l => Plural(l.Sequencia, "jogo invicto", "jogos invicto"))),

        AbaDoRanking.InvictasDuplas => (
            "DUPLAS INVICTAS",
            DeDuplas(hub.InvictasDuplas, l => Plural(l.SequenciaInvicta, "jogo", "jogos"))),

        AbaDoRanking.DesafiosDuplas => (
            "DESAFIOS",
            DeLinhas(hub.Desafios?.Duplas, l => l.Nome, l => $"{l.Pontos} pts")),

        AbaDoRanking.DesafiosJogadores => (
            "DESAFIOS",
            DeLinhas(hub.Desafios?.Jogadores, l => l.Nome, l => $"{l.Pontos} pts")),

        AbaDoRanking.Palpiteiros => (
            "PALPITEIROS",
            DeLinhas(hub.Palpiteiros, l => l.Nome, l => $"{l.Pontos} pts")),

        AbaDoRanking.Times => (
            "TIMES",
            DeLinhas(hub.Times, l => l.Time, l => $"{l.Pontos} pts")),

        AbaDoRanking.Torneio => (
            hub.TorneioSelecionadoNome?.ToUpperInvariant() ?? "TORNEIO",
            DeJogadores(hub.RankingTorneio, l => l.Jogador, l => $"{l.Pontos} pts")),

        _ => throw new ArgumentOutOfRangeException(nameof(aba), aba,
            "Aba do ranking sem tradução pra arte — acrescente o caso aqui, senão o botão some sem dizer por quê."),
    };

    // "Todas" quando ninguém escolheu: a aba "Por categoria" abre na primeira que tem gente, e
    // a arte tem que sair da MESMA que está na tela.
    private static RankingCategoriaVM? PorCategoria(List<RankingCategoriaVM> categorias, string? categoria)
        => categoria is { Length: > 0 }
            ? categorias.FirstOrDefault(c => c.Categoria == categoria)
            : categorias.FirstOrDefault();

    private static List<LinhaDoCartaoDeRanking> DeJogadores<T>(
        IEnumerable<T>? linhas, Func<T, Jogador> quem, Func<T, string> valor)
        => DeLinhas(linhas, l => quem(l).ComoChamar, valor);

    // ⚠️ `NomeDaDupla.De` e não uma emenda com "&" aqui: é a régua única de como uma dupla se
    // escreve nas artes, e uma segunda cópia divergiria no separador — o mesmo torneio sairia
    // escrito de dois jeitos em dois cards da mesma página.
    private static List<LinhaDoCartaoDeRanking> DeDuplas(
        IEnumerable<DuplaContagemVM>? linhas, Func<DuplaContagemVM, string> valor)
        => DeLinhas(linhas, l => NomeDaDupla.De(null, l.Jogador1, l.Jogador2), valor);

    // A posição sai da ORDEM da lista, e não de um campo: é o mesmo número que a tela imprime,
    // que também conta as linhas na ordem em que elas chegam.
    private static List<LinhaDoCartaoDeRanking> DeLinhas<T>(
        IEnumerable<T>? linhas, Func<T, string> nome, Func<T, string> valor)
        => (linhas ?? Enumerable.Empty<T>())
            .Take(MaximoDeLinhas)
            .Select((l, i) => new LinhaDoCartaoDeRanking(i + 1, nome(l), valor(l)))
            .ToList();

    private static string Plural(int quantos, string singular, string plural)
        => $"{quantos} {(quantos == 1 ? singular : plural)}";
}

// Uma linha da arte: a posição, quem, e o número que a aba mede.
public sealed record LinhaDoCartaoDeRanking(int Posicao, string Nome, string Valor);

// A arte inteira, pronta pro Skia. Sem nada de EF aqui de propósito: é o que permite o teste
// montar o pior caso de nome na mão, sem banco.
public sealed record ListaDoRanking(
    string Titulo,
    string Recorte,
    string Legenda,
    IReadOnlyList<LinhaDoCartaoDeRanking> Linhas);

// As tabelas que a tela do ranking mostra, uma a uma.
//
// ⚠️ São mais do que as oito abas porque quatro delas têm SUB-ABAS, e cada sub-aba é uma tabela
// diferente medindo outra coisa. "Vitórias" com um valor só publicaria a tabela de jogadores
// debaixo do botão que está ao lado da tabela de duplas.
public enum AbaDoRanking
{
    PorCategoria,
    Padelimetro,
    AmericanoIndividual,
    AmericanoDuplas,
    Trofeus,
    TrofeusAmericanoIndividual,
    TrofeusAmericanoDuplas,
    VitoriasJogadores,
    VitoriasDuplas,
    InvictosJogadores,
    InvictasDuplas,
    DesafiosDuplas,
    DesafiosJogadores,
    Palpiteiros,
    Times,
    Torneio,
}
