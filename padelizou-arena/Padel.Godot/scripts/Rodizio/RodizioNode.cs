using System.Text;
using Godot;
using Padel.Core;
using Padel.Core.Rodizio;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>
/// A tela do Americano e do Mexicano: cria o evento (formato, de 4 a 16 jogadores, 16/24/32 pontos corridos), mostra a
/// rodada atual (quadras, duplas, quem folga), a partida do humano, a classificação individual e os resultados, e anda:
/// Jogar (a partida de verdade, de pontos corridos, contra a dupla rival), Simular os outros jogos da rodada, Próxima
/// rodada, Menu. Tudo o que é regra (tabela, Mexicano, classificação, folga) vem do Padel.Core.Rodizio.
/// Argumentos: --rodizio ARQ (o evento noutro arquivo) e --rodizio-sozinho (o humano simulado joga e a tela anda
/// sozinha até o fim do evento — de teste, e exige --rodizio ARQ).
/// </summary>
public partial class RodizioNode : Control
{
    private const string VerdeLima = "#a3d827";
    private const string CinzaSuave = "#a9b4cc";

    private readonly Label _titulo;
    private readonly Label _detalhe;
    private readonly Label _minhaPartida;
    /// <summary>O que houve com o arquivo, ou com a última ação.</summary>
    private readonly Label _aviso;
    private readonly Control _formulario;
    private readonly SeletorDeOpcao _formato, _jogadores, _pontos;
    private readonly Label _explicacao;
    private readonly Control _evento;
    private readonly RichTextLabel _rodada;
    private readonly Label _tituloDaClassificacao;
    private readonly RichTextLabel _classificacao;
    private readonly Button _principal, _simular, _proxima, _menu;
    private Action? _acaoPrincipal;
    /// <summary>A última abertura do arquivo, pra tela dizer o que houve (ilegível guardado, versão mais nova, arquivo preso).</summary>
    private CargaDoRodizio? _carga;
    /// <summary>"Novo evento" no fim: o formulário, sem mexer no evento acabado até o "Criar evento".</summary>
    private bool _criando;
    /// <summary>O que a última ação não conseguiu fazer (rodada que não andou, partida que não abriu).</summary>
    private string? _problema;
    /// <summary>O automático já pediu pra fechar o jogo: nada mais é apertado.</summary>
    private bool _saindo;
    private static bool _argumentosLidos;

    /// <summary>A troca de cena de verdade. A conferência troca <see cref="TrocarDeCena"/> pra ver o pedido sem sair da cena dela.</summary>
    internal static Error TrocarDeCenaDeVerdade(SceneTree arvore, string cena) => arvore.ChangeSceneToFile(cena);
    internal static Func<SceneTree, string, Error> TrocarDeCena { get; set; } = TrocarDeCenaDeVerdade;

    /// <summary>O placar de um jogo na tela: pontos da DuplaA – pontos da DuplaB.</summary>
    internal static string TextoDoPlacar(int a, int b) => $"{a} – {b}";

    /// <summary>
    /// A tela inteira nasce aqui, e não no _Ready: os nós já são filhos quando a cena é instanciada, então saem junto com
    /// ela mesmo que ela nunca entre na árvore (instanciar e liberar deixava órfão o que fosse criado fora dela).
    /// </summary>
    public RodizioNode()
    {
        Theme = TemaPadelizou.Tema;
        SetAnchorsPreset(LayoutPreset.FullRect);
        var fundo = new ColorRect { Color = TemaPadelizou.MarinhoProfundo, MouseFilter = MouseFilterEnum.Ignore };
        fundo.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(fundo);
        var margem = new MarginContainer();
        margem.SetAnchorsPreset(LayoutPreset.FullRect);
        margem.AddThemeConstantOverride("margin_left", 56);
        margem.AddThemeConstantOverride("margin_right", 56);
        margem.AddThemeConstantOverride("margin_top", 30);
        margem.AddThemeConstantOverride("margin_bottom", 26);
        AddChild(margem);
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 8);
        margem.AddChild(coluna);

