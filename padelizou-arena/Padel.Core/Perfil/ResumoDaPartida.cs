namespace Padel.Core.Perfil;

/// <summary>
/// Em que modo a partida foi jogada. Entra de fora: quem monta a partida sabe; a <see cref="Partida"/> não sabe.
/// Treino só conta golpes e tempo (nada de partida, vitória ou conquista de partida): é onde se repete golpe à vontade.
/// </summary>
public enum ModoDaPartida { Local, Coop, Online, Carreira, Treino }

/// <summary>
/// Um set, do ponto de vista do time do perfil. Tie-break: os pontos dele (null se o set não foi ao tie-break).
/// Encerrado = false é o set em andamento de uma partida que ainda não acabou (ou foi abandonada).
/// </summary>
public sealed record SetDoResumo(int GamesDoTime, int GamesDoRival, int? TieBreakDoTime = null, int? TieBreakDoRival = null, bool Encerrado = true)
{
    public bool Vencido => Encerrado && GamesDoTime > GamesDoRival;
    public bool VencidoNoTieBreak => Vencido && TieBreakDoTime is not null;
    /// <summary>6-0 a favor. (0-6 é pneu levado, não conta.)</summary>
    public bool Pneu => Vencido && GamesDoTime == 6 && GamesDoRival == 0;
}

/// <summary>
/// O que uma partida deixou pro perfil: o que o <see cref="ColetorDaPartida"/> monta dos eventos e o que o
/// <see cref="AvaliadorDeConquistas"/> lê. Tudo do ponto de vista do(s) jogador(es) do perfil — "time" é o time dele(s).
/// Contagens são int; a duração é o <see cref="Partida.TempoDeJogo"/> (float, segundos de simulação).
/// <list type="bullet">
/// <item><b>Vencedor</b> (VencedoresPorTipo): ponto que o time do perfil ganhou tendo sido um jogador do perfil o
///   último a bater — o adversário não devolveu (dois quiques, saiu depois de quicar, voltou pelo vidro). Por tipo do último golpe.</item>
/// <item><b>Erro</b> (ErrosPorMotivo): ponto que o time perdeu tendo sido um jogador do perfil o último a bater, pelo
///   <see cref="Motivo"/> do árbitro (rede, não passou, vidro sem quicar, fora, dupla falta...).</item>
/// <item><b>GolpesNaRede</b>: golpes do perfil que tocaram a rede e morreram — os pontos perdidos na rede e também as
///   faltas de saque na rede (a primeira, que não vira ponto, e a dupla falta cuja segunda bola foi na rede).</item>
/// <item><b>SaidasPelaPorta</b>: vencedores do perfil em que a bola saiu pela porta (lateral, |y| de 0,45 a 1,25 m, abaixo de 2 m).</item>
/// <item><b>MaiorDesvantagemRevertida</b>: o maior atraso em games (games do rival menos os do time) que o time teve num set que venceu.</item>
/// </list>
/// </summary>
public sealed record ResumoDaPartida
{
    public ModoDaPartida Modo { get; init; }
    public Dificuldade Dificuldade { get; init; } = Dificuldade.Medio;
    /// <summary>
    /// Os dois rivais eram da IA quando a partida começou (<see cref="OpcoesDaPartida.Humanos"/>). Decide VITORIA_NO_DIFICIL
    /// (true) e VITORIA_ONLINE (false). No online nem sempre há gente do outro lado: o host inicia a sala com quem estiver
    /// nela e vaga vazia é IA (ServidorDaPartida.Iniciar). Rival humano que cai no meio e vira IA (Jogador.Humano = false)
    /// não muda isto: a partida começou contra gente.
    /// </summary>
    public bool RivalDaIA { get; init; } = true;
    /// <summary>Índices (time*2 + índice) dos jogadores do perfil: um, ou os dois do mesmo time no coop.</summary>
    public IReadOnlyList<int> IndicesDoPerfil { get; init; } = [0];
    public int TimeDoPerfil { get; init; }
    /// <summary>A partida chegou ao fim (false: resumo tirado no meio, ou partida abandonada).</summary>
    public bool Terminada { get; init; }
    /// <summary>Terminada e vencida pelo time do perfil.</summary>
    public bool Venceu { get; init; }
    public IReadOnlyList<SetDoResumo> Sets { get; init; } = [];
    public int PontosVencidos { get; init; }
    public int PontosPerdidos { get; init; }
    public float DuracaoEmSegundos { get; init; }
    public IReadOnlyDictionary<TipoDeGolpe, int> GolpesPorTipo { get; init; } = new Dictionary<TipoDeGolpe, int>();
    public IReadOnlyDictionary<LadoDoGolpe, int> GolpesPorLado { get; init; } = new Dictionary<LadoDoGolpe, int>();
    public IReadOnlyDictionary<TipoDeGolpe, int> VencedoresPorTipo { get; init; } = new Dictionary<TipoDeGolpe, int>();
    public IReadOnlyDictionary<Motivo, int> ErrosPorMotivo { get; init; } = new Dictionary<Motivo, int>();
    public int GolpesNaRede { get; init; }
    /// <summary>Golpes do ponto mais longo da partida (dos quatro jogadores, saque incluído) — o mesmo da <see cref="Estatisticas.MaiorRally"/>.</summary>
    public int MaiorRally { get; init; }
    /// <summary>Pontos jogados em 40-40 com ponto de ouro (fora do tie-break), e os que o time ganhou.</summary>
    public int PontosDeOuroDisputados { get; init; }
    public int PontosDeOuroVencidos { get; init; }
    public int MaiorDesvantagemRevertida { get; init; }
    public int SaidasPelaPorta { get; init; }

    public int SetsVencidos => Sets.Count(s => s.Vencido);
    public int SetsPerdidos => Sets.Count(s => s.Encerrado && !s.Vencido);
    public int SetsVencidosNoTieBreak => Sets.Count(s => s.VencidoNoTieBreak);
    public int TotalDeGolpes => GolpesPorTipo.Values.Sum();
}
