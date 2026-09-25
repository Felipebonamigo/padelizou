using Godot;
using Padel.Core.Perfil;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>O que houve com o arquivo do perfil — pra tela de fim e a tela Perfil avisarem, e não só o log.</summary>
public enum SituacaoDoPerfil
{
    /// <summary>Lido e gravado.</summary>
    Normal,
    /// <summary>Estava ilegível: foi guardado ao lado ("*.ilegivel-DATA") e um perfil novo começou (e grava).</summary>
    IlegivelGuardado,
    /// <summary>De um jogo mais novo: intocado; nada desta sessão é gravado (atualize o jogo).</summary>
    DeUmJogoMaisNovo,
    /// <summary>Não deu pra ler (preso, sem permissão), ou ilegível sem como guardar a cópia: intocado; tenta de novo na próxima.</summary>
    SemLeitura,
    /// <summary>A última gravação falhou (disco cheio, sem permissão): o progresso fica em memória e a próxima tenta de novo.</summary>
    GravacaoFalhou,
}

/// <summary>
/// O perfil do jogador no disco (é o arquivo que vai pro Steam Cloud): carrega, aplica o <see cref="AvaliadorDeConquistas"/>
/// do Core e grava. O relógio é lido aqui, fora do Core. docs/CONQUISTAS.md, "O que o jogo precisa ligar".
/// <list type="bullet">
/// <item>Arquivo ausente: perfil novo com o nome e a mão das opções (nada é gravado até registrar alguma coisa).</item>
/// <item>Arquivo corrompido: NUNCA é apagado — é renomeado pra "*.ilegivel-DATA" ao lado, e o jogo segue com um perfil novo.</item>
/// <item>Arquivo de uma versão mais nova do jogo: não é tocado. O jogo segue com um perfil em memória e não grava nada,
///   pra quem voltar à versão nova achar o progresso onde deixou.</item>
/// <item>Arquivo que não dá pra ler (preso, sem permissão): não é tocado, e o jogo tenta de novo na próxima gravação.</item>
/// <item>Gravação atômica: escreve num temporário e renomeia por cima — queda de energia não deixa arquivo pela metade.</item>
/// <item>Toda gravação relê o disco antes: duas instâncias do jogo no mesmo arquivo somam, não apagam uma à outra.</item>
/// <item>Nada disso fica só no log: <see cref="Situacao"/> e <see cref="Aviso"/> vão pra tela de fim e pra tela Perfil.</item>
/// </list>
/// </summary>
public sealed class PerfilLocal
{
    public const string ArquivoDoUsuario = "user://perfil.json";

    private static PerfilLocal? _doJogo;
    private static string? _caminhoPedido;

    /// <summary>
    /// --perfil ARQ: grava noutro arquivo (testes sem tela; o --bot e a carreira automática só gravam com ele). Trocar o
    /// caminho troca o <see cref="DoJogo"/>.
    /// </summary>
    public static string? CaminhoPedido
    {
        get => _caminhoPedido;
        set
        {
            if (value == _caminhoPedido) return;
            _caminhoPedido = value;
            _doJogo = null;
        }
    }

    /// <summary>Lê o --perfil ARQ (idempotente; a partida e a carreira chamam, porque qualquer uma pode ser a primeira cena).</summary>
    public static void LerLinhaDeComando(string[] args)
    {
        int i = Array.IndexOf(args, "--perfil");
        if (i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) CaminhoPedido = Path.GetFullPath(args[i + 1]);
    }

    /// <summary>O perfil do jogo: <see cref="ArquivoDoUsuario"/>, ou o <see cref="CaminhoPedido"/>.</summary>
    public static PerfilLocal DoJogo => _doJogo ??= new PerfilLocal(CaminhoPedido ?? ProjectSettings.GlobalizePath(ArquivoDoUsuario));

    private readonly string _caminho;
    /// <summary>O último perfil conhecido: o do disco com o que ainda não foi gravado por cima.</summary>
    private PerfilDoJogador? _atual;
    /// <summary>O último perfil lido do disco nesta sessão (base quando uma releitura falha).</summary>
    private PerfilDoJogador? _doDisco;
    /// <summary>
    /// O que foi aplicado e ainda não está no disco (gravação que falhou, arquivo que não deu pra ler), na ordem, cada um
    /// com o instante em que aconteceu. Toda gravação relê o disco e reaplica isto por cima: o progresso não se perde, e o
    /// que outra instância gravou no meio também não.
    /// </summary>
    private readonly List<Func<PerfilDoJogador, ResultadoDaAvaliacao>> _pendentes = [];
    /// <summary>O arquivo ilegível guardado ao lado nesta sessão (o aviso fica até fechar o jogo).</summary>
    private string? _guardadoEm;

    public PerfilLocal(string caminho) => _caminho = caminho;