        coluna.AddChild(TemaPadelizou.Rotulo("AMERICANO E MEXICANO", TemaPadelizou.Secao));
        _titulo = TemaPadelizou.Rotulo("", TemaPadelizou.TituloDaTela);
        _titulo.AddThemeFontSizeOverride("font_size", 34);
        _detalhe = TemaPadelizou.Rotulo("", TemaPadelizou.Subtitulo);
        _detalhe.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _minhaPartida = TemaPadelizou.Rotulo("");
        _minhaPartida.AddThemeFontOverride("font", TemaPadelizou.Forte);
        _minhaPartida.AddThemeColorOverride("font_color", TemaPadelizou.Lima);
        _minhaPartida.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _aviso = TemaPadelizou.Rotulo("");
        _aviso.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _aviso.AddThemeFontSizeOverride("font_size", 18);
        _aviso.AddThemeColorOverride("font_color", TemaPadelizou.Alerta);
        _aviso.Visible = false;
        coluna.AddChild(_titulo);
        coluna.AddChild(_detalhe);
        coluna.AddChild(_minhaPartida);
        coluna.AddChild(_aviso);
        coluna.AddChild(TemaPadelizou.Espaco(2));

        var corpo = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        coluna.AddChild(corpo);

        // O formulário do evento novo.
        var cartao = new PanelContainer { ThemeTypeVariation = TemaPadelizou.Cartao, CustomMinimumSize = new Vector2(720, 0) };
        cartao.SetAnchorsPreset(LayoutPreset.TopLeft);
        var campos = new VBoxContainer();
        campos.AddThemeConstantOverride("separation", 10);
        cartao.AddChild(campos);
        campos.AddChild(TemaPadelizou.Rotulo("NOVO EVENTO", TemaPadelizou.Secao));
        _formato = Seletor(campos, "Formato", "Formato", ["Americano", "Mexicano"], 0);
        _jogadores = Seletor(campos, "Jogadores", "Jogadores (você é um)",
            Enumerable.Range(TabelaDoAmericano.MinimoDeJogadores, TabelaDoAmericano.MaximoDeJogadores - TabelaDoAmericano.MinimoDeJogadores + 1)
                .Select(n => n.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            8 - TabelaDoAmericano.MinimoDeJogadores);
        _jogadores.Circular = false;
        _pontos = Seletor(campos, "Pontos", "Pontos por jogo", EstadoDoRodizio.PontosOferecidos.Select(p => $"{p} corridos").ToArray(), 1);
        _explicacao = TemaPadelizou.Rotulo("", TemaPadelizou.Descricao);
        _explicacao.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _explicacao.CustomMinimumSize = new Vector2(0, 110);
        campos.AddChild(_explicacao);
        _formulario = cartao;
        corpo.AddChild(_formulario);

        // O evento em andamento: a rodada e os resultados à esquerda, a classificação à direita.
        var linhas = new HBoxContainer();
        linhas.SetAnchorsPreset(LayoutPreset.FullRect);
        linhas.AddThemeConstantOverride("separation", 24);
        var esquerda = Painel(linhas, out var colunaDaRodada);
        esquerda.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rodada = Texto();
        colunaDaRodada.AddChild(_rodada);
        var direita = Painel(linhas, out var colunaDaClassificacao);
        direita.CustomMinimumSize = new Vector2(470, 0);
        _tituloDaClassificacao = TemaPadelizou.Rotulo("CLASSIFICAÇÃO", TemaPadelizou.Secao);
        colunaDaClassificacao.AddChild(_tituloDaClassificacao);
        _classificacao = Texto();
        colunaDaClassificacao.AddChild(_classificacao);
        _evento = linhas;
        corpo.AddChild(_evento);

        var botoes = new HBoxContainer();
        botoes.AddThemeConstantOverride("separation", 14);
        coluna.AddChild(botoes);
        _principal = Botao(botoes, "Jogar", () => _acaoPrincipal?.Invoke());
        _simular = Botao(botoes, "Simular os outros jogos da rodada", Simular);
        _proxima = Botao(botoes, "Próxima rodada", ProximaRodada);
        _menu = Botao(botoes, "Menu", () => IrPara(PartidaNode.CenaDoMenu));
        var dicas = new DicaDeControles { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.End };
        dicas.Definir(new Dica("Setas", "Direcional", "navegar"), new Dica("Enter", "A", "escolher"));
        botoes.AddChild(dicas);
    }

