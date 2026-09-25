using System.Text.Json;

namespace Padel.Core.Perfil;

/// <summary>
/// Por que o arquivo do perfil não foi lido. A diferença importa pra camada de cima:
/// <see cref="Corrompido"/> — voltar ao perfil padrão é aceitável (o arquivo não tem salvação);
/// <see cref="VersaoDesconhecida"/> vinda de um jogo MAIS NOVO (<see cref="PerfilIlegivelException.VeioDeUmJogoMaisNovo"/>) —
/// NÃO sobrescrever: o Steam Cloud espalharia o perfil zerado por cima do de verdade. Pedir pra atualizar o jogo.
/// </summary>
public enum MotivoDoPerfilIlegivel { Corrompido, VersaoDesconhecida }

/// <summary>O arquivo do perfil não pôde ser lido. A mensagem diz o porquê em português, pronta pro log.</summary>
public sealed class PerfilIlegivelException : Exception
{
    public PerfilIlegivelException(MotivoDoPerfilIlegivel motivo, string mensagem, int? versao = null, Exception? causa = null)
        : base(mensagem, causa)
    {
        Motivo = motivo;
        Versao = versao;
    }

    public MotivoDoPerfilIlegivel Motivo { get; }
    /// <summary>A versão escrita no arquivo, quando deu pra ler.</summary>
    public int? Versao { get; }
    public bool VeioDeUmJogoMaisNovo => Motivo == MotivoDoPerfilIlegivel.VersaoDesconhecida && Versao > PersistenciaDoPerfil.VersaoDoArquivo;
}

/// <summary>
/// O perfil em JSON (System.Text.Json), com número de versão — é o arquivo do Steam Cloud. O texto é canônico: o mesmo
/// perfil dá sempre o mesmo texto (dicionários em ordem, instantes em UTC), então dá pra comparar arquivos.
/// <para>
/// Formato (versão 1): Versao, Nome, Destro, PreferenciasAlteradasEm, Partidas, Vitorias, MaiorRally, SegundosDeJogo,
/// GolpesPorTipo e VencedoresPorTipo (nome do <see cref="TipoDeGolpe"/> → contagem), Conquistas (ID → instante ISO 8601).
/// Mudou o formato, sobe <see cref="VersaoDoArquivo"/>. <b>Tipo de golpe novo também sobe a versão</b>: o jogo antigo recusa
/// nome de golpe que não conhece (em vez de descartá-lo e apagar a contagem do Cloud). Conquista nova não precisa:
/// ID desconhecido é guardado como veio.
/// </para>
/// </summary>
public static class PersistenciaDoPerfil
{
    public const int VersaoDoArquivo = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>
    /// O perfil em texto. Recusa (ArgumentException) o único estado impossível que o <see cref="PerfilDoJogador"/> não
    /// barra sozinho — vitórias acima das partidas —, porque o <see cref="Carregar"/> o recusaria depois.
    /// </summary>
    public static string Salvar(PerfilDoJogador perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        if (perfil.Vitorias > perfil.Partidas)
            throw new ArgumentException($"Perfil com mais vitórias ({perfil.Vitorias}) que partidas ({perfil.Partidas}) não vai pro arquivo.", nameof(perfil));
        return JsonSerializer.Serialize(new PerfilSalvo
        {
            Versao = VersaoDoArquivo,
            Nome = perfil.Nome,
            Destro = perfil.Destro,
            PreferenciasAlteradasEm = perfil.PreferenciasAlteradasEm.ToUniversalTime(),
            Partidas = perfil.Partidas,
            Vitorias = perfil.Vitorias,
            MaiorRally = perfil.MaiorRally,
            SegundosDeJogo = perfil.SegundosDeJogo,
            GolpesPorTipo = PorNome(perfil.GolpesPorTipo),
            VencedoresPorTipo = PorNome(perfil.VencedoresPorTipo),
            Conquistas = new SortedDictionary<string, DateTimeOffset>(
                perfil.Conquistas.ToDictionary(par => par.Key, par => par.Value.ToUniversalTime()), StringComparer.Ordinal),
        }, Json);
    }

    /// <summary>Contagem por nome do golpe, na ordem do enum.</summary>
    private static Dictionary<string, int> PorNome(IReadOnlyDictionary<TipoDeGolpe, int> contagem)
    {
        var porNome = new Dictionary<string, int>();
        foreach (var tipo in Enum.GetValues<TipoDeGolpe>())
            if (contagem.TryGetValue(tipo, out int n) && n != 0) porNome[tipo.ToString()] = n;
        return porNome;
    }

