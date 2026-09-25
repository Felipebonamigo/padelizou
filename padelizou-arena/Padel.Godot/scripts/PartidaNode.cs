using System.Globalization;
using Godot;
using Padel.Core;
using Padel.Core.Perfil;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>
/// Raiz da cena da partida: lê a Configuracao (escolhida no menu ou pela linha de comando), escolhe a sessão
/// (onde a partida roda), avança em passo fixo no _PhysicsProcess (120 Hz no project.godot) com a entrada de quem joga
/// nesta máquina, e desenha o Retrato: nós 3D, placar de TV, pausa, fim. A tela nunca lê a Partida direto — só o
/// Retrato —, e é isso que deixa o mesmo desenho servir ao jogo local e ao online.
/// Argumentos que só a partida entende (os outros estão em Configuracao.LerLinhaDeComando): --bot (humano simulado nos
/// controles desta máquina), --esperar SEGUNDOS (tempo de sala do host), --rede-ruim MS PERDA (latência e perda
/// artificiais na saída, pra teste), --sem-replay.
/// </summary>
public partial class PartidaNode : Node3D
{
    public const string CenaDoMenu = "res://cenas/Menu.tscn";
    public const string CenaDaPartida = "res://cenas/Partida.tscn";
    public const string CenaDaCarreira = "res://cenas/Carreira.tscn";

    public ISessao Sessao { get; private set; } = null!;

    private QuadraNode _quadra = null!;
    private BolaNode _bola = null!;
    private readonly List<JogadorNode> _jogadores = [];
    private CameraNode? _camera;
    private PlacarDeTvNode _placar = null!;
    private bool _saindo;
    private SomNode _som = null!;
    private ColetorDaPartida? _coletor;
    private bool _perfilRegistrado;
    private int _sonsTocados;
    private TelaDePausa _pausa = null!;
    private TelaDeFim _fim = null!;
    private MeshInstance3D? _marcaDaCaixa;
    private Caixa? _caixaMarcada;
    private bool _pausado;
    private bool _fimMostrado;
    private double _tempoVivo;
    private double _esperarNaSala = 10;
    private int _latenciaDeTeste;
    private double _perdaDeTeste;
    private bool _bot;
    private readonly Dictionary<int, HumanoSimulado> _bots = [];
    private readonly Entrada[] _entradas = new Entrada[4];
    private readonly ControleDeReplay _replay = new();
    private SetAnterior[] _setsDoPlacar = [];
    /// <summary>Nomes das duas duplas no placar quando a partida é de um torneio (carreira); null = nomes dos jogadores.</summary>
    private string[]? _nomesDasDuplas;
    private bool EmCarreira => EstadoDaCarreira.JogoEmDisputa is not null;
    /// <summary>A linha de comando vale só pra primeira partida do processo: voltar ao menu e escolher outro modo não pode ser atropelado por ela.</summary>
    private static bool _linhaDeComandoLida;

    public override void _Ready()
    {
        GetTree().AutoAcceptQuit = false;   // fechar a janela passa pelo SairLimpo (ver _Notification)
        if (!_linhaDeComandoLida)
        {
            _linhaDeComandoLida = true;
            var args = OS.GetCmdlineUserArgs();
            // O menu já leu a linha de comando quando pulou pra cá; ler de novo é idempotente e cobre abrir esta cena direto.
            Configuracao.LerLinhaDeComando(args);
            PerfilLocal.LerLinhaDeComando(args);
            LerArgumentosDaPartida(args);
        }
        EntradaLocal.ConfigurarMapa(coop: Configuracao.Modo == ModoDeJogo.CoopLocal);
        if (EmCarreira && EstadoDaCarreira.Automatico) { _bot = true; _replay.Ligado = false; }
        if (EstadoDaCarreira.JogoEmDisputa is { } jogo && EstadoDaCarreira.Atual is { } carreira)
        {
            string eu = carreira.DuplaDoJogador.Nome;
            _nomesDasDuplas = [eu, jogo.DuplaA == eu ? jogo.DuplaB : jogo.DuplaA];
        }
        Sessao = CriarSessao(out string quem);
        LigarOPerfil();

        _quadra = new QuadraNode { Name = "Quadra" };
        AddChild(_quadra);
        _bola = new BolaNode { Name = "Bola" };
        AddChild(_bola);
        _som = new SomNode { Name = "Som" };
        AddChild(_som);
        _som.AoTocar += _ => _sonsTocados++;
        _som.Volume(Configuracao.Volume);
        _som.IniciarAmbiente();
        for (int i = 0; i < 4; i++)
        {
            var no = new JogadorNode { Name = $"Jogador{i}" };
            AddChild(no);
            no.Ligar(i);
            _jogadores.Add(no);
        }
        _camera = GetNodeOrNull<CameraNode>("Camera");
        MontarTelas();
        Desenhar(0);
        GD.Print($"Padelizou Arena: partida criada ({Configuracao.Dificuldade}, {quem}, Godot {Engine.GetVersionInfo()["string"]})");
    }

