namespace Padel.Core.Rodizio;

/// <summary>
/// Liga a <see cref="Partida"/> de verdade ao rodízio: o <see cref="Placar"/> final de uma partida de pontos corridos
/// (<see cref="OpcoesDaPartida.PontosCorridos"/> = <see cref="RegrasDoRodizio.PontosPorJogo"/>) vira o resultado do jogo
/// do humano (<see cref="TorneioDeRodizio.InformarResultado(int, int, int)"/>).
/// </summary>
/// <remarks>
/// A partida só conhece "casa" (time 0) e "visitante" (time 1); o rodízio, DuplaA e DuplaB. Quem montou a partida sabe em
/// que time pôs a DuplaA e diz (<c>timeDaDuplaA</c>, sem padrão de propósito: um padrão errado trocaria o placar das
/// duplas sem erro nenhum). O placar precisa ser de pontos corridos, do mesmo total do rodízio e de uma partida acabada;
/// a soma igual ao total o próprio rodízio confere de novo (<see cref="JogoDoRodizio.ProblemaNoPlacar"/>). Placar que não
/// serve é recusado com <see cref="ArgumentException"/> antes de gravar qualquer coisa.
/// </remarks>
public static class PartidaNoRodizio
{
    /// <summary>O resultado do jogo <paramref name="numeroDoJogo"/> da rodada atual, lido do placar final da partida.</summary>
    public static void InformarResultado(this TorneioDeRodizio rodizio, int numeroDoJogo, Placar placar, int timeDaDuplaA)
    {
        var (pontosA, pontosB) = PontosDasDuplas(rodizio, placar, timeDaDuplaA);
        rodizio.InformarResultado(numeroDoJogo, pontosA, pontosB);
    }

    /// <summary>O resultado do ÚNICO jogo pendente da rodada (o do humano; ver <see cref="TorneioDeRodizio.InformarResultado(int, int)"/>).</summary>
    public static void InformarResultado(this TorneioDeRodizio rodizio, Placar placar, int timeDaDuplaA)
    {
        var (pontosA, pontosB) = PontosDasDuplas(rodizio, placar, timeDaDuplaA);
        rodizio.InformarResultado(pontosA, pontosB);
    }

    private static (int PontosA, int PontosB) PontosDasDuplas(TorneioDeRodizio rodizio, Placar placar, int timeDaDuplaA)
    {
        ArgumentNullException.ThrowIfNull(rodizio);
        ArgumentNullException.ThrowIfNull(placar);
        if (timeDaDuplaA is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(timeDaDuplaA), timeDaDuplaA, "A DuplaA jogou no time 0 (casa) ou no 1 (visitante).");
        int doRodizio = rodizio.Regras.PontosPorJogo;
        if (placar.PontosCorridos is not int total)
            throw new ArgumentException(
                $"A partida foi de games e sets ({placar.Resumo()}); o rodízio só aceita partida de {doRodizio} pontos corridos.", nameof(placar));
        if (total != doRodizio)
            throw new ArgumentException($"A partida foi de {total} pontos corridos, e o rodízio joga {doRodizio}.", nameof(placar));
        if (!placar.Acabou)
            throw new ArgumentException($"A partida não acabou: {placar.Resumo()} somam {placar.Pontos[0] + placar.Pontos[1]} de {total}.", nameof(placar));
        return (placar.Pontos[timeDaDuplaA], placar.Pontos[1 - timeDaDuplaA]);
    }
}
