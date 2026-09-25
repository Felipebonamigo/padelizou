using Godot;
using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>
/// Bancada do placar de TV e das telas de pausa e fim, sobre a quadra. Os placares saem de um <see cref="Placar"/>
/// do Core jogado ponto a ponto — o que aparece é o que a regra produz, não texto inventado.
/// Estados (-- --estado N): 1 = 15-30 com mensagem; 2 = ponto de ouro; 3 = tie-break do 3º set com os sets
/// anteriores e ping; 4 = pausa; 5 = fim. Sem --estado, passa por todos a cada 3 s (teclas 1–5 escolhem).
/// "-- --screenshot ARQ.png" salva a tela e fecha.
/// "-- --conferir" roda os testes da interface (<see cref="ConferenciaDaInterface"/>) sem montar a cena e fecha
/// com 0 (tudo passou) ou 1.
/// </summary>
public partial class TestePlacarNode : Node3D
{
    private const string Casa = "Felipe / Marina";
    private const string Rivais = "Bruno / Carla";
    private const double TempoPorEstado = 3.0;

    private PlacarDeTvNode _placar = null!;
    private TelaDePausa _pausa = null!;
    private TelaDeFim _fim = null!;
    private int _estado = 1;
    private bool _ciclar = true;
    private double _relogio;

    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--conferir"))
        {
            _ciclar = false;
            ConferenciaDaInterface.RodarESair(this);
            return;
        }
        TemaPadelizou.ConfigurarEntradaDaInterface();
        AddChild(new QuadraNode { Name = "Quadra" });
        _placar = new PlacarDeTvNode { Name = "PlacarDeTv" };
        AddChild(_placar);

        var telas = new CanvasLayer { Name = "Telas", Layer = 20 };
        AddChild(telas);
        _pausa = new TelaDePausa { Name = "Pausa" };
        _pausa.ContinuarPedido += () => GD.Print("Pausa: Continuar");
        _pausa.OpcoesPedidas += () => GD.Print("Pausa: Opções");
        _pausa.SairProMenuPedido += () => GD.Print("Pausa: Sair pro menu");
        telas.AddChild(_pausa);
        _fim = new TelaDeFim { Name = "Fim" };
        _fim.JogarDeNovoPedido += () => GD.Print("Fim: Jogar de novo");
        _fim.MenuPedido += () => GD.Print("Fim: Menu");
        telas.AddChild(_fim);

        string? screenshot = null;
        var args = OS.GetCmdlineUserArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--estado" && int.TryParse(args[i + 1], out int n) && n is >= 1 and <= 5)
            {
                _estado = n;
                _ciclar = false;
            }
            else if (args[i] == "--screenshot") screenshot = args[i + 1];
        }
        if (screenshot is not null) _ciclar = false;

        Mostrar(_estado);
        if (screenshot is not null) Captura.SalvarESair(this, screenshot);
    }

    public override void _Process(double delta)
    {
        if (!_ciclar) return;
        _relogio += delta;
        if (_relogio < TempoPorEstado) return;
        _relogio = 0;
        Mostrar(_estado % 5 + 1);
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (evento is InputEventKey { Pressed: true, Echo: false } tecla && tecla.Keycode is >= Key.Key1 and <= Key.Key5)
        {
            _ciclar = false;
            Mostrar((int)(tecla.Keycode - Key.Key1) + 1);
        }
    }

    private void Mostrar(int estado)
    {
        _estado = estado;
        _pausa.Fechar();
        _fim.Fechar();
        _placar.Visible = estado != 5;
        switch (estado)
        {
            case 1:
            case 4:
                _placar.Atualizar(DadosDoPlacar.De(Normal(), Casa, Rivais, new Mensagem("Ponto dos rivais — no vidro sem quicar")));
                if (estado == 4) _pausa.Abrir("1º set · 3-2 · 15-30");
                break;
            case 2:
                _placar.Atualizar(DadosDoPlacar.De(PontoDeOuro(), Casa, Rivais, new Mensagem("40-40: ponto de ouro, quem ganhar leva o game", Destaque: true)));
                break;
            case 3:
                _placar.Atualizar(DadosDoPlacar.De(TieBreak(), Casa, Rivais, new Mensagem("Seu saque — aperte pra sacar", Suave: true), pingMs: 48));
                break;
            case 5:
                var final = Encerrada();
                var dados = DadosDoPlacar.De(final, Casa, Rivais);
                _fim.Mostrar("Vitória!", vitoria: true, Casa, Rivais, dados.SetsAnteriores,
                [
                    ("Pontos", "173"), ("Golpes", "1.046"),
                    ("Maior rally", "27"), ("Faltas de saque", "9"),
                    ("Lets", "3"), ("Tempo de jogo", "1h04"),
                ]);
                break;
        }
    }

    // ---- Placares de verdade, jogados no Core ----

    private static void Pontos(Placar placar, int time, int quantos)
    {
        for (int i = 0; i < quantos; i++) placar.PontoPara(time);
    }

    /// <summary>Um game ganho de zero (com ponto de ouro, 4 pontos seguidos fecham).</summary>
    private static void Games(Placar placar, params int[] vencedores)
    {
        foreach (int time in vencedores) Pontos(placar, time, 4);
    }

    /// <summary>6-6 alternando, e o tie-break até <paramref name="casa"/> x <paramref name="rivais"/> (alternando também).</summary>
    private static void AteOTieBreak(Placar placar, int casa, int rivais)
    {
        for (int i = 0; i < 6; i++) Games(placar, 0, 1);
        int c = 0, r = 0;
        while (c < casa || r < rivais)
        {
            if (c < casa && (c <= r || r >= rivais)) { placar.PontoPara(0); c++; }
            else { placar.PontoPara(1); r++; }
        }
    }

    internal static Placar Normal()
    {
        var p = new Placar(pontoDeOuro: true, setsParaVencer: 1);
        Games(p, 0, 1, 0, 1, 0);   // 3-2
        Pontos(p, 0, 1);
        Pontos(p, 1, 2);            // 15-30
        return p;
    }

    internal static Placar PontoDeOuro()
    {
        var p = new Placar(pontoDeOuro: true, setsParaVencer: 1);
        Games(p, 0, 1, 0, 1, 1, 0, 1, 0);   // 4-4
        Pontos(p, 0, 3);
        Pontos(p, 1, 3);                   // 40-40
        return p;
    }

    internal static Placar TieBreak()
    {
        var p = new Placar(pontoDeOuro: true, setsParaVencer: 2);
        Games(p, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0);   // 6-4 casa
        AteOTieBreak(p, 5, 7);                    // 6-7(5) rivais
        AteOTieBreak(p, 5, 4);                    // 3º set, 6-6, tie-break 5-4
        return p;
    }

    internal static Placar Encerrada()
    {
        var p = new Placar(pontoDeOuro: true, setsParaVencer: 2);
        Games(p, 1, 0, 1, 0, 1, 0, 1, 0, 0, 0);   // 6-4
        AteOTieBreak(p, 5, 7);                    // 6-7(5)
        AteOTieBreak(p, 10, 8);                   // 7-6(8)
        return p;
    }
}
