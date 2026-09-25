using Padel.Core.Torneio;

namespace Padel.Core.Perfil;

/// <summary>
/// O que o jogador está fazendo, pro Rich Presence da Steam (a linha embaixo do nome na lista de amigos). Só dados; o
/// texto sai de <see cref="PresencaNaSteam.Texto"/>. Os estados validam o que recebem: estado impossível é defeito de
/// quem chamou, não texto estranho na lista de amigos.
/// </summary>
public abstract record EstadoDePresenca;

public sealed record NoMenu : EstadoDePresenca;

public sealed record Treinando : EstadoDePresenca;

/// <summary>Numa partida, no set <see cref="Set"/> (1 = o primeiro), com os games do jogador primeiro.</summary>
public sealed record JogandoUmSet : EstadoDePresenca
{
    public JogandoUmSet(int gamesDoJogador, int gamesDoRival, int Set = 1)
    {
        // 7 é o máximo de games num set (7-5, 7-6).
        if (gamesDoJogador is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(gamesDoJogador), gamesDoJogador, "Games de 0 a 7.");
        if (gamesDoRival is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(gamesDoRival), gamesDoRival, "Games de 0 a 7.");
        if (Set < 1) throw new ArgumentOutOfRangeException(nameof(Set), Set, "O primeiro set é o 1.");
        GamesDoJogador = gamesDoJogador;
        GamesDoRival = gamesDoRival;
        this.Set = Set;
    }

    public int GamesDoJogador { get; }
    public int GamesDoRival { get; }
    public int Set { get; }
}

/// <summary>Na carreira: etapa (1 = a primeira do circuito) e a fase em que a dupla do jogador está.</summary>
public sealed record NaCarreira : EstadoDePresenca
{
    public NaCarreira(int etapa, FaseAlcancada fase)
    {
        if (etapa < 1) throw new ArgumentOutOfRangeException(nameof(etapa), etapa, "A primeira etapa é a 1.");
        if (!Enum.IsDefined(fase)) throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase desconhecida.");
        Etapa = etapa;
        Fase = fase;
    }

    public int Etapa { get; }
    public FaseAlcancada Fase { get; }
}

/// <summary>Numa partida online, 1x1 ou 2x2.</summary>
public sealed record NoOnline : EstadoDePresenca
{
    public NoOnline(int jogadoresPorTime)
    {
        if (jogadoresPorTime is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(jogadoresPorTime), jogadoresPorTime, "Padel é 1x1 ou 2x2.");
        JogadoresPorTime = jogadoresPorTime;
    }

    public int JogadoresPorTime { get; }
}

/// <summary>
/// O texto curto do Rich Presence, em português, inglês ou espanhol. Pura: mesmo estado e língua, mesmo texto.
/// A camada Steam grava com SetRichPresence("status", ...) — e, pra aparecer na lista de amigos, o arquivo de
/// localização do Rich Presence no Steamworks precisa dos mesmos textos em tokens (ver docs/CONQUISTAS.md).
/// </summary>
public static class PresencaNaSteam
{
    public static string Texto(EstadoDePresenca estado, Idioma idioma)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (!Enum.IsDefined(idioma)) throw new ArgumentOutOfRangeException(nameof(idioma), idioma, "Idioma desconhecido.");
        return estado switch
        {
            NoMenu => new TextoLocalizado("No menu", "In the menu", "En el menú").Em(idioma),
            Treinando => new TextoLocalizado("Treinando", "Training", "Entrenando").Em(idioma),
            JogandoUmSet { Set: 1 } s => new TextoLocalizado(
                $"Jogando um set, {s.GamesDoJogador}-{s.GamesDoRival}",
                $"Playing a set, {s.GamesDoJogador}-{s.GamesDoRival}",
                $"Jugando un set, {s.GamesDoJogador}-{s.GamesDoRival}").Em(idioma),
            JogandoUmSet s => new TextoLocalizado(
                $"Jogando o set {s.Set}, {s.GamesDoJogador}-{s.GamesDoRival}",
                $"Playing set {s.Set}, {s.GamesDoJogador}-{s.GamesDoRival}",
                $"Jugando el set {s.Set}, {s.GamesDoJogador}-{s.GamesDoRival}").Em(idioma),
            NaCarreira c => new TextoLocalizado(
                $"Carreira: etapa {c.Etapa}, {Fase(c.Fase).Portugues}",
                $"Career: stage {c.Etapa}, {Fase(c.Fase).Ingles}",
                $"Carrera: etapa {c.Etapa}, {Fase(c.Fase).Espanhol}").Em(idioma),
            NoOnline o => new TextoLocalizado(
                $"Online {o.JogadoresPorTime}x{o.JogadoresPorTime}",
                $"Online {o.JogadoresPorTime}v{o.JogadoresPorTime}",
                $"En línea {o.JogadoresPorTime}x{o.JogadoresPorTime}").Em(idioma),
            _ => throw new ArgumentException($"Estado de presença desconhecido: {estado.GetType().Name}.", nameof(estado)),
        };
    }

    private static TextoLocalizado Fase(FaseAlcancada fase) => fase switch
    {
        FaseAlcancada.FaseDeGrupos => new("fase de grupos", "group stage", "fase de grupos"),
        FaseAlcancada.PrimeiraRodada => new("primeira rodada", "first round", "primera ronda"),
        FaseAlcancada.Oitavas => new("oitavas de final", "round of 16", "octavos de final"),
        FaseAlcancada.Quartas => new("quartas de final", "quarterfinal", "cuartos de final"),
        FaseAlcancada.Semifinal => new("semifinal", "semifinal", "semifinal"),
        FaseAlcancada.Final => new("final", "final", "final"),
        FaseAlcancada.Campeao => new("campeão", "champion", "campeón"),
        _ => throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase desconhecida."),
    };
}
