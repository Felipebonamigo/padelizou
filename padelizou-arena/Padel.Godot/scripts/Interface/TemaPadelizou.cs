using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// Cores da marca, fontes e o Theme da interface, tudo em código (sem .tres nem fonte baixada).
/// A fonte é a padrão do Godot (Open Sans SemiBold) com variações: engrossada pros títulos e números.
/// Ela não tem setas nem bolinhas (◀ ▶ ●) — o que precisar disso é desenhado, não escrito.
/// </summary>
public static class TemaPadelizou
{
    public static readonly Color Marinho = new("1c2742");
    public static readonly Color MarinhoProfundo = new("111a2e");
    public static readonly Color MarinhoClaro = new("2a3a5f");
    public static readonly Color MarinhoBorda = new("3d5080");
    public static readonly Color Lima = new("a3d827");
    public static readonly Color LimaEscuro = new("7fae14");
    public static readonly Color Branco = new("f4f6fa");
    public static readonly Color TextoSuave = new("a9b4cc");
    public static readonly Color Ouro = new("f5c542");
    public static readonly Color Alerta = new("ff7a66");

    // Nomes das variações de tipo (Control.ThemeTypeVariation).
    public const string BotaoDoMenu = "BotaoDoMenu";
    public const string BotaoCompacto = "BotaoCompacto";
    public const string TituloDoJogo = "TituloDoJogo";
    public const string TituloDaTela = "TituloDaTela";
    public const string Subtitulo = "Subtitulo";
    public const string Secao = "Secao";
    public const string Descricao = "Descricao";
    public const string Erro = "Erro";
    public const string Tecla = "Tecla";
    public const string Cartao = "Cartao";

    private static FontVariation? _forte, _titulo;

    public static Font Normal => ThemeDB.FallbackFont;