    private void LerArgumentosDaPartida(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sem-replay": _replay.Ligado = false; break;
                case "--esperar" when i + 1 < args.Length && double.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var espera): _esperarNaSala = espera; i++; break;
                case "--bot": _bot = true; break;
                case "--rede-ruim" when i + 2 < args.Length && int.TryParse(args[i + 1], out var ms) && double.TryParse(args[i + 2], CultureInfo.InvariantCulture, out var perda):
                    _latenciaDeTeste = ms; _perdaDeTeste = perda; i += 2; break;
            }
        }
    }

    private ISessao CriarSessao(out string quem)
    {
        var modo = Configuracao.Modo;
        bool[] humanos = modo switch
        {
            ModoDeJogo.Demonstracao => OpcoesDaPartida.NinguemHumano,
            ModoDeJogo.CoopLocal => [true, true, false, false],
            _ => [true, false, false, false],
        };
        var opcoes = new OpcoesDaPartida
        {
            Dificuldade = Configuracao.Dificuldade,
            PontoDeOuro = Configuracao.PontoDeOuro,
            SetsParaVencer = Configuracao.SetsParaVencer,
            Humanos = humanos,
            ModoDeGolpe = Configuracao.ModoDeGolpe,
            Semente = Configuracao.Semente,
        };
        ISessao sessao;
        switch (modo)
        {
            case ModoDeJogo.CriarSala:
            {
                var host = new SessaoHost(Configuracao.PortaParaCriar, opcoes, Configuracao.NomeDoJogador, _esperarNaSala);
                host.Transporte.LatenciaDeTesteMs = _latenciaDeTeste; host.Transporte.PerdaDeTeste = _perdaDeTeste;
                sessao = host;
                quem = $"host na porta {Configuracao.PortaParaCriar}";
                break;
            }
            case ModoDeJogo.EntrarNaSala:
            {
                var cliente = new SessaoCliente(Configuracao.EnderecoDaSala, Configuracao.PortaDaSala, Configuracao.NomeDoJogador);
                cliente.Transporte.LatenciaDeTesteMs = _latenciaDeTeste; cliente.Transporte.PerdaDeTeste = _perdaDeTeste;
                sessao = cliente;
                quem = $"cliente de {Configuracao.EnderecoDaSala}:{Configuracao.PortaDaSala}";
                break;
            }
            default:
                sessao = new SessaoLocal(opcoes, Configuracao.NomeDoJogador, Configuracao.Destro);
                quem = modo switch
                {
                    ModoDeJogo.Demonstracao => "4 IAs",
                    ModoDeJogo.CoopLocal => $"coop local, golpe {Configuracao.ModoDeGolpe}",
                    _ => $"1 humano, golpe {Configuracao.ModoDeGolpe}",
                };
                break;
        }
        if (_latenciaDeTeste > 0 || _perdaDeTeste > 0) quem += $", rede ruim de teste: +{_latenciaDeTeste} ms e {_perdaDeTeste:P0} de perda na saída";
        if (_bot) quem += ", humano simulado nos controles";
        return sessao;
    }

    private void MontarTelas()
    {
        _placar = new PlacarDeTvNode { Name = "Placar" };
        AddChild(_placar);
        var telas = new CanvasLayer { Name = "Telas", Layer = 20, ProcessMode = ProcessModeEnum.Always };
        AddChild(telas);
        _pausa = new TelaDePausa { Name = "Pausa" };
        _fim = new TelaDeFim { Name = "Fim" };
        telas.AddChild(_pausa);
        telas.AddChild(_fim);
        _pausa.ContinuarPedido += Continuar;
        _pausa.SairProMenuPedido += IrProMenu;
        _pausa.OpcoesPedidas += IrProMenu;   // atalho: as opções moram no menu; trocar dentro da partida fica pro M3
        _fim.JogarDeNovoPedido += () => TrocarDeCena(EmCarreira || _nomesDasDuplas is not null ? CenaDaCarreira : CenaDaPartida);
        _fim.MenuPedido += IrProMenu;
        var dicas = new DicaDeControles { Name = "Dicas" };
        dicas.Definir(new Dica("Espaço", "A", "sacar / balançar"), new Dica("Shift", "B", "lob"), new Dica("Esc", "Start", "pausa"));
        dicas.GrowHorizontal = Control.GrowDirection.Both;   // cresce pros dois lados: fica centralizada
        dicas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        dicas.OffsetTop = -40; dicas.OffsetBottom = -12;
        telas.AddChild(dicas);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_saindo) return;   // fechando: a partida para enquanto o som é recolhido
        bool online = Sessao is not SessaoLocal;
        if (!_pausado && !_fimMostrado && Input.IsActionJustPressed(EntradaLocal.Pausa)) Pausar();
        _tempoVivo += delta;
        Array.Clear(_entradas);
        var locais = Sessao.JogadoresLocais;
        if (!_pausado)
            for (int n = 0; n < locais.Count; n++) _entradas[locais[n]] = _bot ? EntradaDoBot(locais[n], (float)delta) : EntradaLocal.LerJogadorLocal(n);

        if (_replay.Reproduzindo)
        {
            // Durante o replay a partida local espera; qualquer jogador daqui pula com a ação.
            if (_entradas.Any(e => e.AcaoPressionada || e.LobPressionada)) _replay.Pular();
            if (_replay.Avancar((float)delta, Sessao.Retrato) is RetratoDaPartida quadro) { DesenharReplay(quadro, (float)delta); VerificarFim(); return; }
            GD.Print($"Replay: fim em {_tempoVivo:F1} s");
        }

        // No online a partida não para (os outros continuam jogando): a pausa só abre o menu por cima.
        if (!_pausado || online) Sessao.Avancar(delta, _entradas);
        Desenhar((float)delta);
        VerificarFim();
    }

    private void Pausar()
    {
        _pausado = true;
        _pausa.Abrir(Sessao is SessaoLocal ? null : "A partida online continua enquanto você está aqui.");
        if (Sessao is SessaoLocal) GetTree().Paused = true;
    }

    private void Continuar()
    {
        _pausa.Fechar();
        GetTree().Paused = false;
        _pausado = false;
    }

    private void IrProMenu() => TrocarDeCena(CenaDoMenu);

    private void TrocarDeCena(string cena)
    {
        GetTree().Paused = false;
        var erro = GetTree().ChangeSceneToFile(cena);
        if (erro != Error.Ok) GD.PushError($"não deu pra abrir {cena}: {erro}");
    }

    /// <summary>O humano simulado (Padel.Core) jogando por quem está nesta máquina — demonstração e teste de rede.</summary>
    private Entrada EntradaDoBot(int indice, float delta)
    {
        if (Sessao.EstadoParaOBot() is not EstadoVisivel estado) return Entrada.Vazia;
        if (!_bots.TryGetValue(indice, out var bot))
        {
            bot = new HumanoSimulado(indice, PerfilDeHumano.Avancado, new Aleatorio((Configuracao.Semente ?? 7u) * 31u + (uint)indice), Configuracao.Destro);
            _bots[indice] = bot;
        }
        return bot.Decidir(estado, delta);
    }

    public override void _ExitTree()
    {
        RegistrarNoPerfil();   // saiu no meio (menu, fechar o jogo): a abandonada conta golpes e tempo, não partida
        GetTree().AutoAcceptQuit = true;   // o menu e a carreira fecham do jeito normal
        Sessao?.Dispose();
    }

    private void VerificarFim()
    {
        if (Configuracao.SairApos is double sairApos)
        {
            if (_tempoVivo < sairApos && !Sessao.Acabou) return;
            if (Sessao.Acabou && !_fimMostrado) MostrarFim();   // a partida acabou antes do prazo: a tela de fim entra no screenshot
            GD.Print($"Saindo após {_tempoVivo:F1} s: {Sessao.ResumoParaLog()} sons={_sonsTocados}");
            Configuracao.SairApos = null;
            if (Configuracao.Screenshot is string arquivo) SalvarScreenshotESair(arquivo);
            else SairLimpo();
            return;
        }
        if (Sessao.Acabou && !_fimMostrado && !_replay.Reproduzindo) MostrarFim();
    }

    private void MostrarFim()
    {
        _fimMostrado = true;
        var r = Sessao.Retrato;
        int meuTime = Sessao.JogadoresLocais.Count > 0 ? Sessao.JogadoresLocais[0] / 2 : 0;
        bool demonstracao = Sessao.JogadoresLocais.Count == 0;
        int? vencedor = r.Placar.Vencedor;
        bool vitoria = vencedor == meuTime;
        string resultado = demonstracao
            ? $"{Dupla(r, vencedor ?? 0)} venceu"
            : vencedor is null ? "Partida encerrada" : vitoria ? "Vitória!" : "Derrota";
        var estatisticas = new List<(string, string)>();
        Partida? partida = Sessao switch { SessaoLocal l => l.Partida, SessaoHost h => h.Servidor.Partida, _ => null };
        if (partida is not null)
        {
            var e = partida.Estatisticas;
            estatisticas.Add(("Pontos jogados", e.Pontos.ToString(CultureInfo.InvariantCulture)));
            estatisticas.Add(("Golpes", e.Golpes.ToString(CultureInfo.InvariantCulture)));
            estatisticas.Add(("Maior rally", $"{e.MaiorRally} golpes"));
            if (!demonstracao)
            {
                var eu = partida.Jogadores[Sessao.JogadoresLocais[0]];
                estatisticas.Add(("Seus golpes", eu.Golpes.ToString(CultureInfo.InvariantCulture)));
                estatisticas.Add(("Raquetadas no ar", eu.BalancosNoAr.ToString(CultureInfo.InvariantCulture)));
            }
        }
        estatisticas.Add(("Tempo de jogo", TimeSpan.FromSeconds(r.TempoDeJogo).ToString(@"m\:ss", CultureInfo.InvariantCulture)));
        if (EmCarreira && partida is not null && partida.Placar.Acabou)
        {
            EstadoDaCarreira.InformarResultado(partida.Placar);
            estatisticas.Insert(0, ("Carreira", "resultado guardado — \"Jogar de novo\" volta pra etapa"));
        }
        foreach (var conquista in RegistrarNoPerfil()) estatisticas.Add(("Conquista!", conquista.Nome.Portugues));
        _fim.Mostrar(resultado, vitoria || demonstracao, Dupla(r, 0), Dupla(r, 1), SetsDoPlacar(r), estatisticas);
        if (EstadoDaCarreira.Automatico && _nomesDasDuplas is not null) Callable.From(() => TrocarDeCena(CenaDaCarreira)).CallDeferred();
        GD.Print($"Fim: {resultado} — {r.Placar.Resumo} — {string.Join(", ", estatisticas.Select(e => $"{e.Item1} {e.Item2}"))}");
    }

    private async void SalvarScreenshotESair(string arquivo)
    {
        // Espera dois quadros desenhados pra capturar a cena de verdade, e não o quadro do ciclo de física.
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var imagem = GetViewport().GetTexture().GetImage();
        var erro = imagem.SavePng(arquivo);
        GD.Print($"Screenshot {(erro == Error.Ok ? "salvo em" : "FALHOU: " + erro + " —")} {arquivo}");
        SairLimpo();
    }

    /// <summary>
    /// Perfil e conquistas (docs/CONQUISTAS.md): o coletor nasce com a partida — já, na local; quando a sala fecha, no
    /// host — e só pra quem joga nesta máquina. A demonstração (4 IAs) não conta; o --bot e a carreira automática só
    /// contam com --perfil ARQ, pra teste nenhum sujar o perfil de verdade. O cliente online não tem Partida: conta só
    /// a vitória, pelo evento, no fim.
    /// </summary>
    private void LigarOPerfil()
    {
        switch (Sessao)
        {
            case SessaoLocal local when GravaNoPerfil:
                _coletor = new ColetorDaPartida(local.Partida, ModoDoPerfil(), [.. Sessao.JogadoresLocais]);
                break;
            case SessaoHost host:
                host.PartidaIniciada += partida => { if (GravaNoPerfil) _coletor = new ColetorDaPartida(partida, ModoDaPartida.Online, 0); };
                break;
        }
    }

    private bool GravaNoPerfil => Sessao.JogadoresLocais.Count > 0 && (!_bot || PerfilLocal.CaminhoPedido is not null);

    private ModoDaPartida ModoDoPerfil() =>
        EmCarreira || _nomesDasDuplas is not null ? ModoDaPartida.Carreira
        : Configuracao.Modo == ModoDeJogo.CoopLocal ? ModoDaPartida.Coop
        : ModoDaPartida.Local;

    /// <summary>Uma vez por partida (as estatísticas somam): no fim, ou no abandono. Devolve as conquistas novas.</summary>
    private IReadOnlyList<Conquista> RegistrarNoPerfil()
    {
        if (_perfilRegistrado) return [];
        _perfilRegistrado = true;
        if (_coletor is ColetorDaPartida coletor)
        {
            var resumo = coletor.Resumo();
            coletor.Dispose();
            return PerfilLocal.DoJogo.Registrar(resumo);
        }
        if (Sessao is SessaoCliente { Cliente: var cliente } && GravaNoPerfil && cliente.Indice >= 0
            && cliente.UltimoInstantaneo?.Placar.Vencedor == cliente.Indice / 2)
        {
            try
            {
                return PerfilLocal.DoJogo.Registrar(new VitoriaOnline([.. cliente.Humanos], cliente.Indice));
            }
            catch (ArgumentException e)   // sala que o host montou torta: não vale conquista, e o fim da partida segue
            {
                GD.PushWarning($"Perfil: vitória online não registrada — {e.Message}");
            }
        }
        return [];
    }

    /// <summary>
    /// Fecha o jogo sem som tocando: para tudo e espera o servidor de áudio recolher as reproduções (um ciclo de
    /// mixagem, em tempo de relógio — com --fixed-fps o tempo do jogo não serve) antes do Quit. Sem isso o Godot fecha
    /// acusando "ObjectDB instances were leaked" e "resources still in use at exit" (o CI reprova: ferramentas/rodar_sem_tela.sh).
    /// </summary>
    private async void SairLimpo()
    {
        if (_saindo) return;
        _saindo = true;
        _som.Encerrar();
        await Task.Delay(250);
        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        // Fechar a janela no meio da partida sai pelo mesmo caminho limpo (o AutoAcceptQuit fica desligado só nesta cena).
        if (what == NotificationWMCloseRequest) SairLimpo();
    }

    private void DesenharReplay(RetratoDaPartida quadro, float delta)
    {
        _bola.Atualizar(quadro.Bola);
        foreach (var no in _jogadores) no.Atualizar(quadro, delta);
        _camera?.Replay(quadro.Bola.X, quadro.Bola.Y, quadro.Bola.Z, delta);
        _placar.Atualizar(DadosDoPlacar(quadro));
    }

    private void Desenhar(float delta)
    {
        var r = Sessao.Retrato;
        _bola.Atualizar(r.Bola);
        foreach (var no in _jogadores) no.Atualizar(r, delta);
        if (_camera is not null)
        {
            var locais = Sessao.JogadoresLocais;
            _camera.Lado = locais.Count > 0 && locais[0] >= 2 ? -1 : 1;   // atrás do time de quem joga aqui
            _camera.Seguir(r.Bola.X, delta);
        }
        _placar.Atualizar(DadosDoPlacar(r));
        if (r.CaixaDoSaque != _caixaMarcada)
        {
            _marcaDaCaixa?.QueueFree();
            _marcaDaCaixa = r.CaixaDoSaque is Caixa caixa ? _quadra.CriarMarcaDaCaixa(caixa) : null;
            _caixaMarcada = r.CaixaDoSaque;
        }
        foreach (var j in r.Jogadores) _som.TocarPassos(Coordenadas.NoChao(j.X, j.Y), MathF.Sqrt(j.Vx * j.Vx + j.Vy * j.Vy), j.Indice);
        foreach (var a in r.Acontecimentos) AoAcontecimento(a, r);
        // Replay só na partida local: no online a partida não pode parar pros outros.
        if (Sessao is SessaoLocal && _replay.Observar(r)) GD.Print($"Replay: começou em {_tempoVivo:F1} s ({r.Placar.Resumo} {r.Placar.Pontos[0]}-{r.Placar.Pontos[1]})");
        r.Acontecimentos.Clear();
    }

    private DadosDoPlacar DadosDoPlacar(RetratoDaPartida r) => new(
        Dupla(r, 0), Dupla(r, 1), SetsDoPlacar(r),
        r.Placar.Games[0], r.Placar.Games[1], r.Placar.Pontos[0], r.Placar.Pontos[1],
        r.Placar.Vencedor is null ? r.Placar.TimeSacando : -1,
        r.Placar.EmTieBreak, r.Placar.EmPontoDecisivo,
        r.Mensagem.Length > 0 ? r.Mensagem : null, r.MensagemEmDestaque, r.MensagemSuave,
        r.PingMs, r.Placar.Vencedor is not null);

    private SetAnterior[] SetsDoPlacar(RetratoDaPartida r)
    {
        var sets = r.Placar.SetsAnteriores;
        if (_setsDoPlacar.Length != sets.Count)   // muda uma vez por set: não aloca a cada quadro
            _setsDoPlacar = sets.Select(s => new SetAnterior(s.Games[0], s.Games[1], s.TieBreak?[0], s.TieBreak?[1])).ToArray();
        return _setsDoPlacar;
    }

    private string Dupla(RetratoDaPartida r, int time) =>
        _nomesDasDuplas is { } nomes ? nomes[time] : $"{r.Jogadores[time * 2].Nome} / {r.Jogadores[time * 2 + 1].Nome}";

    private void AoAcontecimento(Acontecimento a, RetratoDaPartida r)
    {
        var onde = Coordenadas.ParaGodot(a.X, a.Y, a.Z);
        switch (a.Tipo)
        {
            case TipoDeEventoDaPartida.Golpe:
                var golpe = a.Golpe ?? TipoDeGolpe.Normal;
                _som.TocarGolpe(onde, golpe.ToString(), ForcaDoGolpe(golpe));
                break;
            case TipoDeEventoDaPartida.Quique: _som.TocarQuique(onde); break;
            case TipoDeEventoDaPartida.Parede: _som.TocarParede(onde, SuperficieNoImpacto(a) == Superficie.Grade); break;
            case TipoDeEventoDaPartida.Rede: _som.TocarRede(onde); break;
            case TipoDeEventoDaPartida.Ponto: _som.Ponto(a.Time == TimeDaCasa()); break;
        }
        if (a.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
            GD.Print($"{a.Tipo}: time {a.Time} — {(a.Motivo is Motivo m ? Partida.Motivos[m] : "")} | {r.Placar.Resumo} {r.Placar.Pontos[0]}-{r.Placar.Pontos[1]}");
    }

    /// <summary>A "casa" do público é o time de quem joga nesta máquina (na demonstração, o time 0).</summary>
    private int TimeDaCasa() => Sessao.JogadoresLocais.Count > 0 ? Sessao.JogadoresLocais[0] / 2 : 0;

    /// <summary>Força do golpe pro volume do som (0 a 1): remate cheio, toque suave.</summary>
    private static float ForcaDoGolpe(TipoDeGolpe golpe) => golpe switch
    {
        TipoDeGolpe.Smash or TipoDeGolpe.SmashPor3 or TipoDeGolpe.SmashPor4 => 1f,
        TipoDeGolpe.Ataque or TipoDeGolpe.Vibora => 0.8f,
        TipoDeGolpe.Normal or TipoDeGolpe.Bandeja or TipoDeGolpe.Erro => 0.6f,
        TipoDeGolpe.Saque or TipoDeGolpe.Defesa => 0.5f,
        _ => 0.35f,   // lob, chiquita, contrapared: toque
    };

    /// <summary>Vidro ou grade no ponto do impacto — a mesma régua que a física usa (<see cref="Quadra.SuperficieDaParede"/>).</summary>
    private static Superficie SuperficieNoImpacto(Acontecimento a)
    {
        bool lateral = MathF.Abs(MathF.Abs(a.X) - Quadra.MeiaLargura) < MathF.Abs(MathF.Abs(a.Y) - Quadra.MeioComprimento);
        return lateral
            ? Quadra.SuperficieDaParede(QualParede.Lateral, a.Y, a.Z)
            : Quadra.SuperficieDaParede(QualParede.Fundo, a.X, a.Z);
    }
}
