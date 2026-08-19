namespace Padelizou.Services;

// QUAIS PLACARES FECHAM UM JOGO NESTE FORMATO — a lista que o palpitrômetro oferece.
//
// 🎯 Existe pra que palpitar placar seja ESCOLHER, e não digitar. Dois campos numéricos no
// celular são dois toques, um teclado por cima da tela e a porta aberta pra "6 x 9" — placar
// que nenhum jogo termina. Uma fileira de fichas (6x0 · 6x1 · … · 7x5) é um toque só, e placar
// impossível deixa de existir por construção, não por validação.
//
// ⚠️ QUEM MANDA NO FORMATO CONTINUA SENDO `FormatoDaPartida` — este arquivo não sabe quantos
// games vale uma fase, ele PERGUNTA. Uma segunda tabela de formato aqui seria a volta do
// `limiteGames: 9` cravado no JavaScript, que sobreviveu tanto tempo justamente por ser uma
// cópia da regra num canto onde ninguém procura.
public static class PlacaresPossiveis
{
    // Um final possível, sempre na ordem VENCEDOR × PERDEDOR — a moeda (games ou sets) é dita
    // pelo formato, não pela ficha.
    public sealed record Placar(int Vencedor, int Perdedor);

    // Em que moeda se palpita este jogo.
    //
    // ⚠️ 2+ sets → o palpite é em SETS, e não em games. Num jogo de mais de um set,
    // `Partida.GamesDupla1` guarda os games do SET EM ANDAMENTO (é assim que a Mesa marca):
    // pedir "quantos games?" ali seria perguntar o placar de um set que a pessoa não sabe qual
    // é, e conferir depois compararia o palpite do jogo com o placar de um pedaço dele.
    public static bool EmSets(FormatoDaPartida.Formato formato) => formato.Sets > 1;

    // A lista inteira, na ordem em que a tela mostra: do mais folgado ao mais apertado.
    public static List<Placar> Do(FormatoDaPartida.Formato formato)
    {
        if (EmSets(formato))
            // Quem vence faz os sets do formato; o outro fica com qualquer coisa abaixo disso.
            // Melhor de 3 (Sets = 2) dá 2x0 e 2x1 — as duas únicas maneiras de o jogo acabar.
            return Enumerable.Range(0, formato.Sets)
                .Select(perdedor => new Placar(formato.Sets, perdedor))
                .ToList();

        if (formato.EhSoma)
        {
            // Soma: joga-se o total e vence quem fizer mais. O placar é qualquer divisão do
            // bolo em que alguém fica na frente — 8 games dão 8x0, 7x1, 6x2 e 5x3.
            //
            // ⚠️ 4x4 fica de fora, e não por esquecimento: o sistema recusa finalizar partida
            // empatada ("no padel alguém tem que fechar o jogo"), então palpitar empate seria
            // palpitar um final que o próprio sistema não deixa gravar.
            var daSoma = new List<Placar>();
            for (int perdedor = 0; formato.Games - perdedor > perdedor; perdedor++)
                daSoma.Add(new Placar(formato.Games - perdedor, perdedor));
            return daSoma;
        }

        // "Até": quem chega no número vence.
        var lista = new List<Placar>();
        for (int perdedor = 0; perdedor < formato.Games; perdedor++)
            lista.Add(new Placar(formato.Games, perdedor));

        // O DESEMPATE do padel: jogo até um número PAR que empata na penúltima vai um game
        // além (até 6 empatado em 5x5 termina 7x5) — é a mesma paridade de
        // `FormatoDaPartida.TetoDeGames`, e a razão dela está escrita lá: limite ímpar já
        // embute o desempate no próprio número (o 9º game do jogo até 9 É o tie-break).
        if (formato.Games > 1 && formato.Games % 2 == 0)
            lista.Add(new Placar(formato.Games + 1, formato.Games - 1));

        return lista;
    }

    // Este placar, na orientação do JOGO (lado 1 = Dupla1), fecha uma partida deste formato?
    //
    // ⚠️ Empate nunca passa, em nenhum formato: partida empatada não finaliza, e um palpite que
    // aponta um final impossível não teria como acertar nem errar.
    public static bool Aceita(FormatoDaPartida.Formato formato, int lado1, int lado2)
    {
        if (lado1 == lado2) return false;

        int vencedor = Math.Max(lado1, lado2);
        int perdedor = Math.Min(lado1, lado2);

        return Do(formato).Any(p => p.Vencedor == vencedor && p.Perdedor == perdedor);
    }

    // ── LENDO O QUE FOI GRAVADO ───────────────────────────────────────────────────────────
    //
    // O palpite guarda games OU sets (ver Models/PalpitePartida), e quem lê precisa saber qual
    // dos dois veio. Esta é a ÚNICA tradução de "quatro colunas" pra "um placar e a moeda
    // dele" — espalhar um `?? ` por cada lugar que lê palpite é como nascem duas respostas
    // discordando sobre o mesmo dado.
    public static PlacarPalpitado Lido(int? games1, int? games2, int? sets1, int? sets2) =>
        sets1 != null && sets2 != null
            ? new PlacarPalpitado(sets1, sets2, EmSets: true)
            : new PlacarPalpitado(games1, games2, EmSets: false);

    // Qual LADO este placar aponta: 1, 2 — ou nulo no empate. É o que amarra o placar ao voto:
    // palpitar "a dupla 1 vence" e "4 x 6" ao mesmo tempo são duas respostas que se contradizem.
    public static int? LadoVencedor(int lado1, int lado2) =>
        lado1 == lado2 ? null : lado1 > lado2 ? 1 : 2;
}

// O placar que a pessoa palpitou, já traduzido: os dois lados e a moeda.
//
// ⚠️ `Existe` exige os DOIS lados: meio placar não dá pra conferir contra nada, e tratar
// "6 x nulo" como 6x0 seria inventar a metade que a pessoa não disse.
public sealed record PlacarPalpitado(int? Lado1, int? Lado2, bool EmSets)
{
    public bool Existe => Lado1 != null && Lado2 != null;
}
