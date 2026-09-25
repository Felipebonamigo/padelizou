using Godot;
using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>
/// As opções da <see cref="Configuracao"/> num cartão: dificuldade, ponto de ouro, formato, golpe, mão, nome e volume.
/// Cada mudança vale na hora (o volume, inclusive, no áudio); Voltar ou Esc/B salvam em user:// e emitem
/// <see cref="Fechado"/>. Serve ao menu e, quando quem integra quiser, à pausa.
/// </summary>
public partial class PainelDeOpcoes : PanelContainer
{
    [Signal] public delegate void FechadoEventHandler();

    private static readonly Dificuldade[] Dificuldades = [Dificuldade.Facil, Dificuldade.Medio, Dificuldade.Dificil];
    private static readonly ModoDeGolpe[] Golpes = [ModoDeGolpe.Manual, ModoDeGolpe.Automatico];

    private readonly SeletorDeOpcao _dificuldade, _pontoDeOuro, _formato, _golpe, _mao, _volume;
    private readonly LineEdit _nome;
    private readonly Button _voltar;
    private readonly Label _descricao;

    public PainelDeOpcoes()
    {
        ThemeTypeVariation = TemaPadelizou.Cartao;
        Theme = TemaPadelizou.Tema;
        CustomMinimumSize = new Vector2(700, 0);

        // Compacto de propósito: sete linhas + título + rodapé cabem em 720 de altura (a base do projeto).
        AddThemeStyleboxOverride("panel", EstiloCompacto());
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 5);
        AddChild(coluna);
        coluna.AddChild(TemaPadelizou.Rotulo("Opções", TemaPadelizou.TituloDaTela));
        coluna.AddChild(TemaPadelizou.Espaco(4));

        _dificuldade = Seletor(coluna, "Dificuldade", ["Fácil", "Médio", "Difícil"],
            "Quão rápido e certeiro a IA joga — o parceiro e os rivais.",
            i => Configuracao.Dificuldade = Dificuldades[i]);
        _pontoDeOuro = Seletor(coluna, "Ponto de ouro", ["Ligado", "Desligado"],
            "Ligado: em 40-40 um ponto só decide o game, como no circuito profissional. Desligado: vantagem.",
            i => Configuracao.PontoDeOuro = i == 0);
        _formato = Seletor(coluna, "Formato", ["Set único", "Melhor de 3"],
            "Sets de 6 games com tie-break no 6-6.",
            i => Configuracao.SetsParaVencer = i + 1);
        _golpe = Seletor(coluna, "Golpe", ["Manual", "Assistido"],
            "Manual: você aperta na hora do contato e o timing decide a bola. Assistido: o jogador bate sozinho quando a bola chega.",
            i => Configuracao.ModoDeGolpe = Golpes[i]);
        _mao = Seletor(coluna, "Mão", ["Destro", "Canhoto"],
            "A mão da raquete do seu jogador: muda o lado do drive e do revés.",
            i => Configuracao.Destro = i == 0);

