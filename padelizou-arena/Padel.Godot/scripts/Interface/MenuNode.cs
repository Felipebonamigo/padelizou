using System.Net;
using System.Net.Sockets;
using Godot;
using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>
/// Tela inicial: Jogar (contra a IA), Coop local, Online (criar sala / entrar numa sala), Opções, Sair.
/// Navega por teclado, controle (direcional, analógico, A/B) e mouse, com foco sempre visível; pensado pra
/// ler bem a 1280x800 (Steam Deck). Atrás, a quadra girando devagar.
/// Na primeira abertura lê user://configuracao.json e a linha de comando; se ela pedir partida direta
/// (--auto, --sair-apos, --host, --conectar) troca pra Partida.tscn sem montar nada — é o que o CI roda.
/// Argumentos só do menu: --tela principal|online|opcoes e --screenshot ARQ.png (salva e fecha).
/// </summary>
public partial class MenuNode : Node
{
    public const string CenaDaPartida = "res://cenas/Partida.tscn";
    private const float MargemLateral = 96f;

    private enum Tela { Principal, Online, Opcoes }

    /// <summary>A linha de comando vale uma vez por execução: voltar da partida pro menu não pode reentrar sozinho na sala.</summary>
    private static bool _primeiraAbertura = true;

    private Control _raiz = null!;
    private Control _principal = null!;
    private Control _online = null!;
    private Control _areaDasOpcoes = null!;
    private PainelDeOpcoes _opcoes = null!;
    private DicaDeControles _dicas = null!;
    private Label _descricao = null!;
    private Button _jogar = null!, _coop = null!, _botaoOnline = null!, _botaoOpcoes = null!, _sair = null!;
    private Button _criar = null!, _entrar = null!;
    private LineEdit _portaParaCriar = null!, _enderecoDaSala = null!, _portaDaSala = null!;
    private Label _erroAoCriar = null!, _erroAoEntrar = null!;
    private Tela _tela = Tela.Principal;
    private Tween? _transicao;
    private bool _saindo;
    private bool _pulouMenu;
    private Camera3D? _camera;
    private float _angulo = 0.55f;

    public override void _Ready()
    {
        bool primeira = _primeiraAbertura;
        _primeiraAbertura = false;
        string[] args = primeira ? OS.GetCmdlineUserArgs() : [];
        if (primeira)
        {
            Configuracao.Carregar();
            foreach (var aviso in Configuracao.LerLinhaDeComando(args)) GD.PushWarning(aviso);
        }
        Configuracao.AplicarVolume();

        if (primeira && Configuracao.PularMenu)
        {
            _pulouMenu = true;
            GD.Print($"Menu: a linha de comando pediu partida direta ({Configuracao.Modo}) — indo pra {CenaDaPartida}.");
            Callable.From(IrPraPartida).CallDeferred();
            return;
        }

        TemaPadelizou.ConfigurarEntradaDaInterface();
        AddChild(new QuadraNode { Name = "Quadra" });
        _camera = GetNodeOrNull<Camera3D>("Camera");
        MontarInterface();
        Mostrar(TelaPedida(args), animar: false);

        if (primeira && Configuracao.Screenshot is string arquivo) Captura.SalvarESair(this, arquivo);
    }

    private static Tela TelaPedida(string[] args)
    {
        int i = Array.IndexOf(args, "--tela");
        string? nome = i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        return nome switch
        {
            "online" => Tela.Online,
            "opcoes" => Tela.Opcoes,
            _ => Tela.Principal,
        };
    }

    public override void _Process(double delta)
    {
        // A quadra gira devagar atrás do menu: uma volta a cada ~3 minutos.
        if (_camera is null) return;
        _angulo += (float)delta * 0.035f;
        const float raio = 19f, altura = 7.5f;
        _camera.Position = new Vector3(Mathf.Sin(_angulo) * raio, altura, Mathf.Cos(_angulo) * raio);
        _camera.LookAt(new Vector3(0, 0.4f, 0), Vector3.Up);
    }

    // ---- Montagem ----

