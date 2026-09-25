namespace Padel.Core;

public enum TipoDeEventoDoPlacar { Ponto, Game, Set, Partida, Encerrada }
public enum LadoDoSaque { Direita, Esquerda }

/// <summary>
/// O que um ponto fez no placar. <see cref="Time"/>: no Ponto, Game e Set, quem ganhou aquilo; na Partida e na Encerrada,
/// o <see cref="Placar.Vencedor"/> — ou −1 no empate dos pontos corridos (<see cref="Placar.Empate"/>). No padel de games
/// e sets o time da Partida é sempre também quem ganhou o último ponto; nos pontos corridos não (13-10 → 13-11).
/// </summary>
public readonly record struct EventoDoPlacar(TipoDeEventoDoPlacar Tipo, int Time);
public readonly record struct Sacador(int Time, int Jogador);
public sealed record SetEncerrado(int[] Games, int[]? TieBreak);

/// <summary>
/// Placar de padel, puro: pontos 0/15/30/40, ponto de ouro ou vantagem, games, tie-break em 6-6,
/// sets, partida em melhor de N. Também diz quem saca e de que lado. Times: 0 casa, 1 visitante;
/// jogadores dentro do time: 0 e 1. Os times alternam a cada game; dentro do time os jogadores
/// alternam a cada vez que o time volta a sacar. No tie-break quem abriu saca 1 ponto, depois 2 a 2.
/// </summary>
/// <remarks>
/// <para><b>Pontos corridos</b> (<see cref="DePontosCorridos"/>; o jogo do Americano e do Mexicano, <c>Padel.Core.Rodizio</c>):
/// cada ponto vale 1 em <see cref="Pontos"/>, sem games, sets, tie-break nem ponto de ouro, e a partida acaba quando a SOMA
/// chega a <see cref="PontosCorridos"/> — 24 pode acabar 13-11 ou 12-12. Como o fim se lê:</para>
/// <list type="bullet">
/// <item><see cref="Acabou"/> é <c>true</c> nos dois casos — quem pergunta "acabou?" (a <see cref="Partida"/>, o perfil, a tela)
///   não muda.</item>
/// <item><see cref="Vencedor"/> é quem fez mais pontos, ou <c>null</c> no empate: quem lê o Vencedor de uma partida acabada
///   precisa tratar o <c>null</c> (no padel de games e sets ele nunca é nulo com a partida acabada, e segue assim).</item>
/// <item><see cref="Empate"/> é o estado explícito do empate; fora dos pontos corridos é sempre <c>false</c>.</item>
/// <item><see cref="Pontos"/> guarda o placar final (não zera, como zera no fim de um game) — é ele que vai pro rodízio.</item>
/// </list>
/// <para>O saque roda em turnos de <see cref="PontosPorTurnoDeSaque"/> pontos, na ordem do padel: um jogador do time que abre,
/// um do outro time, o outro jogador do primeiro time, o outro do segundo, e de novo. É a regra de clube mais comum no Americano
/// — cada jogador saca 4 seguidos, as duplas alternando (padelfast.com, hostatourney.com, liveforpadel.com: resumo da busca de
/// 25/09/2026, com as páginas bloqueadas pela rede daqui; os guias dizem que ela varia por clube, de 2 a 4, e que 4 é o comum).
/// As duplas sacam o mesmo tanto quando o total é múltiplo de 8 (16, 24, 32); os jogadores, só em múltiplo de 16 — em 24, quem
/// abre cada dupla saca 8 e o parceiro 4 (o placar do rodízio é da dupla, então a conta da dupla é a que pesa). Em total que não
/// é múltiplo de 8 (20, 21) quem abre saca mais: é o custo da regra de 4, e o motivo de os clubes usarem 16, 24 e 32. A caixa
/// alterna a cada ponto (<see cref="LadoDoSaque"/>), e cada turno abre pela direita, como um game.</para>
/// </remarks>
public sealed class Placar
{
    private static readonly string[] NomesDosPontos = ["0", "15", "30", "40"];

    /// <summary>Nos pontos corridos, quantos pontos seguidos cada jogador saca antes de o saque passar à outra dupla.</summary>
    public const int PontosPorTurnoDeSaque = 4;