        // Nome: um campo de texto numa linha com a mesma cara das outras.
        var linhaDoNome = new PanelContainer();
        linhaDoNome.AddThemeStyleboxOverride("panel", TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoClaro, 0.55f), 8, 24, 3));
        var conteudoDoNome = new HBoxContainer();
        var rotuloDoNome = TemaPadelizou.Rotulo("Nome");
        rotuloDoNome.AddThemeFontOverride("font", TemaPadelizou.Forte);
        rotuloDoNome.AddThemeFontSizeOverride("font_size", 20);
        rotuloDoNome.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rotuloDoNome.VerticalAlignment = VerticalAlignment.Center;
        _nome = new LineEdit
        {
            Name = "Nome",
            MaxLength = Configuracao.TamanhoMaximoDoNome,
            PlaceholderText = Configuracao.NomePadrao,
            CustomMinimumSize = new Vector2(284, 40),
            Alignment = HorizontalAlignment.Center,
            SelectAllOnFocus = true,
        };
        _nome.TextChanged += texto => Configuracao.NomeDoJogador = texto;
        _nome.FocusExited += () => _nome.Text = Configuracao.NomeDoJogador;
        _nome.TextSubmitted += _ => _nome.FindNextValidFocus()?.GrabFocus();
        conteudoDoNome.AddChild(rotuloDoNome);
        conteudoDoNome.AddChild(_nome);
        linhaDoNome.AddChild(conteudoDoNome);
        coluna.AddChild(linhaDoNome);
        TemaPadelizou.FocoSegueMouse(_nome);
        TemaPadelizou.EditarComControle(_nome);

        _volume = Seletor(coluna, "Volume", Enumerable.Range(0, 11).Select(i => $"{i * 10}%").ToArray(),
            "Volume geral do jogo.",
            i =>
            {
                Configuracao.Volume = i / 10f;
                Configuracao.AplicarVolume();
            });
        _volume.Circular = false;
        _volume.MostrarBarra = true;

        // Rodapé do cartão: a ajuda da linha em foco e, ao lado, o Voltar.
        var rodape = new HBoxContainer();
        rodape.AddThemeConstantOverride("separation", 20);
        _descricao = TemaPadelizou.Rotulo("", TemaPadelizou.Descricao);
        _descricao.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descricao.CustomMinimumSize = new Vector2(0, 50);
        _descricao.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _descricao.VerticalAlignment = VerticalAlignment.Center;
        _voltar = new Button { Text = "Voltar", ThemeTypeVariation = TemaPadelizou.BotaoCompacto, CustomMinimumSize = new Vector2(170, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _voltar.Pressed += Fechar;
        TemaPadelizou.FocoSegueMouse(_voltar);
        rodape.AddChild(_descricao);
        rodape.AddChild(_voltar);
        coluna.AddChild(TemaPadelizou.Espaco(2));
        coluna.AddChild(rodape);

        Descrever(_nome, "Aparece no placar e, no online, pros outros jogadores.");
        Descrever(_voltar, "Salva e volta.");
        Sincronizar();
    }

    private static StyleBoxFlat EstiloCompacto()
    {
        var estilo = TemaPadelizou.Caixa(new Color(TemaPadelizou.MarinhoProfundo, 0.96f), 12, 28, 18);
        estilo.BorderColor = TemaPadelizou.Lima;
        estilo.BorderWidthTop = 5;
        return estilo;
    }

    private SeletorDeOpcao Seletor(VBoxContainer coluna, string rotulo, string[] valores, string descricao, Action<int> aoMudar)
    {
        var seletor = new SeletorDeOpcao { Name = rotulo };
        seletor.Definir(rotulo, valores, 0);
        seletor.ValorMudou += indice => aoMudar(indice);
        Descrever(seletor, descricao);
        coluna.AddChild(seletor);
        return seletor;
    }

    private void Descrever(Control controle, string texto) => controle.FocusEntered += () => _descricao.Text = texto;

    public override void _Ready() => TemaPadelizou.ConfigurarEntradaDaInterface();

    /// <summary>Puxa os valores atuais da Configuracao pros controles.</summary>
    public void Sincronizar()
    {
        _dificuldade.Selecionar(Array.IndexOf(Dificuldades, Configuracao.Dificuldade));
        _pontoDeOuro.Selecionar(Configuracao.PontoDeOuro ? 0 : 1);
        _formato.Selecionar(Configuracao.SetsParaVencer - 1);
        _golpe.Selecionar(Array.IndexOf(Golpes, Configuracao.ModoDeGolpe));
        _mao.Selecionar(Configuracao.Destro ? 0 : 1);
        _nome.Text = Configuracao.NomeDoJogador;
        _volume.Selecionar((int)MathF.Round(Configuracao.Volume * 10));
    }

    /// <summary>Mostra, sincroniza e põe o foco na primeira opção.</summary>
    public void Abrir()
    {
        Sincronizar();
        Show();
        _dificuldade.CallDeferred(Control.MethodName.GrabFocus);
    }

    public void Fechar()
    {
        Configuracao.NomeDoJogador = _nome.Text;
        Configuracao.Salvar();
        Hide();
        EmitSignal(SignalName.Fechado);
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!IsVisibleInTree() || !evento.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Fechar();
    }
}
