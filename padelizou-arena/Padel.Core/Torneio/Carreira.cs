using System.Text.Json;
using System.Text.Json.Serialization;

namespace Padel.Core.Torneio;

/// <summary>Uma etapa do circuito: nome, peso no ranking, tamanho e formato.</summary>
public sealed record EtapaDoCircuito
{
    public EtapaDoCircuito(string nome, CategoriaDaEtapa categoria, int duplas, FormatoDoTorneio formato, int setsParaVencer = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (!Enum.IsDefined(categoria)) throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoria desconhecida.");
        if (!Enum.IsDefined(formato)) throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato desconhecido.");
        if (duplas is < TorneioDeDuplas.MinimoDeDuplas or > TorneioDeDuplas.MaximoDeDuplas)
            throw new ArgumentOutOfRangeException(nameof(duplas), duplas, $"Uma etapa tem de {TorneioDeDuplas.MinimoDeDuplas} a {TorneioDeDuplas.MaximoDeDuplas} duplas.");
        if (setsParaVencer is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(setsParaVencer), setsParaVencer, "De 1 a 3 sets pra vencer.");
        Nome = nome;
        Categoria = categoria;
        Duplas = duplas;
        Formato = formato;
        SetsParaVencer = setsParaVencer;
    }

    public string Nome { get; }
    public CategoriaDaEtapa Categoria { get; }
    public int Duplas { get; }
    public FormatoDoTorneio Formato { get; }
    public int SetsParaVencer { get; }
}

public sealed record PontuacaoNaEtapa(string Dupla, FaseAlcancada Fase, int Pontos);

public sealed record ResultadoDaEtapa(string Etapa, CategoriaDaEtapa Categoria, string Campeao, IReadOnlyList<PontuacaoNaEtapa> Pontuacoes);

public sealed record LinhaDoRanking(int Posicao, string Dupla, int Pontos, int Titulos);

/// <summary>
/// A carreira: um circuito de etapas de força crescente, a dupla do jogador contra um elenco
/// fixo de rivais, pontos de ranking por fase alcançada (<see cref="EscalaDePontos"/>) e o
/// ranking acumulado — que entra em cada etapa como o degrau "ranking" do desempate de grupo,
/// como o ranking anual entra no Padelizou. Salva e carrega em JSON com número de versão,
/// inclusive no meio de uma etapa.
/// </summary>
public sealed class Carreira
{
    /// <summary>A versão do formato do arquivo. Mudou o formato, sobe o número — e o carregar aprende a ler a antiga ou recusa.</summary>
    public const int VersaoDoArquivo = 1;

    /// <summary>Seis etapas: duas P2, duas P1, dois Majors, alternando grupos e chave direta, de 8 a 16 duplas.</summary>
    public static IReadOnlyList<EtapaDoCircuito> CircuitoPadrao { get; } =
    [
        new("Aberto de Porto Alegre", CategoriaDaEtapa.P2, 8, FormatoDoTorneio.GruposEMataMata),
        new("Aberto de Curitiba", CategoriaDaEtapa.P2, 8, FormatoDoTorneio.ChaveDireta),
        new("Aberto de Florianópolis", CategoriaDaEtapa.P1, 12, FormatoDoTorneio.GruposEMataMata),
        new("Aberto de Belo Horizonte", CategoriaDaEtapa.P1, 12, FormatoDoTorneio.ChaveDireta),
        new("Major de São Paulo", CategoriaDaEtapa.Major, 16, FormatoDoTorneio.GruposEMataMata),
        new("Major do Rio de Janeiro", CategoriaDaEtapa.Major, 16, FormatoDoTorneio.ChaveDireta),
    ];

    // Força dos rivais: do mais fraco ao mais forte do circuito, com um tremor pra ninguém
    // empatar em escada perfeita. O jogador escolhe a força da própria dupla.
    private const int ForcaDoRivalMaisFraco = 30;
    private const int ForcaDoRivalMaisForte = 95;
    private const int TremorDaForca = 3;

