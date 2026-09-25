namespace Padel.Core;

public enum EstadoDaPartida { Saque, Rally, FimDoPonto, Fim }
public enum TipoDeEventoDaPartida { SaquePreparado, Golpe, Quique, Parede, Rede, CruzouRede, Saiu, Ponto, Game, Set, Partida, Falta, Let, Fim }

public sealed record EventoDaPartida(TipoDeEventoDaPartida Tipo, Jogador? Jogador = null, int Time = -1, Motivo? Motivo = null, TipoDeGolpe? Golpe = null);
public sealed record Mensagem(string Texto, bool Destaque = false, bool Suave = false);
public sealed record GolpeDado(Jogador Jogador, TipoDeGolpe Tipo, float Em);

public sealed record OpcoesDaPartida
{
    public Dificuldade Dificuldade { get; init; } = Dificuldade.Medio;
    public bool PontoDeOuro { get; init; } = true;
    public int SetsParaVencer { get; init; } = 1;
    /// <summary>Por jogador (time*2 + índice): quem é humano. Padrão: só o da casa, metade direita.</summary>
    public bool[] Humanos { get; init; } = [true, false, false, false];
    public uint? Semente { get; init; }

    public static readonly bool[] NinguemHumano = [false, false, false, false];
}

public sealed class Estatisticas
{
    public int Pontos, Golpes, MaiorRally, Faltas, Lets;
    public Dictionary<Motivo, int> Motivos { get; } = new();
}

/// <summary>
/// A partida: junta placar, árbitro, bola, jogadores e IA numa máquina de estados
/// (saque → rally → fim do ponto → saque… → fim). Não desenha nem lê teclado: recebe a entrada dos
/// humanos por parâmetro e avisa por evento. Roda igual no Godot, num servidor sem engine e no teste.
/// </summary>
public sealed class Partida
{
    public const float EsperaDoSaqueHumano = 8f;   // segundos até o saque sair sozinho
    public const float EsperaDoSaqueDaIA = 1.1f;

    public static readonly IReadOnlyDictionary<Motivo, string> Motivos = new Dictionary<Motivo, string>
    {
        [Motivo.DoisQuiques] = "dois quiques",
        [Motivo.Rede] = "na rede",
        [Motivo.NaoPassou] = "não passou",
        [Motivo.ParedeSemQuicar] = "no vidro sem quicar",
        [Motivo.ParedePropria] = "na própria parede",
        [Motivo.Fora] = "pra fora da quadra",
        [Motivo.VoltouPeloVidro] = "voltou pelo vidro",
        [Motivo.DuplaFalta] = "dupla falta",
        [Motivo.ForaDaCaixa] = "saque fora da caixa",
        [Motivo.BolaMorta] = "bola morta",
    };

    public OpcoesDaPartida Opcoes { get; }
    public Aleatorio Aleatorio { get; }
    public Placar Placar { get; }
    public Arbitro Arbitro { get; } = new();
    public Bola Bola { get; } = new();
    public Jogador[] Jogadores { get; }
    public IA[] IAs { get; }
    public EstadoDaPartida Estado { get; private set; }
    public float Temporizador { get; private set; }
    public Mensagem? Mensagem { get; private set; }
    public GolpeDado? UltimoGolpe { get; private set; }
    public float TempoDeJogo { get; private set; }
    public int RallyAtual { get; private set; }
    public Estatisticas Estatisticas { get; } = new();
    public Caixa CaixaDoSaque { get; private set; }
    public event Action<EventoDaPartida>? Evento;

    private float _rolandoHa;
    private readonly Entrada[] _entradas = new Entrada[4];

