using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// O placar da partida no estilo das transmissões de padel: o <see cref="QuadroDoPlacar"/> no canto de cima,
/// a faixa de mensagem que sobe do rodapé e sai com animação curta, e o ping do online no canto oposto.
/// API só com primitivos: <see cref="Atualizar"/> recebe um <see cref="DadosDoPlacar"/>; pode ser chamado todo
/// quadro — o que não mudou não é redesenhado. Não conhece a Partida nem a rede.
/// </summary>
public partial class PlacarDeTvNode : CanvasLayer
{
    private const float Margem = 24f;
    private const float AlturaDaFaixa = 56f;
    private const float DistanciaDaFaixaAoRodape = 64f;
    private const float Deslize = 26f;

    private QuadroDoPlacar _quadro = null!;
    private Control _faixaAncora = null!;
    private PanelContainer _faixa = null!;
    private StyleBoxFlat _estiloDaFaixa = null!;
    private Panel _acentoDaFaixa = null!;
    private StyleBoxFlat _estiloDoAcento = null!;
    private Label _textoDaFaixa = null!;
    private PanelContainer _ping = null!;
    private Bolinha _luzDoPing = null!;
    private Label _textoDoPing = null!;
    private Tween? _animacaoDaFaixa;
    private (string Texto, bool Destaque, bool Suave) _faixaAtual = ("", false, false);
    private DadosDoPlacar? _pendente;

    public PlacarDeTvNode() => Layer = 10;

    public override void _Ready()
    {
        var raiz = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = TemaPadelizou.Tema };
        raiz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(raiz);

        _quadro = new QuadroDoPlacar { Position = new Vector2(Margem, Margem - 4) };
        raiz.AddChild(_quadro);