    private static readonly string[] Sobrenomes =
    [
        "Almeida", "Barros", "Cardoso", "Duarte", "Esteves", "Falcão", "Gomes", "Hartmann", "Ibarra", "Jardim",
        "Klein", "Lacerda", "Machado", "Nogueira", "Oliveira", "Pacheco", "Queiroz", "Ramos", "Sampaio", "Teixeira",
        "Uchoa", "Vieira", "Xavier", "Zanetti", "Bezerra", "Castro", "Dias", "Fontes", "Guimarães", "Lopes",
        "Moreira", "Nunes", "Pires", "Rezende", "Siqueira", "Tavares", "Valente", "Werneck", "Assis", "Brandão",
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<ResultadoDaEtapa> _resultados;

    public uint Semente { get; }
    public DuplaParticipante DuplaDoJogador { get; }
    public IReadOnlyList<EtapaDoCircuito> Etapas { get; }
    public IReadOnlyList<DuplaParticipante> Rivais { get; }
    public IReadOnlyList<ResultadoDaEtapa> Resultados => _resultados;
    public TorneioDeDuplas? EtapaEmAndamento { get; private set; }
    public int IndiceDaProximaEtapa => _resultados.Count;
    public bool Concluida => _resultados.Count >= Etapas.Count;

    private Carreira(uint semente, DuplaParticipante duplaDoJogador, List<EtapaDoCircuito> etapas,
        List<DuplaParticipante> rivais, List<ResultadoDaEtapa> resultados, TorneioDeDuplas? emAndamento)
    {
        if (etapas.Count == 0) throw new ArgumentException("Um circuito precisa de pelo menos uma etapa.", nameof(etapas));
        if (resultados.Count > etapas.Count) throw new ArgumentException("Mais resultados do que etapas.", nameof(resultados));
        if (emAndamento is not null && resultados.Count >= etapas.Count)
            throw new ArgumentException("Etapa em andamento numa carreira que já acabou.", nameof(emAndamento));
        int vagas = etapas.Max(e => e.Duplas) - 1;
        if (rivais.Count < vagas)
            throw new ArgumentException($"A maior etapa pede {vagas} rivais; há {rivais.Count}.", nameof(rivais));
        var nomes = new HashSet<string>(StringComparer.Ordinal) { duplaDoJogador.Nome };
        foreach (var rival in rivais)
            if (!nomes.Add(rival.Nome))
                throw new ArgumentException($"Duas duplas com o nome \"{rival.Nome}\" no circuito.", nameof(rivais));

        Semente = semente;
        DuplaDoJogador = duplaDoJogador;
        Etapas = etapas;
        Rivais = rivais;
        _resultados = resultados;
        EtapaEmAndamento = emAndamento;
    }

    /// <summary>
    /// Uma carreira nova. Os rivais saem da semente: a mesma semente gera o mesmo circuito,
    /// com os mesmos nomes e forças.
    /// </summary>
    public static Carreira Nova(DuplaParticipante duplaDoJogador, uint semente, IReadOnlyList<EtapaDoCircuito>? etapas = null)
    {
        ArgumentNullException.ThrowIfNull(duplaDoJogador);
        var circuito = (etapas ?? CircuitoPadrao).ToList();
        if (circuito.Count == 0 || circuito.Any(e => e is null))
            throw new ArgumentException("Um circuito precisa de pelo menos uma etapa, e nenhuma vazia.", nameof(etapas));

        // Rivais suficientes pra maior etapa, mais uma folga de 2 por etapa: é a folga que deixa a
        // janela de adversários subir de etapa em etapa (ParticipantesDaEtapa).
        int quantos = circuito.Max(e => e.Duplas) - 1 + 2 * (circuito.Count - 1);
        return new Carreira(semente, duplaDoJogador, circuito, GerarRivais(quantos, semente, duplaDoJogador.Nome), [], null);
    }

    private static List<DuplaParticipante> GerarRivais(int quantos, uint semente, string nomeDoJogador)
    {
        var aleatorio = new Aleatorio(Sementes.Misturar(semente, 0));
        var usados = new HashSet<string>(StringComparer.Ordinal) { nomeDoJogador };
        var rivais = new List<DuplaParticipante>(quantos);
        for (int i = 0; i < quantos; i++)
        {
            // Atalho: sorteia par de sobrenomes até achar um livre. São 40 × 39 = 1.560 duplas
            // possíveis e o circuito padrão usa 25; o teto de tentativas só apareceria num circuito
            // de centenas de etapas. A saída seria gerar os pares em ordem embaralhada, sem repetir.
            string? nome = null;
            for (int tentativa = 0; tentativa < 10_000 && nome is null; tentativa++)
            {
                int a = (int)(aleatorio.Proximo() * Sobrenomes.Length);
                int b = (int)(aleatorio.Proximo() * (Sobrenomes.Length - 1));
                if (b >= a) b++;
                string candidato = $"{Sobrenomes[a]} / {Sobrenomes[b]}";
                if (usados.Add(candidato)) nome = candidato;
            }
            if (nome is null) throw new InvalidOperationException($"Não sobrou nome de dupla livre pro rival {i + 1} de {quantos}.");

            int basica = quantos == 1
                ? (ForcaDoRivalMaisFraco + ForcaDoRivalMaisForte) / 2
                : ForcaDoRivalMaisFraco + (ForcaDoRivalMaisForte - ForcaDoRivalMaisFraco) * i / (quantos - 1);
            int tremor = (int)MathF.Round((aleatorio.Proximo() * 2 - 1) * TremorDaForca);
            rivais.Add(new DuplaParticipante(nome, Math.Clamp(basica + tremor, 0, 100)));
        }
        return rivais;
    }

    /// <summary>
    /// Quem joga a etapa: a dupla do jogador e uma JANELA dos rivais ordenados por força, que sobe
    /// de etapa em etapa — a primeira pega os mais fracos, a última os mais fortes. Os rivais se
    /// repetem entre etapas vizinhas, e é isso que dá ranking pra comparar.
    /// </summary>
    public IReadOnlyList<DuplaParticipante> ParticipantesDaEtapa(int indice)
    {
        if (indice < 0 || indice >= Etapas.Count) throw new ArgumentOutOfRangeException(nameof(indice));
        int vagas = Etapas[indice].Duplas - 1;
        var porForca = Rivais.OrderBy(r => r.Forca).ThenBy(r => r.Nome, StringComparer.Ordinal).ToList();
        int folga = porForca.Count - vagas;
        int inicio = Etapas.Count == 1 ? folga : indice * folga / (Etapas.Count - 1);
        return [DuplaDoJogador, .. porForca.Skip(inicio).Take(vagas)];
    }

    /// <summary>Abre a próxima etapa, com o ranking de agora congelado dentro dela.</summary>
    public TorneioDeDuplas IniciarEtapa()
    {
        if (EtapaEmAndamento is not null)
            throw new InvalidOperationException($"A etapa {IndiceDaProximaEtapa + 1} ({Etapas[IndiceDaProximaEtapa].Nome}) já está em andamento.");
        if (Concluida) throw new InvalidOperationException($"A carreira acabou: as {Etapas.Count} etapas foram jogadas.");

        int indice = IndiceDaProximaEtapa;
        var etapa = Etapas[indice];
        var regras = new RegrasDoTorneio(etapa.Formato, SetsParaVencer: etapa.SetsParaVencer);
        EtapaEmAndamento = new TorneioDeDuplas(ParticipantesDaEtapa(indice), regras,
            Sementes.Misturar(Semente, (uint)indice + 1), PontosPorDupla());
        return EtapaEmAndamento;
    }

    /// <summary>Fecha a etapa em andamento (que precisa ter campeão) e soma os pontos de cada dupla.</summary>
    public ResultadoDaEtapa FecharEtapa()
    {
        var torneio = EtapaEmAndamento ?? throw new InvalidOperationException("Nenhuma etapa em andamento.");
        var campeao = torneio.Campeao ?? throw new InvalidOperationException($"A etapa {Etapas[IndiceDaProximaEtapa].Nome} ainda não tem campeão.");
        var etapa = Etapas[IndiceDaProximaEtapa];
        var pontuacoes = torneio.Duplas
            .Select(d =>
            {
                var fase = torneio.FaseDaDupla(d.Nome);
                return new PontuacaoNaEtapa(d.Nome, fase, EscalaDePontos.Por(etapa.Categoria, fase));
            })
            .ToList();
        var resultado = new ResultadoDaEtapa(etapa.Nome, etapa.Categoria, campeao.Nome, pontuacoes);
        _resultados.Add(resultado);
        EtapaEmAndamento = null;
        return resultado;
    }

    /// <summary>Pontos acumulados de toda dupla do circuito (quem não pontuou vale 0).</summary>
    public IReadOnlyDictionary<string, int> PontosPorDupla()
    {
        var pontos = new Dictionary<string, int>(StringComparer.Ordinal) { [DuplaDoJogador.Nome] = 0 };
        foreach (var rival in Rivais) pontos[rival.Nome] = 0;
        foreach (var p in _resultados.SelectMany(r => r.Pontuacoes))
            pontos[p.Dupla] = pontos.GetValueOrDefault(p.Dupla) + p.Pontos;
        return pontos;
    }

    /// <summary>O ranking acumulado: pontos; empate, mais títulos; depois o nome (a ordem precisa ser total).</summary>
    public IReadOnlyList<LinhaDoRanking> Ranking()
    {
        var titulos = _resultados.GroupBy(r => r.Campeao).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return PontosPorDupla()
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => titulos.GetValueOrDefault(kv.Key))
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select((kv, i) => new LinhaDoRanking(i + 1, kv.Key, kv.Value, titulos.GetValueOrDefault(kv.Key)))
            .ToList();
    }