    public string Caminho => _caminho;
    public PerfilDoJogador Atual => _atual ??= Ler();

    /// <summary>O que houve com o arquivo na última leitura ou gravação.</summary>
    public SituacaoDoPerfil Situacao { get; private set; }

    /// <summary>O que dizer ao jogador sobre o perfil (tela de fim, tela Perfil); null quando está tudo gravado e em ordem.</summary>
    public string? Aviso { get; private set; }

    /// <summary>Tudo o que foi registrado está no disco. Falso: há progresso só em memória (e conquista que não foi salva).</summary>
    public bool Gravado => _pendentes.Count == 0;

    /// <summary>Uma partida terminada ou abandonada. Chame UMA vez por partida: as estatísticas somam.</summary>
    public IReadOnlyList<Conquista> Registrar(ResumoDaPartida resumo)
    {
        var agora = DateTimeOffset.UtcNow;
        return Aplicar(perfil => AvaliadorDeConquistas.Aplicar(perfil, resumo, agora));
    }

    /// <summary>Carreira (etapa vencida, circuito encerrado) e vitória online do cliente.</summary>
    public IReadOnlyList<Conquista> Registrar(EventoDeFora evento)
    {
        var agora = DateTimeOffset.UtcNow;
        return Aplicar(perfil => AvaliadorDeConquistas.Aplicar(perfil, evento, agora));
    }

    /// <summary>
    /// O jogador trocou o nome ou a mão nas Opções: vai pro perfil com o instante da troca, que é o que decide a mesclagem
    /// do Cloud (docs/CONQUISTAS.md). Só quando ELE troca — jogar uma partida não é troca de preferência.
    /// </summary>
    public void MudarPreferencias(string nome, bool destro)
    {
        var agora = DateTimeOffset.UtcNow;
        Aplicar(perfil => new ResultadoDaAvaliacao(perfil.ComPreferencias(nome, destro, agora), []));
    }

    /// <summary>
    /// Relê o disco (outra instância do jogo pode ter gravado) — pra tela Perfil mostrar o que está lá. Com progresso pendente
    /// (gravação que falhou, arquivo que não deu pra ler) é uma "próxima vez": tenta gravá-lo de novo. Sem isso a leitura boa
    /// zerava o aviso com o progresso ainda fora do disco (Situacao Normal com Gravado falso), e quem largou a partida — sem
    /// tela de fim — nunca sabia que o progresso não foi salvo.
    /// </summary>
    public PerfilDoJogador Reler()
    {
        var perfil = Ler();
        _atual = perfil;
        if (!Gravado) Salvar(perfil);   // a leitura que falhou (versão mais nova, preso) não grava: o Salvar respeita a situação
        return perfil;
    }

    /// <summary>
    /// Relê o disco e aplica por cima: duas instâncias no mesmo arquivo (host e cliente na mesma máquina, o jogo aberto duas
    /// vezes) somam em vez de uma gravar a memória velha por cima da outra.
    /// atalho: sem trava entre processos — a janela que sobra é o ler-aplicar-gravar, milissegundos, contra partidas que
    /// levam minutos. Saída: um Mutex nomeado pelo caminho em volta deste método, se aparecer perda com duas gravando juntas.
    /// </summary>
    private IReadOnlyList<Conquista> Aplicar(Func<PerfilDoJogador, ResultadoDaAvaliacao> mudanca)
    {
        var resultado = mudanca(Ler());
        _pendentes.Add(mudanca);
        _atual = resultado.Perfil;
        Salvar(resultado.Perfil);
        foreach (var c in resultado.Novas) GD.Print($"Conquista: {c.Id} — {c.Nome.Portugues}{(Gravado ? "" : " (não salva)")}");
        // Steam: SetAchievement(c.Id) pra cada nova, SetStat das estatísticas e StoreStats — entra com o
        // Facepunch.Steamworks (D5, M4). Até lá o perfil no disco é a fonte, e a sincronia na abertura cobre o atraso.
        return resultado.Novas;
    }

    private PerfilDoJogador Novo() => PerfilDoJogador.Novo(Configuracao.NomeDoJogador, Configuracao.Destro, DateTimeOffset.UtcNow);

    /// <summary>O perfil do disco (ou a base possível, se não deu pra ler) com o que ainda não foi gravado por cima.</summary>
    private PerfilDoJogador Ler()
    {
        var perfil = Carregar();
        foreach (var pendente in _pendentes) perfil = pendente(perfil).Perfil;
        return perfil;
    }

