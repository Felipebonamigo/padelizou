using System.Globalization;
using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Raiz da cena da partida: escolhe a sessão (onde a partida roda), avança em passo fixo no _PhysicsProcess
/// (120 Hz no project.godot) com a entrada de quem joga nesta máquina, e desenha o Retrato nos nós 3D e no HUD.
/// A tela nunca lê a Partida direto — só o Retrato —, e é isso que deixa o mesmo desenho servir ao jogo local e ao online.
/// Argumentos de linha de comando depois de "--": --auto (4 IAs), --coop (dois humanos no mesmo time),
/// --semente N, --sair-apos SEGUNDOS (pra CI), --screenshot ARQUIVO.png (salva a tela ao sair; precisa de
/// renderização, não funciona em --headless), --auto-golpe (assistência), --facil, --dificil, --sem-replay.
/// Na partida local, pontos que merecem ganham replay em câmera lenta (ControleDeReplay); a ação pula.
/// </summary>
public partial class PartidaNode : Node3D
{
    [Export] public Dificuldade Dificuldade = Dificuldade.Medio;
    [Export] public bool PontoDeOuro = true;
    [Export] public int SetsParaVencer = 1;
    [Export] public bool ModoAutomatico;
    [Export] public bool Coop;
    [Export] public ModoDeGolpe ModoDeGolpe = ModoDeGolpe.Manual;

    public ISessao Sessao { get; private set; } = null!;

    private QuadraNode _quadra = null!;
    private BolaNode _bola = null!;
    private readonly List<JogadorNode> _jogadores = [];
    private CameraNode? _camera;
    private HudNode _hud = null!;
    private MeshInstance3D? _marcaDaCaixa;
    private Caixa? _caixaMarcada;
    private bool _pausado;
    private double _sairApos = -1;
    private double _tempoVivo;
    private string? _screenshot;
    private uint? _semente;
    private readonly Entrada[] _entradas = new Entrada[4];
    private readonly ControleDeReplay _replay = new();

    public override void _Ready()
    {
        EntradaLocal.ConfigurarMapa();
        LerLinhaDeComando(OS.GetCmdlineUserArgs());

        bool[] humanos = ModoAutomatico ? OpcoesDaPartida.NinguemHumano : Coop ? [true, true, false, false] : [true, false, false, false];
        Sessao = new SessaoLocal(new OpcoesDaPartida
        {
            Dificuldade = Dificuldade,
            PontoDeOuro = PontoDeOuro,
            SetsParaVencer = SetsParaVencer,
            Humanos = humanos,
            ModoDeGolpe = ModoDeGolpe,
            Semente = _semente,
        });

        _quadra = new QuadraNode { Name = "Quadra" };
        AddChild(_quadra);
        _bola = new BolaNode { Name = "Bola" };
        AddChild(_bola);
        foreach (var r in Sessao.Retrato.Jogadores)
        {
            var no = new JogadorNode { Name = $"Jogador{r.Indice}" };
            AddChild(no);
            no.Ligar(r);
            _jogadores.Add(no);
        }
        _camera = GetNodeOrNull<CameraNode>("Camera");
        _hud = new HudNode { Name = "Hud" };
        AddChild(_hud);
        Desenhar(0);
        string quem = ModoAutomatico ? "4 IAs" : Coop ? $"coop local, golpe {ModoDeGolpe}" : $"1 humano, golpe {ModoDeGolpe}";
        GD.Print($"Padelizou Arena: partida criada ({Dificuldade}, {quem}, Godot {Engine.GetVersionInfo()["string"]})");
    }

