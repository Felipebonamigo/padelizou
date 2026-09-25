using System.Text.Json;
using System.Text.Json.Serialization;
using Padel.Core.Torneio;

namespace Padel.Core.Rodizio;

/// <summary>
/// Um Americano ou um Mexicano, rodada a rodada: jogadores individuais, duplas que mudam a cada
/// rodada, jogos de pontos corridos, classificação individual. Os jogos entre IAs são simulados
/// quando a rodada deles abre (<see cref="JogoDePontosCorridos"/>, semente por jogo); o jogo de um
/// humano fica pendente até o jogo de verdade informar o placar (<see cref="InformarResultado(int, int, int)"/>),
/// e a rodada só fecha com todos os resultados.
/// </summary>
/// <remarks>
/// <para><b>Americano</b>: a tabela inteira sai da semente na criação (<see cref="TabelaDoAmericano"/>)
/// e fica no estado — carregar um arquivo nunca remonta rodada já sorteada, mesmo que o desenho da
/// tabela mude numa versão futura.</para>
/// <para><b>Mexicano</b>: a primeira rodada é sorteada (ou pela força — <see cref="RegrasDoRodizio.PrimeiraRodada"/>);
/// da segunda em diante, a rodada sai da classificação do fim da anterior: em cada grupo de 4
/// consecutivos (1º–4º, 5º–8º…), 1º+4º contra 2º+3º, e o grupo de cima joga na quadra 1. É a
/// variante mais comum; existe também 1º+3º contra 2º+4º, e nenhuma federação fixa uma delas
/// (padelmix.app, hostatourney.com, ligapadla.pl — resumo da busca, 25/09/2026). As folgas do
/// Mexicano são as MESMAS da tabela do Americano com a mesma semente, então têm as mesmas
/// garantias: diferença ≤ 1 em toda rodada e ninguém folga duas seguidas.</para>
/// <para>O estado é pequeno — jogadores, regras, semente, jogos e a rodada atual — e o resto
/// (folgas, classificação, campeão) é calculado dele, como no <see cref="TorneioDeDuplas"/>.</para>
/// </remarks>
public sealed class TorneioDeRodizio
{
    /// <summary>A versão do formato do arquivo. Mudou o formato, sobe o número — e o carregar aprende a ler a antiga ou recusa.</summary>
    public const int VersaoDoArquivo = 1;

    // O sorteio da primeira rodada do Mexicano deriva da semente por esta parte; os jogos usam o
    // próprio número (1 a 256) e a tabela a parte dela — nenhum acaso depende da ORDEM dos pedidos.
    private const uint ParteDoSorteio = 0x5E1A0;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<JogadorDoRodizio> _jogadores;
    private readonly Dictionary<string, JogadorDoRodizio> _porNome;
    private readonly List<JogoDoRodizio> _jogos;
    private readonly IReadOnlyList<RodadaDaTabela> _tabela;

    public IReadOnlyList<JogadorDoRodizio> Jogadores => _jogadores;
    public RegrasDoRodizio Regras { get; }
    public uint Semente { get; }
    public IReadOnlyList<JogoDoRodizio> Jogos => _jogos;
    public int RodadaAtual { get; private set; }
    public int TotalDeRodadas { get; }

    /// <summary>A última rodada está com todos os resultados.</summary>
    public bool Encerrado => RodadaAtual == TotalDeRodadas - 1 && RodadaCompleta(RodadaAtual);

    /// <param name="jogadores">De 4 a 16, nomes únicos. No Americano a ordem não importa (a semente embaralha).</param>
    public TorneioDeRodizio(IReadOnlyList<JogadorDoRodizio> jogadores, RegrasDoRodizio? regras = null, uint semente = 1)
    {
        ArgumentNullException.ThrowIfNull(jogadores);
        Regras = regras ?? new RegrasDoRodizio();
        if (RegrasDoRodizio.Problema(Regras) is { } problema)
            throw new ArgumentException($"Regras de rodízio inválidas: {problema}", nameof(regras));
        Semente = semente;
        _jogadores = jogadores.ToList();
        _porNome = Indexar(_jogadores);
        TotalDeRodadas = Regras.Rodadas ?? TabelaDoAmericano.RodadasPadrao(_jogadores.Count);
        _tabela = TabelaDoAmericano.Montar(_jogadores.Count, Semente, TotalDeRodadas);
        _jogos = [];

        if (Regras.Formato == FormatoDoRodizio.Americano)
            for (int r = 0; r < TotalDeRodadas; r++)
                for (int q = 0; q < _tabela[r].Jogos.Count; q++)
                {
                    var jogo = _tabela[r].Jogos[q];
                    _jogos.Add(new JogoDoRodizio(_jogos.Count + 1, r, q + 1,
                        new DuplaDoRodizio(_jogadores[jogo.A1].Nome, _jogadores[jogo.A2].Nome),
                        new DuplaDoRodizio(_jogadores[jogo.B1].Nome, _jogadores[jogo.B2].Nome)));
                }
        else
            CriarRodadaDoMexicano(0);
        SimularJogosDasIAs();
    }

