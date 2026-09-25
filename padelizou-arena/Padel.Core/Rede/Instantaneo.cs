namespace Padel.Core.Rede;

/// <summary>A bola inteira — o bastante pra continuar a simulação dela com o mesmo código do host.</summary>
public readonly record struct EstadoDaBola(float X, float Y, float Z, float Vx, float Vy, float Vz, float Wx, float Wy, float Wz, bool EmJogo, bool Rolando = false, bool Parada = false)
{
    public static EstadoDaBola De(Bola b) => new(b.X, b.Y, b.Z, b.Vx, b.Vy, b.Vz, b.Wx, b.Wy, b.Wz, b.EmJogo, b.Rolando, b.Parada);

    public void Aplicar(Bola b)
    {
        b.X = X; b.Y = Y; b.Z = Z;
        b.Vx = Vx; b.Vy = Vy; b.Vz = Vz;
        b.Wx = Wx; b.Wy = Wy; b.Wz = Wz;
        b.EmJogo = EmJogo; b.Rolando = Rolando; b.Parada = Parada;
    }

    public float DistanciaAte(EstadoDaBola outra)
    {
        float dx = X - outra.X, dy = Y - outra.Y, dz = Z - outra.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

/// <summary>Um jogador como o renderizador precisa: onde está, pra onde vai, em que ponto do balanço.</summary>
public readonly record struct EstadoDoJogador(float X, float Y, float Vx, float Vy, float Balanco, float TempoNoBalanco, bool BalancoDeLob, bool Humano, float Cooldown = 0)
{
    public static EstadoDoJogador De(Jogador j) => new(j.X, j.Y, j.Vx, j.Vy, j.Balanco, j.TempoNoBalanco, j.BalancoDeLob, j.Humano, j.Cooldown);

    public bool Balancando => Balanco > 0;
    public float Rapidez => MathF.Sqrt(Vx * Vx + Vy * Vy);
}

/// <summary>
/// O placar como dado. O cliente não tem o <see cref="Padel.Core.Placar"/> do host, então o texto dos pontos e o
/// resumo são refeitos aqui — com um teste que compara, ponto a ponto, com o Placar do Core
/// (RedeSerializacaoTests.O_placar_do_instantaneo_mostra_o_mesmo_texto_que_o_placar_do_core).
/// </summary>
public sealed class EstadoDoPlacar : IEquatable<EstadoDoPlacar>
{
    private static readonly string[] NomesDosPontos = ["0", "15", "30", "40"];

    public EstadoDoPlacar(IReadOnlyList<int> pontos, IReadOnlyList<int> games, IReadOnlyList<int> sets, IReadOnlyList<SetEncerrado> setsAnteriores,
        bool emTieBreak, int? vencedor, Sacador sacador, bool pontoDeOuro, int setsParaVencer)
    {
        Pontos = [pontos[0], pontos[1]];
        Games = [games[0], games[1]];
        Sets = [sets[0], sets[1]];
        SetsAnteriores = setsAnteriores.Select(s => new SetEncerrado([s.Games[0], s.Games[1]], s.TieBreak is null ? null : [s.TieBreak[0], s.TieBreak[1]])).ToArray();
        EmTieBreak = emTieBreak;
        Vencedor = vencedor;
        Sacador = sacador;
        PontoDeOuro = pontoDeOuro;
        SetsParaVencer = setsParaVencer;
    }

    public static readonly EstadoDoPlacar Inicial = new([0, 0], [0, 0], [0, 0], [], false, null, new Sacador(0, 0), true, 1);

    public static EstadoDoPlacar De(Placar p) =>
        new(p.Pontos, p.Games, p.Sets, p.SetsAnteriores, p.EmTieBreak, p.Vencedor, p.Sacador, p.PontoDeOuro, p.SetsParaVencer);

    public IReadOnlyList<int> Pontos { get; }
    public IReadOnlyList<int> Games { get; }
    public IReadOnlyList<int> Sets { get; }
    public IReadOnlyList<SetEncerrado> SetsAnteriores { get; }
    public bool EmTieBreak { get; }
    public int? Vencedor { get; }
    public Sacador Sacador { get; }
    public bool PontoDeOuro { get; }
    public int SetsParaVencer { get; }

    public bool Acabou => Vencedor is not null;
    public LadoDoSaque LadoDoSaque => (Pontos[0] + Pontos[1]) % 2 == 0 ? LadoDoSaque.Direita : LadoDoSaque.Esquerda;
    /// <summary>Mesma regra de <see cref="Placar.EmPontoDecisivo"/>: 40-40 com ponto de ouro, fora do tie-break.</summary>
    public bool EmPontoDecisivo => !EmTieBreak && PontoDeOuro && Pontos[0] == 3 && Pontos[1] == 3;

    /// <summary>Mesma regra de <see cref="Placar.TextoDosPontos"/>.</summary>
    public string TextoDosPontos(int time)
    {
        if (EmTieBreak) return Pontos[time].ToString();
        int a = Pontos[time], b = Pontos[1 - time];
        if (a >= 3 && b >= 3) return a > b ? "AD" : "40";
        return NomesDosPontos[Math.Min(a, 3)];
    }

    /// <summary>Mesma regra de <see cref="Placar.Resumo"/>: "6-4 3-6 7-6(5)" do ponto de vista do time 0.</summary>
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

    public bool Equals(EstadoDoPlacar? outro)
    {
        if (outro is null) return false;
        if (ReferenceEquals(this, outro)) return true;
        return Pontos.SequenceEqual(outro.Pontos) && Games.SequenceEqual(outro.Games) && Sets.SequenceEqual(outro.Sets)
            && EmTieBreak == outro.EmTieBreak && Vencedor == outro.Vencedor && Sacador == outro.Sacador
            && PontoDeOuro == outro.PontoDeOuro && SetsParaVencer == outro.SetsParaVencer
            && SetsAnteriores.Count == outro.SetsAnteriores.Count
            && SetsAnteriores.Zip(outro.SetsAnteriores).All(par => MesmoSet(par.First, par.Second));
    }

    private static bool MesmoSet(SetEncerrado a, SetEncerrado b) =>
        a.Games.SequenceEqual(b.Games) && (a.TieBreak is null ? b.TieBreak is null : b.TieBreak is not null && a.TieBreak.SequenceEqual(b.TieBreak));

    public override bool Equals(object? obj) => Equals(obj as EstadoDoPlacar);
    public override int GetHashCode() => HashCode.Combine(Pontos[0], Pontos[1], Games[0], Games[1], Sets[0], Sets[1], SetsAnteriores.Count, Vencedor);
    public override string ToString() => $"{Resumo()} {TextoDosPontos(0)}-{TextoDosPontos(1)} (saca {Sacador.Time}/{Sacador.Jogador})";
}

/// <summary>
/// Um evento da partida com número: o id cresce 1 a 1 no host e o cliente descarta o que já viu — é o que deixa
/// o host repetir o evento em vários instantâneos (pra sobreviver à perda) sem o som tocar duas vezes.
/// Jogador é o índice (time*2 + índice no time), ou -1.
/// </summary>
public readonly record struct EventoNumerado(uint Id, uint Tick, TipoDeEventoDaPartida Tipo, int Jogador = -1, int Time = -1, TipoDeGolpe? Golpe = null, Motivo? Motivo = null)
{
    public static EventoNumerado De(EventoDaPartida e, uint id, uint tick) =>
        new(id, tick, e.Tipo, e.Jogador is Jogador j ? j.Time * 2 + j.Indice : -1, e.Time, e.Golpe, e.Motivo);
}

/// <summary>
/// A bola logo depois de algo que a física não explica — golpe, saque, bola recolocada, ponto encerrado. Com isso
/// o cliente reconstrói a trajetória exata entre dois instantâneos simulando a partir do último marco.
/// </summary>
public readonly record struct Descontinuidade(uint Tick, EstadoDaBola Bola);

/// <summary>Até que número de sequência o host já aplicou as entradas do jogador remoto deste índice.</summary>
public readonly record struct Confirmacao(int Indice, uint Seq);

/// <summary>
/// O instantâneo (snapshot) que o host manda a 30 Hz: tudo o que o cliente precisa pra desenhar, tocar som e
/// reconciliar a predição. Classe simples e mutável — é um pacote, não um modelo.
/// </summary>
public sealed class Instantaneo
{
    public uint Tick { get; set; }
    public EstadoDaPartida Estado { get; set; }
    public float Temporizador { get; set; }
    public EstadoDaBola Bola { get; set; }
    public EstadoDoJogador[] Jogadores { get; set; } = new EstadoDoJogador[4];
    public EstadoDoPlacar Placar { get; set; } = EstadoDoPlacar.Inicial;
    public Mensagem? Mensagem { get; set; }
    public Caixa CaixaDoSaque { get; set; }
    public List<Confirmacao> Confirmacoes { get; set; } = [];
    public List<EventoNumerado> Eventos { get; set; } = [];
    public List<Descontinuidade> Descontinuidades { get; set; } = [];

    /// <summary>O estado da partida agora; confirmações, eventos e descontinuidades quem põe é o servidor.</summary>
    public static Instantaneo Capturar(Partida partida, uint tick)
    {
        var jogadores = new EstadoDoJogador[4];
        for (int i = 0; i < 4; i++) jogadores[i] = EstadoDoJogador.De(partida.Jogadores[i]);
        return new Instantaneo
        {
            Tick = tick,
            Estado = partida.Estado,
            Temporizador = partida.Temporizador,
            Bola = EstadoDaBola.De(partida.Bola),
            Jogadores = jogadores,
            Placar = EstadoDoPlacar.De(partida.Placar),
            Mensagem = partida.Mensagem,
            CaixaDoSaque = partida.CaixaDoSaque,
        };
    }

    /// <summary>A confirmação do jogador deste índice, se o host já processou alguma entrada dele.</summary>
    public uint? SeqConfirmada(int indice)
    {
        foreach (var c in Confirmacoes) if (c.Indice == indice) return c.Seq;
        return null;
    }
}