    public override void _Ready()
    {
        if (!_argumentosLidos)
        {
            _argumentosLidos = true;
            var args = OS.GetCmdlineUserArgs();
            Configuracao.LerLinhaDeComando(args);
            PerfilLocal.LerLinhaDeComando(args);
            EstadoDoRodizio.LerLinhaDeComando(args);
            if (args.Contains("--rodizio-sozinho"))
            {
                if (EstadoDoRodizio.CaminhoPedido is null)
                {
                    // De teste: sem arquivo próprio, jogaria (e gravaria) o evento de quem joga no user://.
                    GD.PrintErr("Rodízio: --rodizio-sozinho é de teste e exige --rodizio ARQ (o evento de teste noutro arquivo); "
                        + $"sem ele, jogaria o evento de verdade em {EstadoDoRodizio.Arquivo}. Nada foi tocado.");
                    GetTree().Quit(1);
                    return;
                }
                EstadoDoRodizio.Automatico = true;
            }
        }
        TemaPadelizou.ConfigurarEntradaDaInterface();
        if (EstadoDoRodizio.Atual is null) _carga = EstadoDoRodizio.Carregar();
        Atualizar();
        if (EstadoDoRodizio.Automatico) Callable.From(ApertarSozinho).CallDeferred();
        else if (Configuracao.Screenshot is string arquivo) Captura.SalvarESair(this, arquivo);
    }

    // ---- Montagem ----

    private SeletorDeOpcao Seletor(VBoxContainer coluna, string nome, string rotulo, string[] valores, int indice)
    {
        var seletor = new SeletorDeOpcao { Name = nome };
        seletor.Definir(rotulo, valores, indice);
        seletor.ValorMudou += _ => Explicar();
        coluna.AddChild(seletor);
        return seletor;
    }

