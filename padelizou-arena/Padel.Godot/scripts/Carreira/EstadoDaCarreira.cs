using Godot;
using Padel.Core;
using Padel.Core.Torneio;

namespace Padel.Godot;

/// <summary>Como a abertura da carreira terminou.</summary>
public enum SituacaoDaCarreira
{
    /// <summary>Aberta.</summary>
    Carregada,
    /// <summary>Não há arquivo: pode começar outra.</summary>
    Ausente,
    /// <summary>Estava ilegível e foi guardada ao lado ("*.ilegivel-DATA"): pode começar outra.</summary>
    Ilegivel,
    /// <summary>Salva por um jogo mais novo: intocada; atualize o jogo.</summary>
    DeUmJogoMaisNovo,
    /// <summary>Não deu pra ler (arquivo preso, sem permissão), ou ilegível sem cópia: intocada; tente de novo.</summary>
    SemLeitura,
}

/// <summary>A situação da abertura e o que dizer ao jogador (null quando não há nada a dizer).</summary>
public sealed record CargaDaCarreira(SituacaoDaCarreira Situacao, string? Mensagem)
{
    /// <summary>Pode começar uma carreira nova sem apagar nada.</summary>
    public bool PodeComecarOutra => Situacao is SituacaoDaCarreira.Ausente or SituacaoDaCarreira.Ilegivel;
}

/// <summary>
/// A carreira em andamento, entre a tela de carreira e a partida: o circuito do Padel.Core.Torneio, salvo em
/// user://carreira.json (ou no --carreira ARQ) a cada mudança, e o jogo que a próxima partida decide. A lógica é toda do
/// Core; aqui só se guarda, se carrega e se traduz força de dupla em dificuldade de IA.
/// </summary>
public static class EstadoDaCarreira
{
    public const string Arquivo = "user://carreira.json";
    public const int ForcaDaDuplaDoJogador = 60;

    public static Carreira? Atual { get; private set; }

    /// <summary>
    /// O jogo que o "Jogar" da carreira mandou pra partida, com as opções DELE: a dificuldade do rival, o formato da etapa e
    /// a semente do jogo. Não passa pela Configuracao, que guarda as preferências do jogador (o menu as grava no
    /// configuracao.json): antes a carreira escrevia ali, e o próximo Jogar do menu salvava a dificuldade do rival.
    /// </summary>
    public sealed record JogoPedido(JogoDoTorneio Jogo, Dificuldade Dificuldade, int SetsParaVencer, uint Semente);

    private static JogoPedido? _jogoPedido;

    /// <summary>A carreira no disco é de um jogo mais novo, ou ilegível sem cópia: nada é gravado nela.</summary>
    private static bool _naoGravar;

    /// <summary>Modo de teste: a tela aperta "Jogar" sozinha e a partida volta pra ela sozinha (humano simulado).</summary>
    public static bool Automatico { get; set; }

    /// <summary>
    /// --carreira ARQ: a carreira noutro arquivo, o espelho do --perfil. Os modos de teste (--carreira-sozinha,
    /// --carreira-nova) exigem: sem ele jogariam, ou apagariam, a carreira de quem joga.
    /// </summary>
    public static string? CaminhoPedido { get; private set; }

    public static string Caminho => CaminhoPedido ?? ProjectSettings.GlobalizePath(Arquivo);

    /// <summary>Lê o --carreira ARQ. Idempotente: o menu, a carreira e a partida chamam (qualquer um pode ser a primeira cena).</summary>
    public static void LerLinhaDeComando(string[] args)
    {
        int i = Array.IndexOf(args, "--carreira");
        if (i < 0 || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) return;
        string caminho = Path.GetFullPath(args[i + 1]);
        if (caminho != CaminhoPedido) UsarArquivo(caminho);
    }

    /// <summary>Troca o arquivo da carreira (null = <see cref="Arquivo"/>) e esquece a que estava carregada.</summary>
    public static void UsarArquivo(string? caminho)
    {
        CaminhoPedido = caminho;
        Atual = null;
        _jogoPedido = null;
        _naoGravar = false;
        ErroAoSalvar = null;
    }