    /// <summary>Sempre <c>false</c> nos pontos corridos: lá não existe ponto de ouro.</summary>
    public bool PontoDeOuro { get; }
    /// <summary>Nos pontos corridos não se aplica (fica 1, e nenhum set acontece).</summary>
    public int SetsParaVencer { get; }
    /// <summary>O total de pontos corridos da partida (a soma que a encerra), ou <c>null</c> no padel de games e sets.</summary>
    public int? PontosCorridos { get; private init; }
    public int[] Pontos { get; private set; } = [0, 0];
    public int[] Games { get; private set; } = [0, 0];
    public int[] Sets { get; } = [0, 0];
    public List<SetEncerrado> SetsAnteriores { get; } = [];
    public bool EmTieBreak { get; private set; }
    /// <summary>Quem venceu a partida; <c>null</c> enquanto ela corre — e no empate dos pontos corridos (<see cref="Empate"/>).</summary>
    public int? Vencedor { get; private set; }
    /// <summary>A partida de pontos corridos acabou empatada (12-12 em 24). <see cref="Vencedor"/> fica <c>null</c> e <see cref="Acabou"/>, <c>true</c>.</summary>
    public bool Empate { get; private set; }
    /// <summary>Time que abriu o game (no tie-break: quem abriu o tie-break; nos pontos corridos: o dono do turno de saque).</summary>
    public int SacadorDoGame { get; private set; }
    private readonly int[] _proximoJogador = [0, 0];
    private readonly int[] _jogadorSacador = [0, 0];
    private int _turnoDeSaque;

    public Placar(bool pontoDeOuro = true, int setsParaVencer = 1, int timeQueSaca = 0)
    {
        PontoDeOuro = pontoDeOuro;
        SetsParaVencer = setsParaVencer;
        SacadorDoGame = timeQueSaca;
        AbrirTurnoDeSaque(timeQueSaca);
    }

    /// <summary>
    /// Placar de pontos corridos: a partida acaba quando a soma dos pontos chega a <paramref name="total"/> (ver as
    /// observações da classe). <paramref name="timeQueSaca"/> abre o primeiro turno de saque.
    /// </summary>
    public static Placar DePontosCorridos(int total, int timeQueSaca = 0)
    {
        if (total < 1) throw new ArgumentOutOfRangeException(nameof(total), total, "A partida de pontos corridos vale pelo menos 1 ponto.");
        if (timeQueSaca is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(timeQueSaca), timeQueSaca, "O time é 0 (casa) ou 1 (visitante).");
        return new Placar(pontoDeOuro: false, setsParaVencer: 1, timeQueSaca) { PontosCorridos = total };
    }

    private void AbrirTurnoDeSaque(int time)
    {
        _turnoDeSaque = time;
        _jogadorSacador[time] = _proximoJogador[time];
        _proximoJogador[time] ^= 1;
    }

    /// <summary>A partida acabou: tem <see cref="Vencedor"/>, ou empatou nos pontos corridos (<see cref="Empate"/>).</summary>
    public bool Acabou => Vencedor is not null || Empate;
    public Sacador Sacador => new(_turnoDeSaque, _jogadorSacador[_turnoDeSaque]);
    /// <summary>Pontos do game em andamento; nos pontos corridos, da partida inteira (não há game).</summary>
    public int TotalDePontosNoGame => Pontos[0] + Pontos[1];
    /// <summary>Direita com soma de pontos par (vale no tie-break e nos pontos corridos também).</summary>
    public LadoDoSaque LadoDoSaque => TotalDePontosNoGame % 2 == 0 ? LadoDoSaque.Direita : LadoDoSaque.Esquerda;
    public bool EmPontoDecisivo => !EmTieBreak && PontoDeOuro && Pontos[0] == 3 && Pontos[1] == 3;

    /// <summary>"0/15/30/40/AD"; no tie-break e nos pontos corridos, o número de pontos.</summary>
    public string TextoDosPontos(int time)
    {
        if (EmTieBreak || PontosCorridos is not null) return Pontos[time].ToString();
        int a = Pontos[time], b = Pontos[1 - time];
        if (a >= 3 && b >= 3) return a > b ? "AD" : "40";
        return NomesDosPontos[Math.Min(a, 3)];
    }

