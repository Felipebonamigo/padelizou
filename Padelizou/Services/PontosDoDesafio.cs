namespace Padelizou.Services;

// Quanto vale um desafio confirmado. Espec em DESAFIOS.md, seção 3 — mudou a regra, muda LÁ
// primeiro.
//
// Classe pura de propósito: recebe números, devolve números, não conhece banco. É o mesmo
// desenho do Padelímetro, e pelo mesmo motivo — cada conta precisa caber num teste sem subir
// meio sistema junto.
//
// ⚠️ ESTES PONTOS NÃO SÃO PADELÍMETRO, e não podem virar. O Padelímetro decide em que
// categoria a pessoa pode se INSCREVER, e um placar sem testemunha não pode mexer nisso: já
// vimos três amigos fabricarem ranking num Americano lançando os placares que quisessem. O
// desafio tem o mesmo furo e maior — bastam quatro pessoas e nenhum organizador. Por isso
// trilha própria, exatamente como o Americano virou a Trilha C.
//
// O que este arquivo LÊ do Padelímetro é só a expectativa (Padelimetro.Expectativa), que é
// matemática pura. Nada aqui escreve nível de ninguém.
public static class PontosDoDesafio
{
    // Jogar já vale. É o que faz o ranking premiar quem aparece, e não só quem ganha —
    // e é o que sobra pro terceiro confronto seguido contra a mesma dupla.
    public const int Presenca = 1;

    // A vitória vale 20 × (1 − chance que a dupla tinha), presa entre 5 e 20:
    //   • ganhar de quem é muito melhor  → 20
    //   • ganhar de quem é igual         → 10
    //   • ganhar de quem é muito pior    →  5
    //
    // O piso existe pra que ganhar de quem é mais fraco não vire zero: seria desestimular
    // justamente quem topa jogar com dupla nova. O teto é o próprio limite da conta —
    // `20 × (1 − expectativa)` não passa de 20 — e está escrito como constante pra ninguém
    // mexer na base achando que a faixa continua a mesma.
    public const int BaseDaVitoria = 20;
    public const int MinimoDaVitoria = 5;
    public const int MaximoDaVitoria = BaseDaVitoria;

    // Nível de quem ainda não tem Padelímetro. A dupla sem número nenhum entra com expectativa
    // 0,5 (jogo equilibrado) e a vitória vale os 10 do meio — é o valor honesto pra "não sei".
    public const double ExpectativaNeutra = 0.5;

    // Chance que a dupla do lado A tinha, pelos níveis dos quatro. Nulo em qualquer jogador =
    // não dá pra comparar, e o palpite neutro é melhor do que somar zero e transformar quem
    // não tem número em freguês.
    public static double Expectativa(int? a1, int? a2, int? b1, int? b2)
    {
        if (a1 == null || a2 == null || b1 == null || b2 == null) return ExpectativaNeutra;

        var nivelA = Padelimetro.NivelDaDupla(a1.Value, a2.Value);
        var nivelB = Padelimetro.NivelDaDupla(b1.Value, b2.Value);
        return Padelimetro.Expectativa(nivelA, nivelB);
    }

    public static int DaVitoria(double expectativaDoVencedor)
    {
        var bruto = (int)Math.Round(BaseDaVitoria * (1.0 - expectativaDoVencedor),
            MidpointRounding.AwayFromZero);
        return Math.Clamp(bruto, MinimoDaVitoria, MaximoDaVitoria);
    }

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
    public static (int Desafiante, int Desafiado) Do(
        int? ladoVencedor, double expectativaDoDesafiante, int confrontosAnterioresNoMes)
    {
        if (ladoVencedor is not (1 or 2)) return (Presenca, Presenca);

        var expectativaDoVencedor = ladoVencedor == 1
            ? expectativaDoDesafiante
            : 1.0 - expectativaDoDesafiante;

        var vitoria = ComDescontoDeRepeticao(DaVitoria(expectativaDoVencedor), confrontosAnterioresNoMes);

        return ladoVencedor == 1
            ? (Presenca + vitoria, Presenca)
            : (Presenca, Presenca + vitoria);
    }

    // Quem não apareceu não pontua, e o adversário também não: o jogo não aconteceu, e não há
    // nada pra medir. É a mesma régua do W.O. no Padelímetro.
    public static (int Desafiante, int Desafiado) SemComparecimento() => (0, 0);
}
