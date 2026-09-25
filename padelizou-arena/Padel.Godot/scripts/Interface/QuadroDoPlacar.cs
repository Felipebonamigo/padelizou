using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// O bloco do placar no estilo das transmissões de padel: uma linha por dupla com a marca de saque, o nome,
/// os sets anteriores (o perdedor do tie-break leva o número pequeno, 7-6⁵), os games do set atual e os pontos.
/// Embaixo, uma aba de destaque pra ponto de ouro ou tie-break. Com a partida encerrada, sobram os sets.
/// Usado pelo <see cref="PlacarDeTvNode"/> na partida e pela <see cref="TelaDeFim"/>.
/// </summary>
public partial class QuadroDoPlacar : VBoxContainer
{
    private const float AlturaDaLinha = 40f;
    private static readonly Color CorDaBola = new("e3f24a");

    private sealed class Linha
    {
        public required Bolinha Saque { get; init; }
        public required Label Nome { get; init; }
        public List<(Label Games, Label TieBreak)> Sets { get; } = [];
        public Label? Games { get; set; }
        public PanelContainer? CaixaDosPontos { get; set; }
        public StyleBoxFlat? EstiloDosPontos { get; set; }
        public Label? Pontos { get; set; }
    }

    private StyleBoxFlat _estiloDoPainel = TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoProfundo, 0.94f), 6, 0, 0);
    private GridContainer _grade = null!;
    private PanelContainer _aba = null!;
    private StyleBoxFlat _estiloDaAba = null!;
    private Label _textoDaAba = null!;
    private Tween? _pulso;
    private readonly Linha[] _linhas = new Linha[2];
    private (int Sets, bool Encerrada)? _forma;
    private DadosDoPlacar? _ultimo;
    private DadosDoPlacar? _pendente;

    /// <summary>Cor de fundo do quadro (padrão: marinho profundo quase opaco, pra ler sobre a quadra).</summary>
    public Color Fundo
    {
        get => _estiloDoPainel.BgColor;
        set => _estiloDoPainel.BgColor = value;
    }

    public QuadroDoPlacar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 0);
    }

    public override void _Ready()
    {
        var painel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        painel.AddThemeStyleboxOverride("panel", _estiloDoPainel);
        var linha = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        linha.AddThemeConstantOverride("separation", 0);
        var acento = new Panel { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore };
        var estiloDoAcento = TemaPadelizou.Caixa(TemaPadelizou.Lima, 0, 0, 0);
        estiloDoAcento.CornerRadiusTopLeft = estiloDoAcento.CornerRadiusBottomLeft = 6;
        acento.AddThemeStyleboxOverride("panel", estiloDoAcento);
        _grade = new GridContainer { MouseFilter = MouseFilterEnum.Ignore };
        _grade.AddThemeConstantOverride("h_separation", 0);
        _grade.AddThemeConstantOverride("v_separation", 2);
        linha.AddChild(acento);
        linha.AddChild(_grade);
        painel.AddChild(linha);
        AddChild(painel);

        _aba = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _estiloDaAba = TemaPadelizou.Caixa(TemaPadelizou.Ouro, 0, 14, 3);
        _estiloDaAba.CornerRadiusBottomLeft = _estiloDaAba.CornerRadiusBottomRight = 6;
        _aba.AddThemeStyleboxOverride("panel", _estiloDaAba);
        _textoDaAba = new Label();
        _textoDaAba.AddThemeFontOverride("font", TemaPadelizou.Titulo);
        _textoDaAba.AddThemeFontSizeOverride("font_size", 14);
        _textoDaAba.AddThemeColorOverride("font_color", TemaPadelizou.Marinho);
        _aba.AddChild(_textoDaAba);
        AddChild(_aba);

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
        if (dados.MesmoConteudo(_ultimo)) return;

        var forma = (dados.SetsAnteriores.Count, dados.PartidaEncerrada);
        if (_forma != forma)
        {
            Montar(forma.Count, forma.PartidaEncerrada);
            _forma = forma;
            _ultimo = null;
        }

        int? vencedorDaPartida = dados.PartidaEncerrada ? VencedorPelosSets(dados.SetsAnteriores) : null;
        string[] nomes = [dados.DuplaCasa, dados.DuplaRivais];
        int[] games = [dados.GamesCasa, dados.GamesRivais];
        string[] pontos = [dados.PontosCasa, dados.PontosRivais];
        var corDosPontos = dados.PontoDecisivo ? TemaPadelizou.Ouro : dados.EmTieBreak ? TemaPadelizou.Branco : TemaPadelizou.Lima;

        for (int time = 0; time < 2; time++)
        {
            var l = _linhas[time];
            l.Saque.Acesa = !dados.PartidaEncerrada && dados.TimeQueSaca == time;
            l.Nome.Text = nomes[time].ToUpperInvariant();
            l.Nome.AddThemeColorOverride("font_color", vencedorDaPartida == time ? TemaPadelizou.Lima : TemaPadelizou.Branco);

            for (int s = 0; s < l.Sets.Count; s++)
            {
                var set = dados.SetsAnteriores[s];
                var (rotuloDosGames, rotuloDoTieBreak) = l.Sets[s];
                rotuloDosGames.Text = (time == 0 ? set.GamesCasa : set.GamesRivais).ToString();
                rotuloDosGames.AddThemeColorOverride("font_color", set.Vencedor == time ? TemaPadelizou.Branco : TemaPadelizou.TextoSuave);
                bool perdeuNoTieBreak = set.Vencedor is not null && set.Vencedor != time && set.TieBreakDoPerdedor is not null;
                rotuloDoTieBreak.Text = perdeuNoTieBreak ? set.TieBreakDoPerdedor.ToString() : "";
            }

            if (l.Games is not null) l.Games.Text = games[time].ToString();
            if (l.Pontos is not null && l.EstiloDosPontos is not null && l.CaixaDosPontos is not null)
            {
                bool mudou = _ultimo is not null && l.Pontos.Text != pontos[time];
                l.Pontos.Text = pontos[time];
                l.EstiloDosPontos.BgColor = corDosPontos;
                if (mudou) Piscar(l.CaixaDosPontos);
            }
        }

        AtualizarAba(dados);
        _ultimo = dados;
    }

    private static int? VencedorPelosSets(IReadOnlyList<SetAnterior> sets)
    {
        int casa = sets.Count(s => s.Vencedor == 0), rivais = sets.Count(s => s.Vencedor == 1);
        return casa > rivais ? 0 : rivais > casa ? 1 : null;
    }

    private void AtualizarAba(DadosDoPlacar dados)
    {
        string texto = dados.PartidaEncerrada ? "" : dados.PontoDecisivo ? "PONTO DE OURO" : dados.EmTieBreak ? "TIE-BREAK" : "";
        bool mostrar = texto.Length > 0;
        bool eraOuro = _aba.Visible && _textoDaAba.Text == "PONTO DE OURO";
        _aba.Visible = mostrar;
        _textoDaAba.Text = texto;
        _estiloDaAba.BgColor = dados.PontoDecisivo ? TemaPadelizou.Ouro : TemaPadelizou.Branco;

        bool ouro = mostrar && dados.PontoDecisivo;
        if (ouro && !eraOuro)
        {
            // Pulso lento enquanto durar o ponto de ouro: chama o olho sem piscar feito alarme.
            _pulso?.Kill();
            _aba.Modulate = Colors.White;
            _pulso = CreateTween().SetLoops();
            _pulso.TweenProperty(_aba, "modulate:a", 0.7f, 0.6f).SetTrans(Tween.TransitionType.Sine);
            _pulso.TweenProperty(_aba, "modulate:a", 1f, 0.6f).SetTrans(Tween.TransitionType.Sine);
        }
        else if (!ouro && _pulso is not null)
        {
            _pulso.Kill();
            _pulso = null;
            _aba.Modulate = Colors.White;
        }
    }

    private void Piscar(Control caixa)
    {
        caixa.SelfModulate = new Color(1.5f, 1.5f, 1.5f);
        CreateTween().TweenProperty(caixa, "self_modulate", Colors.White, 0.35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    private void Montar(int sets, bool encerrada)
    {
        foreach (var filho in _grade.GetChildren())
        {
            _grade.RemoveChild(filho);
            filho.QueueFree();
        }
        _grade.Columns = 2 + sets + (encerrada ? 0 : 2);
        // Encerrada, a última coluna é um set (com o expoente do tie-break), e não a caixa dos pontos: precisa de respiro.
        _estiloDoPainel.ContentMarginRight = encerrada ? 14 : 0;

        for (int time = 0; time < 2; time++)
        {
            var saque = new Bolinha { Raio = 5.5f, Cor = CorDaBola, Acesa = false, CustomMinimumSize = new Vector2(28, AlturaDaLinha) };
            var nome = new Label
            {
                CustomMinimumSize = new Vector2(210, AlturaDaLinha),
                VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText = true,
            };
            nome.AddThemeFontOverride("font", TemaPadelizou.Forte);
            nome.AddThemeFontSizeOverride("font_size", 19);
            var margemDoNome = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margemDoNome.AddThemeConstantOverride("margin_right", 14);
            margemDoNome.AddChild(nome);

            var linha = new Linha { Saque = saque, Nome = nome };
            _grade.AddChild(saque);
            _grade.AddChild(margemDoNome);

            for (int s = 0; s < sets; s++)
            {
                var celula = new HBoxContainer { CustomMinimumSize = new Vector2(36, AlturaDaLinha), Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
                celula.AddThemeConstantOverride("separation", 1);
                var rotuloDosGames = new Label { VerticalAlignment = VerticalAlignment.Center, SizeFlagsVertical = SizeFlags.ShrinkCenter };
                rotuloDosGames.AddThemeFontOverride("font", TemaPadelizou.Forte);
                rotuloDosGames.AddThemeFontSizeOverride("font_size", 21);
                var rotuloDoTieBreak = new Label { VerticalAlignment = VerticalAlignment.Top, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(0, 30) };
                rotuloDoTieBreak.AddThemeFontSizeOverride("font_size", 12);
                rotuloDoTieBreak.AddThemeColorOverride("font_color", TemaPadelizou.TextoSuave);
                celula.AddChild(rotuloDosGames);
                celula.AddChild(rotuloDoTieBreak);
                _grade.AddChild(celula);
                linha.Sets.Add((rotuloDosGames, rotuloDoTieBreak));
            }

            if (!encerrada)
            {
                var caixaDosGames = new PanelContainer { CustomMinimumSize = new Vector2(46, AlturaDaLinha), MouseFilter = MouseFilterEnum.Ignore };
                caixaDosGames.AddThemeStyleboxOverride("panel", TemaPadelizou.Caixa(TemaPadelizou.MarinhoClaro, 0, 0, 0));
                var rotuloDosGames = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                rotuloDosGames.AddThemeFontOverride("font", TemaPadelizou.Forte);
                rotuloDosGames.AddThemeFontSizeOverride("font_size", 22);
                caixaDosGames.AddChild(rotuloDosGames);
                _grade.AddChild(caixaDosGames);
                linha.Games = rotuloDosGames;

                // Só o canto de fora arredonda: o de cima na linha da casa, o de baixo na dos rivais.
                var estiloDosPontos = TemaPadelizou.Caixa(TemaPadelizou.Lima, 0, 0, 0);
                if (time == 0) estiloDosPontos.CornerRadiusTopRight = 6;
                else estiloDosPontos.CornerRadiusBottomRight = 6;
                var caixaDosPontos = new PanelContainer { CustomMinimumSize = new Vector2(64, AlturaDaLinha), MouseFilter = MouseFilterEnum.Ignore };
                caixaDosPontos.AddThemeStyleboxOverride("panel", estiloDosPontos);
                var rotuloDosPontos = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                rotuloDosPontos.AddThemeFontOverride("font", TemaPadelizou.Titulo);
                rotuloDosPontos.AddThemeFontSizeOverride("font_size", 22);
                rotuloDosPontos.AddThemeColorOverride("font_color", TemaPadelizou.Marinho);
                caixaDosPontos.AddChild(rotuloDosPontos);
                _grade.AddChild(caixaDosPontos);
                linha.CaixaDosPontos = caixaDosPontos;
                linha.EstiloDosPontos = estiloDosPontos;
                linha.Pontos = rotuloDosPontos;
            }
            _linhas[time] = linha;
        }
    }
}