    public EventoDoPlacar PontoPara(int time)
    {
        if (Acabou) return new EventoDoPlacar(TipoDeEventoDoPlacar.Encerrada, Vencedor ?? -1);
        if (time is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(time));
        Pontos[time] += 1;
        if (PontosCorridos is int total) return PontoCorrido(time, total);
        int a = Pontos[time], b = Pontos[1 - time];
        if (EmTieBreak)
        {
            if (a >= 7 && a - b >= 2) return FecharGame(time);
            RodarSaqueNoTieBreak();
            return new EventoDoPlacar(TipoDeEventoDoPlacar.Ponto, time);
        }
        bool fechou = PontoDeOuro ? (a >= 4 && a > b) : (a >= 4 && a - b >= 2);
        return fechou ? FecharGame(time) : new EventoDoPlacar(TipoDeEventoDoPlacar.Ponto, time);
    }

    /// <summary>
    /// Nos pontos corridos: com a soma no total, a partida acaba (vence quem tem mais; igual é empate); senão, a cada
    /// <see cref="PontosPorTurnoDeSaque"/> pontos o saque passa ao outro time, como na virada de um game.
    /// </summary>
    private EventoDoPlacar PontoCorrido(int time, int total)
    {
        int soma = TotalDePontosNoGame;
        if (soma >= total)
        {
            if (Pontos[0] == Pontos[1]) Empate = true;
            else Vencedor = Pontos[0] > Pontos[1] ? 0 : 1;
            return new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, Vencedor ?? -1);
        }
        if (soma % PontosPorTurnoDeSaque == 0)
        {
            SacadorDoGame = 1 - SacadorDoGame;
            AbrirTurnoDeSaque(SacadorDoGame);
        }
        return new EventoDoPlacar(TipoDeEventoDoPlacar.Ponto, time);
    }

    private void RodarSaqueNoTieBreak()
    {
        int n = TotalDePontosNoGame;
        int turno = ((n + 1) / 2) % 2 == 0 ? SacadorDoGame : 1 - SacadorDoGame;
        if (turno != _turnoDeSaque) AbrirTurnoDeSaque(turno);
    }

    private EventoDoPlacar FecharGame(int time)
    {
        bool eraTieBreak = EmTieBreak;
        int[]? pontosDoTieBreak = eraTieBreak ? [Pontos[0], Pontos[1]] : null;
        Games[time] += 1;
        Pontos = [0, 0];
        EmTieBreak = false;
        int g = Games[time], h = Games[1 - time];
        EventoDoPlacar evento;
        if (eraTieBreak || (g >= 6 && g - h >= 2))
        {
            evento = FecharSet(time, pontosDoTieBreak);
        }
        else
        {
            if (g == 6 && h == 6) EmTieBreak = true;
            evento = new EventoDoPlacar(TipoDeEventoDoPlacar.Game, time);
        }
        // Próximo game: o outro time saca. Depois do tie-break, saca quem NÃO abriu o tie-break.
        SacadorDoGame = 1 - SacadorDoGame;
        if (!Acabou) AbrirTurnoDeSaque(SacadorDoGame);
        return evento;
    }

    private EventoDoPlacar FecharSet(int time, int[]? pontosDoTieBreak)
    {
        Sets[time] += 1;
        SetsAnteriores.Add(new SetEncerrado([Games[0], Games[1]], pontosDoTieBreak));
        Games = [0, 0];
        if (Sets[time] >= SetsParaVencer)
        {
            Vencedor = time;
            return new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, time);
        }
        return new EventoDoPlacar(TipoDeEventoDoPlacar.Set, time);
    }

    /// <summary>"6-4 3-6 7-6(5)" do ponto de vista do time 0, com o set em andamento no fim. Nos pontos corridos, "13-11".</summary>
    public string Resumo()
    {
        if (PontosCorridos is not null) return $"{Pontos[0]}-{Pontos[1]}";
        var partes = SetsAnteriores.Select(s =>
        {
            string texto = $"{s.Games[0]}-{s.Games[1]}";
            return s.TieBreak is null ? texto : $"{texto}({Math.Min(s.TieBreak[0], s.TieBreak[1])})";
        }).ToList();
        if (!Acabou) partes.Add($"{Games[0]}-{Games[1]}");
        return string.Join(' ', partes);
    }
}
