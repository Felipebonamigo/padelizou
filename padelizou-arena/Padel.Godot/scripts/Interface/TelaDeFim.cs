using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// Fim de partida: o resultado, o placar dos sets (o mesmo quadro da transmissão), as estatísticas em pares
/// rótulo/valor, e Jogar de novo / Menu — cada um só emite o seu sinal. Processa mesmo com a árvore pausada.
/// </summary>
public partial class TelaDeFim : Control
{
    [Signal] public delegate void JogarDeNovoPedidoEventHandler();
    [Signal] public delegate void MenuPedidoEventHandler();

    private readonly Label _resultado;
    private readonly Label _aviso;
    private readonly QuadroDoPlacar _quadro;
    private readonly GridContainer _estatisticas;
    private readonly Button _jogarDeNovo;
    private Tween? _entrada;

    public TelaDeFim()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always;
        Theme = TemaPadelizou.Tema;
        SetAnchorsPreset(LayoutPreset.FullRect);

        var coluna = TemaPadelizou.MontarSobreposicao(this, 620, 0.8f);
        coluna.AddChild(TemaPadelizou.Rotulo("FIM DE PARTIDA", TemaPadelizou.Secao));
        _resultado = TemaPadelizou.Rotulo("", TemaPadelizou.TituloDaTela);
        _resultado.AddThemeFontSizeOverride("font_size", 48);
        coluna.AddChild(_resultado);
        _aviso = TemaPadelizou.Rotulo("", TemaPadelizou.Descricao);
        _aviso.AddThemeColorOverride("font_color", TemaPadelizou.Alerta);
        _aviso.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _aviso.Visible = false;
        coluna.AddChild(_aviso);
        coluna.AddChild(TemaPadelizou.Espaco(4));

        _quadro = new QuadroDoPlacar { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, Fundo = new Color(TemaPadelizou.MarinhoClaro, 0.6f) };
        coluna.AddChild(_quadro);
        coluna.AddChild(TemaPadelizou.Espaco(10));

        coluna.AddChild(TemaPadelizou.Rotulo("ESTATÍSTICAS", TemaPadelizou.Secao));
        _estatisticas = new GridContainer { Columns = 4 };
        _estatisticas.AddThemeConstantOverride("h_separation", 22);
        _estatisticas.AddThemeConstantOverride("v_separation", 8);
        coluna.AddChild(_estatisticas);
        coluna.AddChild(TemaPadelizou.Espaco(14));

        var botoes = new HBoxContainer();
        botoes.AddThemeConstantOverride("separation", 12);
        _jogarDeNovo = Botao(botoes, "Jogar de novo", SignalName.JogarDeNovoPedido);
        Botao(botoes, "Menu", SignalName.MenuPedido);
        coluna.AddChild(botoes);

        coluna.AddChild(TemaPadelizou.Espaco(6));
        var dicas = new DicaDeControles();
        dicas.Definir(new Dica("Setas", "Direcional", "navegar"), new Dica("Enter", "A", "escolher"));
        coluna.AddChild(dicas);
    }

    private Button Botao(HBoxContainer linha, string texto, StringName sinal)
    {
        var botao = new Button { Text = texto, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 56) };
        botao.AddThemeFontSizeOverride("font_size", 24);
        botao.Pressed += () => EmitSignal(sinal);
        TemaPadelizou.FocoSegueMouse(botao);
        linha.AddChild(botao);
        return botao;
    }

    public override void _Ready() => TemaPadelizou.ConfigurarEntradaDaInterface();

    /// <param name="resultado">A manchete: "Vitória!", "Derrota", "A casa venceu"…</param>
    /// <param name="vitoria">Pinta a manchete de lima (a vitória de quem está olhando pra tela).</param>
    /// <param name="sets">Os sets jogados, do ponto de vista da casa.</param>
    /// <param name="estatisticas">Pares rótulo/valor, na ordem de exibição (dois por linha).</param>
    /// <param name="aviso">Uma linha de alerta embaixo da manchete (ex.: o perfil não foi salvo). Null esconde.</param>
    public void Mostrar(string resultado, bool vitoria, string duplaCasa, string duplaRivais,
        IReadOnlyList<SetAnterior> sets, IReadOnlyList<(string Rotulo, string Valor)> estatisticas, string? aviso = null)
    {
        _resultado.Text = resultado;
        _aviso.Text = aviso ?? "";
        _aviso.Visible = !string.IsNullOrEmpty(aviso);
        _resultado.AddThemeColorOverride("font_color", vitoria ? TemaPadelizou.Lima : TemaPadelizou.Branco);
        _quadro.Atualizar(new DadosDoPlacar(duplaCasa, duplaRivais, sets, 0, 0, "", "", -1, PartidaEncerrada: true));

        foreach (var filho in _estatisticas.GetChildren())
        {
            _estatisticas.RemoveChild(filho);
            filho.QueueFree();
        }
        _estatisticas.Columns = estatisticas.Count > 3 ? 4 : 2;
        foreach (var (rotulo, valor) in estatisticas)
        {
            var nome = TemaPadelizou.Rotulo(rotulo, TemaPadelizou.Descricao);
            nome.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _estatisticas.AddChild(nome);
            var numero = TemaPadelizou.Rotulo(valor, "", HorizontalAlignment.Right);
            numero.AddThemeFontOverride("font", TemaPadelizou.Forte);
            numero.AddThemeFontSizeOverride("font_size", 22);
            numero.CustomMinimumSize = new Vector2(64, 0);
            _estatisticas.AddChild(numero);
        }

        Show();
        _entrada?.Kill();
        Modulate = new Color(1, 1, 1, 0);
        _entrada = CreateTween();
        _entrada.TweenProperty(this, "modulate:a", 1f, 0.25f);
        _jogarDeNovo.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Fechar()
    {
        _entrada?.Kill();
        Hide();
    }
}
