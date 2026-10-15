using Godot;
using Padel.Core;
using Padel.Core.Rodizio;

namespace Padel.Godot;

/// <summary>Como a abertura do rodízio terminou.</summary>
public enum SituacaoDoRodizio
{
    /// <summary>Aberto.</summary>
    Carregado,
    /// <summary>Não há arquivo: pode criar outro.</summary>
    Ausente,
    /// <summary>Estava ilegível e foi guardado ao lado ("*.ilegivel-DATA"): pode criar outro.</summary>
    Ilegivel,
    /// <summary>Salvo por um jogo mais novo: intocado; atualize o jogo.</summary>
    DeUmJogoMaisNovo,
    /// <summary>Não deu pra ler (arquivo preso, sem permissão), ou ilegível sem cópia: intocado; tente de novo.</summary>
    SemLeitura,
}

/// <summary>A situação da abertura e o que dizer ao jogador (null quando não há nada a dizer).</summary>
public sealed record CargaDoRodizio(SituacaoDoRodizio Situacao, string? Mensagem)
{
    /// <summary>Pode criar um evento novo sem apagar nada.</summary>
    public bool PodeCriarOutro => Situacao is SituacaoDoRodizio.Ausente or SituacaoDoRodizio.Ilegivel;
}

/// <summary>
/// O Americano ou Mexicano em andamento, entre a tela do rodízio e a partida: o <see cref="TorneioDeRodizio"/> do
/// Padel.Core, salvo em user://rodizio.json (ou no --rodizio ARQ) a cada mudança — o arquivo é o JSON do próprio
/// torneio —, e o jogo que a próxima partida decide. A regra é toda do Core; aqui só se guarda, se carrega, se monta o
/// evento (quem joga, com que força) e se traduz a força dos rivais em dificuldade de IA, como a carreira.
/// </summary>
/// <remarks>
/// atalho: o carregar e o salvar repetem o padrão do <see cref="EstadoDaCarreira"/> (ilegível guardado ao lado,
/// versão mais nova intocada, gravação atômica) em vez de dividir código com ele, porque o EstadoDaCarreira.cs é de
/// outra onda; a saída é extrair um "ArquivoDoJogo" comum aos dois quando as ondas se juntarem.
/// </remarks>
public static class EstadoDoRodizio
{
    public const string Arquivo = "user://rodizio.json";

    /// <summary>A força do humano (0–100) — a mesma da dupla dele na carreira. Só pesa no Mexicano "por força".</summary>
    public const int ForcaDoJogador = EstadoDaCarreira.ForcaDaDuplaDoJogador;

    /// <summary>Os totais de pontos corridos que a tela oferece: os de clube que dão o mesmo número de saques às duplas.</summary>
    public static readonly int[] PontosOferecidos = [16, 24, 32];

    // Os adversários: nomes curtos (cabem no placar) e distintos no começo, pra ninguém se confundir numa tabela de 16.
    private static readonly string[] NomesDasIAs =
    [
        "Bia", "Caio", "Duda", "Edu", "Gabi", "Hugo", "Iara", "João", "Lara", "Léo",
        "Malu", "Nina", "Otávio", "Paula", "Rafa", "Sofia", "Téo", "Vini", "Yara", "Zeca",
    ];

    // As forças das IAs saem da semente do evento por esta parte (a tabela e os jogos usam as deles, no Core).
    private const uint ParteDasForcas = 0xF0C4;

    public static TorneioDeRodizio? Atual { get; private set; }

    /// <summary>O nome do humano no evento carregado (o primeiro; a tela joga com um só), ou null.</summary>
    public static string? NomeDoHumano => Atual?.Jogadores.FirstOrDefault(j => j.Humano)?.Nome;

