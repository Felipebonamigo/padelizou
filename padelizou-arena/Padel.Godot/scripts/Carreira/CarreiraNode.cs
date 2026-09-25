using System.Globalization;
using System.Text;
using Godot;
using Padel.Core.Torneio;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>
/// A tela da carreira: a etapa do circuito, os grupos e a chave, o próximo jogo da sua dupla e o ranking. "Jogar"
/// abre a partida de verdade contra a dupla rival (dificuldade pela força dela); ao acabar, a partida volta pra cá com
/// o resultado. Tudo o que é regra (grupos, chave, pontos) vem do Padel.Core.Torneio — a mesma régua do Padelizou.
/// Argumentos: --carreira-nova (descarta a salva), --carreira-sozinha (humano simulado joga e a tela avança sozinha: teste).
/// </summary>
public partial class CarreiraNode : Control
{
    private Label _titulo = null!;
    private Label _detalhe = null!;
    private RichTextLabel _quadro = null!;
    private RichTextLabel _ranking = null!;
    private Button _principal = null!;
    private Button _simular = null!;
    private Button _menu = null!;
    private Action? _acaoPrincipal;
    private static bool _argumentosLidos;

    public override void _Ready()
    {
        if (!_argumentosLidos)
        {
            _argumentosLidos = true;
            var args = OS.GetCmdlineUserArgs();
            Configuracao.LerLinhaDeComando(args);
            PerfilLocal.LerLinhaDeComando(args);
            if (args.Contains("--carreira-sozinha")) EstadoDaCarreira.Automatico = true;
            if (args.Contains("--carreira-nova") && File.Exists(EstadoDaCarreira.Caminho)) File.Delete(EstadoDaCarreira.Caminho);
        }
        if (EstadoDaCarreira.Atual is null && !EstadoDaCarreira.Carregar())
            EstadoDaCarreira.Nova($"{Configuracao.NomeDoJogador} / Parceiro", Configuracao.Semente ?? (uint)Time.GetUnixTimeFromSystem());
        Montar();
        Atualizar();
        if (EstadoDaCarreira.Automatico) Callable.From(ApertarSozinho).CallDeferred();
        else if (Configuracao.Screenshot is string arquivo) Captura.SalvarESair(this, arquivo);
    }