    private void MontarInterface()
    {
        var camada = new CanvasLayer { Name = "Interface" };
        AddChild(camada);
        _raiz = new Control { Theme = TemaPadelizou.Tema, MouseFilter = Control.MouseFilterEnum.Ignore };
        _raiz.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        camada.AddChild(_raiz);

        // Vinheta: marinho quase opaco à esquerda, onde mora o texto, abrindo pra quadra à direita.
        var gradiente = new Gradient();
        gradiente.SetColor(0, new Color(TemaPadelizou.MarinhoProfundo, 0.97f));
        gradiente.SetColor(1, new Color(TemaPadelizou.Marinho, 0.30f));
        gradiente.AddPoint(0.45f, new Color(TemaPadelizou.MarinhoProfundo, 0.88f));
        var vinheta = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradiente, Width = 256, Height = 4, FillFrom = Vector2.Zero, FillTo = new Vector2(1, 0) },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vinheta.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _raiz.AddChild(vinheta);

        _principal = MontarPrincipal();
        _online = MontarOnline();
        _areaDasOpcoes = MontarOpcoes();

        _dicas = new DicaDeControles();
        _dicas.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _dicas.GrowVertical = Control.GrowDirection.Begin;
        _dicas.OffsetLeft = MargemLateral;
        _dicas.OffsetTop = _dicas.OffsetBottom = -32;
        _raiz.AddChild(_dicas);