    private void LerLinhaDeComando(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--auto": ModoAutomatico = true; break;
                case "--coop": Coop = true; break;
                case "--semente" when i + 1 < args.Length && uint.TryParse(args[i + 1], out var s): _semente = s; i++; break;
                case "--sair-apos" when i + 1 < args.Length && double.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var t): _sairApos = t; i++; break;
                case "--screenshot" when i + 1 < args.Length: _screenshot = args[i + 1]; i++; break;
                case "--auto-golpe": ModoDeGolpe = ModoDeGolpe.Automatico; break;
                case "--facil": Dificuldade = Dificuldade.Facil; break;
                case "--dificil": Dificuldade = Dificuldade.Dificil; break;
                case "--sem-replay": _replay.Ligado = false; break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed(EntradaLocal.Pausa)) _pausado = !_pausado;
        if (_pausado) return;
        _tempoVivo += delta;
        Array.Clear(_entradas);
        var locais = Sessao.JogadoresLocais;
        for (int n = 0; n < locais.Count; n++) _entradas[locais[n]] = EntradaLocal.LerJogadorLocal(n);

        if (_replay.Reproduzindo)
        {
            // Durante o replay a partida local espera; qualquer jogador daqui pula com a ação.
            if (_entradas.Any(e => e.AcaoPressionada || e.LobPressionada)) _replay.Pular();
            if (_replay.Avancar((float)delta, Sessao.Retrato) is RetratoDaPartida quadro) { DesenharReplay(quadro, (float)delta); VerificarSaida(); return; }
            GD.Print($"Replay: fim em {_tempoVivo:F1} s");
        }

        Sessao.Avancar(delta, _entradas);
        Desenhar((float)delta);
        VerificarSaida();
    }

    private void VerificarSaida()
    {
        if (_sairApos >= 0 && (_tempoVivo >= _sairApos || Sessao.Acabou))
        {
            GD.Print($"Saindo após {_tempoVivo:F1} s: {Sessao.ResumoParaLog()}");
            _sairApos = -1;
            if (_screenshot is not null) SalvarScreenshotESair(_screenshot);
            else GetTree().Quit();
        }
    }

    private async void SalvarScreenshotESair(string arquivo)
    {
        // Espera dois quadros desenhados pra capturar a cena de verdade, e não o quadro do ciclo de física.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var imagem = GetViewport().GetTexture().GetImage();
        var erro = imagem.SavePng(arquivo);
        GD.Print($"Screenshot {(erro == Error.Ok ? "salvo em" : "FALHOU: " + erro + " —")} {arquivo}");
        GetTree().Quit();
    }

    private void DesenharReplay(RetratoDaPartida quadro, float delta)
    {
        _bola.Atualizar(quadro.Bola);
        foreach (var no in _jogadores) no.Atualizar(quadro, delta);
        _camera?.Replay(quadro.Bola.X, quadro.Bola.Y, quadro.Bola.Z, delta);
        _hud.Atualizar(quadro);
    }

    private void Desenhar(float delta)
    {
        var r = Sessao.Retrato;
        _bola.Atualizar(r.Bola);
        foreach (var no in _jogadores) no.Atualizar(r, delta);
        _camera?.Seguir(r.Bola.X, delta);
        _hud.Atualizar(r);
        if (r.CaixaDoSaque != _caixaMarcada)
        {
            _marcaDaCaixa?.QueueFree();
            _marcaDaCaixa = r.CaixaDoSaque is Caixa caixa ? _quadra.CriarMarcaDaCaixa(caixa) : null;
            _caixaMarcada = r.CaixaDoSaque;
        }
        foreach (var a in r.Acontecimentos) AoAcontecimento(a, r);
        // Replay só na partida local: no online a partida não pode parar pros outros.
        if (Sessao is SessaoLocal && _replay.Observar(r)) GD.Print($"Replay: começou em {_tempoVivo:F1} s ({r.Placar.Resumo} {r.Placar.Pontos[0]}-{r.Placar.Pontos[1]})");
        r.Acontecimentos.Clear();
    }

    private static void AoAcontecimento(Acontecimento a, RetratoDaPartida r)
    {
        // Ponto de encaixe do som e dos efeitos. Por enquanto, log do que muda o placar.
        if (a.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
            GD.Print($"{a.Tipo}: time {a.Time} — {(a.Motivo is Motivo m ? Partida.Motivos[m] : "")} | {r.Placar.Resumo} {r.Placar.Pontos[0]}-{r.Placar.Pontos[1]}");
    }
}
