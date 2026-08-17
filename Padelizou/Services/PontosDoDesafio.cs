namespace Padelizou.Services;

// Quanto vale um desafio confirmado. Espec em DESAFIOS.md, seção 3 — mudou a regra, muda LÁ
// primeiro.
//
// Classe pura de propósito: recebe números, devolve números, não conhece banco. É o mesmo
// desenho do Padelímetro, e pelo mesmo motivo — cada conta precisa caber num teste sem subir
// meio sistema junto.
//
// ⚠️ ESTES PONTOS NÃO SÃO PADELÍMETRO, e não podem virar. O Padelímetro decide em que categoria
// a pessoa pode se INSCREVER, e um placar sem testemunha não pode mexer nisso: já vimos três
// amigos fabricarem ranking num Americano lançando os placares que quisessem. O desafio tem o
// mesmo furo e maior — bastam quatro pessoas e nenhum organizador. Por isso trilha própria,
// exatamente como o Americano virou a Trilha C.
//
// 🔌 E DESDE 17/08/2026 A TRILHA É MESMO SEPARADA: a expectativa (o fator Elo que fazia a
// vitória valer entre 5 e 20 conforme o nível do adversário) SAIU, por decisão do Felipe.
// Duas razões, e as duas apontam pro mesmo lugar:
//
//   • O Padelímetro nasce só de TORNEIO. Quem nunca jogou um não tem número, e caía no valor
//     neutro — ou seja, boa parte das duplas já pontuava pelo fixo, e as outras não. A mesma
//     vitória valia 9 pra uns e 10 pra outros por um motivo que a tela não mostrava.
//   • Ler o Padelímetro aqui reamarrava as duas trilhas que a espec tinha separado de
//     propósito. Agora o desafio não lê NEM escreve o nível de ninguém.
public static class PontosDoDesafio
{
    // Jogar já vale. É o que faz o ranking premiar quem aparece, e não só quem ganha —
    // e é o que sobra pro terceiro confronto seguido contra a mesma dupla.
    public const int Presenca = 1;

    // A vitória vale o mesmo pra todo mundo: 10.
    //
    // É o valor que já era o neutro quando a conta olhava o nível do adversário — então o
    // ranking não muda de escala, ele só para de tratar duas vitórias iguais de forma diferente.
    public const int Vitoria = 10;

    public static int DaVitoria() => Vitoria;

    // ── O anti-farm ────────────────────────────────────────────────────────────────────
    //
    // Quatro amigos jogando entre si toda terça subiriam para sempre. Contra a MESMA dupla, no
    // mesmo mês: o 1º confronto vale cheio, o 2º vale metade, do 3º em diante a vitória vale
    // zero — sobra a presença, que é o que de fato aconteceu (eles jogaram).
    //
    // A metade arredonda pra CIMA: 5 vira 3, não 2. O desconto existe pra tirar o incentivo de
    // farmar, não pra punir quem tem um rival de verdade e joga com ele todo mês.
    public static int ComDescontoDeRepeticao(int pontosDaVitoria, int confrontosAnterioresNoMes) =>
        confrontosAnterioresNoMes switch
        {
            <= 0 => pontosDaVitoria,
            1 => (int)Math.Ceiling(pontosDaVitoria / 2.0),
            _ => 0
        };

    // O total de cada lado num desafio confirmado.
    //
    // ladoVencedor: 1 = desafiante, 2 = desafiado (ver EstadoDoDesafio.LadoVencedor).
    // Empate não existe aqui — o placar não fecha sem vencedor —, mas se chegar um, os dois
    // levam só a presença em vez de o método inventar um campeão.
    public static (int Desafiante, int Desafiado) Do(int? ladoVencedor, int confrontosAnterioresNoMes)
    {
        if (ladoVencedor is not (1 or 2)) return (Presenca, Presenca);

        var vitoria = ComDescontoDeRepeticao(DaVitoria(), confrontosAnterioresNoMes);

        return ladoVencedor == 1
            ? (Presenca + vitoria, Presenca)
            : (Presenca, Presenca + vitoria);
    }

    // Quem não apareceu não pontua, e o adversário também não: o jogo não aconteceu, e não há
    // nada pra medir. É a mesma régua do W.O. no Padelímetro.
    public static (int Desafiante, int Desafiado) SemComparecimento() => (0, 0);
}
