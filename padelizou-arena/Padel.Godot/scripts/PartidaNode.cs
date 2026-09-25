using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Raiz da cena: cria a Partida do Core, avança em passo fixo no _PhysicsProcess (120 Hz no project.godot),
/// lê a entrada local e espelha o estado nos nós 3D e no HUD.
/// Argumentos de linha de comando depois de "--": --auto (4 IAs), --semente N, --sair-apos SEGUNDOS (pra CI),
/// --screenshot ARQUIVO.png (salva a tela ao sair; precisa de renderização, não funciona em --headless),
/// --auto-golpe (assistência: bate sozinho ao alcance, sem timing).
/// </summary>
public partial class PartidaNode : Node3D
{
    [Export] public Dificuldade Dificuldade = Dificuldade.Medio;
    [Export] public bool PontoDeOuro = true;
    [Export] public int SetsParaVencer = 1;
    [Export] public bool ModoAutomatico;
    [Export] public ModoDeGolpe ModoDeGolpe = ModoDeGolpe.Manual;

    public Partida Partida { get; private set; } = null!;

    private QuadraNode _quadra = null!;
    private BolaNode _bola = null!;
    private readonly List<JogadorNode> _jogadores = [];
    private CameraNode? _camera;
    private HudNode _hud = null!;
    private MeshInstance3D? _marcaDaCaixa;
    private bool _pausado;
    private double _sairApos = -1;
    private double _tempoVivo;
    private string? _screenshot;
    private readonly Entrada[] _entradas = new Entrada[4];

    public override void _Ready()
    {
        EntradaLocal.ConfigurarMapa();
        uint? semente = null;
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--auto": ModoAutomatico = true; break;
                case "--semente" when i + 1 < args.Length && uint.TryParse(args[i + 1], out var s): semente = s; i++; break;
                case "--sair-apos" when i + 1 < args.Length && double.TryParse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var t): _sairApos = t; i++; break;
                case "--screenshot" when i + 1 < args.Length: _screenshot = args[i + 1]; i++; break;
                case "--auto-golpe": ModoDeGolpe = ModoDeGolpe.Automatico; break;
                case "--facil": Dificuldade = Dificuldade.Facil; break;
                case "--dificil": Dificuldade = Dificuldade.Dificil; break;
            }
        }

        Partida = new Partida(new OpcoesDaPartida
        {
            Dificuldade = Dificuldade,
            PontoDeOuro = PontoDeOuro,
            SetsParaVencer = SetsParaVencer,
            Humanos = ModoAutomatico ? OpcoesDaPartida.NinguemHumano : [true, false, false, false],
            ModoDeGolpe = ModoDeGolpe,
            Semente = semente,
        });
        Partida.Evento += AoEvento;

        _quadra = new QuadraNode { Name = "Quadra" };
        AddChild(_quadra);
        _bola = new BolaNode { Name = "Bola" };
        AddChild(_bola);
        foreach (var j in Partida.Jogadores)
        {
            var no = new JogadorNode { Name = j.Nome.Replace(' ', '_') };
            AddChild(no);
            no.Ligar(j);
            _jogadores.Add(no);
        }
        _camera = GetNodeOrNull<CameraNode>("Camera");
        _hud = new HudNode { Name = "Hud" };
        AddChild(_hud);
        Espelhar(0);
        GD.Print($"Padelizou Arena: partida criada ({Dificuldade}, {(ModoAutomatico ? "4 IAs" : $"1 humano, golpe {ModoDeGolpe}")}, Godot {Engine.GetVersionInfo()["string"]})");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed(EntradaLocal.Pausa)) _pausado = !_pausado;
        if (_pausado) return;
        _entradas[0] = ModoAutomatico ? Entrada.Vazia : EntradaLocal.Ler();
        Partida.Avancar((float)delta, _entradas);
        Espelhar((float)delta);

        _tempoVivo += delta;
        if (_sairApos >= 0 && (_tempoVivo >= _sairApos || Partida.Acabou))
        {
            var e = Partida.Estatisticas;
            GD.Print($"Saindo após {_tempoVivo:F1} s: estado={Partida.Estado} placar={Partida.Placar.Resumo()} pontos={e.Pontos} golpes={e.Golpes} maiorRally={e.MaiorRally}");
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

    private void Espelhar(float delta)
    {
        _bola.Atualizar(Partida.Bola);
        foreach (var no in _jogadores) no.Atualizar();
        _camera?.Seguir(Partida.Bola.X, delta);
        _hud.Atualizar(Partida);
        bool mostrarCaixa = Partida.Estado == EstadoDaPartida.Saque;
        if (mostrarCaixa && _marcaDaCaixa is null) _marcaDaCaixa = _quadra.CriarMarcaDaCaixa(Partida.CaixaDoSaque);
        else if (!mostrarCaixa && _marcaDaCaixa is not null) { _marcaDaCaixa.QueueFree(); _marcaDaCaixa = null; }
    }

    private void AoEvento(EventoDaPartida evento)
    {
        // Ponto de encaixe do som e das animações (M1). Por enquanto, log dos eventos que mudam o placar.
        if (evento.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
            GD.Print($"{evento.Tipo}: time {evento.Time} — {(evento.Motivo is Motivo m ? Partida.Motivos[m] : "")} | {Partida.Placar.Resumo()} {Partida.Placar.TextoDosPontos(0)}-{Partida.Placar.TextoDosPontos(1)}");
    }
}
