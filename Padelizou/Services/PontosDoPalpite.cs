namespace Padelizou.Services;

// Como se mede um palpiteiro no Palpitrômetro: **quantidade de acertos** (decisão do Felipe,
// 12/08/2026). Um acerto, um ponto.
//
// Classe pura, como PontosDoDesafio e Padelimetro: recebe números, devolve números, não
// conhece banco. Quem lê o banco é o PalpiteService.
//
// ── POR QUE O TOTAL, E NÃO A MÉDIA ─────────────────────────────────────────────────────
//
// A regra veio de um exemplo do Felipe: **quem acertou 9 de 11 tem que ficar na frente de
// quem acertou 8 de 8**. Por aproveitamento seria o contrário (81,8% contra 100%), e é isso
// que o total corrige — palpitar mais e continuar acertando é mérito, não diluição.
//
// Duas versões anteriores caíram no caminho, e vale saber por quê:
//
//   · ponderar pelo tamanho da zebra (cravar o azarão pagava mais) — caiu por LEGIBILIDADE:
//     acertou é acertou, e tabela que se confere de cabeça vale mais que tabela com legenda;
//   · aproveitamento puro — caiu por este exemplo, e levou junto o piso mínimo de palpites
//     que ele exigia. Com o total, o problema do piso **não existe**: quem acertou 1 de 1 tem
//     1 ponto e fica no fim da lista sozinho, sem ninguém precisar barrá-lo.
//
// ⚠️ Consequência aceita: quem palpita em MAIS jogos leva vantagem. É de propósito — é o
// mesmo espírito da presença que pontua nos Desafios, e recompensa quem acompanha o torneio.
//
// ⚠️ ESTES NÚMEROS NÃO SÃO PADELÍMETRO e nunca podem virar. O Padelímetro decide em que
// categoria a pessoa se INSCREVE — ele mede quem JOGA. Aqui se mede quem ASSISTE, e acertar
// palpite não põe ninguém uma categoria acima. Trilha própria, como o desafio e o Americano.
public static class PontosDoPalpite
{
    // Aproveitamento em porcentagem. NÃO é o que ordena o ranking — entra na tela ao lado do
    // total, porque "9 de 11" e "9 de 40" contam histórias diferentes sobre o mesmo 9.
    //
    // Sem palpite resolvido devolve NULO, não zero: "0%" mentiria dizendo que a pessoa errou
    // tudo, quando ela só não palpitou ainda.
    public static double? Aproveitamento(int acertos, int palpitesResolvidos) =>
        palpitesResolvidos <= 0
            ? null
            : Math.Round(acertos * 100.0 / palpitesResolvidos, 1);

    // ── Quem não conta ─────────────────────────────────────────────────────────────────
    //
    // Quem está DENTRO da partida não pontua nela. Não é desconfiança: é que os quatro em
    // quadra são os únicos que podem mudar o resultado do próprio palpite, e um ranking em que
    // dá pra decidir se você acertou não é ranking. Vale nos dois sentidos — inclusive apostar
    // contra si mesmo e entregar o jogo.
    //
    // Continua PODENDO votar, e o voto continua contando na barra: palpitar em si mesmo é
    // metade da graça, e sumir com o botão na hora do seu jogo pareceria defeito. O que ele
    // não faz é entrar na conta.
    //
    // ⚠️ Em categoria de TIMES não exclui ninguém, e isso é de propósito. Numa linha de time o
    // Jogador1 é o ORGANIZADOR que cadastrou, não quem entra em quadra (ver Dupla.NomeTime) —
    // excluir por ali tiraria o acerto de quem nem joga e deixaria passar quem joga.
    public static bool JogaAPartida(int jogadorId, IEnumerable<int?> jogadoresEmQuadra) =>
        jogadoresEmQuadra.Any(id => id == jogadorId);
}