    /// <summary>O que o "Jogar" da tela da carreira faz antes de abrir a partida do jogo: guarda o pedido pra ela.</summary>
    public static void PrepararJogo(JogoDoTorneio jogo, Dificuldade dificuldade, int setsParaVencer)
    {
        var carreira = Atual ?? throw new InvalidOperationException("Jogar um jogo da carreira sem carreira carregada.");
        // Cada jogo com a sua semente, tirada da carreira, da etapa e do número do jogo: reproduzível, e nenhum jogo repete
        // o outro (com uma semente só, todo jogo contra a mesma dificuldade saía idêntico — visto no teste automático).
        _jogoPedido = new JogoPedido(jogo, dificuldade, setsParaVencer, SementeDoJogo(carreira.Semente, carreira.IndiceDaProximaEtapa, jogo.Numero));
    }

    /// <summary>
    /// A partida que nasce TOMA o jogo pedido: ele passa a morar nela e some daqui. O jogo morre com a partida — largada
    /// no meio (Pausa → Sair pro menu), a próxima partida (avulsa, coop, sala online) não decide esse jogo por engano.
    /// </summary>
    public static JogoPedido? TomarJogoPedido()
    {
        var pedido = _jogoPedido;
        _jogoPedido = null;
        return pedido;
    }

