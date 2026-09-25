using Godot;
using Padel.Core.Perfil;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>
/// O perfil do jogador no disco (é o arquivo que vai pro Steam Cloud): carrega, aplica o <see cref="AvaliadorDeConquistas"/>
/// do Core e grava. O relógio é lido aqui, fora do Core. docs/CONQUISTAS.md, "O que o jogo precisa ligar".
/// <list type="bullet">
/// <item>Arquivo ausente: perfil novo com o nome e a mão das opções (nada é gravado até registrar alguma coisa).</item>
/// <item>Arquivo corrompido: NUNCA é apagado — é renomeado pra "*.ilegivel-DATA" ao lado, e o jogo segue com um perfil novo.</item>
/// <item>Arquivo de uma versão mais nova do jogo: não é tocado. O jogo segue com um perfil em memória e não grava nada,
///   pra quem voltar à versão nova achar o progresso onde deixou.</item>
/// <item>Gravação atômica: escreve num temporário e renomeia por cima — queda de energia não deixa arquivo pela metade.</item>
/// </list>
/// </summary>
public sealed class PerfilLocal
{
    public const string ArquivoDoUsuario = "user://perfil.json";

    private static PerfilLocal? _doJogo;

    /// <summary>--perfil ARQ: grava noutro arquivo (testes sem tela; o --bot e a carreira automática só gravam com ele).</summary>
    public static string? CaminhoPedido { get; set; }

    /// <summary>Lê o --perfil ARQ (idempotente; a partida e a carreira chamam, porque qualquer uma pode ser a primeira cena).</summary>
    public static void LerLinhaDeComando(string[] args)
    {
        int i = Array.IndexOf(args, "--perfil");
        if (i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) CaminhoPedido = Path.GetFullPath(args[i + 1]);
    }

    /// <summary>O perfil do jogo: <see cref="ArquivoDoUsuario"/>, ou o <see cref="CaminhoPedido"/>.</summary>
    public static PerfilLocal DoJogo => _doJogo ??= new PerfilLocal(CaminhoPedido ?? ProjectSettings.GlobalizePath(ArquivoDoUsuario));

    private readonly string _caminho;
    private PerfilDoJogador? _atual;
    private bool _naoGravar;

    public PerfilLocal(string caminho) => _caminho = caminho;

    public string Caminho => _caminho;
    public PerfilDoJogador Atual => _atual ??= Carregar();

    /// <summary>Uma partida terminada ou abandonada. Chame UMA vez por partida: as estatísticas somam.</summary>
    public IReadOnlyList<Conquista> Registrar(ResumoDaPartida resumo) => Aplicar(AvaliadorDeConquistas.Aplicar(Atual, resumo, DateTimeOffset.UtcNow));

    /// <summary>Carreira (etapa vencida, circuito encerrado) e vitória online do cliente.</summary>
    public IReadOnlyList<Conquista> Registrar(EventoDeFora evento) => Aplicar(AvaliadorDeConquistas.Aplicar(Atual, evento, DateTimeOffset.UtcNow));

    private IReadOnlyList<Conquista> Aplicar(ResultadoDaAvaliacao resultado)
    {
        _atual = resultado.Perfil;
        Salvar();
        foreach (var c in resultado.Novas) GD.Print($"Conquista: {c.Id} — {c.Nome.Portugues}");
        // Steam: SetAchievement(c.Id) pra cada nova, SetStat das estatísticas e StoreStats — entra com o
        // Facepunch.Steamworks (D5, M4). Até lá o perfil no disco é a fonte, e a sincronia na abertura cobre o atraso.
        return resultado.Novas;
    }

    private PerfilDoJogador Novo() => PerfilDoJogador.Novo(Configuracao.NomeDoJogador, Configuracao.Destro, DateTimeOffset.UtcNow);

    private PerfilDoJogador Carregar()
    {
        if (!File.Exists(_caminho)) return Novo();
        try
        {
            return PersistenciaDoPerfil.Carregar(File.ReadAllText(_caminho));
        }
        catch (PerfilIlegivelException e) when (e.VeioDeUmJogoMaisNovo)
        {
            _naoGravar = true;
            GD.PushWarning($"Perfil em {_caminho} é de uma versão mais nova do jogo (versão {e.Versao}): fica intocado; este jogo segue sem gravar progresso.");
            return Novo();
        }
        catch (PerfilIlegivelException e)
        {
            string guardado = GuardarIlegivel();
            GD.PushWarning($"Perfil em {_caminho} ilegível ({e.Message}): guardado em {guardado}; começando um perfil novo.");
            return Novo();
        }
    }

    private string GuardarIlegivel()
    {
        string baseDoNome = $"{_caminho}.ilegivel-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        string destino = baseDoNome;
        for (int n = 2; File.Exists(destino); n++) destino = $"{baseDoNome}-{n}";
        File.Move(_caminho, destino);
        return destino;
    }

    private void Salvar()
    {
        if (_naoGravar || _atual is not PerfilDoJogador perfil) return;
        string temporario = _caminho + ".gravando";
        try
        {
            if (Path.GetDirectoryName(_caminho) is string pasta && pasta.Length > 0) Directory.CreateDirectory(pasta);
            File.WriteAllText(temporario, PersistenciaDoPerfil.Salvar(perfil));
            File.Move(temporario, _caminho, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Disco cheio ou sem permissão: o progresso fica em memória e a próxima gravação tenta de novo.
            GD.PushWarning($"Não consegui gravar o perfil em {_caminho}: {e.Message}");
            try { File.Delete(temporario); } catch (IOException) { }
        }
    }
}