    private void Montar()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        var fundo = new ColorRect { Color = TemaPadelizou.MarinhoProfundo };
        fundo.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(fundo);
        var margem = new MarginContainer();
        margem.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var lado in new[] { "left", "right", "top", "bottom" }) margem.AddThemeConstantOverride($"margin_{lado}", 48);
        AddChild(margem);
        var coluna = new VBoxContainer();
        coluna.AddThemeConstantOverride("separation", 14);
        margem.AddChild(coluna);

        _titulo = Rotulo(34, TemaPadelizou.Branco);
        _detalhe = Rotulo(20, TemaPadelizou.TextoSuave);
        coluna.AddChild(_titulo);
        coluna.AddChild(_detalhe);

        var linhas = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        linhas.AddThemeConstantOverride("separation", 32);
        coluna.AddChild(linhas);
        _quadro = Texto();
        _ranking = Texto();
        _ranking.CustomMinimumSize = new Vector2(380, 0);
        _ranking.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        linhas.AddChild(_quadro);
        linhas.AddChild(_ranking);

        var botoes = new HBoxContainer();
        botoes.AddThemeConstantOverride("separation", 16);
        coluna.AddChild(botoes);
        _principal = Botao("Jogar", () => _acaoPrincipal?.Invoke());
        _simular = Botao("Simular o resto da etapa", SimularEtapa);
        _menu = Botao("Menu", () => GetTree().ChangeSceneToFile(PartidaNode.CenaDoMenu));
        botoes.AddChild(_principal);
        botoes.AddChild(_simular);
        botoes.AddChild(_menu);
    }

    private static Label Rotulo(int tamanho, Color cor)
    {
        var l = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", tamanho);
        l.AddThemeColorOverride("font_color", cor);
        return l;
    }

    private static RichTextLabel Texto()
    {
        var t = new RichTextLabel { BbcodeEnabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ScrollFollowing = false };
        t.AddThemeFontSizeOverride("normal_font_size", 18);
        t.AddThemeFontSizeOverride("bold_font_size", 18);
        t.AddThemeColorOverride("default_color", TemaPadelizou.Branco);
        return t;
    }

    private Button Botao(string texto, Action aoApertar)
    {
        var b = new Button { Text = texto, CustomMinimumSize = new Vector2(220, 52), FocusMode = FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.Pressed += aoApertar;
        return b;
    }

    private void Atualizar()
    {
        var carreira = EstadoDaCarreira.Atual;
        if (carreira is null) return;
        string eu = carreira.DuplaDoJogador.Nome;
        _ranking.Text = TextoDoRanking(carreira, eu);
        _simular.Visible = false;

        if (carreira.Concluida)
        {
            var minha = carreira.Ranking().FirstOrDefault(l => l.Dupla == eu);
            _titulo.Text = "Circuito encerrado";
            _detalhe.Text = minha is null ? "" : $"{eu} terminou em {minha.Posicao}º, com {minha.Pontos} pontos e {minha.Titulos} título(s).";
            _quadro.Text = TextoDosResultados(carreira);
            Principal("Nova carreira", () => { EstadoDaCarreira.Nova(eu, (uint)Time.GetUnixTimeFromSystem()); Atualizar(); });
            return;
        }

        int indice = carreira.IndiceDaProximaEtapa;
        var etapa = carreira.Etapas[indice];
        string cabecalho = $"Etapa {indice + 1} de {carreira.Etapas.Count} — {etapa.Nome} ({EstadoDaCarreira.NomeDaCategoria(etapa.Categoria)})";
        if (carreira.EtapaEmAndamento is not TorneioDeDuplas torneio)
        {
            _titulo.Text = cabecalho;
            _detalhe.Text = $"{etapa.Duplas} duplas, {(etapa.Formato == FormatoDoTorneio.GruposEMataMata ? "grupos e mata-mata" : "chave direta")}, melhor de {etapa.SetsParaVencer * 2 - 1} set(s). Sua dupla: {eu} (força {carreira.DuplaDoJogador.Forca}).";
            _quadro.Text = TextoDosResultados(carreira);
            Principal("Começar etapa", () => { carreira.IniciarEtapa(); EstadoDaCarreira.Salvar(); Atualizar(); });
            return;
        }

        _titulo.Text = cabecalho;
        _quadro.Text = TextoDoTorneio(torneio, eu);
        var proximo = EstadoDaCarreira.ProximoJogoDoJogador();
        if (proximo is not null)
        {
            string rival = proximo.DuplaA == eu ? proximo.DuplaB : proximo.DuplaA;
            var dupla = torneio.Dupla(rival);
            var dificuldade = EstadoDaCarreira.DificuldadePara(dupla.Forca);
            _detalhe.Text = $"{torneio.NomeDaFase(proximo)}: {eu} x {rival} (força {dupla.Forca} — IA {NomeDaDificuldade(dificuldade)}).";
            Principal("Jogar", () => Jogar(proximo, rival, dificuldade, etapa.SetsParaVencer));
            _simular.Visible = false;
            _quadro.Text = TextoDoTorneio(torneio, eu);   // pode ter andado rodadas de IA
            return;
        }
        // A dupla foi eliminada ou a etapa acabou.
        if (!torneio.Encerrado)
        {
            _detalhe.Text = $"{eu} caiu na {EstadoDaCarreira.NomeDaFase(torneio.FaseDaDupla(eu))}. As outras duplas seguem.";
            Principal("Ver o fim da etapa", SimularEtapa);
            return;
        }
        _detalhe.Text = $"Campeão: {torneio.Campeao?.Nome}. Sua dupla: {EstadoDaCarreira.NomeDaFase(torneio.FaseDaDupla(eu))}.";
        Principal("Fechar a etapa e somar os pontos", () =>
        {
            var resultado = carreira.FecharEtapa();
            EstadoDaCarreira.Salvar();
            RegistrarNoPerfil(carreira, resultado.Campeao == eu, eu);
            GD.Print($"Carreira: etapa {resultado.Etapa} fechada — campeão {resultado.Campeao}; {eu}: {EstadoDaCarreira.NomeDaFase(resultado.Pontuacoes.First(p => p.Dupla == eu).Fase)}, {resultado.Pontuacoes.First(p => p.Dupla == eu).Pontos} pontos");
            Atualizar();
        });
    }

    /// <summary>
    /// Conquistas da carreira (docs/CONQUISTAS.md, passo 3): etapa vencida e, se o circuito acabou, a posição final.
    /// A carreira automática (teste) só grava com --perfil ARQ.
    /// </summary>
    private static void RegistrarNoPerfil(Padel.Core.Torneio.Carreira carreira, bool campeao, string eu)
    {
        if (EstadoDaCarreira.Automatico && PerfilLocal.CaminhoPedido is null) return;
        if (campeao) PerfilLocal.DoJogo.Registrar(new Padel.Core.Perfil.EtapaVencida());
        if (carreira.Concluida && carreira.Ranking().FirstOrDefault(l => l.Dupla == eu) is { } minha)
            PerfilLocal.DoJogo.Registrar(new Padel.Core.Perfil.CircuitoEncerrado(minha.Posicao));
    }

    private void Principal(string texto, Action acao)
    {
        _principal.Text = texto;
        _acaoPrincipal = acao;
        _principal.GrabFocus();
    }

    private void SimularEtapa()
    {
        if (EstadoDaCarreira.Atual?.EtapaEmAndamento is TorneioDeDuplas torneio && !torneio.JogosPendentes.Any())
        {
            torneio.JogarAteOFim();
            EstadoDaCarreira.Salvar();
        }
        Atualizar();
    }

    private void Jogar(JogoDoTorneio jogo, string rival, Padel.Core.Dificuldade dificuldade, int setsParaVencer)
    {
        EstadoDaCarreira.JogoEmDisputa = jogo;
        // Cada jogo com a sua semente, tirada da carreira, da etapa e do número do jogo: reproduzível, e nenhum jogo repete
        // o outro (com uma semente só, todo jogo contra a mesma dificuldade saía idêntico — visto no teste automático).
        if (EstadoDaCarreira.Atual is { } carreira)
            Configuracao.Semente = EstadoDaCarreira.SementeDoJogo(carreira.Semente, carreira.IndiceDaProximaEtapa, jogo.Numero);
        Configuracao.Modo = ModoDeJogo.Local;
        Configuracao.Dificuldade = dificuldade;
        Configuracao.SetsParaVencer = setsParaVencer;
        GD.Print($"Carreira: jogo {jogo.Numero} contra {rival} ({dificuldade})");
        GetTree().ChangeSceneToFile(PartidaNode.CenaDaPartida);
    }

    private void ApertarSozinho()
    {
        if (!IsInsideTree()) return;
        if (EstadoDaCarreira.Atual?.Concluida == true) { GD.Print("Carreira: circuito encerrado"); GetTree().Quit(); return; }
        _acaoPrincipal?.Invoke();
        if (IsInsideTree() && GetTree().CurrentScene == this) Callable.From(ApertarSozinho).CallDeferred();
    }

    private static string NomeDaDificuldade(Padel.Core.Dificuldade d) => d switch
    {
        Padel.Core.Dificuldade.Facil => "fácil",
        Padel.Core.Dificuldade.Dificil => "difícil",
        _ => "média",
    };

    private static string TextoDoTorneio(TorneioDeDuplas torneio, string eu)
    {
        var s = new StringBuilder();
        foreach (var grupo in torneio.Grupos)
        {
            s.Append($"[b]Grupo {grupo.Nome}[/b]\n");
            foreach (var linha in torneio.Classificacao(grupo.Nome))
            {
                string nome = linha.Dupla == eu ? $"[color=#a3d827]{linha.Dupla}[/color]" : linha.Dupla;
                s.Append($"  {nome} — {linha.Vitorias}V {linha.Derrotas}D, saldo {linha.Saldo:+0;-0;0}\n");
            }
        }
        var chave = torneio.Jogos.Where(j => !j.DeGrupo).ToList();
        if (chave.Count > 0)
        {
            s.Append("\n[b]Chave[/b]\n");
            foreach (var jogo in chave)
            {
                string resultado = jogo.Jogado ? $"{jogo.Resumo()}  → {jogo.Vencedor}" : "a jogar";
                string linha = $"  {torneio.NomeDaFase(jogo)}: {jogo.DuplaA} x {jogo.DuplaB} — {resultado}\n";
                s.Append(jogo.Envolve(eu) ? $"[color=#a3d827]{linha}[/color]" : linha);
            }
        }
        return s.ToString();
    }

    private static string TextoDoRanking(Carreira carreira, string eu)
    {
        var s = new StringBuilder("[b]Ranking do circuito[/b]\n");
        foreach (var linha in carreira.Ranking().Take(10))
        {
            string texto = $"{linha.Posicao,2}º {linha.Dupla} — {linha.Pontos.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))}";
            s.Append(linha.Dupla == eu ? $"[color=#a3d827]{texto}[/color]\n" : $"{texto}\n");
        }
        return s.ToString();
    }

    private static string TextoDosResultados(Carreira carreira)
    {
        if (carreira.Resultados.Count == 0) return "Nenhuma etapa jogada ainda.";
        var s = new StringBuilder("[b]Etapas jogadas[/b]\n");
        string eu = carreira.DuplaDoJogador.Nome;
        foreach (var r in carreira.Resultados)
        {
            var minha = r.Pontuacoes.FirstOrDefault(p => p.Dupla == eu);
            string fase = minha is null ? "—" : EstadoDaCarreira.NomeDaFase(minha.Fase);
            s.Append($"  {r.Etapa}: campeão {r.Campeao}; vocês — {fase}, {minha?.Pontos ?? 0} pontos\n");
        }
        return s.ToString();
    }
}
