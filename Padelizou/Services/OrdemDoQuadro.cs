namespace Padelizou.Services;

// Em que ORDEM os jogos de cada rodada devem ser desenhados pra que o chaveamento pareça um
// chaveamento — cada par de jogos vizinhos desembocando no jogo seguinte.
//
// O problema: os jogos nascem numerados 1..N na ordem de criação, e o pareamento do
// mata-mata é PRIMEIRO x ÚLTIMO (ChaveamentoMataMata.ParearVencedores cruza o jogo j com o
// jogo N+1-j). Desenhados na ordem de criação, os jogos 1 e 2 aparecem colados e vão pra
// lados opostos da chave — o quadro fica certo nos dados e mentiroso no desenho.
//
// A ordem certa sai de trás pra frente. A final tem um jogo; cada jogo da rodada anterior
// que a alimenta é (j, N+1-j). Então, dada a ordem da rodada SEGUINTE, a ordem desta é cada
// jogo dela seguido do seu par espelhado:
//
//   Final     (1 jogo)   → [1]
//   Semifinal (2 jogos)  → [1, 2]
//   Quartas   (4 jogos)  → [1, 4, 2, 3]
//   Oitavas   (8 jogos)  → [1, 8, 4, 5, 2, 7, 3, 6]
//
// Lendo Oitavas nessa ordem, os dois primeiros (1 e 8) desembocam na Quartas 1, os dois
// seguintes (4 e 5) na Quartas 4 — e a Quartas está desenhada como [1, 4, ...], bem no meio
// dos seus dois alimentadores. É o que faz a chave fechar visualmente até a final.
public static class OrdemDoQuadro
{
    // Números dos jogos (base 1) na ordem em que devem ser desenhados.
    // `jogosNaRodada` não precisa ser potência de 2: a primeira rodada de uma chave com bye
    // tem menos jogos que o quadro, e aí a ordem simplesmente devolve o que existe.
    public static List<int> De(int jogosNaRodada)
    {
        if (jogosNaRodada <= 1) return jogosNaRodada == 1 ? new List<int> { 1 } : new List<int>();

        // A rodada seguinte tem metade dos jogos (arredondando pra cima quando a chave é
        // torta por causa de bye).
        var proxima = De((jogosNaRodada + 1) / 2);

        var ordem = new List<int>(jogosNaRodada);
        foreach (var jogo in proxima)
        {
            if (jogo <= jogosNaRodada && !ordem.Contains(jogo)) ordem.Add(jogo);

            int espelhado = jogosNaRodada + 1 - jogo;
            if (espelhado >= 1 && espelhado <= jogosNaRodada && !ordem.Contains(espelhado)) ordem.Add(espelhado);
        }

        // Rede de segurança: qualquer jogo que a conta não tenha alcançado (chave torta)
        // entra no fim, em vez de sumir do desenho.
        for (int jogo = 1; jogo <= jogosNaRodada; jogo++)
            if (!ordem.Contains(jogo)) ordem.Add(jogo);

        return ordem;
    }

    // Reordena a lista de jogos de uma rodada (que vem na ordem de criação) pro desenho.
    public static List<T> Ordenar<T>(IReadOnlyList<T> jogosNaOrdemDeCriacao) =>
        De(jogosNaOrdemDeCriacao.Count)
            .Select(numero => jogosNaOrdemDeCriacao[numero - 1])
            .ToList();
}
