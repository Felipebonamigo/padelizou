using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// Pausa: Continuar, Opções, Sair pro menu — cada botão só emite o seu sinal; quem abriu decide o que fazer.
/// Esc/B/Start (ui_cancel ou a ação "pausa") pedem Continuar, e o evento sai marcado como tratado. Atenção de quem
/// integra: abra a pausa pelo evento (_UnhandledInput), como o PartidaNode — por polling, o mesmo aperto que acabou de
/// fechar a pausa ainda conta como "acabou de apertar" no quadro de física e a reabre.
/// Processa mesmo com a árvore pausada.
/// </summary>
public partial class TelaDePausa : Control
{
    [Signal] public delegate void ContinuarPedidoEventHandler();
    [Signal] public delegate void OpcoesPedidasEventHandler();
    [Signal] public delegate void SairProMenuPedidoEventHandler();

    private readonly Button _continuar;
    private readonly Label _detalhe;
    private Tween? _entrada;

    public TelaDePausa()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        Theme = TemaPadelizou.Tema;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var coluna = TemaPadelizou.MontarSobreposicao(this, 460);
        coluna.AddChild(TemaPadelizou.Rotulo("PARTIDA", TemaPadelizou.Secao));
        coluna.AddChild(TemaPadelizou.Rotulo("Pausa", TemaPadelizou.TituloDaTela));
        _detalhe = TemaPadelizou.Rotulo("", TemaPadelizou.Subtitulo);
        coluna.AddChild(_detalhe);
        coluna.AddChild(TemaPadelizou.Espaco(8));

        _continuar = Botao(coluna, "Continuar", SignalName.ContinuarPedido);
        Botao(coluna, "Opções", SignalName.OpcoesPedidas);
        Botao(coluna, "Sair pro menu", SignalName.SairProMenuPedido);

        coluna.AddChild(TemaPadelizou.Espaco(10));
        var dicas = new DicaDeControles();
        dicas.Definir(new Dica("Enter", "A", "escolher"), new Dica("Esc", "B", "continuar"));
        coluna.AddChild(dicas);
    }

    private Button Botao(VBoxContainer coluna, string texto, StringName sinal)
    {
        var botao = new Button { Text = texto, ThemeTypeVariation = TemaPadelizou.BotaoDoMenu, Alignment = HorizontalAlignment.Left };
        botao.Pressed += () => EmitSignal(sinal);
        TemaPadelizou.FocoSegueMouse(botao);
        coluna.AddChild(botao);
        return botao;
    }

    public override void _Ready() => TemaPadelizou.ConfigurarEntradaDaInterface();

    public bool Aberta => Visible;

    /// <param name="detalhe">Uma linha embaixo do título, ex.: o placar ("1º set · 3-2 · 15-30"). Null esconde.</param>
    public void Abrir(string? detalhe = null)
    {
        _detalhe.Text = detalhe ?? "";
        _detalhe.Visible = !string.IsNullOrEmpty(detalhe);
        Show();
        _entrada?.Kill();
        Modulate = new Color(1, 1, 1, 0);
        _entrada = CreateTween();
        _entrada.TweenProperty(this, "modulate:a", 1f, 0.15f);
        _continuar.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Fechar()
    {
        _entrada?.Kill();
        Hide();
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!Visible) return;
        if (evento.IsActionPressed("ui_cancel") || (InputMap.HasAction("pausa") && evento.IsActionPressed("pausa")))
        {
            GetViewport().SetInputAsHandled();
            EmitSignal(SignalName.ContinuarPedido);
        }
    }
}