    // ── O arquivo ────────────────────────────────────────────────────────────────────────

    public string Salvar() => JsonSerializer.Serialize(new CarreiraSalva
    {
        Versao = VersaoDoArquivo,
        Semente = Semente,
        DuplaDoJogador = DuplaDoJogador,
        Etapas = Etapas.ToList(),
        Rivais = Rivais.ToList(),
        Resultados = _resultados.ToList(),
        EtapaEmAndamento = EtapaEmAndamento?.Estado(),
    }, Json);

    /// <summary>
    /// Lê um arquivo de <see cref="Salvar"/>. A versão é conferida ANTES de tudo, e versão que
    /// este jogo não conhece é recusada — ler um formato desconhecido "do jeito que der" é como
    /// uma carreira some sem aviso. Arquivo torto vira <see cref="InvalidDataException"/> com o
    /// motivo, nunca uma carreira pela metade.
    /// </summary>
    public static Carreira Carregar(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int versao = LerVersao(json);
        if (versao != VersaoDoArquivo)
            throw new InvalidDataException(
                $"Esta carreira foi salva na versão {versao} do formato, e este jogo só lê a versão {VersaoDoArquivo}."
                + (versao > VersaoDoArquivo ? " Ela veio de um jogo mais novo: atualize o jogo pra abri-la." : ""));

        CarreiraSalva? salva;
        try
        {
            salva = JsonSerializer.Deserialize<CarreiraSalva>(json, Json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"O arquivo da carreira não é um JSON de carreira válido: {e.Message}", e);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"O arquivo da carreira tem um valor inválido: {e.Message}", e);
        }
        if (salva is null) throw new InvalidDataException("O arquivo da carreira está vazio.");

