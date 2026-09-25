using Padel.Core.Torneio;

namespace Padel.Core.Rodizio;

/// <summary>
/// O jogo de pontos corridos entre duas duplas de IA, sem física: ponto a ponto, cada um com a
/// mesma chance, até a soma chegar ao total. A força da dupla é a MÉDIA dos dois jogadores.
/// </summary>
/// <remarks>
/// A chance do ponto é a mesma curva do <see cref="SimuladorDePartida"/> (logística com
/// <see cref="SimuladorDePartida.EscalaDaForca"/>): +10 na força ≈ 52,5 % dos pontos. A função de lá
/// (<see cref="SimuladorDePartida.ChanceDoPonto"/>) não serve direto porque recebe <c>int</c> e a
/// média de uma dupla pode ter meio ponto (85 e 70 dão 77,5) — passar a SOMA dobraria o efeito da
/// força. O saque fica de fora de propósito: nos pontos corridos ele gira a cada poucos pontos e as
/// duas duplas sacam o mesmo tanto num jogo de total par, então a vantagem se anula na média; o
/// que se perde é só um pouco de variância.
/// </remarks>
public static class JogoDePontosCorridos
{
    /// <summary>Chance de A vencer um ponto contra B. Simétrica: A + B = 1.</summary>
    public static float ChanceDoPonto(float forcaA, float forcaB) =>
        1f / (1f + MathF.Exp(-(forcaA - forcaB) / SimuladorDePartida.EscalaDaForca));

    /// <summary>
    /// Joga os <paramref name="pontosPorJogo"/> pontos e devolve quantos foram de cada dupla (a soma é
    /// sempre o total). Mesmo <paramref name="aleatorio"/>, mesmo placar.
    /// </summary>
    public static (int PontosA, int PontosB) Simular(float forcaA, float forcaB, int pontosPorJogo, Aleatorio aleatorio)
    {
        ArgumentNullException.ThrowIfNull(aleatorio);
        if (pontosPorJogo < 1) throw new ArgumentOutOfRangeException(nameof(pontosPorJogo), pontosPorJogo, "O jogo vale pelo menos 1 ponto.");
        // Os mesmos limites do SimuladorDePartida: ninguém ganha ou perde ponto com certeza.
        float chance = Util.Limitar(ChanceDoPonto(forcaA, forcaB), 0.02f, 0.98f);
        int deA = 0;
        for (int ponto = 0; ponto < pontosPorJogo; ponto++)
            if (aleatorio.Proximo() < chance) deA++;
        return (deA, pontosPorJogo - deA);
    }
}
