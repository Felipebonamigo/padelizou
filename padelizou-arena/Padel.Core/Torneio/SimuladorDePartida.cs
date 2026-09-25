namespace Padel.Core.Torneio;

/// <summary>
/// O resultado de uma partida entre duas IAs, SEM física: ponto a ponto, com a chance de cada
/// ponto saindo da força relativa, e o <see cref="Placar"/> de verdade contando. Reusar o Placar
/// é o que faz o placar ser coerente de graça — 6-0 a 6-4, 7-5, 7-6 com tie-break, ponto de
/// ouro, troca de saque — sem uma segunda cópia da regra do set aqui.
/// </summary>
/// <remarks>
/// Rodar a <see cref="Partida"/> com física pra cada jogo de IA custaria minutos por torneio;
/// ponto a ponto custa microssegundos e dá a mesma forma de placar. O que se perde é o estilo
/// (quem joga de rede, quem erra mais): aqui só a força conta.
/// </remarks>
public static class SimuladorDePartida
{
    /// <summary>
    /// Quanto a força pesa no ponto: com 10 de diferença a mais forte leva ~52,5 % dos pontos;
    /// com 50, ~62 %; com 100 (a escala inteira), ~73 %. Parece pouco e não é: o game, o set e a
    /// partida ampliam — 50 de diferença vence o set em ~97 % das vezes.
    /// </summary>
    public const float EscalaDaForca = 100f;

    /// <summary>
    /// Pontos a mais pra quem saca. No padel a vantagem do saque é pequena (saque por baixo,
    /// abaixo da cintura); 4 pontos percentuais dão ~58 % de games confirmados entre iguais
    /// (medido em 19 mil games com ponto de ouro, 25/09/2026).
    /// Número de calibração, não de regra: ajustar com estatística de transmissão.
    /// </summary>
    public const float VantagemDoSaque = 0.04f;

    // Só existe pra um defeito no Placar não virar laço infinito: um set tem ~60 pontos, e um
    // tie-break que passasse de alguns milhares seria impossível na prática.
    private const int TetoDePontos = 20_000;

    /// <summary>Chance de A vencer um ponto contra B, sem contar o saque. Simétrica: A + B = 1.</summary>
    public static float ChanceDoPonto(int forcaA, int forcaB) =>
        1f / (1f + MathF.Exp(-(forcaA - forcaB) / EscalaDaForca));

    /// <summary>
    /// Joga a partida inteira. Os sets vêm do ponto de vista de A (<c>Games[0]</c> é de A).
    /// Quem abre sacando é sorteado no mesmo <paramref name="aleatorio"/>: a mesma semente
    /// reproduz o mesmo placar, ponto por ponto.
    /// </summary>
    public static List<SetEncerrado> Simular(int forcaA, int forcaB, int setsParaVencer, Aleatorio aleatorio, bool pontoDeOuro = true)
    {
        ArgumentNullException.ThrowIfNull(aleatorio);
        if (setsParaVencer < 1) throw new ArgumentOutOfRangeException(nameof(setsParaVencer));

        var placar = new Placar(pontoDeOuro, setsParaVencer, timeQueSaca: aleatorio.Proximo() < 0.5f ? 0 : 1);
        float chanceDeA = ChanceDoPonto(forcaA, forcaB);
        for (int pontos = 0; !placar.Acabou; pontos++)
        {
            if (pontos >= TetoDePontos)
                throw new InvalidOperationException($"A partida simulada não fechou em {TetoDePontos} pontos ({placar.Resumo()}).");
            float chance = placar.Sacador.Time == 0 ? chanceDeA + VantagemDoSaque : chanceDeA - VantagemDoSaque;
            placar.PontoPara(aleatorio.Proximo() < Util.Limitar(chance, 0.02f, 0.98f) ? 0 : 1);
        }
        return PlacarDoJogo.Copiar(placar.SetsAnteriores);
    }
}