        var dupla = salva.DuplaDoJogador ?? throw new InvalidDataException("Carreira salva sem a dupla do jogador.");
        var etapas = SemNulos(salva.Etapas, "as etapas");
        var rivais = SemNulos(salva.Rivais, "os rivais");
        var resultados = SemNulos(salva.Resultados, "os resultados");
        foreach (var r in resultados)
            if (r.Etapa is null || r.Campeao is null || r.Pontuacoes is null || r.Pontuacoes.Any(p => p?.Dupla is null))
                throw new InvalidDataException("Carreira salva com um resultado de etapa incompleto.");
        var emAndamento = salva.EtapaEmAndamento is { } estado ? TorneioDeDuplas.Restaurar(estado) : null;

        try
        {
            return new Carreira(salva.Semente, dupla, etapas, rivais, resultados, emAndamento);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"Carreira salva inválida: {e.Message}", e);
        }
    }

    private static int LerVersao(string json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            var raiz = documento.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object
                && raiz.TryGetProperty("Versao", out var versao)
                && versao.ValueKind == JsonValueKind.Number
                && versao.TryGetInt32(out int numero))
                return numero;
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"O arquivo da carreira não é um JSON válido: {e.Message}", e);
        }
        throw new InvalidDataException(
            "O arquivo da carreira não diz a versão do formato (o campo \"Versao\", um número); sem ela não dá pra saber como ler.");
    }

    private static List<T> SemNulos<T>(List<T>? lista, string oQue) where T : class =>
        lista is not null && lista.All(item => item is not null)
            ? lista
            : throw new InvalidDataException($"Carreira salva sem {oQue} (ou com um item vazio).");
}

/// <summary>A carreira como vai pro arquivo. Tudo anulável: é o que veio do disco, antes de conferir.</summary>
internal sealed class CarreiraSalva
{
    public int Versao { get; set; }
    public uint Semente { get; set; }
    public DuplaParticipante? DuplaDoJogador { get; set; }
    public List<EtapaDoCircuito>? Etapas { get; set; }
    public List<DuplaParticipante>? Rivais { get; set; }
    public List<ResultadoDaEtapa>? Resultados { get; set; }
    public EstadoDoTorneio? EtapaEmAndamento { get; set; }
}