    /// <summary>
    /// O jogo que o "Jogar" da tela mandou pra partida, com tudo o que ela precisa: o jogo (e a semente do evento, pra
    /// conferir na volta que o evento carregado é o mesmo), em que time da partida a DuplaA jogou, os quatro nomes na
    /// ordem da partida (humano, parceiro, rival, rival), a dificuldade pela força dos rivais, o total de pontos corridos
    /// e a semente da partida. Não passa pela Configuracao, que guarda as preferências do jogador.
    /// </summary>
    public sealed record JogoPedido(
        JogoDoRodizio Jogo,
        uint SementeDoEvento,
        int TimeDaDuplaA,
        IReadOnlyList<string> Nomes,
        Dificuldade Dificuldade,
        int PontosPorJogo,
        uint Semente,
        FormatoDoRodizio Formato)
    {
        /// <summary>As duplas como o placar mostra: a do humano (time 0) e a rival (time 1).</summary>
        public string[] NomesDasDuplas => [$"{Nomes[0]} / {Nomes[1]}", $"{Nomes[2]} / {Nomes[3]}"];
    }

    private static JogoPedido? _jogoPedido;

    /// <summary>O rodízio no disco é de um jogo mais novo, ou ilegível sem cópia: nada é gravado nele.</summary>
    private static bool _naoGravar;

    /// <summary>Modo de teste (--rodizio-sozinho): a tela aperta sozinha e a partida volta pra ela sozinha (humano simulado).</summary>
    public static bool Automatico { get; set; }

    /// <summary>--rodizio ARQ: o rodízio noutro arquivo. O modo de teste (--rodizio-sozinho) exige: sem ele jogaria o evento de quem joga.</summary>
    public static string? CaminhoPedido { get; private set; }

    public static string Caminho => CaminhoPedido ?? ProjectSettings.GlobalizePath(Arquivo);

    /// <summary>A última gravação que falhou (disco cheio, sem permissão), pra tela dizer; null = a última deu certo.</summary>
    public static string? ErroAoSalvar { get; private set; }

    /// <summary>
    /// A rodada cujos outros jogos a tela já mostrou ("Simular os outros jogos da rodada"). O Core simula os jogos das
    /// IAs quando a rodada abre; a tela os guarda até o jogador pedir, como numa noite de clube em que cada quadra joga
    /// ao mesmo tempo e o placar das outras só chega no fim. É só apresentação: fica na memória, e reabrir o jogo esconde
    /// de novo os da rodada atual (os das rodadas fechadas aparecem sempre).
    /// </summary>
    public static int RodadaRevelada { get; private set; } = -1;

    /// <summary>Os outros jogos da rodada atual já estão na tela.</summary>
    public static bool Revelada => Atual is { } rodizio && RodadaRevelada == rodizio.RodadaAtual;