    private TorneioDeRodizio(List<JogadorDoRodizio> jogadores, RegrasDoRodizio regras, uint semente, List<JogoDoRodizio> jogos, int rodadaAtual)
    {
        Regras = regras;
        Semente = semente;
        _jogadores = jogadores;
        _porNome = Indexar(jogadores);
        TotalDeRodadas = regras.Rodadas ?? TabelaDoAmericano.RodadasPadrao(jogadores.Count);
        _tabela = TabelaDoAmericano.Montar(jogadores.Count, semente, TotalDeRodadas);
        _jogos = jogos;
        RodadaAtual = rodadaAtual;
    }

    private static Dictionary<string, JogadorDoRodizio> Indexar(List<JogadorDoRodizio> jogadores)
    {
        if (jogadores.Count is < TabelaDoAmericano.MinimoDeJogadores or > TabelaDoAmericano.MaximoDeJogadores)
            throw new ArgumentException(
                $"Um rodízio tem de {TabelaDoAmericano.MinimoDeJogadores} a {TabelaDoAmericano.MaximoDeJogadores} jogadores; vieram {jogadores.Count}.",
                nameof(jogadores));
        var porNome = new Dictionary<string, JogadorDoRodizio>(StringComparer.Ordinal);
        foreach (var jogador in jogadores)
        {
            ArgumentNullException.ThrowIfNull(jogador, nameof(jogadores));
            if (!porNome.TryAdd(jogador.Nome, jogador))
                throw new ArgumentException($"Dois jogadores com o nome \"{jogador.Nome}\": o nome é a identidade no rodízio.", nameof(jogadores));
        }
        return porNome;
    }

    // ── Montagem ─────────────────────────────────────────────────────────────────────────

    private void CriarRodadaDoMexicano(int rodada)
    {
        var folgam = _tabela[rodada].Folgas.Select(i => _jogadores[i].Nome).ToHashSet(StringComparer.Ordinal);
        List<string> ordem;
        if (rodada > 0)
            ordem = Classificacao().Select(l => l.Jogador).Where(nome => !folgam.Contains(nome)).ToList();
        else if (Regras.PrimeiraRodada == PrimeiraRodadaDoMexicano.PorForca)
            ordem = _jogadores.Where(j => !folgam.Contains(j.Nome))
                .OrderByDescending(j => j.Forca)
                .ThenBy(j => j.Nome, StringComparer.Ordinal)
                .Select(j => j.Nome)
                .ToList();
        else
            ordem = Sorteio(_jogadores.Select(j => j.Nome).Where(nome => !folgam.Contains(nome)).ToList());

        for (int k = 0; k + 3 < ordem.Count; k += 4)
            _jogos.Add(new JogoDoRodizio(_jogos.Count + 1, rodada, k / 4 + 1,
                new DuplaDoRodizio(ordem[k], ordem[k + 3]),
                new DuplaDoRodizio(ordem[k + 1], ordem[k + 2])));
    }

    private List<string> Sorteio(List<string> nomes)
    {
        var aleatorio = new Aleatorio(Sementes.Misturar(Semente, ParteDoSorteio));
        for (int i = nomes.Count - 1; i > 0; i--)
        {
            int j = (int)(aleatorio.Proximo() * (i + 1));
            (nomes[i], nomes[j]) = (nomes[j], nomes[i]);
        }
        return nomes;
    }

    private void SimularJogosDasIAs()
    {
        for (int i = 0; i < _jogos.Count; i++)
        {
            var jogo = _jogos[i];
            if (jogo.Rodada != RodadaAtual || jogo.Jogado) continue;
            var quatro = jogo.Jogadores.Select(nome => _porNome[nome]).ToList();
            if (quatro.Any(j => j.Humano)) continue;
            var aleatorio = new Aleatorio(Sementes.Misturar(Semente, (uint)jogo.Numero));
            var (a, b) = JogoDePontosCorridos.Simular(
                (quatro[0].Forca + quatro[1].Forca) / 2f, (quatro[2].Forca + quatro[3].Forca) / 2f, Regras.PontosPorJogo, aleatorio);
            _jogos[i] = jogo with { PontosA = a, PontosB = b };
        }
    }