        var versao = TemaPadelizou.Rotulo(TextoDaVersao(), TemaPadelizou.Descricao, HorizontalAlignment.Right);
        versao.AddThemeFontSizeOverride("font_size", 14);
        versao.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        versao.GrowHorizontal = Control.GrowDirection.Begin;
        versao.GrowVertical = Control.GrowDirection.Begin;
        versao.OffsetRight = versao.OffsetLeft = -32;
        versao.OffsetTop = versao.OffsetBottom = -28;
        _raiz.AddChild(versao);
    }

    private static string TextoDaVersao()
    {
        var v = Engine.GetVersionInfo();
        return $"protótipo · Godot {v["major"]}.{v["minor"]}.{v["patch"]}";
    }

    private MarginContainer Coluna(float largura)
    {
        var margem = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margem.AnchorLeft = 0;
        margem.AnchorTop = 0;
        margem.AnchorBottom = 1;
        margem.AnchorRight = 0;
        margem.OffsetRight = MargemLateral + largura;
        margem.AddThemeConstantOverride("margin_left", (int)MargemLateral);
        margem.AddThemeConstantOverride("margin_top", 44);
        margem.AddThemeConstantOverride("margin_bottom", 72);
        _raiz.AddChild(margem);
        return margem;
    }

    private Control MontarPrincipal()
    {
        var margem = Coluna(560);
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 0);
        margem.AddChild(coluna);

        // As duas linhas do título bem juntas (separação negativa), como um logotipo.
        var titulo = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        titulo.AddThemeConstantOverride("separation", -26);
        titulo.AddChild(TemaPadelizou.Rotulo("PADELIZOU", TemaPadelizou.TituloDoJogo));
        var arena = TemaPadelizou.Rotulo("ARENA", TemaPadelizou.TituloDoJogo);
        arena.AddThemeColorOverride("font_color", TemaPadelizou.Lima);
        titulo.AddChild(arena);
        coluna.AddChild(titulo);
        coluna.AddChild(TemaPadelizou.Espaco(6));
        coluna.AddChild(new ColorRect { Color = TemaPadelizou.Lima, CustomMinimumSize = new Vector2(120, 6), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore });
        coluna.AddChild(TemaPadelizou.Espaco(12));
        coluna.AddChild(TemaPadelizou.Rotulo("Padel 2x2 com regras de verdade.", TemaPadelizou.Subtitulo));
        coluna.AddChild(TemaPadelizou.Espaco(26));

        var botoes = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, CustomMinimumSize = new Vector2(440, 0) };
        botoes.AddThemeConstantOverride("separation", 4);
        coluna.AddChild(botoes);
        _jogar = BotaoDoMenu(botoes, "Jogar", () => $"Você e um parceiro da IA contra uma dupla da IA.\n{ResumoDaPartida()}", () => Iniciar(ModoDeJogo.Local));
        _coop = BotaoDoMenu(botoes, "Coop local", () => $"Você e um amigo na mesma dupla, cada um com um controle.\nControles conectados agora: {Input.GetConnectedJoypads().Count}.", () => Iniciar(ModoDeJogo.CoopLocal));
        _botaoOnline = BotaoDoMenu(botoes, "Online", () => "Crie uma sala pra um amigo entrar pelo seu IP, ou entre na sala dele.", () => Mostrar(Tela.Online));
        _botaoOpcoes = BotaoDoMenu(botoes, "Opções", () => "Dificuldade, ponto de ouro, formato, golpe, mão, nome e volume.", () => Mostrar(Tela.Opcoes));
        _sair = BotaoDoMenu(botoes, "Sair", () => "Fecha o jogo.", () => GetTree().Quit());

        coluna.AddChild(TemaPadelizou.Espaco(16));
        _descricao = TemaPadelizou.Rotulo("", TemaPadelizou.Descricao);
        _descricao.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _descricao.CustomMinimumSize = new Vector2(520, 50);
        _descricao.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        coluna.AddChild(_descricao);
        return margem;
    }

    private Button BotaoDoMenu(VBoxContainer botoes, string texto, Func<string> descricao, Action aoApertar)
    {
        var botao = new Button { Text = texto, ThemeTypeVariation = TemaPadelizou.BotaoDoMenu, Alignment = HorizontalAlignment.Left };
        botao.Pressed += () =>
        {
            if (!_saindo) aoApertar();
        };
        botao.FocusEntered += () => _descricao.Text = descricao();
        TemaPadelizou.FocoSegueMouse(botao);
        botoes.AddChild(botao);
        return botao;
    }

    private static string ResumoDaPartida()
    {
        string dificuldade = Configuracao.Dificuldade switch
        {
            Dificuldade.Facil => "Fácil",
            Dificuldade.Dificil => "Difícil",
            _ => "Médio",
        };
        string formato = Configuracao.SetsParaVencer == 2 ? "melhor de 3" : "set único";
        string pontoDeOuro = Configuracao.PontoDeOuro ? "ponto de ouro" : "com vantagem";
        string golpe = Configuracao.ModoDeGolpe == ModoDeGolpe.Automatico ? "golpe assistido" : "golpe manual";
        return $"{dificuldade} · {formato} · {pontoDeOuro} · {golpe}";
    }

    private Control MontarOnline()
    {
        var margem = Coluna(880);
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 10);
        margem.AddChild(coluna);
        coluna.AddChild(TemaPadelizou.Rotulo("JOGAR COM AMIGOS", TemaPadelizou.Secao));
        coluna.AddChild(TemaPadelizou.Rotulo("Online", TemaPadelizou.TituloDaTela));
        coluna.AddChild(TemaPadelizou.Rotulo("Na rede de casa, ou pela internet com a porta liberada no roteador.", TemaPadelizou.Subtitulo));
        coluna.AddChild(TemaPadelizou.Espaco(4));

        var cartoes = new HBoxContainer();
        cartoes.AddThemeConstantOverride("separation", 24);
        coluna.AddChild(cartoes);

        // Criar sala
        var criar = Cartao(cartoes, "CRIAR SALA", "Você hospeda a partida; os outros entram pelo seu IP.");
        if (IpNaRede() is string ip)
        {
            var seuIp = TemaPadelizou.Rotulo($"Seu IP na rede: {ip}");
            seuIp.AddThemeFontOverride("font", TemaPadelizou.Forte);
            seuIp.AddThemeColorOverride("font_color", TemaPadelizou.Lima);
            criar.AddChild(seuIp);
        }
        _portaParaCriar = Campo(Linha(criar), "Porta", Configuracao.PortaParaCriar.ToString(), "7777", 120);
        _erroAoCriar = TemaPadelizou.Rotulo("", TemaPadelizou.Erro);
        _erroAoCriar.Visible = false;
        criar.AddChild(_erroAoCriar);
        criar.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        _criar = BotaoDoCartao(criar, "Criar sala", CriarSala);
        _portaParaCriar.TextSubmitted += _ => CriarSala();

        // Entrar numa sala
        var entrar = Cartao(cartoes, "ENTRAR NUMA SALA", "Digite o IP e a porta de quem criou a sala.");
        var enderecoEPorta = Linha(entrar);
        _enderecoDaSala = Campo(enderecoEPorta, "Endereço (IP)", Configuracao.EnderecoDaSala, "192.168.0.12", 230);
        _portaDaSala = Campo(enderecoEPorta, "Porta", Configuracao.PortaDaSala.ToString(), "7777", 110);
        _erroAoEntrar = TemaPadelizou.Rotulo("", TemaPadelizou.Erro);
        _erroAoEntrar.Visible = false;
        entrar.AddChild(_erroAoEntrar);
        entrar.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        _entrar = BotaoDoCartao(entrar, "Entrar", EntrarNaSala);
        _enderecoDaSala.TextSubmitted += _ => _portaDaSala.GrabFocus();
        _portaDaSala.TextSubmitted += _ => EntrarNaSala();

        coluna.AddChild(TemaPadelizou.Espaco(8));
        var voltar = new Button { Text = "Voltar", ThemeTypeVariation = TemaPadelizou.BotaoCompacto, SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, CustomMinimumSize = new Vector2(200, 0) };
        voltar.Pressed += () => Mostrar(Tela.Principal);
        TemaPadelizou.FocoSegueMouse(voltar);
        coluna.AddChild(voltar);
        return margem;
    }

    private static VBoxContainer Cartao(HBoxContainer cartoes, string titulo, string texto)
    {
        var cartao = new PanelContainer { ThemeTypeVariation = TemaPadelizou.Cartao, CustomMinimumSize = new Vector2(420, 0) };
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 10);
        cartao.AddChild(coluna);
        coluna.AddChild(TemaPadelizou.Rotulo(titulo, TemaPadelizou.Secao));
        var descricao = TemaPadelizou.Rotulo(texto, TemaPadelizou.Descricao);
        descricao.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        coluna.AddChild(descricao);
        cartoes.AddChild(cartao);
        return coluna;
    }

    private static HBoxContainer Linha(VBoxContainer coluna)
    {
        var linha = new HBoxContainer();
        linha.AddThemeConstantOverride("separation", 14);
        coluna.AddChild(linha);
        return linha;
    }

    /// <summary>Rótulo pequeno em cima de um campo de texto, os dois numa coluninha dentro da linha.</summary>
    private static LineEdit Campo(HBoxContainer linha, string rotulo, string valor, string exemplo, float largura)
    {
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 4);
        linha.AddChild(coluna);
        var nome = TemaPadelizou.Rotulo(rotulo, TemaPadelizou.Descricao);
        nome.AddThemeFontSizeOverride("font_size", 16);
        coluna.AddChild(nome);
        var campo = new LineEdit
        {
            Text = valor,
            PlaceholderText = exemplo,
            CustomMinimumSize = new Vector2(largura, 46),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SelectAllOnFocus = true,
        };
        TemaPadelizou.FocoSegueMouse(campo);
        TemaPadelizou.EditarComControle(campo);
        coluna.AddChild(campo);
        return campo;
    }

    private static Button BotaoDoCartao(VBoxContainer coluna, string texto, Action aoApertar)
    {
        var botao = new Button { Text = texto, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        botao.AddThemeFontSizeOverride("font_size", 24);
        botao.Pressed += aoApertar;
        TemaPadelizou.FocoSegueMouse(botao);
        coluna.AddChild(botao);
        return botao;
    }

    /// <summary>O primeiro IPv4 privado da máquina (192.168.x, 10.x, 172.16–31.x) — o que o amigo digita.</summary>
    private static string? IpNaRede() =>
        IP.GetLocalAddresses().FirstOrDefault(texto =>
            IPAddress.TryParse(texto, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
            && ip.GetAddressBytes() is var b
            && (b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)));

    private Control MontarOpcoes()
    {
        var margem = Coluna(720);
        margem.AddThemeConstantOverride("margin_top", 40);
        _opcoes = new PainelDeOpcoes { SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
        _opcoes.Fechado += () => Mostrar(Tela.Principal, foco: _botaoOpcoes);
        margem.AddChild(_opcoes);
        return margem;
    }

    // ---- Navegação ----

    private void Mostrar(Tela tela, bool animar = true, Control? foco = null)
    {
        _tela = tela;
        _principal.Visible = tela == Tela.Principal;
        _online.Visible = tela == Tela.Online;
        _areaDasOpcoes.Visible = tela == Tela.Opcoes;
        var ativa = tela switch
        {
            Tela.Online => _online,
            Tela.Opcoes => _areaDasOpcoes,
            _ => _principal,
        };

        _transicao?.Kill();
        if (animar)
        {
            ativa.Modulate = new Color(1, 1, 1, 0);
            _transicao = CreateTween();
            _transicao.TweenProperty(ativa, "modulate:a", 1f, 0.16f);
        }
        else ativa.Modulate = Colors.White;

        switch (tela)
        {
            case Tela.Principal:
                _dicas.Definir(new Dica("Setas", "Direcional", "navegar"), new Dica("Enter", "A", "escolher"));
                (foco ?? _jogar).CallDeferred(Control.MethodName.GrabFocus);
                break;
            case Tela.Online:
                _erroAoCriar.Visible = _erroAoEntrar.Visible = false;
                _dicas.Definir(new Dica("Setas", "Direcional", "navegar"), new Dica("Enter", "A", "escolher"), new Dica("Esc", "B", "voltar"));
                _criar.CallDeferred(Control.MethodName.GrabFocus);
                break;
            case Tela.Opcoes:
                _dicas.Definir(new Dica("Setas", "Direcional", "navegar"), new Dica("Esq./Dir.", "Esq./Dir.", "mudar"), new Dica("Esc", "B", "salvar e voltar"));
                _opcoes.Abrir();
                break;
        }
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (_saindo || !evento.IsActionPressed("ui_cancel")) return;
        switch (_tela)
        {
            case Tela.Online:
                GetViewport().SetInputAsHandled();
                Mostrar(Tela.Principal, foco: _botaoOnline);
                break;
            case Tela.Principal:
                // Esc/B na tela inicial leva o foco pro Sair — fechar direto seria fácil demais de apertar sem querer.
                GetViewport().SetInputAsHandled();
                _sair.GrabFocus();
                break;
        }
    }

    private void CriarSala()
    {
        if (!Configuracao.TentarLerPorta(_portaParaCriar.Text, out int porta))
        {
            Avisar(_erroAoCriar, "A porta vai de 1 a 65535.");
            _portaParaCriar.GrabFocus();
            return;
        }
        Configuracao.PortaParaCriar = porta;
        Iniciar(ModoDeJogo.CriarSala);
    }

    private void EntrarNaSala()
    {
        string texto = _enderecoDaSala.Text.Trim();
        string host;
        int porta;
        if (Configuracao.EnderecoValido(texto))
        {
            host = texto;
            if (!Configuracao.TentarLerPorta(_portaDaSala.Text, out porta))
            {
                Avisar(_erroAoEntrar, "A porta vai de 1 a 65535.");
                _portaDaSala.GrabFocus();
                return;
            }
        }
        else if (!Configuracao.TentarLerEndereco(texto, out host, out porta))   // aceita "IP:porta" colado no campo
        {
            Avisar(_erroAoEntrar, "Digite o IP (ex.: 192.168.0.12) ou o nome da máquina.");
            _enderecoDaSala.GrabFocus();
            return;
        }
        Configuracao.EnderecoDaSala = host;
        Configuracao.PortaDaSala = porta;
        Iniciar(ModoDeJogo.EntrarNaSala);
    }

    private static void Avisar(Label erro, string texto)
    {
        erro.Text = texto;
        erro.Visible = true;
    }

    private void Iniciar(ModoDeJogo modo)
    {
        if (_saindo) return;
        _saindo = true;
        Configuracao.Modo = modo;
        Configuracao.Salvar();
        GD.Print($"Menu: {modo} — indo pra {CenaDaPartida}.");

        // Cortina marinho: esconde a troca de cena e segura clique/tecla durante ela.
        var cortina = new ColorRect { Color = TemaPadelizou.MarinhoProfundo, Modulate = new Color(1, 1, 1, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        cortina.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _raiz.AddChild(cortina);
        _raiz.GetViewport().GuiReleaseFocus();
        var tween = CreateTween();
        tween.TweenProperty(cortina, "modulate:a", 1f, 0.22f);
        tween.TweenCallback(Callable.From(IrPraPartida));
    }

    private void IrPraPartida()
    {
        var erro = GetTree().ChangeSceneToFile(CenaDaPartida);
        if (erro == Error.Ok) return;
        GD.PushError($"Menu: não consegui abrir {CenaDaPartida}: {erro}");
        if (_pulouMenu) GetTree().Quit(1);   // sem tela (CI), travar esperando ninguém é pior que falhar
        else _saindo = false;
    }
}