    /// <summary>
    /// Abre a carreira do <see cref="Caminho"/>, no padrão do <see cref="PerfilLocal"/>: o que não dá pra ler nunca é
    /// sobrescrito sem cópia. Só <see cref="SituacaoDaCarreira.Ausente"/> e <see cref="SituacaoDaCarreira.Ilegivel"/>
    /// (já guardada ao lado) deixam começar outra; nos outros casos a tela diz o que houve e nada é gravado.
    /// </summary>
    public static CargaDaCarreira Carregar()
    {
        Atual = null;
        _naoGravar = false;
        string caminho = Caminho;
        if (!File.Exists(caminho)) return new(SituacaoDaCarreira.Ausente, null);
        string texto;
        try { texto = File.ReadAllText(caminho); }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            // Preso pelo antivírus ou pela sincronização, sem permissão: passageiro. Começar outra gravaria por cima.
            GD.PushWarning($"Carreira em {caminho} não deu pra ler: {erro.Message}");
            return new(SituacaoDaCarreira.SemLeitura, $"Não deu pra ler a carreira salva ({erro.Message}). Nada foi mexido: feche o que estiver usando o arquivo e tente de novo.");
        }
        if (VersaoGravada(texto) is int versao && versao > Carreira.VersaoDoArquivo)
        {
            // De um jogo mais novo (Steam Cloud entre máquinas, beta desfeito): intocada, e nada é gravado nela.
            _naoGravar = true;
            GD.PushWarning($"Carreira em {caminho} é da versão {versao} do formato (este jogo lê a {Carreira.VersaoDoArquivo}): fica intocada.");
            return new(SituacaoDaCarreira.DeUmJogoMaisNovo, $"Sua carreira foi salva por uma versão mais nova do jogo. Atualize o jogo pra continuar — o arquivo não foi mexido.");
        }
        try
        {
            Atual = Carreira.Carregar(texto);
            return new(SituacaoDaCarreira.Carregada, null);
        }
        catch (InvalidDataException erro)
        {
            string guardada;
            try { guardada = PerfilLocal.GuardarIlegivel(caminho); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _naoGravar = true;
                GD.PushWarning($"Carreira em {caminho} ilegível ({erro.Message}) e não deu pra guardá-la ao lado ({e.Message}): fica intocada.");
                return new(SituacaoDaCarreira.SemLeitura, $"A carreira salva está ilegível e não deu pra guardar uma cópia dela ({e.Message}). Nada foi mexido.");
            }
            GD.PushWarning($"Carreira em {caminho} ilegível ({erro.Message}): guardada em {guardada}.");
            return new(SituacaoDaCarreira.Ilegivel, $"A carreira salva estava ilegível e foi guardada em {Path.GetFileName(guardada)}, ao lado; esta é uma carreira nova.");
        }
    }

    /// <summary>O "Versao" do arquivo, se ele for um JSON com esse número; null se nem isso (aí o Core diz o que está errado).</summary>
    private static int? VersaoGravada(string texto)
    {
        try
        {
            using var documento = System.Text.Json.JsonDocument.Parse(texto);
            return documento.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && documento.RootElement.TryGetProperty("Versao", out var versao)
                && versao.ValueKind == System.Text.Json.JsonValueKind.Number
                && versao.TryGetInt32(out int numero) ? numero : null;
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public static void Nova(string nomeDaDupla, uint semente)
    {
        Atual = Carreira.Nova(new DuplaParticipante(nomeDaDupla, ForcaDaDuplaDoJogador, humana: true), semente);
        Salvar();
    }

    /// <summary>A última gravação que falhou (disco cheio, sem permissão), pra tela dizer; null = a última deu certo.</summary>
    public static string? ErroAoSalvar { get; private set; }

    /// <summary>
    /// Grava num temporário e renomeia por cima (como o perfil): uma queda no meio não deixa o JSON pela metade, que a
    /// próxima abertura trataria como ilegível. Não grava na carreira de um jogo mais novo.
    /// </summary>
    public static void Salvar()
    {
        if (Atual is null || _naoGravar) return;
        string caminho = Caminho;
        string temporario = caminho + ".gravando";
        try
        {
            if (Path.GetDirectoryName(caminho) is string pasta && pasta.Length > 0) Directory.CreateDirectory(pasta);
            File.WriteAllText(temporario, Atual.Salvar());
            File.Move(temporario, caminho, overwrite: true);
            ErroAoSalvar = null;
        }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            ErroAoSalvar = erro.Message;
            GD.PushWarning($"Não consegui salvar a carreira em {caminho}: {erro.Message}");
            try { File.Delete(temporario); } catch (IOException) { }
        }
    }

    /// <summary>O próximo jogo da dupla do jogador na etapa em andamento, andando as rodadas das IAs até ele (ou até a etapa acabar).</summary>
    public static JogoDoTorneio? ProximoJogoDoJogador()
    {
        if (Atual?.EtapaEmAndamento is not TorneioDeDuplas torneio) return null;
        string eu = Atual.DuplaDoJogador.Nome;
        for (int guarda = 0; guarda < 64 && !torneio.Encerrado; guarda++)
        {
            var meu = torneio.JogosPendentes.FirstOrDefault(j => j.Envolve(eu));
            if (meu is not null) return meu;
            if (!torneio.AvancarRodada()) break;
        }
        Salvar();
        return null;
    }

    /// <summary>Guarda o resultado da partida jogada de verdade (a dupla do jogador foi o time 0).</summary>
    public static void InformarResultado(JogoDoTorneio jogo, Placar placar)
    {
        if (Atual?.EtapaEmAndamento is not TorneioDeDuplas torneio)
        {
            GD.PushWarning($"Carreira: o resultado do jogo {jogo.Numero} ({placar.Resumo()}) não foi guardado — não há etapa em andamento na carreira carregada.");
            return;
        }
        torneio.InformarResultado(jogo.Numero, placar, Atual.DuplaDoJogador.Nome);
        Salvar();
    }

    /// <summary>A força da dupla rival vira a dificuldade da IA na partida de verdade.</summary>
    public static Dificuldade DificuldadePara(int forcaDoRival) => forcaDoRival switch
    {
        < 50 => Dificuldade.Facil,
        < 75 => Dificuldade.Medio,
        _ => Dificuldade.Dificil,
    };

    /// <summary>Semente da partida de um jogo da carreira: mistura (splitmix32) da semente da carreira, da etapa e do número do jogo.</summary>
    public static uint SementeDoJogo(uint semente, int etapa, int numeroDoJogo)
    {
        uint x = semente ^ (uint)(etapa * 1000 + numeroDoJogo) * 0x9E3779B9u;
        x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
        x = (x ^ (x >> 13)) * 0xC2B2AE35u;
        return x ^ (x >> 16);
    }

    /// <summary>A fase como a transmissão diz (o enum do Core é identificador, não texto de tela).</summary>
    public static string NomeDaFase(FaseAlcancada fase) => fase switch
    {
        FaseAlcancada.FaseDeGrupos => "fase de grupos",
        FaseAlcancada.PrimeiraRodada => "primeira rodada",
        FaseAlcancada.Oitavas => "oitavas",
        FaseAlcancada.Quartas => "quartas",
        FaseAlcancada.Semifinal => "semifinal",
        FaseAlcancada.Final => "final (vice)",
        FaseAlcancada.Campeao => "campeões",
        _ => fase.ToString(),
    };

    public static string NomeDaCategoria(CategoriaDaEtapa c) => c switch { CategoriaDaEtapa.Major => "Major", _ => c.ToString() };
}