    // ── O que a tela pergunta ────────────────────────────────────────────────────────────

    public JogadorDoRodizio Jogador(string nome) =>
        _porNome.TryGetValue(nome, out var jogador) ? jogador : throw new ArgumentException($"Não há jogador \"{nome}\" neste rodízio.", nameof(nome));

    public IReadOnlyList<JogoDoRodizio> JogosDaRodadaAtual => _jogos.Where(j => j.Rodada == RodadaAtual).ToList();

    /// <summary>Os jogos da rodada atual sem resultado — só os de humano, que o jogo precisa rodar.</summary>
    public IReadOnlyList<JogoDoRodizio> JogosPendentes => _jogos.Where(j => j.Rodada == RodadaAtual && !j.Jogado).ToList();

    /// <summary>Quem folga na rodada (vazio pra rodada do Mexicano que ainda não foi montada).</summary>
    public IReadOnlyList<string> FolgasDaRodada(int rodada)
    {
        var jogos = _jogos.Where(j => j.Rodada == rodada).ToList();
        if (jogos.Count == 0) return [];
        var jogam = jogos.SelectMany(j => j.Jogadores).ToHashSet(StringComparer.Ordinal);
        return _jogadores.Select(j => j.Nome).Where(nome => !jogam.Contains(nome)).ToList();
    }

