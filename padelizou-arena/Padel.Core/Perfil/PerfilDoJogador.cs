namespace Padel.Core.Perfil;

/// <summary>
/// O perfil do jogador: o que vai pro Steam Cloud. Imutável — quem muda devolve outro (<see cref="ComPreferencias"/>,
/// <see cref="AvaliadorDeConquistas.Aplicar(PerfilDoJogador, ResumoDaPartida, DateTimeOffset)"/>, <see cref="Mesclar"/>).
/// Nenhum instante sai do relógio aqui dentro: todos entram por parâmetro (o Core não lê DateTime.Now) e são guardados em UTC.
/// <para>
/// Igualdade é pelo conteúdo, dicionários incluídos (o record comum compararia a referência do dicionário). Os
/// dicionários de entrada são copiados: mexer no dicionário de quem montou o perfil não muda o perfil.
/// </para>
/// </summary>
public sealed record PerfilDoJogador
{
    public const string NomePadrao = "Jogador";

    // Guardados como ReadOnlyDictionary: nem um cast pra Dictionary muda um perfil que já existe.
    private readonly IReadOnlyDictionary<TipoDeGolpe, int> _golpesPorTipo = new Dictionary<TipoDeGolpe, int>().AsReadOnly();
    private readonly IReadOnlyDictionary<TipoDeGolpe, int> _vencedoresPorTipo = new Dictionary<TipoDeGolpe, int>().AsReadOnly();
    private readonly IReadOnlyDictionary<string, DateTimeOffset> _conquistas = new Dictionary<string, DateTimeOffset>().AsReadOnly();
    private readonly DateTimeOffset _preferenciasAlteradasEm;
    private readonly string _nome = NomePadrao;
    private readonly int _partidas, _vitorias, _maiorRally;
    private readonly double _segundosDeJogo;

    // Todo valor que entra é conferido aqui: um perfil que existe sempre pode ser salvo E lido de volta. Se um estado
    // impossível chegasse ao arquivo, o Carregar o recusaria como corrompido e a camada de cima voltaria ao perfil
    // padrão — por cima do Cloud. (Vitórias acima das partidas depende de dois campos: quem confere é o Salvar.)

    public string Nome { get => _nome; init => _nome = NomeValido(value); }
    /// <summary>Mão da raquete (<see cref="Jogador.Destro"/> do jogador do perfil).</summary>
    public bool Destro { get; init; } = true;

    /// <summary>
    /// Quando nome ou mão mudaram pela última vez. É o que decide o "mais recente" na <see cref="Mesclar"/> — jogar uma
    /// partida não mexe aqui, então jogar num PC não desfaz a troca de nome feita no outro.
    /// </summary>
    public DateTimeOffset PreferenciasAlteradasEm { get => _preferenciasAlteradasEm; init => _preferenciasAlteradasEm = value.ToUniversalTime(); }