    /// <summary>Um cartão no estilo do tema (filete lima em cima), com margem menor que a do <see cref="TemaPadelizou.Cartao"/>: a tabela de 16 precisa caber.</summary>
    private static PanelContainer Painel(HBoxContainer linha, out VBoxContainer coluna)
    {
        var painel = new PanelContainer();
        var estilo = TemaPadelizou.Caixa(new Color(TemaPadelizou.Marinho, 0.9f), 12, 22, 14);
        estilo.BorderColor = TemaPadelizou.Lima;
        estilo.BorderWidthTop = 4;
        painel.AddThemeStyleboxOverride("panel", estilo);
        coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 6);
        painel.AddChild(coluna);
        linha.AddChild(painel);
        return painel;
    }

    private static RichTextLabel Texto()
    {
        var t = new RichTextLabel { BbcodeEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ScrollFollowing = false, FocusMode = FocusModeEnum.None };
        t.AddThemeFontSizeOverride("normal_font_size", 18);
        t.AddThemeFontSizeOverride("bold_font_size", 18);
        t.AddThemeFontOverride("bold_font", TemaPadelizou.Forte);
        t.AddThemeColorOverride("default_color", TemaPadelizou.Branco);
        t.AddThemeConstantOverride("table_h_separation", 16);
        t.AddThemeConstantOverride("table_v_separation", 5);
        return t;
    }

    private static Button Botao(HBoxContainer linha, string texto, Action aoApertar)
    {
        var b = new Button { Text = texto, CustomMinimumSize = new Vector2(150, 52), FocusMode = FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.Pressed += aoApertar;
        TemaPadelizou.FocoSegueMouse(b);
        linha.AddChild(b);
        return b;
    }

    // ---- Estado da tela ----

    private void Atualizar()
    {
        var rodizio = EstadoDoRodizio.Atual;
        MostrarAvisos();
        if (rodizio is null && _carga is { PodeCriarOutro: false })
        {
            MostrarQueNaoAbriu();
            return;
        }
        if (rodizio is null || _criando)
        {
            MostrarFormulario();
            return;
        }
        MostrarEvento(rodizio);
    }

    private void MostrarAvisos()
    {
        var avisos = new List<string>();
        if (_carga?.Mensagem is string doArquivo && _carga.PodeCriarOutro) avisos.Add(doArquivo);
        if (EstadoDoRodizio.ErroAoSalvar is string erro) avisos.Add($"Não deu pra salvar o evento ({erro}): o que mudou aqui se perde ao fechar o jogo.");
        if (_problema is string problema) avisos.Add(problema);
        _aviso.Text = string.Join("\n", avisos);
        _aviso.Visible = avisos.Count > 0;
    }

    private void MostrarFormulario()
    {
        _formulario.Visible = true;
        _evento.Visible = false;
        _titulo.Text = "Novo evento";
        _detalhe.Text = "Cada um joga por si: a dupla muda a cada rodada e os pontos que ela faz vão pros dois. Você é um dos jogadores.";
        _minhaPartida.Visible = false;
        _simular.Visible = _proxima.Visible = false;
        Explicar();
        Principal("Criar evento", Criar);
    }

    /// <summary>O que o evento escolhido vai ser: rodadas, quadras e folgas, tirado da tabela do Core.</summary>
    private void Explicar()
    {
        int jogadores = TabelaDoAmericano.MinimoDeJogadores + _jogadores.Indice;
        int rodadas = TabelaDoAmericano.RodadasPadrao(jogadores);
        int quadras = jogadores / 4, folgam = jogadores % 4;
        int pontos = EstadoDoRodizio.PontosOferecidos[_pontos.Indice];
        string formato = _formato.Indice == 1
            ? "Mexicano: a 1ª rodada é sorteada; da 2ª em diante, em cada grupo de 4 da classificação, o 1º e o 4º jogam contra o 2º e o 3º."
            : "Americano: a tabela inteira sai antes do primeiro jogo, e ninguém repete parceiro.";
        string folga = folgam == 0 ? "ninguém folga" : $"{folgam} folga{(folgam > 1 ? "m" : "")} por rodada e soma{(folgam > 1 ? "m" : "")} a média dos próprios pontos";
        _explicacao.Text = $"{formato}\n{rodadas} rodadas, {quadras} quadra{(quadras > 1 ? "s" : "")}, {folga}. Cada jogo acaba quando a soma chega a {pontos}: {pontos / 2 + 1}-{pontos / 2 - 1} ganha, {pontos / 2}-{pontos / 2} empata.";
    }

    private void Criar()
    {
        var regras = new RegrasDoRodizio(_formato.Indice == 1 ? FormatoDoRodizio.Mexicano : FormatoDoRodizio.Americano, EstadoDoRodizio.PontosOferecidos[_pontos.Indice]);
        int total = TabelaDoAmericano.MinimoDeJogadores + _jogadores.Indice;
        uint semente = Configuracao.Semente ?? (uint)Time.GetUnixTimeFromSystem();
        EstadoDoRodizio.Nova(regras, EstadoDoRodizio.JogadoresDoEvento(Configuracao.NomeDoJogador, total, semente), semente);
        _criando = false;
        _carga = null;
        _problema = null;
        GD.Print($"Rodízio: {EstadoDoRodizio.NomeDoFormato(regras.Formato)} de {total} jogadores, {regras.PontosPorJogo} pontos corridos, semente {semente}");
        Atualizar();
    }

    private void MostrarEvento(TorneioDeRodizio rodizio)
    {
        _formulario.Visible = false;
        _evento.Visible = true;
        _minhaPartida.Visible = true;
        string formato = EstadoDoRodizio.NomeDoFormato(rodizio.Regras.Formato);
        int quadras = rodizio.Jogadores.Count / 4;
        bool revelada = EstadoDoRodizio.Revelada;
        string? eu = EstadoDoRodizio.NomeDoHumano;
        var meu = EstadoDoRodizio.JogoDoHumanoNaRodada();
        _detalhe.Text = $"{rodizio.Jogadores.Count} jogadores · {quadras} quadra{(quadras > 1 ? "s" : "")} · jogos de {rodizio.Regras.PontosPorJogo} pontos corridos";
        _rodada.Text = TextoDaRodada(rodizio, eu, revelada);
        var linhas = EstadoDoRodizio.ClassificacaoVisivel();
        _tituloDaClassificacao.Text = revelada ? "CLASSIFICAÇÃO" : "CLASSIFICAÇÃO · SEM OS OUTROS JOGOS DA RODADA";
        _classificacao.Text = TextoDaClassificacao(linhas, eu);
        _simular.Visible = _proxima.Visible = false;

        if (rodizio.Encerrado && revelada)
        {
            _titulo.Text = $"{formato} encerrado";
            var minha = eu is null ? null : linhas.FirstOrDefault(l => l.Jogador == eu);
            _minhaPartida.Text = $"Campeão: {rodizio.Campeao?.Nome}." + (minha is null ? "" : $" Você terminou em {minha.Posicao}º, com {minha.Pontos} pontos.");
            Principal("Novo evento", () => { _criando = true; _problema = null; Atualizar(); });
            return;
        }

        _titulo.Text = $"{formato} · rodada {rodizio.RodadaAtual + 1} de {rodizio.TotalDeRodadas}";
        if (meu is { Jogado: false } pendente && eu is not null)
        {
            var (minhaDupla, rivais) = Duplas(pendente, eu);
            int forca = EstadoDoRodizio.ForcaDaDupla(rodizio, rivais);
            _minhaPartida.Text = $"Sua partida: quadra {pendente.Quadra} — {minhaDupla} x {rivais} (rivais de força {forca}: IA {NomeDaDificuldade(EstadoDaCarreira.DificuldadePara(forca))})";
            Principal("Jogar", () => Jogar(pendente));
            _simular.Visible = !revelada;
            return;
        }

        _minhaPartida.Text = eu is null ? "Evento sem humano: as rodadas se jogam sozinhas."
            : meu is { PontosA: int a, PontosB: int b } jogado ? TextoDoMeuJogo(jogado, eu, a, b)
            : "Você folga nesta rodada: soma a média dos seus pontos por jogo quando ela fechar.";
        if (!revelada)
        {
            Principal("Simular os outros jogos da rodada", Simular);
            _proxima.Visible = !rodizio.Encerrado;
            return;
        }
        Principal("Próxima rodada", ProximaRodada);
    }

    private static string TextoDoMeuJogo(JogoDoRodizio jogo, string eu, int a, int b)
    {
        bool euEmA = jogo.DuplaA.Tem(eu);
        int meus = euEmA ? a : b, deles = euEmA ? b : a;
        string resultado = meus > deles ? "vitória" : meus == deles ? "empate" : "derrota";
        var (minha, rivais) = Duplas(jogo, eu);
        return $"Sua partida: {minha} {meus} – {deles} {rivais} ({resultado}).";
    }

    /// <summary>A carga não deixou abrir: a de um jogo mais novo só manda atualizar; a que não deu pra ler oferece tentar de novo.</summary>
    private void MostrarQueNaoAbriu()
    {
        _formulario.Visible = false;
        _evento.Visible = false;
        _minhaPartida.Visible = false;
        _simular.Visible = _proxima.Visible = false;
        _detalhe.Text = _carga?.Mensagem ?? "";
        if (_carga?.Situacao == SituacaoDoRodizio.DeUmJogoMaisNovo)
        {
            _titulo.Text = "Evento de uma versão mais nova";
            _acaoPrincipal = null;
            _principal.Visible = false;
            _menu.GrabFocus();
            return;
        }
        _titulo.Text = "Não deu pra abrir o evento";
        Principal("Tentar de novo", () => { _carga = EstadoDoRodizio.Carregar(); Atualizar(); });
    }

    private void Principal(string texto, Action acao)
    {
        _principal.Visible = true;
        _principal.Text = texto;
        _acaoPrincipal = acao;
        _principal.GrabFocus();
    }

    // ---- O que os botões fazem ----

    private void Jogar(JogoDoRodizio jogo)
    {
        EstadoDoRodizio.PrepararJogo(jogo);
        GD.Print($"Rodízio: jogo {jogo.Numero} (rodada {jogo.Rodada + 1}, quadra {jogo.Quadra}) — {jogo.DuplaA} x {jogo.DuplaB}");
        IrPara(PartidaNode.CenaDaPartida);
    }

    private void Simular()
    {
        EstadoDoRodizio.Revelar();
        if (EstadoDoRodizio.Atual is { Encerrado: true } rodizio)
        {
            string? eu = EstadoDoRodizio.NomeDoHumano;
            var minha = rodizio.Classificacao().FirstOrDefault(l => l.Jogador == eu);
            GD.Print($"Rodízio: evento encerrado — campeão {rodizio.Campeao?.Nome}" + (minha is null ? "" : $"; {eu} em {minha.Posicao}º, com {minha.Pontos} pontos"));
        }
        Atualizar();
    }

    private void ProximaRodada()
    {
        _problema = EstadoDoRodizio.ProximaRodada() is string motivo ? $"A rodada não andou: {motivo}" : null;
        if (_problema is null && EstadoDoRodizio.Atual is { } rodizio) GD.Print($"Rodízio: rodada {rodizio.RodadaAtual + 1} de {rodizio.TotalDeRodadas}");
        Atualizar();
    }

    private void IrPara(string cena)
    {
        var erro = TrocarDeCena(GetTree(), cena);
        if (erro == Error.Ok) return;
        _problema = $"Não deu pra abrir {cena} ({erro}).";
        GD.PushError($"Rodízio: {_problema}");
        Atualizar();
    }

    /// <summary>Modo de teste: aperta o botão principal até o evento acabar; qualquer tropeço sai com erro (e diz qual).</summary>
    private void ApertarSozinho()
    {
        if (!IsInsideTree() || _saindo) return;
        if ((EstadoDoRodizio.Atual is null && !_formulario.Visible) || _problema is not null)
        {
            GD.PrintErr($"Rodízio: o automático parou — {_titulo.Text}: {_detalhe.Text} {_aviso.Text}");
            SairLimpo(1);
            return;
        }
        if (EstadoDoRodizio.Atual is { Encerrado: true } && EstadoDoRodizio.Revelada)
        {
            SairLimpo(0);
            return;
        }
        _acaoPrincipal?.Invoke();
        if (IsInsideTree() && GetTree().CurrentScene == this) Callable.From(ApertarSozinho).CallDeferred();
    }

    /// <summary>
    /// Fecha o jogo depois de um ciclo de mixagem, em tempo de relógio (com --fixed-fps o tempo do jogo não serve), como a
    /// SairLimpo da partida: a tela volta da última partida com os sons dela ainda no servidor de áudio, e fechar na hora
    /// saía acusando "ObjectDB instances were leaked" e "resources still in use at exit".
    /// </summary>
    private async void SairLimpo(int codigo)
    {
        if (_saindo) return;
        _saindo = true;
        var arvore = GetTree();   // antes da espera: se o nó for liberado no meio, o Quit ainda acontece
        await Task.Delay(250);
        arvore.Quit(codigo);
    }

    // ---- Textos ----

    private static (DuplaDoRodizio Minha, DuplaDoRodizio Rivais) Duplas(JogoDoRodizio jogo, string eu) =>
        jogo.DuplaA.Tem(eu) ? (jogo.DuplaA, jogo.DuplaB) : (jogo.DuplaB, jogo.DuplaA);

    private static string NomeDaDificuldade(Dificuldade d) => d switch
    {
        Dificuldade.Facil => "fácil",
        Dificuldade.Dificil => "difícil",
        _ => "média",
    };

    private static string Cor(string texto, string cor) => $"[color={cor}]{texto}[/color]";

    private static string TextoDaRodada(TorneioDeRodizio rodizio, string? eu, bool revelada)
    {
        var s = new StringBuilder();
        int atual = rodizio.RodadaAtual;
        s.Append(Cor($"[b]RODADA {atual + 1}[/b]", VerdeLima)).Append('\n');
        TabelaDeJogos(s, rodizio.JogosDaRodadaAtual, eu, j => j.Jogado && (revelada || (eu is not null && j.Envolve(eu))), naoMostrado: j => eu is not null && j.Envolve(eu) ? "a jogar" : "em quadra");
        Folgas(s, rodizio.FolgasDaRodada(atual), eu);
        if (atual > 0) s.Append('\n').Append(Cor("[b]RESULTADOS[/b]", VerdeLima)).Append('\n');
        for (int r = atual - 1; r >= 0; r--)
        {
            s.Append(Cor($"Rodada {r + 1}", CinzaSuave)).Append('\n');
            TabelaDeJogos(s, rodizio.Jogos.Where(j => j.Rodada == r).ToList(), eu, _ => true, naoMostrado: _ => "");
            Folgas(s, rodizio.FolgasDaRodada(r), eu);
        }
        return s.ToString();
    }

    private static void TabelaDeJogos(StringBuilder s, IReadOnlyList<JogoDoRodizio> jogos, string? eu, Func<JogoDoRodizio, bool> mostrarPlacar, Func<JogoDoRodizio, string> naoMostrado)
    {
        s.Append("[table=4]");
        foreach (var jogo in jogos.OrderBy(j => j.Quadra))
        {
            bool meu = eu is not null && jogo.Envolve(eu);
            string placar = mostrarPlacar(jogo) && jogo is { PontosA: int a, PontosB: int b } ? $"[b]{TextoDoPlacar(a, b)}[/b]" : Cor(naoMostrado(jogo), CinzaSuave);
            string Celula(string texto, int expandir = 1) => $"[cell expand={expandir}]{(meu ? Cor(texto, VerdeLima) : texto)}[/cell]";
            s.Append(Celula($"Quadra {jogo.Quadra}"))
             .Append(Celula(jogo.DuplaA.ToString(), 3))
             .Append($"[cell expand=2]{placar}[/cell]")
             .Append(Celula(jogo.DuplaB.ToString(), 3));
        }
        s.Append("[/table]\n");
    }

    private static void Folgas(StringBuilder s, IReadOnlyList<string> folgam, string? eu)
    {
        if (folgam.Count == 0) return;
        string nomes = string.Join(", ", folgam.Select(n => n == eu ? Cor(n, VerdeLima) : n));
        s.Append(Cor(folgam.Count > 1 ? "Folgam: " : "Folga: ", CinzaSuave)).Append(nomes).Append('\n');
    }

    private static string TextoDaClassificacao(IReadOnlyList<LinhaDoRodizio> linhas, string? eu)
    {
        var s = new StringBuilder("[table=6]");
        foreach (var titulo in new[] { "#", "Jogador", "Pts", "J", "V-E-D", "Saldo" })
            s.Append($"[cell]{Cor(titulo, CinzaSuave)}[/cell]");
        foreach (var l in linhas)
        {
            bool minha = l.Jogador == eu;
            string C(string texto, int expandir = 1) => $"[cell expand={expandir}]{(minha ? Cor(texto, VerdeLima) : texto)}[/cell]";
            s.Append(C($"{l.Posicao}º"))
             .Append(C(l.Jogador, 4))
             .Append(C($"[b]{l.Pontos}[/b]"))
             .Append(C(l.Jogos.ToString(System.Globalization.CultureInfo.InvariantCulture)))
             .Append(C($"{l.Vitorias}-{l.Empates}-{l.Derrotas}", 2))
             .Append(C(l.Saldo.ToString("+0;-0;0", System.Globalization.CultureInfo.InvariantCulture)));
        }
        return s.Append("[/table]").ToString();
    }
}
