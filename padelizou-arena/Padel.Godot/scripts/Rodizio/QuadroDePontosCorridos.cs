using System.Globalization;
using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// O placar da partida de pontos corridos (Americano/Mexicano), no mesmo desenho de transmissão do
/// <see cref="QuadroDoPlacar"/>: uma linha por dupla com a marca de saque, o nome e os pontos em número — sem games nem
/// sets, que esse jogo não tem. Embaixo, a aba diz quanto do jogo já foi ("22 DE 24"), fica dourada no último ponto e,
/// no fim, diz o empate. Usado pelo <see cref="PlacarDeTvNode"/> na partida e pela <see cref="TelaDeFim"/>.
/// </summary>
/// <remarks>
/// Um quadro à parte, e não um modo do <see cref="QuadroDoPlacar"/>: lá a grade inteira (sets, games, a caixa de pontos
/// de tênis) é o que muda, e aqui sobra só uma coluna. Monta tudo no construtor, então pode receber o placar antes de
/// entrar na árvore.
/// </remarks>
public partial class QuadroDePontosCorridos : VBoxContainer
{
    private const float AlturaDaLinha = 40f;
    private static readonly Color CorDaBola = new("e3f24a");

    private readonly StyleBoxFlat _estiloDoPainel = TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoProfundo, 0.94f), 6, 0, 0);
    private readonly Bolinha[] _saque = new Bolinha[2];
    private readonly Label[] _nomes = new Label[2];
    private readonly PanelContainer[] _caixas = new PanelContainer[2];
    private readonly StyleBoxFlat[] _estilos = new StyleBoxFlat[2];
    private readonly Label[] _pontos = new Label[2];
    private readonly StyleBoxFlat _estiloDaAba;
    private readonly Label _textoDaAba;
    private DadosDoPlacar? _ultimo;

    /// <summary>Cor de fundo do quadro (padrão: marinho profundo quase opaco, pra ler sobre a quadra).</summary>
    public Color Fundo
    {
        get => _estiloDoPainel.BgColor;
        set => _estiloDoPainel.BgColor = value;
    }

    public QuadroDePontosCorridos()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 0);

        var painel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        painel.AddThemeStyleboxOverride("panel", _estiloDoPainel);
        var linha = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        linha.AddThemeConstantOverride("separation", 0);
        var acento = new Panel { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore };
        var estiloDoAcento = TemaPadelizou.Caixa(TemaPadelizou.Lima, 0, 0, 0);
        estiloDoAcento.CornerRadiusTopLeft = estiloDoAcento.CornerRadiusBottomLeft = 6;
        acento.AddThemeStyleboxOverride("panel", estiloDoAcento);
        var grade = new GridContainer { Columns = 3, MouseFilter = MouseFilterEnum.Ignore };
        grade.AddThemeConstantOverride("h_separation", 0);
        grade.AddThemeConstantOverride("v_separation", 2);
        linha.AddChild(acento);
        linha.AddChild(grade);
        painel.AddChild(linha);
        AddChild(painel);

        for (int time = 0; time < 2; time++)
        {
            _saque[time] = new Bolinha { Raio = 5.5f, Cor = CorDaBola, Acesa = false, CustomMinimumSize = new Vector2(28, AlturaDaLinha) };
            _nomes[time] = new Label
            {
                CustomMinimumSize = new Vector2(230, AlturaDaLinha),
                VerticalAlignment = VerticalAlignment.Center,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ClipText = true,
            };
            _nomes[time].AddThemeFontOverride("font", TemaPadelizou.Forte);
            _nomes[time].AddThemeFontSizeOverride("font_size", 19);
            var margemDoNome = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margemDoNome.AddThemeConstantOverride("margin_right", 14);
            margemDoNome.AddChild(_nomes[time]);

            // Só o canto de fora arredonda: o de cima na linha da casa, o de baixo na dos rivais.
            _estilos[time] = TemaPadelizou.Caixa(TemaPadelizou.Lima, 0, 0, 0);
            if (time == 0) _estilos[time].CornerRadiusTopRight = 6;
            else _estilos[time].CornerRadiusBottomRight = 6;
            _caixas[time] = new PanelContainer { CustomMinimumSize = new Vector2(72, AlturaDaLinha), MouseFilter = MouseFilterEnum.Ignore };
            _caixas[time].AddThemeStyleboxOverride("panel", _estilos[time]);
            _pontos[time] = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _pontos[time].AddThemeFontOverride("font", TemaPadelizou.Titulo);
            _pontos[time].AddThemeFontSizeOverride("font_size", 24);
            _pontos[time].AddThemeColorOverride("font_color", TemaPadelizou.Marinho);
            _caixas[time].AddChild(_pontos[time]);

            grade.AddChild(_saque[time]);
            grade.AddChild(margemDoNome);
            grade.AddChild(_caixas[time]);
        }

        var aba = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _estiloDaAba = TemaPadelizou.Caixa(TemaPadelizou.Branco, 0, 14, 3);
        _estiloDaAba.CornerRadiusBottomLeft = _estiloDaAba.CornerRadiusBottomRight = 6;
        aba.AddThemeStyleboxOverride("panel", _estiloDaAba);
        _textoDaAba = new Label();
        _textoDaAba.AddThemeFontOverride("font", TemaPadelizou.Titulo);
        _textoDaAba.AddThemeFontSizeOverride("font_size", 14);
        _textoDaAba.AddThemeColorOverride("font_color", TemaPadelizou.Marinho);
        aba.AddChild(_textoDaAba);
        AddChild(aba);
    }

    /// <summary>
    /// Mostra o placar. Os pontos vêm como texto (o <see cref="DadosDoPlacar"/> é só de primitivos) e, nos pontos
    /// corridos, são números: é deles que saem a soma da aba e quem venceu. Pode ser chamado todo quadro.
    /// </summary>
    public void Atualizar(DadosDoPlacar dados)
    {
        if (dados.MesmoConteudo(_ultimo)) return;
        int total = dados.PontosCorridos ?? 0;
        string[] nomes = [dados.DuplaCasa, dados.DuplaRivais];
        string[] pontos = [dados.PontosCasa, dados.PontosRivais];
        bool numeros = int.TryParse(dados.PontosCasa, NumberStyles.None, CultureInfo.InvariantCulture, out int casa)
            & int.TryParse(dados.PontosRivais, NumberStyles.None, CultureInfo.InvariantCulture, out int rivais);
        bool encerrada = dados.PartidaEncerrada;
        int? vencedor = encerrada && numeros && casa != rivais ? (casa > rivais ? 0 : 1) : null;
        bool empate = encerrada && numeros && casa == rivais;

        for (int time = 0; time < 2; time++)
        {
            _saque[time].Acesa = !encerrada && dados.TimeQueSaca == time;
            _nomes[time].Text = nomes[time].ToUpperInvariant();
            _nomes[time].AddThemeColorOverride("font_color", vencedor == time ? TemaPadelizou.Lima : TemaPadelizou.Branco);
            bool mudou = _ultimo is not null && _pontos[time].Text != pontos[time];
            _pontos[time].Text = pontos[time];
            // No fim: quem venceu fica com a caixa lima, quem perdeu apaga; no empate, as duas douradas.
            bool apagada = vencedor is int v && v != time;
            _estilos[time].BgColor = empate ? TemaPadelizou.Ouro : apagada ? TemaPadelizou.MarinhoClaro : TemaPadelizou.Lima;
            _pontos[time].AddThemeColorOverride("font_color", apagada ? TemaPadelizou.Branco : TemaPadelizou.Marinho);
            if (mudou && IsInsideTree()) Piscar(_caixas[time]);   // o Tween precisa da árvore
        }

        int soma = casa + rivais;
        bool ultimoPonto = !encerrada && numeros && soma == total - 1;
        _textoDaAba.Text = encerrada
            ? (empate ? "EMPATE" : $"FIM · {total} PONTOS CORRIDOS")
            : numeros
                ? $"{(ultimoPonto ? "ÚLTIMO PONTO" : "PONTOS CORRIDOS")} · {soma} DE {total}"
                : $"PONTOS CORRIDOS · DE {total}";
        _estiloDaAba.BgColor = empate || ultimoPonto ? TemaPadelizou.Ouro : TemaPadelizou.Branco;
        _ultimo = dados;
    }

    private void Piscar(Control caixa)
    {
        caixa.SelfModulate = new Color(1.5f, 1.5f, 1.5f);
        CreateTween().TweenProperty(caixa, "self_modulate", Colors.White, 0.35f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
