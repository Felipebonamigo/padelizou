namespace Padel.Core.Perfil;

/// <summary>
/// O que acontece fora de uma <see cref="Partida"/> e vale conquista: a carreira e o online informam. Só dados, sem
/// tipo do Godot nem da Steam — quem chama é a camada de cima (ver docs/CONQUISTAS.md).
/// </summary>
public abstract record EventoDeFora;

/// <summary>A dupla do jogador foi campeã de uma etapa da carreira.</summary>
public sealed record EtapaVencida : EventoDeFora;

/// <summary>O circuito da carreira acabou e a dupla do jogador ficou nesta posição do ranking (1 = primeiro).</summary>
public sealed record CircuitoEncerrado : EventoDeFora
{
    public CircuitoEncerrado(int posicao)
    {
        if (posicao < 1) throw new ArgumentOutOfRangeException(nameof(posicao), posicao, "Posição no ranking começa em 1.");
        Posicao = posicao;
    }

    public int Posicao { get; }
}

/// <summary>
/// O jogador venceu uma partida online até o fim. Quem informa é a camada online — no cliente não existe Partida (só a
/// visão da rede), então o coletor não roda lá. Entra quem era humano quando a partida começou (o Comecou do host:
/// <c>ClienteDaPartida.Humanos</c>) e a vaga do jogador (<c>ClienteDaPartida.Indice</c>): é daqui que sai se havia rival
/// humano, e não de quem chama. O host inicia a sala com quem estiver nela e vaga vazia é IA, então o cliente parceiro do
/// host pode ter jogado contra duas IAs — aí é vitória (PRIMEIRA_VITORIA), mas não VITORIA_ONLINE.
/// </summary>
public sealed record VitoriaOnline : EventoDeFora
{
    public VitoriaOnline(IReadOnlyList<bool> humanos, int indiceDoJogador)
    {
        ArgumentNullException.ThrowIfNull(humanos);
        if (humanos.Count != 4) throw new ArgumentException($"Humanos tem as 4 vagas (time*2 + índice), não {humanos.Count}.", nameof(humanos));
        if (indiceDoJogador is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(indiceDoJogador), indiceDoJogador, "A vaga vai de 0 a 3 (ClienteDaPartida.Indice é -1 antes do BemVindo).");
        if (!humanos[indiceDoJogador]) throw new ArgumentException($"A vaga {indiceDoJogador} era da IA: quem venceu é o humano da vaga.", nameof(humanos));
        int rival = 1 - indiceDoJogador / 2;
        AlgumRivalHumano = humanos[rival * 2] || humanos[rival * 2 + 1];
    }

    /// <summary>Ao menos um dos dois rivais era humano quando a partida começou (quem cai no meio e vira IA continua contando).</summary>
    public bool AlgumRivalHumano { get; }
}
