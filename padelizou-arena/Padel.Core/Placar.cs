namespace Padel.Core;

public enum TipoDeEventoDoPlacar { Ponto, Game, Set, Partida, Encerrada }
public enum LadoDoSaque { Direita, Esquerda }

public readonly record struct EventoDoPlacar(TipoDeEventoDoPlacar Tipo, int Time);
public readonly record struct Sacador(int Time, int Jogador);
public sealed record SetEncerrado(int[] Games, int[]? TieBreak);

/// <summary>
/// Placar de padel, puro: pontos 0/15/30/40, ponto de ouro ou vantagem, games, tie-break em 6-6,
/// sets, partida em melhor de N. Também diz quem saca e de que lado. Times: 0 casa, 1 visitante;
/// jogadores dentro do time: 0 e 1. Os times alternam a cada game; dentro do time os jogadores
/// alternam a cada vez que o time volta a sacar. No tie-break quem abriu saca 1 ponto, depois 2 a 2.
/// </summary>
public sealed class Placar
{
    private static readonly string[] NomesDosPontos = ["0", "15", "30", "40"];

    public bool PontoDeOuro { get; }
    public int SetsParaVencer { get; }
    public int[] Pontos { get; private set; } = [0, 0];
    public int[] Games { get; private set; } = [0, 0];
    public int[] Sets { get; } = [0, 0];
    public List<SetEncerrado> SetsAnteriores { get; } = [];
    public bool EmTieBreak { get; private set; }
    public int? Vencedor { get; private set; }
    /// <summary>Time que abriu o game (no tie-break: quem abriu o tie-break).</summary>
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

    private void AbrirTurnoDeSaque(int time)
    {
        _turnoDeSaque = time;
        _jogadorSacador[time] = _proximoJogador[time];
        _proximoJogador[time] ^= 1;
    }

    public bool Acabou => Vencedor is not null;
    public Sacador Sacador => new(_turnoDeSaque, _jogadorSacador[_turnoDeSaque]);
    public int TotalDePontosNoGame => Pontos[0] + Pontos[1];
    /// <summary>Direita com soma de pontos par (vale no tie-break também).</summary>
    public LadoDoSaque LadoDoSaque => TotalDePontosNoGame % 2 == 0 ? LadoDoSaque.Direita : LadoDoSaque.Esquerda;
    public bool EmPontoDecisivo => !EmTieBreak && PontoDeOuro && Pontos[0] == 3 && Pontos[1] == 3;

    public string TextoDosPontos(int time)
    {
        if (EmTieBreak) return Pontos[time].ToString();
        int a = Pontos[time], b = Pontos[1 - time];
        if (a >= 3 && b >= 3) return a > b ? "AD" : "40";
        return NomesDosPontos[Math.Min(a, 3)];
    }

    public EventoDoPlacar PontoPara(int time)
    {
        if (Acabou) return new EventoDoPlacar(TipoDeEventoDoPlacar.Encerrada, Vencedor!.Value);
        if (time is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(time));
        Pontos[time] += 1;
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

    /// <summary>"6-4 3-6 7-6(5)" do ponto de vista do time 0, com o set em andamento no fim.</summary>
    public string Resumo()
    {
        var partes = SetsAnteriores.Select(s =>
        {
            string texto = $"{s.Games[0]}-{s.Games[1]}";
            return s.TieBreak is null ? texto : $"{texto}({Math.Min(s.TieBreak[0], s.TieBreak[1])})";
        }).ToList();
        if (!Acabou) partes.Add($"{Games[0]}-{Games[1]}");
        return string.Join(' ', partes);
    }
}