        // Faixa de mensagem: uma âncora na largura toda, presa no rodapé; a animação mexe nos offsets dela.
        _faixaAncora = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _faixaAncora.AnchorLeft = 0;
        _faixaAncora.AnchorRight = 1;
        _faixaAncora.AnchorTop = _faixaAncora.AnchorBottom = 1;
        PosicionarFaixa(Deslize);
        var centro = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _faixa = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _estiloDaFaixa = TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoProfundo, 0.94f), 6, 0, 0);
        _faixa.AddThemeStyleboxOverride("panel", _estiloDaFaixa);
        var conteudo = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        conteudo.AddThemeConstantOverride("separation", 0);
        _acentoDaFaixa = new Panel { CustomMinimumSize = new Vector2(8, AlturaDaFaixa), MouseFilter = Control.MouseFilterEnum.Ignore };
        _estiloDoAcento = TemaPadelizou.Caixa(TemaPadelizou.Lima, 0, 0, 0);
        _estiloDoAcento.CornerRadiusTopLeft = _estiloDoAcento.CornerRadiusBottomLeft = 6;
        _acentoDaFaixa.AddThemeStyleboxOverride("panel", _estiloDoAcento);
        var margem = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margem.AddThemeConstantOverride("margin_left", 26);
        margem.AddThemeConstantOverride("margin_right", 30);
        _textoDaFaixa = new Label { VerticalAlignment = VerticalAlignment.Center };
        _textoDaFaixa.AddThemeFontOverride("font", TemaPadelizou.Forte);
        margem.AddChild(_textoDaFaixa);
        conteudo.AddChild(_acentoDaFaixa);
        conteudo.AddChild(margem);
        _faixa.AddChild(conteudo);
        centro.AddChild(_faixa);
        _faixaAncora.AddChild(centro);
        raiz.AddChild(_faixaAncora);

        // Ping: canto de cima, do lado oposto ao placar; cresce pra esquerda.
        _ping = new PanelContainer { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, GrowHorizontal = Control.GrowDirection.Begin };
        _ping.AddThemeStyleboxOverride("panel", TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoProfundo, 0.85f), 6, 12, 5));
        _ping.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _ping.OffsetLeft = _ping.OffsetRight = -Margem;
        _ping.OffsetTop = _ping.OffsetBottom = Margem - 4;
        var linhaDoPing = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        linhaDoPing.AddThemeConstantOverride("separation", 8);
        _luzDoPing = new Bolinha { Raio = 5f, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _textoDoPing = new Label();
        _textoDoPing.AddThemeFontOverride("font", TemaPadelizou.Forte);
        _textoDoPing.AddThemeFontSizeOverride("font_size", 16);
        linhaDoPing.AddChild(_luzDoPing);
        linhaDoPing.AddChild(_textoDoPing);
        _ping.AddChild(linhaDoPing);
        raiz.AddChild(_ping);

        if (_pendente is not null)
        {
            var dados = _pendente;
            _pendente = null;
            Atualizar(dados);
        }
    }

    public void Atualizar(DadosDoPlacar dados)
    {
        if (!IsNodeReady())
        {
            _pendente = dados;
            return;
        }
        _quadro.Atualizar(dados);
        AtualizarFaixa(dados.Mensagem ?? "", dados.MensagemEmDestaque, dados.MensagemSuave);
        AtualizarPing(dados.PingMs);
    }

    private void AtualizarPing(int? ms)
    {
        _ping.Visible = ms is not null;
        if (ms is not int valor) return;
        string texto = $"{valor} ms";
        if (_textoDoPing.Text == texto) return;
        _textoDoPing.Text = texto;
        // Padel online: abaixo de 80 ms não se sente; até 150 dá pra jogar; acima disso a bola "pula".
        _luzDoPing.Cor = valor < 80 ? TemaPadelizou.Lima : valor < 150 ? TemaPadelizou.Ouro : TemaPadelizou.Alerta;
    }

    private void AtualizarFaixa(string texto, bool destaque, bool suave)
    {
        // Sem texto o estilo não importa — e normalizar evita criar um Tween vazio (o Godot reclama).
        var nova = texto.Length > 0 ? (texto, destaque, suave) : ("", false, false);
        if (nova == _faixaAtual) return;
        bool haviaFaixa = _faixaAtual.Texto.Length > 0;
        _faixaAtual = nova;

        _animacaoDaFaixa?.Kill();
        _animacaoDaFaixa = CreateTween();
        if (haviaFaixa)
        {
            // A que está na tela desce e some antes da próxima subir — 0,12 s, pra não atrasar a notícia.
            _animacaoDaFaixa.TweenProperty(_faixaAncora, "modulate:a", 0f, 0.12f);
            _animacaoDaFaixa.Parallel().TweenMethod(Callable.From<float>(PosicionarFaixa), DeslocamentoAtual(), Deslize, 0.12f);
        }
        if (texto.Length == 0) return;

        _animacaoDaFaixa.TweenCallback(Callable.From(() => AplicarEstiloDaFaixa(texto, destaque, suave)));
        _animacaoDaFaixa.TweenMethod(Callable.From<float>(PosicionarFaixa), Deslize, 0f, 0.24f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _animacaoDaFaixa.Parallel().TweenProperty(_faixaAncora, "modulate:a", 1f, 0.18f);
    }

    private void AplicarEstiloDaFaixa(string texto, bool destaque, bool suave)
    {
        _textoDaFaixa.Text = texto;
        _textoDaFaixa.AddThemeFontSizeOverride("font_size", suave ? 19 : destaque ? 26 : 23);
        _textoDaFaixa.AddThemeColorOverride("font_color", destaque ? TemaPadelizou.Marinho : suave ? new Color(TemaPadelizou.Branco, 0.88f) : TemaPadelizou.Branco);
        _estiloDaFaixa.BgColor = destaque ? TemaPadelizou.Lima : new Color(TemaPadelizou.MarinhoProfundo, suave ? 0.78f : 0.94f);
        _estiloDoAcento.BgColor = destaque ? TemaPadelizou.Marinho : TemaPadelizou.Lima;
        _acentoDaFaixa.Visible = !suave;
        _acentoDaFaixa.CustomMinimumSize = new Vector2(8, suave ? 44 : AlturaDaFaixa);
    }

    private float DeslocamentoAtual() => _faixaAncora.OffsetBottom + DistanciaDaFaixaAoRodape;

    /// <summary>0 = na posição; positivo = mais pra baixo (de onde a faixa sobe).</summary>
    private void PosicionarFaixa(float deslocamento)
    {
        _faixaAncora.OffsetBottom = -DistanciaDaFaixaAoRodape + deslocamento;
        _faixaAncora.OffsetTop = _faixaAncora.OffsetBottom - AlturaDaFaixa;
    }
}