    /// <summary>Semibold engrossado: botões, números do placar.</summary>
    public static FontVariation Forte => _forte ??= new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.55f };

    /// <summary>O mais pesado, com as letras um pouco afastadas: títulos em caixa alta.</summary>
    public static FontVariation Titulo => _titulo ??= CriarTitulo();

    private static FontVariation CriarTitulo()
    {
        var fonte = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 1.0f };
        fonte.SetSpacing(TextServer.SpacingType.Glyph, 2);
        return fonte;
    }

    private static Theme? _tema;

    /// <summary>O Theme da interface (um só, compartilhado).</summary>
    public static Theme Tema => _tema ??= Criar();

    private static Theme Criar()
    {
        var t = new Theme { DefaultFont = Normal, DefaultFontSize = 20 };

        t.SetColor("font_color", "Label", Branco);

        // Botão: foco = preenchido de lima com texto marinho. O mouse passando pega o foco (FocoSegueMouse),
        // então hover e foco têm a mesma cara e o teclado, o controle e o mouse contam a mesma história.
        t.SetStylebox("normal", "Button", Caixa(new Color(MarinhoClaro, 0.55f), 8, 20, 10));
        t.SetStylebox("hover", "Button", Caixa(Lima, 8, 20, 10));
        t.SetStylebox("pressed", "Button", Caixa(LimaEscuro, 8, 20, 10));
        t.SetStylebox("hover_pressed", "Button", Caixa(LimaEscuro, 8, 20, 10));
        t.SetStylebox("disabled", "Button", Caixa(new Color(MarinhoClaro, 0.25f), 8, 20, 10));
        t.SetStylebox("focus", "Button", Caixa(Lima, 8, 20, 10));
        t.SetColor("font_color", "Button", Branco);
        t.SetColor("font_hover_color", "Button", Marinho);
        t.SetColor("font_focus_color", "Button", Marinho);
        t.SetColor("font_pressed_color", "Button", Marinho);
        t.SetColor("font_hover_pressed_color", "Button", Marinho);
        t.SetColor("font_disabled_color", "Button", new Color(TextoSuave, 0.5f));
        t.SetFont("font", "Button", Forte);
        t.SetFontSize("font_size", "Button", 22);

        t.SetTypeVariation(BotaoDoMenu, "Button");
        t.SetFontSize("font_size", BotaoDoMenu, 26);
        t.SetStylebox("normal", BotaoDoMenu, Caixa(new Color(0, 0, 0, 0), 8, 26, 8));
        foreach (var estado in new[] { "hover", "focus" }) t.SetStylebox(estado, BotaoDoMenu, Caixa(Lima, 8, 26, 8));
        foreach (var estado in new[] { "pressed", "hover_pressed" }) t.SetStylebox(estado, BotaoDoMenu, Caixa(LimaEscuro, 8, 26, 8));

        t.SetTypeVariation(BotaoCompacto, "Button");
        t.SetFontSize("font_size", BotaoCompacto, 20);

        t.SetTypeVariation(TituloDoJogo, "Label");
        t.SetFont("font", TituloDoJogo, Titulo);
        t.SetFontSize("font_size", TituloDoJogo, 70);

        t.SetTypeVariation(TituloDaTela, "Label");
        t.SetFont("font", TituloDaTela, Titulo);
        t.SetFontSize("font_size", TituloDaTela, 40);

        t.SetTypeVariation(Subtitulo, "Label");
        t.SetColor("font_color", Subtitulo, TextoSuave);
        t.SetFontSize("font_size", Subtitulo, 20);

        t.SetTypeVariation(Secao, "Label");
        t.SetColor("font_color", Secao, Lima);
        t.SetFont("font", Secao, Titulo);
        t.SetFontSize("font_size", Secao, 15);

        t.SetTypeVariation(Descricao, "Label");
        t.SetColor("font_color", Descricao, TextoSuave);
        t.SetFontSize("font_size", Descricao, 18);

        t.SetTypeVariation(Erro, "Label");
        t.SetColor("font_color", Erro, Alerta);
        t.SetFontSize("font_size", Erro, 16);

        t.SetTypeVariation(Tecla, "Label");
        t.SetFont("font", Tecla, Forte);
        t.SetFontSize("font_size", Tecla, 15);
        t.SetColor("font_color", Tecla, Branco);
        t.SetStylebox("normal", Tecla, Caixa(new Color(MarinhoClaro, 0.9f), 5, 9, 2, TextoSuave, 1));

        // Cartão: marinho quase opaco com o filete lima em cima, como as artes de transmissão.
        t.SetTypeVariation(Cartao, "PanelContainer");
        var cartao = Caixa(new Color(MarinhoProfundo, 0.96f), 12, 32, 26);
        cartao.BorderColor = Lima;
        cartao.BorderWidthTop = 5;
        t.SetStylebox("panel", Cartao, cartao);

        // Campo de texto: foco = borda lima grossa (desenhada por cima da normal).
        t.SetStylebox("normal", "LineEdit", Caixa(MarinhoProfundo, 8, 14, 8, MarinhoBorda, 2));
        t.SetStylebox("read_only", "LineEdit", Caixa(new Color(MarinhoProfundo, 0.6f), 8, 14, 8, MarinhoBorda, 1));
        var foco = Caixa(new Color(0, 0, 0, 0), 8, 14, 8, Lima, 3);
        foco.DrawCenter = false;
        t.SetStylebox("focus", "LineEdit", foco);
        t.SetColor("font_color", "LineEdit", Branco);
        t.SetColor("font_placeholder_color", "LineEdit", new Color(TextoSuave, 0.6f));
        t.SetColor("caret_color", "LineEdit", Lima);
        t.SetColor("selection_color", "LineEdit", new Color(Lima, 0.35f));
        t.SetFontSize("font_size", "LineEdit", 22);

        return t;
    }

    /// <summary>StyleBoxFlat arredondado com margem interna e borda opcional.</summary>
    public static StyleBoxFlat Caixa(Color fundo, int raio = 8, float margemH = 16, float margemV = 8, Color? borda = null, int espessura = 0)
    {
        var s = new StyleBoxFlat { BgColor = fundo };
        s.SetCornerRadiusAll(raio);
        s.ContentMarginLeft = s.ContentMarginRight = margemH;
        s.ContentMarginTop = s.ContentMarginBottom = margemV;
        if (borda is Color cor && espessura > 0)
        {
            s.BorderColor = cor;
            s.SetBorderWidthAll(espessura);
        }
        return s;
    }

    /// <summary>InputMap::ALL_DEVICES do Godot, que o C# não expõe: o evento vale pra qualquer controle.</summary>
    private const int QualquerControle = -1;

    /// <summary>
    /// O mapa padrão do Godot 4.7 navega com direcional e analógico (ui_up/down/left/right), mas o ui_accept e o
    /// ui_cancel só têm teclado. Acrescenta o A e o B do controle (Steam Deck incluso), de qualquer controle — como
    /// o direcional do mapa padrão. Um InputEventJoypadButton novo nasce com Device = 0, e aí só o primeiro controle
    /// confirmaria e voltaria: no coop local, o segundo navegaria e não escolheria nada. Idempotente.
    /// </summary>
    public static void ConfigurarEntradaDaInterface()
    {
        Garantir("ui_accept", new InputEventJoypadButton { ButtonIndex = JoyButton.A, Device = QualquerControle });
        Garantir("ui_cancel", new InputEventJoypadButton { ButtonIndex = JoyButton.B, Device = QualquerControle });
    }

    private static void Garantir(string acao, InputEvent evento)
    {
        if (InputMap.HasAction(acao) && !InputMap.ActionHasEvent(acao, evento)) InputMap.ActionAddEvent(acao, evento);
    }

    /// <summary>
    /// No Godot 4.7 um LineEdit que recebe o foco pela navegação só começa a editar com Enter do teclado ou clique:
    /// o A do controle não conta, e sem isto quem joga no controle (Steam Deck) não consegue digitar o nome nem o IP.
    /// Aqui o A começa a edição e o B termina.
    /// </summary>
    public static void EditarComControle(LineEdit campo) =>
        campo.GuiInput += evento =>
        {
            if (evento is not InputEventJoypadButton) return;
            if (!campo.IsEditing() && evento.IsActionPressed("ui_accept"))
            {
                campo.Edit();
                campo.AcceptEvent();
            }
            else if (campo.IsEditing() && evento.IsActionPressed("ui_cancel"))
            {
                campo.Unedit();
                campo.AcceptEvent();
            }
        };

    /// <summary>O mouse em cima pega o foco: um destaque só, venha a mão do mouse, do teclado ou do controle.</summary>
    public static void FocoSegueMouse(Control controle) =>
        controle.MouseEntered += () =>
        {
            if (controle.IsVisibleInTree() && controle.FocusMode != Control.FocusModeEnum.None && !controle.HasFocus()) controle.GrabFocus();
        };

    /// <summary>
    /// Fundo escurecido na tela toda (que também segura o clique) e um cartão centralizado.
    /// Devolve a coluna de dentro do cartão, onde vai o conteúdo.
    /// </summary>
    public static VBoxContainer MontarSobreposicao(Control dono, float larguraDoCartao, float opacidadeDoFundo = 0.72f)
    {
        var fundo = new ColorRect { Color = new Color(MarinhoProfundo, opacidadeDoFundo), MouseFilter = Control.MouseFilterEnum.Stop };
        fundo.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dono.AddChild(fundo);
        var centro = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centro.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var cartao = new PanelContainer { ThemeTypeVariation = Cartao, CustomMinimumSize = new Vector2(larguraDoCartao, 0) };
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 12);
        cartao.AddChild(coluna);
        centro.AddChild(cartao);
        dono.AddChild(centro);
        return coluna;
    }

    /// <summary>Espaço vertical fixo numa coluna.</summary>
    public static Control Espaco(float altura) => new() { CustomMinimumSize = new Vector2(0, altura), MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>Label pronto com a variação de tema.</summary>
    public static Label Rotulo(string texto, string variacao = "", HorizontalAlignment alinhamento = HorizontalAlignment.Left) =>
        new() { Text = texto, ThemeTypeVariation = variacao, HorizontalAlignment = alinhamento };
}