    public Partida(OpcoesDaPartida? opcoes = null)
    {
        Opcoes = opcoes ?? new OpcoesDaPartida();
        if (Opcoes.Humanos.Length != 4) throw new ArgumentException("Humanos precisa ter 4 posições", nameof(opcoes));
        uint semente = Opcoes.Semente ?? (uint)Random.Shared.Next();
        Aleatorio = new Aleatorio(semente);
        Placar = new Placar(Opcoes.PontoDeOuro, Opcoes.SetsParaVencer, timeQueSaca: 0);
        var rival = Perfis.Por(Opcoes.Dificuldade);
        bool[] h = Opcoes.Humanos;
        Jogadores =
        [
            new Jogador(0, 0, "Você", h[0], h[0] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(0, 1, "Parceiro", h[1], h[1] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(1, 0, "Rival 1", h[2], h[2] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
            new Jogador(1, 1, "Rival 2", h[3], h[3] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
        ];
        IAs = [new IA(0, Perfis.Parceiro, Aleatorio), new IA(1, rival, Aleatorio)];
        IniciarPonto();
    }

    public Jogador[] JogadoresDoTime(int time) => time == 0 ? [Jogadores[0], Jogadores[1]] : [Jogadores[2], Jogadores[3]];
    public Jogador Sacador => Jogadores[Placar.Sacador.Time * 2 + Placar.Sacador.Jogador];
    public bool Acabou => Estado == EstadoDaPartida.Fim;

    private void Emitir(EventoDaPartida evento) => Evento?.Invoke(evento);

    public void IniciarPonto()
    {
        var sacador = Sacador;
        int lado = sacador.Lado;
        bool direita = Placar.LadoDoSaque == LadoDoSaque.Direita;
        int sinalDoSacador = direita ? lado : -lado;   // metade em que o sacador está
        int timeReceptor = 1 - sacador.Time;
        var (receptor, parceiroDoReceptor) = QuemRecebe(timeReceptor, direita);
        var parceiroDoSacador = JogadoresDoTime(sacador.Time).First(j => j != sacador);

        sacador.X = sinalDoSacador * 2.6f; sacador.Y = lado * 8.3f;
        parceiroDoSacador.X = -sinalDoSacador * 2.3f; parceiroDoSacador.Y = lado * 3.6f;
        receptor.X = -sinalDoSacador * 2.8f; receptor.Y = -lado * 8.0f;
        parceiroDoReceptor.X = sinalDoSacador * 2.3f; parceiroDoReceptor.Y = -lado * 4.2f;
        foreach (var j in Jogadores) j.Cooldown = 0;

        Bola.Reiniciar();
        Bola.Posicionar(sacador.X, sacador.Y, 0.8f);
        CaixaDoSaque = Quadra.CaixaDeSaque(-lado, direita);
        RallyAtual = 0;
        _rolandoHa = 0;
        UltimoGolpe = null;
        Estado = EstadoDaPartida.Saque;
        Temporizador = sacador.Humano ? EsperaDoSaqueHumano : EsperaDoSaqueDaIA;
        bool segundo = Arbitro.Faltas > 0;
        Mensagem = sacador.Humano
            ? new Mensagem(segundo ? "Segundo saque — aperte pra sacar" : "Seu saque — aperte pra sacar", Suave: true)
            : null;
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.SaquePreparado, sacador, sacador.Time));
    }

    /// <summary>Quem recebe é o jogador da metade diagonal ao sacador; o parceiro dele fica na rede.</summary>
    private (Jogador, Jogador) QuemRecebe(int timeReceptor, bool direita)
    {
        var dupla = JogadoresDoTime(timeReceptor);
        var receptor = direita ? dupla[0] : dupla[1];   // índice 0 = metade direita do time = diagonal do saque à direita
        return (receptor, receptor == dupla[0] ? dupla[1] : dupla[0]);
    }

    public void Sacar(Entrada entrada)
    {
        var sacador = Sacador;
        var caixa = CaixaDoSaque;
        float alvoX, alvoY;
        if (sacador.Humano)
        {
            alvoX = Util.Limitar(caixa.CentroX + entrada.Dx * sacador.Lado * 1.8f + Aleatorio.Gaussiana() * 0.35f, caixa.XMin + 0.3f, caixa.XMax - 0.3f);
            alvoY = caixa.CentroY - sacador.Lado * 0.5f;
        }
        else
        {
            float ruido = IAs[sacador.Time].Perfil.ErroDeMira * 0.55f;
            alvoX = caixa.CentroX + Aleatorio.Gaussiana() * ruido;
            alvoY = caixa.CentroY + Aleatorio.Gaussiana() * ruido;
        }
        Bola.Lancar(Golpes.Calcular(Bola.X, Bola.Y, Bola.Z, alvoX, alvoY, tempoDeVoo: 1.0f));
        Arbitro.IniciarSaque(sacador.Time, caixa);
        sacador.Cooldown = 0.5f;
        sacador.Golpes += 1;
        UltimoGolpe = new GolpeDado(sacador, TipoDeGolpe.Saque, TempoDeJogo);
        Estado = EstadoDaPartida.Rally;
        Mensagem = null;
        RallyAtual = 1;
        Estatisticas.Golpes += 1;
        foreach (var ia in IAs) ia.AoSacar(this, sacador.Time);
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Golpe, sacador, sacador.Time, Golpe: TipoDeGolpe.Saque));
    }