    /// <summary>
    /// Lê um arquivo de <see cref="Salvar"/>. A versão é conferida ANTES de tudo, e versão que este jogo não conhece é
    /// recusada — ler um formato desconhecido "do jeito que der" é como um perfil some sem aviso. Arquivo torto vira
    /// <see cref="PerfilIlegivelException"/> com o motivo, nunca um perfil pela metade.
    /// </summary>
    public static PerfilDoJogador Carregar(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        int versao = LerVersao(json);
        if (versao != VersaoDoArquivo)
            throw new PerfilIlegivelException(MotivoDoPerfilIlegivel.VersaoDesconhecida,
                $"Este perfil foi salvo na versão {versao} do formato, e este jogo só lê a versão {VersaoDoArquivo}."
                + (versao > VersaoDoArquivo ? " Ele veio de um jogo mais novo: atualize o jogo pra abri-lo (e não o sobrescreva)." : ""),
                versao);

        PerfilSalvo? salvo;
        try
        {
            salvo = JsonSerializer.Deserialize<PerfilSalvo>(json, Json);
        }
        catch (JsonException e)
        {
            throw Corrompido($"o arquivo não é um JSON de perfil válido: {e.Message}", e);
        }
        if (salvo is null) throw Corrompido("o arquivo está vazio.");

        string nome = salvo.Nome ?? throw Faltando("Nome");
        if (string.IsNullOrWhiteSpace(nome)) throw Corrompido("o campo \"Nome\" está em branco.");
        bool destro = salvo.Destro ?? throw Faltando("Destro");
        var preferenciasEm = salvo.PreferenciasAlteradasEm ?? throw Faltando("PreferenciasAlteradasEm");
        int partidas = NaoNegativo(salvo.Partidas, "Partidas");
        int vitorias = NaoNegativo(salvo.Vitorias, "Vitorias");
        if (vitorias > partidas) throw Corrompido($"\"Vitorias\" ({vitorias}) passa de \"Partidas\" ({partidas}).");
        int maiorRally = NaoNegativo(salvo.MaiorRally, "MaiorRally");
        double segundos = salvo.SegundosDeJogo ?? throw Faltando("SegundosDeJogo");
        if (!double.IsFinite(segundos) || segundos < 0) throw Corrompido($"\"SegundosDeJogo\" impossível: {segundos}.");
        var conquistas = salvo.Conquistas ?? throw Faltando("Conquistas");
        foreach (var id in conquistas.Keys)
            if (!CatalogoDeConquistas.IdValido(id)) throw Corrompido($"ID de conquista inválido em \"Conquistas\": \"{id}\" (IDs são MAIÚSCULAS_COM_SUBLINHADO).");

        var golpes = PorTipo(salvo.GolpesPorTipo, "GolpesPorTipo");
        var vencedores = PorTipo(salvo.VencedoresPorTipo, "VencedoresPorTipo");
        try
        {
            return new PerfilDoJogador
            {
                Nome = nome,
                Destro = destro,
                PreferenciasAlteradasEm = preferenciasEm,
                Partidas = partidas,
                Vitorias = vitorias,
                MaiorRally = maiorRally,
                SegundosDeJogo = segundos,
                GolpesPorTipo = golpes,
                VencedoresPorTipo = vencedores,
                Conquistas = new Dictionary<string, DateTimeOffset>(conquistas, StringComparer.Ordinal),
            };
        }
        catch (ArgumentException e)
        {
            // As conferências acima cobrem o que o perfil recusa; isto é a rede de segurança, com o motivo do perfil.
            throw Corrompido($"valor inválido: {e.Message}", e);
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
            throw Corrompido($"o arquivo não é um JSON válido: {e.Message}", e);
        }
        throw Corrompido("o arquivo não diz a versão do formato (o campo \"Versao\", um número); sem ela não dá pra saber como ler.");
    }

    private static Dictionary<TipoDeGolpe, int> PorTipo(Dictionary<string, int>? porNome, string campo)
    {
        if (porNome is null) throw Faltando(campo);
        var contagem = new Dictionary<TipoDeGolpe, int>();
        foreach (var (nome, n) in porNome)
        {
            // Só o nome exato do enum: número ("3") ou nome em outra caixa não é o que Salvar escreve.
            if (!Enum.TryParse(nome, ignoreCase: false, out TipoDeGolpe tipo) || !Enum.IsDefined(tipo) || tipo.ToString() != nome)
                throw Corrompido($"tipo de golpe desconhecido em \"{campo}\": \"{nome}\" (golpe novo exige versão nova do arquivo).");
            if (n < 0) throw Corrompido($"contagem negativa em \"{campo}\": {nome} = {n}.");
            contagem[tipo] = n;
        }
        return contagem;
    }

    private static int NaoNegativo(int? valor, string campo)
    {
        int n = valor ?? throw Faltando(campo);
        return n >= 0 ? n : throw Corrompido($"\"{campo}\" negativo: {n}.");
    }

    private static PerfilIlegivelException Faltando(string campo) => Corrompido($"falta o campo \"{campo}\" (ou está vazio).");

    private static PerfilIlegivelException Corrompido(string motivo, Exception? causa = null) =>
        new(MotivoDoPerfilIlegivel.Corrompido, $"O arquivo do perfil está corrompido: {motivo}", causa: causa);
}

/// <summary>O perfil como vai pro arquivo. Tudo anulável: é o que veio do disco, antes de conferir.</summary>
internal sealed class PerfilSalvo
{
    public int Versao { get; set; }
    public string? Nome { get; set; }
    public bool? Destro { get; set; }
    public DateTimeOffset? PreferenciasAlteradasEm { get; set; }
    public int? Partidas { get; set; }
    public int? Vitorias { get; set; }
    public int? MaiorRally { get; set; }
    public double? SegundosDeJogo { get; set; }
    public Dictionary<string, int>? GolpesPorTipo { get; set; }
    public Dictionary<string, int>? VencedoresPorTipo { get; set; }
    public IDictionary<string, DateTimeOffset>? Conquistas { get; set; }
}