    /// <summary>Lê o arquivo e acerta a <see cref="Situacao"/>. Nunca lança por causa do disco.</summary>
    private PerfilDoJogador Carregar()
    {
        if (!File.Exists(_caminho)) return Normal(_doDisco = Novo());
        string texto;
        try { texto = File.ReadAllText(_caminho); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Preso por outro programa, sem permissão: passageiro. Gravar agora seria por cima do que não deu pra ler.
            GD.PushWarning($"Perfil em {_caminho} não deu pra ler ({e.Message}): nada é gravado até conseguir.");
            return Problema(SituacaoDoPerfil.SemLeitura, $"Não deu pra ler o seu perfil ({e.Message}). O progresso desta sessão fica guardado enquanto o jogo estiver aberto, e o jogo tenta de novo na próxima partida.");
        }
        try
        {
            return Normal(_doDisco = PersistenciaDoPerfil.Carregar(texto));
        }
        catch (PerfilIlegivelException e) when (e.VeioDeUmJogoMaisNovo)
        {
            GD.PushWarning($"Perfil em {_caminho} é de uma versão mais nova do jogo (versão {e.Versao}): fica intocado; este jogo segue sem gravar progresso.");
            return Problema(SituacaoDoPerfil.DeUmJogoMaisNovo, "Seu perfil é de uma versão mais nova do jogo: atualize o jogo. O progresso desta sessão não será salvo.");
        }
        catch (PerfilIlegivelException e)
        {
            try { _guardadoEm = GuardarIlegivel(); }
            catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
            {
                GD.PushWarning($"Perfil em {_caminho} ilegível ({e.Message}) e não deu pra guardá-lo ao lado ({erro.Message}): fica intocado; nada é gravado.");
                return Problema(SituacaoDoPerfil.SemLeitura, $"Seu perfil está ilegível e não deu pra guardar uma cópia dele ({erro.Message}). Nada foi mexido; o progresso desta sessão não será salvo.");
            }
            GD.PushWarning($"Perfil em {_caminho} ilegível ({e.Message}): guardado em {_guardadoEm}; começando um perfil novo.");
            return Normal(_doDisco = Novo());
        }
    }

    /// <summary>Leitura boa: a situação é normal (ou "ilegível guardado", que avisa até fechar o jogo).</summary>
    private PerfilDoJogador Normal(PerfilDoJogador perfil)
    {
        Situacao = _guardadoEm is null ? SituacaoDoPerfil.Normal : SituacaoDoPerfil.IlegivelGuardado;
        Aviso = _guardadoEm is null ? null : $"Seu perfil estava ilegível e foi guardado em {Path.GetFileName(_guardadoEm)}, ao lado; este é um perfil novo.";
        return perfil;
    }

    /// <summary>Leitura que não pode ser gravada por cima: segue em memória, sobre o último perfil lido (ou um novo).</summary>
    private PerfilDoJogador Problema(SituacaoDoPerfil situacao, string aviso)
    {
        Situacao = situacao;
        Aviso = aviso;
        return _doDisco ?? Novo();
    }

    private string GuardarIlegivel() => GuardarIlegivel(_caminho);

    /// <summary>
    /// Renomeia um save que não deu pra ler pra "*.ilegivel-DATA" ao lado (nunca apaga), pra investigar ou recuperar.
    /// Lança IOException/UnauthorizedAccessException se não der — aí quem chama NÃO pode gravar por cima.
    /// A carreira usa o mesmo.
    /// </summary>
    internal static string GuardarIlegivel(string caminho)
    {
        string baseDoNome = $"{caminho}.ilegivel-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        string destino = baseDoNome;
        for (int n = 2; File.Exists(destino); n++) destino = $"{baseDoNome}-{n}";
        File.Move(caminho, destino);
        return destino;
    }

    /// <summary>Grava (temporário + renomear) se a leitura deixou; tudo gravado limpa as pendências.</summary>
    private void Salvar(PerfilDoJogador perfil)
    {
        if (Situacao is SituacaoDoPerfil.DeUmJogoMaisNovo or SituacaoDoPerfil.SemLeitura) return;
        string temporario = _caminho + ".gravando";
        try
        {
            if (Path.GetDirectoryName(_caminho) is string pasta && pasta.Length > 0) Directory.CreateDirectory(pasta);
            File.WriteAllText(temporario, PersistenciaDoPerfil.Salvar(perfil));
            File.Move(temporario, _caminho, overwrite: true);
            _pendentes.Clear();
            _doDisco = perfil;
            Normal(perfil);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Disco cheio ou sem permissão: o progresso fica nas pendências e a próxima gravação tenta de novo.
            GD.PushWarning($"Não consegui gravar o perfil em {_caminho}: {e.Message}");
            Situacao = SituacaoDoPerfil.GravacaoFalhou;
            Aviso = $"Não deu pra salvar o perfil ({e.Message}). O progresso fica guardado enquanto o jogo estiver aberto, e o jogo tenta de novo na próxima partida.";
            try { File.Delete(temporario); } catch (Exception erro) when (erro is IOException or UnauthorizedAccessException) { }
        }
    }
}