    /// <summary>Avança a simulação. entradas: uma por jogador (time*2 + índice); faltando, vale Entrada.Vazia.</summary>
    public void Avancar(float dt, ReadOnlySpan<Entrada> entradas = default)
    {
        if (!(dt > 0)) return;
        for (int i = 0; i < 4; i++) _entradas[i] = i < entradas.Length ? entradas[i] : Entrada.Vazia;
        TempoDeJogo += dt;
        foreach (var j in Jogadores) j.AvancarTempo(dt);
        switch (Estado)
        {
            case EstadoDaPartida.Saque:
            {
                Temporizador -= dt;
                var sacador = Sacador;
                var entrada = EntradaDe(sacador);
                if ((sacador.Humano && entrada.AcaoPressionada) || Temporizador <= 0) Sacar(entrada);
                break;
            }
            case EstadoDaPartida.Rally:
                Rally(dt);
                break;
            case EstadoDaPartida.FimDoPonto:
                Temporizador -= dt;
                if (Temporizador <= 0)
                {
                    if (Placar.Acabou) { Estado = EstadoDaPartida.Fim; Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Fim, Time: Placar.Vencedor!.Value)); }
                    else IniciarPonto();
                }
                break;
            default:
                break;
        }
    }

    private Entrada EntradaDe(Jogador j) => _entradas[j.Time * 2 + j.Indice];

    private void Rally(float dt)
    {
        foreach (var j in Jogadores)
        {
            if (!j.Humano) continue;
            var e = EntradaDe(j);
            j.Mover(e.Dx * j.Lado, e.Dy * j.Lado, dt);   // do referencial do jogador pro mundo
        }
        foreach (var ia in IAs) ia.Reagir(dt, this);

        var bola = Bola;
        if (bola.EmJogo && !bola.Rolando)
        {
            foreach (var j in Jogadores)
            {
                if (j.Cooldown > 0 || !Arbitro.PodeGolpear(j.Time) || !j.Alcanca(bola)) continue;
                Golpear(j);
                break;
            }
        }

        var eventos = new List<EventoDaBola>(4);
        bola.Avancar(dt, eventos);
        bool rebateu = false;
        foreach (var evento in eventos)
        {
            Emitir(new EventoDaPartida(TraduzirEvento(evento.Tipo)));
            var decisao = Arbitro.Processar(evento);
            if (decisao is Decisao d) { Decidir(d); return; }
            if (evento.Tipo is TipoDeEventoDaBola.Quique or TipoDeEventoDaBola.Parede) rebateu = true;
        }
        if (rebateu) foreach (var ia in IAs) ia.AoRebater(this);

        if (bola.Rolando)
        {
            _rolandoHa += dt;
            if (_rolandoHa > 0.6f)
            {
                // Bola rolando é bola que já quicou e não vai quicar de novo: conta como o quique seguinte.
                var decisao = Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, bola.X, bola.Y, 0, Quadra.LadoDe(bola.Y)))
                    ?? Decisao.Ponto(Arbitro.Receptor ?? 0, Motivo.BolaMorta);
                Decidir(decisao);
            }
        }
        else
        {
            _rolandoHa = 0;
        }
        if (Estado == EstadoDaPartida.Rally && !bola.EmJogo)
        {
            Decidir(Decisao.Ponto(Arbitro.Receptor ?? 0, Motivo.Fora));
        }
    }

    private static TipoDeEventoDaPartida TraduzirEvento(TipoDeEventoDaBola tipo) => tipo switch
    {
        TipoDeEventoDaBola.Quique => TipoDeEventoDaPartida.Quique,
        TipoDeEventoDaBola.Parede => TipoDeEventoDaPartida.Parede,
        TipoDeEventoDaBola.Rede => TipoDeEventoDaPartida.Rede,
        TipoDeEventoDaBola.CruzouRede => TipoDeEventoDaPartida.CruzouRede,
        _ => TipoDeEventoDaPartida.Saiu,
    };

    private void Golpear(Jogador jogador)
    {
        var bola = Bola;
        var golpe = jogador.Humano ? GolpeDoHumano(jogador, EntradaDe(jogador)) : IAs[jogador.Time].EscolherGolpe(jogador, bola, this);
        bola.Lancar(Golpes.Calcular(bola.X, bola.Y, bola.Z, golpe.AlvoX, golpe.AlvoY, golpe.TempoDeVoo, ignorarRede: golpe.IgnorarRede));
        jogador.Cooldown = 0.4f;
        jogador.Golpes += 1;
        Arbitro.RegistrarGolpe(jogador.Time);
        UltimoGolpe = new GolpeDado(jogador, golpe.Tipo, TempoDeJogo);
        RallyAtual += 1;
        Estatisticas.Golpes += 1;
        foreach (var ia in IAs) ia.AoGolpear(this, golpe, jogador);
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Golpe, jogador, jogador.Time, Golpe: golpe.Tipo));
    }

    /// <summary>
    /// Mira do humano (no referencial dele): esquerda/direita escolhem o canto; pra frente (rumo à rede)
    /// encurta e acelera; pra trás joga fundo e seguro; a ação segurada vira lob; bola alta é smash.
    /// </summary>
    private Golpe GolpeDoHumano(Jogador jogador, Entrada entrada)
    {
        var bola = Bola;
        int lado = jogador.Lado, ladoDoAlvo = -lado;
        float sigma = 0.3f + 0.7f * jogador.DificuldadeDoGolpe(bola);
        float Ruido() => Aleatorio.Gaussiana() * sigma;
        float x = entrada.Dx < -0.3f ? -3.3f * lado : entrada.Dx > 0.3f ? 3.3f * lado : jogador.X * 0.4f;
        x = Util.Limitar(x + Ruido(), -4.5f, 4.5f);
        if (entrada.AcaoSegurada) return new Golpe(x, ladoDoAlvo * 8.2f, 1.7f, TipoDeGolpe.Lob);
        if (bola.Z > 1.7f) return new Golpe(x, ladoDoAlvo * 4.2f, 0.42f, TipoDeGolpe.Smash);
        if (entrada.Dy < -0.3f) return new Golpe(x, ladoDoAlvo * (4.5f + Ruido()), 0.6f, TipoDeGolpe.Ataque);
        if (entrada.Dy > 0.3f) return new Golpe(x, ladoDoAlvo * (7.8f + Ruido()), 1.05f, TipoDeGolpe.Defesa);
        return new Golpe(x, ladoDoAlvo * (6.6f + Ruido()), 0.8f, TipoDeGolpe.Normal);
    }

    private void Decidir(Decisao decisao)
    {
        if (decisao.Tipo == TipoDeDecisao.Ponto) { EncerrarPonto(decisao.Para, decisao.Motivo); return; }
        Bola.EmJogo = false;
        Estado = EstadoDaPartida.FimDoPonto;
        Temporizador = 1.3f;
        if (decisao.Tipo == TipoDeDecisao.Falta)
        {
            Estatisticas.Faltas += 1;
            Mensagem = new Mensagem($"Falta — {Motivos[decisao.Motivo]}. Segundo saque");
            Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Falta, Motivo: decisao.Motivo));
        }
        else
        {
            Estatisticas.Lets += 1;
            Mensagem = new Mensagem("Let — a bola tocou a rede. Repete o saque");
            Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Let));
        }
    }

    private void EncerrarPonto(int para, Motivo motivo)
    {
        var evento = Placar.PontoPara(para);
        Estatisticas.Pontos += 1;
        Estatisticas.Motivos[motivo] = Estatisticas.Motivos.GetValueOrDefault(motivo) + 1;
        Estatisticas.MaiorRally = Math.Max(Estatisticas.MaiorRally, RallyAtual);
        Bola.EmJogo = false;
        Arbitro.NovoPonto();
        Mensagem = new Mensagem(TextoDoPonto(para, motivo, evento), Destaque: evento.Tipo != TipoDeEventoDoPlacar.Ponto);
        Estado = EstadoDaPartida.FimDoPonto;
        Temporizador = evento.Tipo == TipoDeEventoDoPlacar.Ponto ? 1.5f : 2.4f;
        var tipo = evento.Tipo switch
        {
            TipoDeEventoDoPlacar.Game => TipoDeEventoDaPartida.Game,
            TipoDeEventoDoPlacar.Set => TipoDeEventoDaPartida.Set,
            TipoDeEventoDoPlacar.Partida => TipoDeEventoDaPartida.Partida,
            _ => TipoDeEventoDaPartida.Ponto,
        };
        Emitir(new EventoDaPartida(tipo, Time: para, Motivo: motivo));
    }

    private string TextoDoPonto(int para, Motivo motivo, EventoDoPlacar evento)
    {
        string descricao = Motivos[motivo];
        bool casa = para == 0;
        return evento.Tipo switch
        {
            TipoDeEventoDoPlacar.Game => $"{(casa ? "Game da casa" : "Game dos rivais")} — {descricao}",
            TipoDeEventoDoPlacar.Set => $"{(casa ? "Set da casa!" : "Set dos rivais.")} {Placar.Resumo()}",
            TipoDeEventoDoPlacar.Partida => $"{(casa ? "A casa venceu!" : "Os rivais venceram.")} {Placar.Resumo()}",
            _ => $"{(casa ? "Ponto da casa" : "Ponto dos rivais")} — {descricao}",
        };
    }
}