    /// <summary>Lê o --rodizio ARQ. Idempotente: o menu, a tela do rodízio e a partida chamam (qualquer um pode ser a primeira cena).</summary>
    public static void LerLinhaDeComando(string[] args)
    {
        int i = Array.IndexOf(args, "--rodizio");
        if (i < 0 || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal)) return;
        string caminho = Path.GetFullPath(args[i + 1]);
        if (caminho != CaminhoPedido) UsarArquivo(caminho);
    }

    /// <summary>Troca o arquivo do rodízio (null = <see cref="Arquivo"/>) e esquece o que estava carregado.</summary>
    public static void UsarArquivo(string? caminho)
    {
        CaminhoPedido = caminho;
        Atual = null;
        _jogoPedido = null;
        _naoGravar = false;
        ErroAoSalvar = null;
        RodadaRevelada = -1;
    }

    /// <summary>
    /// Os jogadores de um evento novo: o humano (com <see cref="ForcaDoJogador"/>) e <paramref name="total"/> − 1 IAs
    /// com nomes da lista e forças variadas de 38 a 90 — o que cobre as três dificuldades da IA. Tudo sai da
    /// <paramref name="semente"/>: o mesmo evento, os mesmos adversários. Nome de IA igual ao do humano (sem diferenciar
    /// maiúscula) fica de fora: o nome é a identidade no rodízio.
    /// </summary>
    public static List<JogadorDoRodizio> JogadoresDoEvento(string humano, int total, uint semente)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(humano);
        if (total is < TabelaDoAmericano.MinimoDeJogadores or > TabelaDoAmericano.MaximoDeJogadores)
            throw new ArgumentOutOfRangeException(nameof(total), total, $"Um rodízio tem de {TabelaDoAmericano.MinimoDeJogadores} a {TabelaDoAmericano.MaximoDeJogadores} jogadores.");
        var aleatorio = new Aleatorio(semente ^ ParteDasForcas);
        var nomes = NomesDasIAs.Where(n => !string.Equals(n, humano, StringComparison.OrdinalIgnoreCase)).ToList();
        for (int i = nomes.Count - 1; i > 0; i--)
        {
            int j = (int)(aleatorio.Proximo() * (i + 1));
            (nomes[i], nomes[j]) = (nomes[j], nomes[i]);
        }
        var jogadores = new List<JogadorDoRodizio>(total) { new(humano, ForcaDoJogador, humano: true) };
        for (int i = 0; i < total - 1; i++) jogadores.Add(new JogadorDoRodizio(nomes[i], 38 + (int)(aleatorio.Proximo() * 53)));
        return jogadores;
    }

    /// <summary>Começa um evento (por cima do que houver no <see cref="Caminho"/>) e grava.</summary>
    public static void Nova(RegrasDoRodizio regras, IReadOnlyList<JogadorDoRodizio> jogadores, uint semente)
    {
        Atual = new TorneioDeRodizio(jogadores, regras, semente);
        RodadaRevelada = -1;
        Salvar();
    }

    /// <summary>
    /// Abre o rodízio do <see cref="Caminho"/>, no padrão do perfil e da carreira: o que não dá pra ler nunca é
    /// sobrescrito sem cópia. Só <see cref="SituacaoDoRodizio.Ausente"/> e <see cref="SituacaoDoRodizio.Ilegivel"/> (já
    /// guardado ao lado) deixam criar outro; nos outros casos a tela diz o que houve e nada é gravado.
    /// </summary>
    public static CargaDoRodizio Carregar()
    {
        Atual = null;
        _naoGravar = false;
        RodadaRevelada = -1;
        string caminho = Caminho;
        if (!File.Exists(caminho)) return new(SituacaoDoRodizio.Ausente, null);
        string texto;
        try { texto = File.ReadAllText(caminho); }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            // Preso pelo antivírus ou pela sincronização, sem permissão: passageiro. Criar outro gravaria por cima.
            GD.PushWarning($"Rodízio em {caminho} não deu pra ler: {erro.Message}");
            return new(SituacaoDoRodizio.SemLeitura, $"Não deu pra ler o evento salvo ({erro.Message}). Nada foi mexido: feche o que estiver usando o arquivo e tente de novo.");
        }
        if (VersaoGravada(texto) is int versao && versao > TorneioDeRodizio.VersaoDoArquivo)
        {
            // De um jogo mais novo (Steam Cloud entre máquinas, beta desfeito): intocado, e nada é gravado nele.
            _naoGravar = true;
            GD.PushWarning($"Rodízio em {caminho} é da versão {versao} do formato (este jogo lê a {TorneioDeRodizio.VersaoDoArquivo}): fica intocado.");
            return new(SituacaoDoRodizio.DeUmJogoMaisNovo, "Seu evento foi salvo por uma versão mais nova do jogo. Atualize o jogo pra continuar — o arquivo não foi mexido.");
        }
        try
        {
            Atual = TorneioDeRodizio.Carregar(texto);
            return new(SituacaoDoRodizio.Carregado, null);
        }
        catch (InvalidDataException erro)
        {
            string guardado;
            try { guardado = PerfilLocal.GuardarIlegivel(caminho); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _naoGravar = true;
                GD.PushWarning($"Rodízio em {caminho} ilegível ({erro.Message}) e não deu pra guardá-lo ao lado ({e.Message}): fica intocado.");
                return new(SituacaoDoRodizio.SemLeitura, $"O evento salvo está ilegível e não deu pra guardar uma cópia dele ({e.Message}). Nada foi mexido.");
            }
            GD.PushWarning($"Rodízio em {caminho} ilegível ({erro.Message}): guardado em {guardado}.");
            return new(SituacaoDoRodizio.Ilegivel, $"O evento salvo estava ilegível e foi guardado em {Path.GetFileName(guardado)}, ao lado. Crie outro.");
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

    /// <summary>
    /// Grava num temporário e renomeia por cima (como o perfil e a carreira): uma queda no meio não deixa o JSON pela
    /// metade, que a próxima abertura trataria como ilegível. Não grava no rodízio de um jogo mais novo.
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
            GD.PushWarning($"Não consegui salvar o rodízio em {caminho}: {erro.Message}");
            try { File.Delete(temporario); } catch (IOException) { }
        }
    }

    /// <summary>O jogo do humano na rodada atual (pendente ou já jogado); null se ele folga, ou se não há evento.</summary>
    public static JogoDoRodizio? JogoDoHumanoNaRodada() =>
        Atual is { } rodizio && NomeDoHumano is string eu ? rodizio.JogosDaRodadaAtual.FirstOrDefault(j => j.Envolve(eu)) : null;

    /// <summary>A força dos rivais que vira dificuldade: a média dos dois, arredondada (metade pra cima).</summary>
    public static int ForcaDaDupla(TorneioDeRodizio rodizio, DuplaDoRodizio dupla) =>
        (rodizio.Jogador(dupla.Jogador1).Forca + rodizio.Jogador(dupla.Jogador2).Forca + 1) / 2;

    /// <summary>O que o "Jogar" da tela faz antes de abrir a partida: guarda o pedido pra ela.</summary>
    public static void PrepararJogo(JogoDoRodizio jogo)
    {
        var rodizio = Atual ?? throw new InvalidOperationException("Jogar um jogo do rodízio sem evento carregado.");
        string eu = NomeDoHumano ?? throw new InvalidOperationException("Este rodízio não tem humano: não há jogo pra jogar.");
        bool euEmA = jogo.DuplaA.Tem(eu);
        if (!euEmA && !jogo.DuplaB.Tem(eu)) throw new ArgumentException($"O jogo {jogo.Numero} não é de {eu}.", nameof(jogo));
        var minha = euEmA ? jogo.DuplaA : jogo.DuplaB;
        var rivais = euEmA ? jogo.DuplaB : jogo.DuplaA;
        string parceiro = minha.Jogador1 == eu ? minha.Jogador2 : minha.Jogador1;
        _jogoPedido = new JogoPedido(
            jogo,
            rodizio.Semente,
            TimeDaDuplaA: euEmA ? 0 : 1,
            Nomes: [eu, parceiro, rivais.Jogador1, rivais.Jogador2],
            EstadoDaCarreira.DificuldadePara(ForcaDaDupla(rodizio, rivais)),
            rodizio.Regras.PontosPorJogo,
            // Cada jogo com a sua semente (a do evento, a rodada e o número): reproduzível, e nenhum jogo repete o outro.
            EstadoDaCarreira.SementeDoJogo(rodizio.Semente, jogo.Rodada, jogo.Numero),
            rodizio.Regras.Formato);
    }

    /// <summary>
    /// A partida que nasce TOMA o jogo pedido: ele passa a morar nela e some daqui. Largada no meio (Pausa → Sair pro
    /// menu), a próxima partida (avulsa, coop, sala online) não decide esse jogo por engano.
    /// </summary>
    public static JogoPedido? TomarJogoPedido()
    {
        var pedido = _jogoPedido;
        _jogoPedido = null;
        return pedido;
    }

    /// <summary>
    /// Leva o placar final da partida ao rodízio (<see cref="PartidaNoRodizio"/>) e grava. Devolve o que impediu —
    /// evento trocado, placar que o rodízio recusa, gravação que falhou — pra tela de fim dizer; null = guardado.
    /// </summary>
    public static string? InformarResultado(JogoPedido pedido, Placar placar)
    {
        if (Atual is not { } rodizio)
            return Recusar(pedido, placar, "não há evento carregado");
        var jogo = rodizio.Jogos.FirstOrDefault(j => j.Numero == pedido.Jogo.Numero);
        if (rodizio.Semente != pedido.SementeDoEvento || jogo is null || jogo.DuplaA != pedido.Jogo.DuplaA || jogo.DuplaB != pedido.Jogo.DuplaB)
            return Recusar(pedido, placar, "o evento carregado não é o desta partida");
        try
        {
            rodizio.InformarResultado(pedido.Jogo.Numero, placar, pedido.TimeDaDuplaA);
        }
        catch (Exception erro) when (erro is ArgumentException or InvalidOperationException)
        {
            return Recusar(pedido, placar, erro.Message);
        }
        Salvar();
        return ErroAoSalvar is string falha ? $"o resultado ficou no jogo aberto, mas não deu pra salvar o evento ({falha}): ele se perde ao fechar o jogo" : null;
    }

    private static string Recusar(JogoPedido pedido, Placar placar, string motivo)
    {
        GD.PushWarning($"Rodízio: o resultado do jogo {pedido.Jogo.Numero} ({placar.Resumo()}) não foi guardado — {motivo}.");
        return $"o resultado ({placar.Resumo()}) não foi guardado: {motivo}";
    }

    /// <summary>A tela mostra os outros jogos da rodada atual (e a classificação passa a contá-los).</summary>
    public static void Revelar()
    {
        if (Atual is { } rodizio) RodadaRevelada = rodizio.RodadaAtual;
    }

    /// <summary>
    /// Fecha a rodada e abre a próxima (no Mexicano, montada pela classificação), e grava. Devolve o motivo de não ter
    /// andado — falta resultado, ou o evento acabou —, ou null se andou.
    /// </summary>
    public static string? ProximaRodada()
    {
        if (Atual is not { } rodizio) return "não há evento carregado.";
        if (rodizio.Encerrado) return "o evento já acabou.";
        if (!rodizio.AvancarRodada())
            return $"a rodada {rodizio.RodadaAtual + 1} ainda tem jogo sem resultado ({string.Join(", ", rodizio.JogosPendentes.Select(j => $"jogo {j.Numero}"))}).";
        Salvar();
        return null;
    }

    /// <summary>
    /// A classificação que a tela mostra. Com os outros jogos da rodada já na tela (<see cref="Revelada"/>), é a do
    /// rodízio. Antes disso, é a das rodadas fechadas mais o jogo do próprio humano nesta — sem ela, a tabela somaria
    /// pontos de jogos que a tela diz que ainda não apareceram. As folgas são as das rodadas fechadas, como o Core conta.
    /// </summary>
    public static List<LinhaDoRodizio> ClassificacaoVisivel()
    {
        if (Atual is not { } rodizio) return [];
        if (Revelada) return rodizio.Classificacao();
        string? eu = NomeDoHumano;
        var visiveis = rodizio.Jogos.Where(j => j.Rodada < rodizio.RodadaAtual || (eu is not null && j.Rodada == rodizio.RodadaAtual && j.Envolve(eu))).ToList();
        var folgas = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int r = 0; r < rodizio.RodadaAtual; r++)
            foreach (var nome in rodizio.FolgasDaRodada(r)) folgas[nome] = folgas.GetValueOrDefault(nome) + 1;
        return ClassificacaoDoRodizio.Ordenar(rodizio.Jogadores.Select(j => j.Nome).ToList(), visiveis, rodizio.Regras.PontosPorJogo, folgas);
    }

    /// <summary>O formato como a tela diz.</summary>
    public static string NomeDoFormato(FormatoDoRodizio formato) => formato == FormatoDoRodizio.Mexicano ? "Mexicano" : "Americano";
}