    /// <summary>
    /// A classificação de agora. A folga só conta quando a rodada dela fecha (todos os jogos com
    /// resultado): antes disso quem folgou estaria somando enquanto os outros ainda jogam.
    /// </summary>
    public List<LinhaDoRodizio> Classificacao()
    {
        var folgas = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int r = 0; r <= RodadaAtual; r++)
        {
            if (!RodadaCompleta(r)) continue;
            foreach (var nome in FolgasDaRodada(r)) folgas[nome] = folgas.GetValueOrDefault(nome) + 1;
        }
        return ClassificacaoDoRodizio.Ordenar(_jogadores.Select(j => j.Nome).ToList(), _jogos, Regras.PontosPorJogo, folgas);
    }

    /// <summary>O 1º da classificação quando o evento acaba; <c>null</c> antes.</summary>
    public JogadorDoRodizio? Campeao => Encerrado ? _porNome[Classificacao()[0].Jogador] : null;

    // Rodada montada e com todos os resultados. Rodada sem jogo (a do Mexicano que ainda não abriu)
    // não está completa: sem essa guarda, todo mundo "folgaria" nela.
    private bool RodadaCompleta(int rodada)
    {
        bool algum = false;
        foreach (var jogo in _jogos)
        {
            if (jogo.Rodada != rodada) continue;
            if (!jogo.Jogado) return false;
            algum = true;
        }
        return algum;
    }

    // ── O que o jogo faz ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// O resultado de um jogo da rodada atual: pontos da <see cref="JogoDoRodizio.DuplaA"/> e da
    /// <see cref="JogoDoRodizio.DuplaB"/>, que precisam somar <see cref="RegrasDoRodizio.PontosPorJogo"/>.
    /// Placar impossível é recusado com o motivo (<see cref="ArgumentException"/>); jogo já jogado
    /// ou de outra rodada, com <see cref="InvalidOperationException"/>.
    /// </summary>
    public void InformarResultado(int numeroDoJogo, int pontosA, int pontosB)
    {
        int indice = _jogos.FindIndex(j => j.Numero == numeroDoJogo);
        if (indice < 0) throw new ArgumentException($"Não há jogo {numeroDoJogo} neste rodízio.", nameof(numeroDoJogo));
        var jogo = _jogos[indice];
        if (jogo.Jogado)
            throw new InvalidOperationException($"O jogo {numeroDoJogo} já tem resultado ({jogo.PontosA} a {jogo.PontosB}).");
        if (jogo.Rodada != RodadaAtual)
            throw new InvalidOperationException($"O jogo {numeroDoJogo} é da rodada {jogo.Rodada + 1}; a rodada atual é a {RodadaAtual + 1}.");
        if (JogoDoRodizio.ProblemaNoPlacar(pontosA, pontosB, Regras.PontosPorJogo) is { } problema)
            throw new ArgumentException($"Placar impossível pro jogo {numeroDoJogo}: {problema}");
        _jogos[indice] = jogo with { PontosA = pontosA, PontosB = pontosB };
    }

    /// <summary>
    /// O resultado do ÚNICO jogo pendente da rodada — o do humano. Com nenhum ou mais de um
    /// pendente (dois humanos em quadras diferentes), recusa: aí o jogo precisa dizer o número.
    /// </summary>
    public void InformarResultado(int pontosA, int pontosB)
    {
        var pendentes = JogosPendentes;
        if (pendentes.Count == 0)
            throw new InvalidOperationException($"A rodada {RodadaAtual + 1} não tem jogo esperando resultado.");
        if (pendentes.Count > 1)
            throw new InvalidOperationException(
                $"A rodada {RodadaAtual + 1} tem {pendentes.Count} jogos esperando resultado ({string.Join(", ", pendentes.Select(j => j.Numero))}): informe o número do jogo.");
        InformarResultado(pendentes[0].Numero, pontosA, pontosB);
    }

    /// <summary>
    /// Fecha a rodada atual e abre a próxima (no Mexicano, montando-a pela classificação; nos dois,
    /// simulando os jogos das IAs). Devolve <c>false</c> sem mexer em nada quando falta resultado
    /// de humano, ou quando o rodízio já acabou.
    /// </summary>
    public bool AvancarRodada()
    {
        if (!RodadaCompleta(RodadaAtual) || RodadaAtual >= TotalDeRodadas - 1) return false;
        RodadaAtual++;
        if (Regras.Formato == FormatoDoRodizio.Mexicano) CriarRodadaDoMexicano(RodadaAtual);
        SimularJogosDasIAs();
        return true;
    }

    /// <summary>Joga até o fim um rodízio sem humano pendente (assistir, ou teste).</summary>
    public void JogarAteOFim()
    {
        while (!Encerrado)
            if (!AvancarRodada())
                throw new InvalidOperationException(
                    $"Rodada {RodadaAtual + 1} esperando o resultado do humano (jogo {JogosPendentes.FirstOrDefault()?.Numero}).");
    }

    // ── O arquivo ────────────────────────────────────────────────────────────────────────

    public string Salvar() => JsonSerializer.Serialize(new RodizioSalvo
    {
        Versao = VersaoDoArquivo,
        Jogadores = _jogadores.ToList(),
        Regras = Regras,
        Semente = Semente,
        Jogos = _jogos.ToList(),
        RodadaAtual = RodadaAtual,
    }, Json);

    /// <summary>
    /// Lê um arquivo de <see cref="Salvar"/>. A versão é conferida ANTES de tudo, e versão que este
    /// jogo não conhece é recusada — ler um formato desconhecido "do jeito que der" é como um evento
    /// some sem aviso. Arquivo torto vira <see cref="InvalidDataException"/> com o motivo, nunca um
    /// rodízio pela metade.
    /// </summary>
    public static TorneioDeRodizio Carregar(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int versao = LerVersao(json);
        if (versao != VersaoDoArquivo)
            throw new InvalidDataException(
                $"Este rodízio foi salvo na versão {versao} do formato, e este jogo só lê a versão {VersaoDoArquivo}."
                + (versao > VersaoDoArquivo ? " Ele veio de um jogo mais novo: atualize o jogo pra abri-lo." : ""));

        RodizioSalvo? salvo;
        try
        {
            salvo = JsonSerializer.Deserialize<RodizioSalvo>(json, Json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"O arquivo do rodízio não é um JSON de rodízio válido: {e.Message}", e);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"O arquivo do rodízio tem um valor inválido: {e.Message}", e);
        }
        if (salvo is null) throw new InvalidDataException("O arquivo do rodízio está vazio.");
        return Restaurar(salvo);
    }

    private static TorneioDeRodizio Restaurar(RodizioSalvo salvo)
    {
        var jogadores = salvo.Jogadores is { } lista && lista.All(j => j is not null)
            ? lista
            : throw new InvalidDataException("Rodízio salvo sem a lista de jogadores (ou com um jogador vazio).");
        var regras = salvo.Regras ?? throw new InvalidDataException("Rodízio salvo sem as regras.");
        if (RegrasDoRodizio.Problema(regras) is { } problemaNasRegras)
            throw new InvalidDataException($"Rodízio salvo com regras inválidas: {problemaNasRegras}");
        var jogos = salvo.Jogos is { } salvos && salvos.All(j => j is not null)
            ? salvos
            : throw new InvalidDataException("Rodízio salvo sem a lista de jogos (ou com um jogo vazio).");

        TorneioDeRodizio rodizio;
        try
        {
            rodizio = new TorneioDeRodizio(jogadores, regras, salvo.Semente, jogos, salvo.RodadaAtual);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"Rodízio salvo inválido: {e.Message}", e);
        }
        if (rodizio.ProblemaNoEstado() is { } problema)
            throw new InvalidDataException($"Rodízio salvo inválido: {problema}");
        // Um arquivo bem formado já tem os jogos de IA da rodada atual simulados; se faltar algum,
        // simular aqui (semente pelo número do jogo) dá o mesmo placar que ele teria tido.
        rodizio.SimularJogosDasIAs();
        return rodizio;
    }

    /// <summary>O que um arquivo torto poderia quebrar, conferido depois de montar; <c>null</c> se está tudo certo.</summary>
    private string? ProblemaNoEstado()
    {
        if (RodadaAtual < 0 || RodadaAtual >= TotalDeRodadas)
            return $"rodada atual {RodadaAtual + 1} fora das {TotalDeRodadas} rodadas.";
        int ultimaMontada = Regras.Formato == FormatoDoRodizio.Americano ? TotalDeRodadas - 1 : RodadaAtual;
        int quadras = _jogadores.Count / 4;
        for (int i = 0; i < _jogos.Count; i++)
        {
            var jogo = _jogos[i];
            if (jogo.Numero != i + 1) return $"o {i + 1}º jogo tem o número {jogo.Numero}; os jogos são numerados em sequência.";
            if (jogo.DuplaA is null || jogo.DuplaB is null) return $"o jogo {jogo.Numero} está sem uma das duplas.";
            var nomes = new[] { jogo.DuplaA.Jogador1, jogo.DuplaA.Jogador2, jogo.DuplaB.Jogador1, jogo.DuplaB.Jogador2 };
            if (nomes.Any(nome => nome is null || !_porNome.ContainsKey(nome)))
                return $"o jogo {jogo.Numero} tem jogador que não está no rodízio.";
            if (nomes.Distinct(StringComparer.Ordinal).Count() != 4) return $"o jogo {jogo.Numero} repete jogador.";
            if (jogo.Rodada < 0 || jogo.Rodada > ultimaMontada) return $"o jogo {jogo.Numero} é de uma rodada que não existe ({jogo.Rodada + 1}).";
            if (jogo.Quadra < 1 || jogo.Quadra > quadras) return $"o jogo {jogo.Numero} é na quadra {jogo.Quadra}, e há {quadras}.";
            if ((jogo.PontosA is null) != (jogo.PontosB is null)) return $"o jogo {jogo.Numero} tem o placar pela metade.";
            if (jogo.PontosA is { } a && jogo.PontosB is { } b && JogoDoRodizio.ProblemaNoPlacar(a, b, Regras.PontosPorJogo) is { } placar)
                return $"o jogo {jogo.Numero} tem placar impossível: {placar}";
            if (jogo.Rodada < RodadaAtual && !jogo.Jogado) return $"o jogo {jogo.Numero} é de rodada fechada e não tem resultado.";
            if (jogo.Rodada > RodadaAtual && jogo.Jogado) return $"o jogo {jogo.Numero} é de rodada futura e já tem resultado.";
        }
        for (int r = 0; r <= ultimaMontada; r++)
        {
            var daRodada = _jogos.Where(j => j.Rodada == r).ToList();
            if (daRodada.Count != quadras) return $"a rodada {r + 1} tem {daRodada.Count} jogos; são {quadras} quadras.";
            var nomes = daRodada.SelectMany(j => j.Jogadores).ToList();
            if (nomes.Distinct(StringComparer.Ordinal).Count() != nomes.Count) return $"alguém joga dois jogos na rodada {r + 1}.";
            if (daRodada.Select(j => j.Quadra).Distinct().Count() != quadras) return $"a rodada {r + 1} repete quadra.";
        }
        return null;
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
            throw new InvalidDataException($"O arquivo do rodízio não é um JSON válido: {e.Message}", e);
        }
        throw new InvalidDataException(
            "O arquivo do rodízio não diz a versão do formato (o campo \"Versao\", um número); sem ela não dá pra saber como ler.");
    }
}

/// <summary>O rodízio como vai pro arquivo. Tudo anulável: é o que veio do disco, antes de conferir.</summary>
internal sealed class RodizioSalvo
{
    public int Versao { get; set; }
    public List<JogadorDoRodizio>? Jogadores { get; set; }
    public RegrasDoRodizio? Regras { get; set; }
    public uint Semente { get; set; }
    public List<JogoDoRodizio>? Jogos { get; set; }
    public int RodadaAtual { get; set; }
}