    /// <summary>Partidas terminadas fora do treino.</summary>
    public int Partidas { get => _partidas; init => _partidas = NaoNegativo(value, nameof(Partidas)); }
    public int Vitorias { get => _vitorias; init => _vitorias = NaoNegativo(value, nameof(Vitorias)); }
    /// <summary>O maior rally (golpes num ponto) já jogado fora do treino.</summary>
    public int MaiorRally { get => _maiorRally; init => _maiorRally = NaoNegativo(value, nameof(MaiorRally)); }
    /// <summary>Soma do tempo de simulação das partidas e treinos. double: somam-se centenas de horas de floats.</summary>
    public double SegundosDeJogo
    {
        get => _segundosDeJogo;
        init => _segundosDeJogo = double.IsFinite(value) && value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(SegundosDeJogo), value, "Tempo de jogo é finito e não negativo.");
    }
    /// <summary>Golpes dos jogadores do perfil, por tipo, de todas as partidas e do treino.</summary>
    public IReadOnlyDictionary<TipoDeGolpe, int> GolpesPorTipo { get => _golpesPorTipo; init => _golpesPorTipo = SemZeros(value); }
    /// <summary>Pontos vencidos pelo jogador do perfil, pelo tipo do último golpe dele (fora do treino).</summary>
    public IReadOnlyDictionary<TipoDeGolpe, int> VencedoresPorTipo { get => _vencedoresPorTipo; init => _vencedoresPorTipo = SemZeros(value); }
    /// <summary>ID da conquista → instante do desbloqueio (UTC). Guarda também IDs que este jogo não conhece (vieram de um mais novo).</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Conquistas
    {
        get => _conquistas;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var id in value.Keys)
                if (!CatalogoDeConquistas.IdValido(id))
                    throw new ArgumentException($"ID de conquista inválido: \"{id}\" (IDs são MAIÚSCULAS_COM_SUBLINHADO).", nameof(Conquistas));
            _conquistas = value.ToDictionary(par => par.Key, par => par.Value.ToUniversalTime(), StringComparer.Ordinal).AsReadOnly();
        }
    }

    private static int NaoNegativo(int valor, string campo) =>
        valor >= 0 ? valor : throw new ArgumentOutOfRangeException(campo, valor, "Contagem não pode ser negativa.");

    private static IReadOnlyDictionary<TipoDeGolpe, int> SemZeros(IReadOnlyDictionary<TipoDeGolpe, int> contagem)
    {
        ArgumentNullException.ThrowIfNull(contagem);
        foreach (var (tipo, n) in contagem)
        {
            if (!Enum.IsDefined(tipo)) throw new ArgumentOutOfRangeException(nameof(contagem), tipo, "Tipo de golpe desconhecido.");
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(contagem), n, $"Contagem negativa de {tipo}.");
        }
        return contagem.Where(par => par.Value != 0).ToDictionary(par => par.Key, par => par.Value).AsReadOnly();
    }

    public static PerfilDoJogador Novo(string nome, bool destro, DateTimeOffset instante) =>
        new() { Nome = NomeValido(nome), Destro = destro, PreferenciasAlteradasEm = instante };

    public PerfilDoJogador ComPreferencias(string nome, bool destro, DateTimeOffset instante) =>
        this with { Nome = NomeValido(nome), Destro = destro, PreferenciasAlteradasEm = instante };

    private static string NomeValido(string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);
        return string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("O nome do perfil não pode ficar em branco.", nameof(nome)) : nome.Trim();
    }

    public bool Desbloqueou(string id) => _conquistas.ContainsKey(id);

    /// <summary>
    /// Junta dois perfis do mesmo jogador que divergiram (dois PCs, conflito do Steam Cloud). Sem uma versão-base comum
    /// não dá pra saber o que cada lado somou, então:
    /// <list type="bullet">
    /// <item>cada contador (partidas, vitórias, maior rally, tempo, cada tipo de golpe e de vencedor) fica com o
    ///   <b>máximo</b> dos dois — nunca conta em dobro, e perde no máximo o que um lado jogou offline enquanto o outro também jogava;</item>
    /// <item>conquistas: a <b>união</b>, cada uma com o instante <b>mais antigo</b> (quando foi desbloqueada de verdade);</item>
    /// <item>nome e mão: de quem os mudou por último (<see cref="PreferenciasAlteradasEm"/>). Empate no instante: o nome
    ///   que vem antes na ordem ordinal (e, no mesmo nome, destro) — arbitrário, mas o mesmo nos dois PCs.</item>
    /// </list>
    /// É comutativa (Mesclar(a, b) == Mesclar(b, a)) e idempotente (Mesclar(a, a) == a): os dois PCs chegam ao mesmo perfil.
    /// </summary>
    public static PerfilDoJogador Mesclar(PerfilDoJogador a, PerfilDoJogador b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var preferencias = PreferenciasMaisRecentes(a, b);
        var conquistas = new Dictionary<string, DateTimeOffset>(a.Conquistas, StringComparer.Ordinal);
        foreach (var (id, instante) in b.Conquistas)
            conquistas[id] = conquistas.TryGetValue(id, out var outro) && outro <= instante ? outro : instante;
        return new PerfilDoJogador
        {
            Nome = preferencias.Nome,
            Destro = preferencias.Destro,
            PreferenciasAlteradasEm = preferencias.PreferenciasAlteradasEm,
            Partidas = Math.Max(a.Partidas, b.Partidas),
            Vitorias = Math.Max(a.Vitorias, b.Vitorias),
            MaiorRally = Math.Max(a.MaiorRally, b.MaiorRally),
            SegundosDeJogo = Math.Max(a.SegundosDeJogo, b.SegundosDeJogo),
            GolpesPorTipo = Maximo(a.GolpesPorTipo, b.GolpesPorTipo),
            VencedoresPorTipo = Maximo(a.VencedoresPorTipo, b.VencedoresPorTipo),
            Conquistas = conquistas,
        };
    }

    private static PerfilDoJogador PreferenciasMaisRecentes(PerfilDoJogador a, PerfilDoJogador b)
    {
        int porInstante = a.PreferenciasAlteradasEm.CompareTo(b.PreferenciasAlteradasEm);
        if (porInstante != 0) return porInstante > 0 ? a : b;
        int porNome = string.CompareOrdinal(a.Nome, b.Nome);
        if (porNome != 0) return porNome < 0 ? a : b;
        return a.Destro ? a : b;
    }

    private static Dictionary<TipoDeGolpe, int> Maximo(IReadOnlyDictionary<TipoDeGolpe, int> a, IReadOnlyDictionary<TipoDeGolpe, int> b)
    {
        var maximo = new Dictionary<TipoDeGolpe, int>(a);
        foreach (var (tipo, n) in b) maximo[tipo] = Math.Max(maximo.GetValueOrDefault(tipo), n);
        return maximo;
    }

    public bool Equals(PerfilDoJogador? outro) =>
        outro is not null
        && Nome == outro.Nome
        && Destro == outro.Destro
        && PreferenciasAlteradasEm == outro.PreferenciasAlteradasEm
        && Partidas == outro.Partidas
        && Vitorias == outro.Vitorias
        && MaiorRally == outro.MaiorRally
        && SegundosDeJogo.Equals(outro.SegundosDeJogo)
        && MesmoConteudo(_golpesPorTipo, outro._golpesPorTipo)
        && MesmoConteudo(_vencedoresPorTipo, outro._vencedoresPorTipo)
        && MesmoConteudo(_conquistas, outro._conquistas);

    private static bool MesmoConteudo<TChave, TValor>(IReadOnlyDictionary<TChave, TValor> a, IReadOnlyDictionary<TChave, TValor> b) =>
        a.Count == b.Count && a.All(par => b.TryGetValue(par.Key, out var valor) && EqualityComparer<TValor>.Default.Equals(par.Value, valor));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nome);
        hash.Add(Destro);
        hash.Add(PreferenciasAlteradasEm);
        hash.Add(Partidas);
        hash.Add(Vitorias);
        hash.Add(MaiorRally);
        hash.Add(SegundosDeJogo);
        hash.Add(SemOrdem(_golpesPorTipo));
        hash.Add(SemOrdem(_vencedoresPorTipo));
        hash.Add(SemOrdem(_conquistas));
        return hash.ToHashCode();
    }

    /// <summary>Dicionário não tem ordem: soma (sem checagem de estouro — Enumerable.Sum estouraria) dos hashes dos pares.</summary>
    private static int SemOrdem<TChave, TValor>(IReadOnlyDictionary<TChave, TValor> dicionario) =>
        dicionario.Aggregate(0, (soma, par) => unchecked(soma + HashCode.Combine(par.Key, par.Value)));
}
