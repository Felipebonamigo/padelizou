namespace Padel.Core.Torneio;

/// <summary>
/// Um torneio de duplas, rodada a rodada: grupos (todos contra todos) seguidos de mata-mata, ou
/// chave direta. Os jogos entre duas IAs são simulados quando a rodada deles abre
/// (<see cref="SimuladorDePartida"/>, semente por jogo); o jogo de uma dupla humana fica
/// pendente até o jogo de verdade informar o placar (<see cref="InformarResultado(int, Placar, string)"/>),
/// e a rodada só fecha com todos os resultados.
/// </summary>
/// <remarks>
/// O estado é pequeno de propósito — duplas, regras, semente, ranking, grupos, quadro, jogos e a
/// rodada atual — e todo o resto (tabela, quem avança, campeão) é CALCULADO dele. É isso que deixa
/// salvar no meio de uma etapa sem guardar nada derivado que pudesse discordar do resto.
/// </remarks>
public sealed class TorneioDeDuplas
{
    public const int MinimoDeDuplas = 2;
    /// <summary>O teto da chave direta do Padelizou (<c>ChaveamentoMataMata.MaximoDeDuplasNaChaveDireta</c>): acima disso a fase não tem nome.</summary>
    public const int MaximoDeDuplas = 32;

    private readonly List<DuplaParticipante> _duplas;
    private readonly Dictionary<string, DuplaParticipante> _porNome;
    private readonly Dictionary<string, int> _pontos;
    private readonly List<GrupoDoTorneio> _grupos;
    private readonly List<string?> _quadro;
    private readonly List<JogoDoTorneio> _jogos;
    private readonly Dictionary<(int Rodada, int Posicao), int> _jogoDaChave = new();
    private readonly int _rodadasDeGrupo;

    public IReadOnlyList<DuplaParticipante> Duplas => _duplas;
    public RegrasDoTorneio Regras { get; }
    public uint Semente { get; }
    /// <summary>O ranking do começo do torneio — o degrau 5 do desempate de grupo. Congelado aqui: a tabela não escorrega no meio do torneio.</summary>
    public IReadOnlyDictionary<string, int> PontosDeRanking => _pontos;
    public IReadOnlyList<GrupoDoTorneio> Grupos => _grupos;
    /// <summary>As vagas da primeira rodada do mata-mata (<see cref="QuadroDoMataMata"/>). Vazio enquanto os grupos não fecham.</summary>
    public IReadOnlyList<string?> Quadro => _quadro;
    public IReadOnlyList<JogoDoTorneio> Jogos => _jogos;
    public int RodadaAtual { get; private set; }

    public bool EmFaseDeGrupos => _grupos.Count > 0 && _quadro.Count == 0;
    public bool Encerrado => Campeao is not null;

    /// <param name="duplas">De 2 a 32, nomes únicos. A ordem não importa: a semeadura é pela força.</param>
    /// <param name="pontosDeRanking">Pontos de ranking por nome (a carreira passa o acumulado); desempata grupo e semeadura.</param>
    public TorneioDeDuplas(IReadOnlyList<DuplaParticipante> duplas, RegrasDoTorneio? regras = null, uint semente = 1,
        IReadOnlyDictionary<string, int>? pontosDeRanking = null)
    {
        ArgumentNullException.ThrowIfNull(duplas);
        Regras = regras ?? new RegrasDoTorneio();
        Semente = semente;
        _duplas = duplas.ToList();
        _porNome = IndexarDuplas(_duplas);
        ValidarRegras(Regras);
        _pontos = _duplas.ToDictionary(d => d.Nome, d => pontosDeRanking?.GetValueOrDefault(d.Nome) ?? 0, StringComparer.Ordinal);
        _jogos = [];

        var semeadura = Semeadura();
        if (Regras.Formato == FormatoDoTorneio.GruposEMataMata)
        {
            _grupos = SorteioDosGrupos.Montar(semeadura);
            _quadro = [];
            _rodadasDeGrupo = CriarJogosDosGrupos();
            SimularJogosDasIAs();
        }
        else
        {
            _grupos = [];
            _quadro = QuadroDoMataMata.Semeado(semeadura);
            _rodadasDeGrupo = 0;
            AbrirRodadaDaChave(0);
        }
    }

    private TorneioDeDuplas(List<DuplaParticipante> duplas, RegrasDoTorneio regras, uint semente, Dictionary<string, int> pontos,
        List<GrupoDoTorneio> grupos, List<string?> quadro, List<JogoDoTorneio> jogos, int rodadaAtual)
    {
        _duplas = duplas;
        _porNome = IndexarDuplas(duplas);
        Regras = regras;
        ValidarRegras(regras);
        Semente = semente;
        _pontos = pontos;
        _grupos = grupos;
        _quadro = quadro;
        _jogos = jogos;
        RodadaAtual = rodadaAtual;
        _rodadasDeGrupo = RodadasDosGrupos(grupos);
        for (int i = 0; i < jogos.Count; i++)
            if (!jogos[i].DeGrupo) _jogoDaChave[(jogos[i].RodadaDaChave, jogos[i].PosicaoNaChave)] = i;
    }

    private static Dictionary<string, DuplaParticipante> IndexarDuplas(List<DuplaParticipante> duplas)
    {
        if (duplas.Count is < MinimoDeDuplas or > MaximoDeDuplas)
            throw new ArgumentException($"Um torneio tem de {MinimoDeDuplas} a {MaximoDeDuplas} duplas; vieram {duplas.Count}.", nameof(duplas));
        var porNome = new Dictionary<string, DuplaParticipante>(StringComparer.Ordinal);
        foreach (var dupla in duplas)
        {
            ArgumentNullException.ThrowIfNull(dupla, nameof(duplas));
            if (!porNome.TryAdd(dupla.Nome, dupla))
                throw new ArgumentException($"Duas duplas com o nome \"{dupla.Nome}\": o nome é a identidade no torneio.", nameof(duplas));
        }
        return porNome;
    }

    private static void ValidarRegras(RegrasDoTorneio regras)
    {
        ArgumentNullException.ThrowIfNull(regras);
        if (!Enum.IsDefined(regras.Formato))
            throw new ArgumentException($"Formato de torneio desconhecido: {regras.Formato}.", nameof(regras));
        if (regras.ClassificadosPorGrupo < 1)
            throw new ArgumentException("Pelo menos 1 dupla classifica por grupo.", nameof(regras));
        if (regras.SetsParaVencer is < 1 or > 3)
            throw new ArgumentException("A partida é de 1 a 3 sets pra vencer.", nameof(regras));
    }

    // ── Semeadura e montagem ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A ordem das cabeças de chave: força; depois pontos de ranking; depois o sorteio estável.
    /// O Padelizou semeia pelo ranking e sorteia o empate (<c>TorneiosController.Chaves</c>,
    /// <c>OrderByDescending(pontos).ThenBy(Guid.NewGuid())</c>); no jogo a força É o nível da
    /// dupla (a tarefa pede "cabeças de chave pela força"), o ranking desempata, e o sorteio usa
    /// a semente do torneio em vez de Guid — a mesma semente monta a mesma chave.
    /// </summary>
    private List<string> Semeadura() =>
        _duplas
            .OrderByDescending(d => d.Forca)
            .ThenByDescending(d => _pontos[d.Nome])
            .ThenBy(d => Sementes.Sorteio(Semente, d.Nome))
            .ThenBy(d => d.Nome, StringComparer.Ordinal)
            .Select(d => d.Nome)
            .ToList();

    // Todos contra todos dentro do grupo, na ordem do Padelizou (a × b pra a < b). O k-ésimo jogo
    // de cada grupo vai pra rodada k: os grupos daqui têm 2 ou 3 duplas (SorteioDosGrupos), e aí
    // ninguém joga duas vezes na mesma rodada. Atalho: com grupo de 4+ a rodada k repetiria
    // dupla; a saída é o método do círculo (pôr uma "folga" e girar), se um dia houver grupo de 4.
    private int CriarJogosDosGrupos()
    {
        var porGrupo = _grupos.Select(g =>
        {
            var pares = new List<(string A, string B)>();
            for (int a = 0; a < g.Duplas.Count; a++)
                for (int b = a + 1; b < g.Duplas.Count; b++)
                    pares.Add((g.Duplas[a], g.Duplas[b]));
            return (g.Nome, Pares: pares);
        }).ToList();

        int rodadas = porGrupo.Max(g => g.Pares.Count);
        for (int rodada = 0; rodada < rodadas; rodada++)
            foreach (var (nome, pares) in porGrupo)
                if (rodada < pares.Count)
                    _jogos.Add(new JogoDoTorneio(_jogos.Count + 1, rodada, pares[rodada].A, pares[rodada].B, Grupo: nome));
        return rodadas;
    }

    private static int RodadasDosGrupos(IEnumerable<GrupoDoTorneio> grupos) =>
        grupos.Select(g => g.Duplas.Count * (g.Duplas.Count - 1) / 2).DefaultIfEmpty(0).Max();

    private int RodadasDaChave => _quadro.Count <= 1 ? 0 : (int)Math.Log2(_quadro.Count);

    /// <summary>Fecha os grupos: classificados de cada tabela, cruzamento do Padelizou, e a árvore.</summary>
    private void MontarChaveDosGrupos()
    {
        int vagas = Math.Max(1, Regras.ClassificadosPorGrupo);
        var classificados = new List<Classificado>();
        foreach (var grupo in _grupos)
        {
            var tabela = Classificacao(grupo.Nome);
            for (int pos = 0; pos < tabela.Count && pos < vagas; pos++)
                classificados.Add(new Classificado(tabela[pos].Dupla, grupo.Nome, tabela[pos].Vitorias, tabela[pos].Saldo, pos + 1, tabela[pos].Jogos));
        }

        if (classificados.Count == 1)
        {
            // Um grupo só e uma vaga: o 1º do grupo é o campeão, sem mata-mata.
            _quadro.Add(classificados[0].Dupla);
            return;
        }
        var (confrontos, byes) = CruzamentoDosGrupos.MontarPrimeiraFase(classificados, vagas);
        _quadro.AddRange(QuadroDoMataMata.DoCruzamento(confrontos, byes));
    }

    private void AbrirRodadaDaChave(int rodadaDaChave)
    {
        int jogos = _quadro.Count >> (rodadaDaChave + 1);
        for (int k = 0; k < jogos; k++)
        {
            var (a, b) = Participantes(rodadaDaChave, k);
            if (a is not null && b is not null)
            {
                _jogoDaChave[(rodadaDaChave, k)] = _jogos.Count;
                _jogos.Add(new JogoDoTorneio(_jogos.Count + 1, RodadaAtual, a, b, RodadaDaChave: rodadaDaChave, PosicaoNaChave: k));
            }
            else if (a is null && b is null)
            {
                throw new InvalidOperationException($"Jogo {k} da rodada {rodadaDaChave} da chave sem nenhuma dupla — o quadro está torto.");
            }
            // Uma dupla só: é o bye da primeira rodada, e ela passa sem jogo.
        }
        SimularJogosDasIAs();
    }

    private void SimularJogosDasIAs()
    {
        for (int i = 0; i < _jogos.Count; i++)
        {
            var jogo = _jogos[i];
            if (jogo.Rodada != RodadaAtual || jogo.Jogado) continue;
            var a = _porNome[jogo.DuplaA];
            var b = _porNome[jogo.DuplaB];
            if (a.Humana || b.Humana) continue;
            var aleatorio = new Aleatorio(Sementes.Misturar(Semente, (uint)jogo.Numero));
            _jogos[i] = jogo with { Sets = SimuladorDePartida.Simular(a.Forca, b.Forca, Regras.SetsParaVencer, aleatorio, Regras.PontoDeOuro) };
        }
    }

    // ── A árvore: quem está em cada jogo, quem venceu ────────────────────────────────────

    private (string? A, string? B) Participantes(int rodadaDaChave, int posicao) =>
        rodadaDaChave == 0
            ? (_quadro[2 * posicao], _quadro[2 * posicao + 1])
            : (VencedorDaChave(rodadaDaChave - 1, 2 * posicao), VencedorDaChave(rodadaDaChave - 1, 2 * posicao + 1));

    private string? VencedorDaChave(int rodadaDaChave, int posicao)
    {
        var (a, b) = Participantes(rodadaDaChave, posicao);
        if (rodadaDaChave == 0 && (a is null || b is null)) return a ?? b;   // bye
        return _jogoDaChave.TryGetValue((rodadaDaChave, posicao), out int indice) ? _jogos[indice].Vencedor : null;
    }

    // ── O que a tela pergunta ────────────────────────────────────────────────────────────

    public DuplaParticipante? Campeao
    {
        get
        {
            if (_quadro.Count == 0) return null;
            string? nome = RodadasDaChave == 0 ? _quadro[0] : VencedorDaChave(RodadasDaChave - 1, 0);
            return nome is null ? null : _porNome[nome];
        }
    }

    public DuplaParticipante Dupla(string nome) =>
        _porNome.TryGetValue(nome, out var dupla) ? dupla : throw new ArgumentException($"Não há dupla \"{nome}\" neste torneio.", nameof(nome));

    public IReadOnlyList<JogoDoTorneio> JogosDaRodadaAtual => _jogos.Where(j => j.Rodada == RodadaAtual).ToList();

    /// <summary>Os jogos da rodada atual sem resultado — só os de dupla humana, que o jogo precisa rodar.</summary>
    public IReadOnlyList<JogoDoTorneio> JogosPendentes => _jogos.Where(j => j.Rodada == RodadaAtual && !j.Jogado).ToList();

    /// <summary>Quem entra direto na segunda rodada do mata-mata, na ordem das cabeças de chave (força).</summary>
    public IReadOnlyList<string> ByesDaPrimeiraRodada
    {
        get
        {
            var byes = new List<string>();
            for (int k = 0; 2 * k + 1 < _quadro.Count; k++)
            {
                var (a, b) = (_quadro[2 * k], _quadro[2 * k + 1]);
                if (a is not null && b is null) byes.Add(a);
                else if (a is null && b is not null) byes.Add(b);
            }
            var ordem = Semeadura();
            return byes.OrderBy(ordem.IndexOf).ToList();
        }
    }

    public List<LinhaDaClassificacao> Classificacao(string grupo)
    {
        var doGrupo = _grupos.FirstOrDefault(g => g.Nome == grupo)
            ?? throw new ArgumentException($"Não há grupo \"{grupo}\" neste torneio.", nameof(grupo));
        return ClassificacaoDoGrupo.Ordenar(doGrupo.Duplas, _jogos.Where(j => j.Grupo == grupo).ToList(), _pontos, Semente);
    }

    public string NomeDaFase(JogoDoTorneio jogo) =>
        jogo.Grupo is { } grupo ? $"Grupo {grupo}" : QuadroDoMataMata.NomeDaFase(_quadro.Count >> jogo.RodadaDaChave);

    /// <summary>
    /// Até onde a dupla chegou (até agora, se o torneio não acabou). Bye conta como passar: quem
    /// descansou na primeira rodada e perdeu na segunda chegou à segunda.
    /// </summary>
    public FaseAlcancada FaseDaDupla(string nome)
    {
        Dupla(nome);
        if (Campeao?.Nome == nome) return FaseAlcancada.Campeao;
        if (!_quadro.Contains(nome)) return FaseAlcancada.FaseDeGrupos;

        int ultima = 0;
        for (int r = 0; r < RodadasDaChave; r++)
            for (int k = 0; k < _quadro.Count >> (r + 1); k++)
            {
                var (a, b) = Participantes(r, k);
                if (a == nome || b == nome) ultima = r;
            }
        return QuadroDoMataMata.FaseDaRodada(_quadro.Count >> ultima);
    }

    // ── O que o jogo faz ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// O resultado de um jogo da rodada atual, com os sets do ponto de vista da
    /// <see cref="JogoDoTorneio.DuplaA"/>. Placar impossível é recusado com o motivo.
    /// </summary>
    public void InformarResultado(int numeroDoJogo, IReadOnlyList<SetEncerrado> sets)
    {
        int indice = _jogos.FindIndex(j => j.Numero == numeroDoJogo);
        if (indice < 0) throw new ArgumentException($"Não há jogo {numeroDoJogo} neste torneio.", nameof(numeroDoJogo));
        var jogo = _jogos[indice];
        if (jogo.Jogado) throw new InvalidOperationException($"O jogo {numeroDoJogo} já tem resultado ({jogo.Resumo()}).");
        if (jogo.Rodada != RodadaAtual)
            throw new InvalidOperationException($"O jogo {numeroDoJogo} é da rodada {jogo.Rodada + 1}; a rodada atual é a {RodadaAtual + 1}.");
        if (PlacarDoJogo.ProblemaNoPlacar(sets, Regras.SetsParaVencer) is { } problema)
            throw new ArgumentException($"Placar impossível pro jogo {numeroDoJogo}: {problema}", nameof(sets));
        _jogos[indice] = jogo with { Sets = PlacarDoJogo.Copiar(sets) };
    }

    /// <summary>
    /// O resultado vindo da Partida de verdade: lê <see cref="Placar.SetsAnteriores"/> e vira o
    /// placar se a dupla que jogou como time 0 (casa) for a <see cref="JogoDoTorneio.DuplaB"/>.
    /// </summary>
    public void InformarResultado(int numeroDoJogo, Placar placar, string duplaNoTime0)
    {
        ArgumentNullException.ThrowIfNull(placar);
        if (!placar.Acabou)
            throw new ArgumentException($"A partida do jogo {numeroDoJogo} ainda não acabou ({placar.Resumo()}).", nameof(placar));
        var jogo = _jogos.FirstOrDefault(j => j.Numero == numeroDoJogo)
            ?? throw new ArgumentException($"Não há jogo {numeroDoJogo} neste torneio.", nameof(numeroDoJogo));
        IReadOnlyList<SetEncerrado> sets =
            duplaNoTime0 == jogo.DuplaA ? placar.SetsAnteriores
            : duplaNoTime0 == jogo.DuplaB ? PlacarDoJogo.Inverter(placar.SetsAnteriores)
            : throw new ArgumentException($"\"{duplaNoTime0}\" não joga o jogo {numeroDoJogo} ({jogo.DuplaA} x {jogo.DuplaB}).", nameof(duplaNoTime0));
        InformarResultado(numeroDoJogo, sets);
    }

    /// <summary>
    /// Fecha a rodada atual e abre a próxima (simulando os jogos de IA dela). Devolve
    /// <c>false</c> sem mexer em nada quando ainda falta resultado de dupla humana, ou quando o
    /// torneio já acabou.
    /// </summary>
    public bool AvancarRodada()
    {
        if (Encerrado || _jogos.Any(j => j.Rodada == RodadaAtual && !j.Jogado)) return false;

        if (EmFaseDeGrupos)
        {
            if (RodadaAtual + 1 < _rodadasDeGrupo)
            {
                RodadaAtual++;
                SimularJogosDasIAs();
                return true;
            }
            MontarChaveDosGrupos();
            if (Encerrado) return true;
            RodadaAtual++;
            AbrirRodadaDaChave(0);
            return true;
        }

        RodadaAtual++;
        AbrirRodadaDaChave(RodadaAtual - _rodadasDeGrupo);
        return true;
    }

    /// <summary>Joga até o fim um torneio sem humano pendente (assistir, ou teste).</summary>
    public void JogarAteOFim()
    {
        while (!Encerrado)
            if (!AvancarRodada())
                throw new InvalidOperationException(
                    $"Rodada {RodadaAtual + 1} esperando o resultado da dupla humana (jogo {JogosPendentes.FirstOrDefault()?.Numero}).");
    }

    // ── Salvar e restaurar (quem usa é a Carreira) ───────────────────────────────────────

    internal EstadoDoTorneio Estado() => new()
    {
        Duplas = _duplas.ToList(),
        Regras = Regras,
        Semente = Semente,
        PontosDeRanking = new Dictionary<string, int>(_pontos, StringComparer.Ordinal),
        Grupos = _grupos.ToList(),
        Quadro = _quadro.ToList(),
        Jogos = _jogos.ToList(),
        RodadaAtual = RodadaAtual,
    };

    /// <summary>Reconstrói do arquivo, conferindo o que um arquivo torto poderia quebrar.</summary>
    internal static TorneioDeDuplas Restaurar(EstadoDoTorneio estado)
    {
        var duplas = estado.Duplas ?? throw new InvalidDataException("Etapa salva sem a lista de duplas.");
        var regras = estado.Regras ?? throw new InvalidDataException("Etapa salva sem as regras.");
        var grupos = estado.Grupos ?? throw new InvalidDataException("Etapa salva sem os grupos.");
        var quadro = estado.Quadro ?? throw new InvalidDataException("Etapa salva sem o quadro.");
        var jogos = estado.Jogos ?? throw new InvalidDataException("Etapa salva sem os jogos.");

        var nomes = duplas.Where(d => d is not null).Select(d => d.Nome).ToHashSet(StringComparer.Ordinal);
        bool Conhecida(string? nome) => nome is not null && nomes.Contains(nome);

        foreach (var grupo in grupos)
            if (grupo?.Nome is null || grupo.Duplas is null || !grupo.Duplas.All(Conhecida))
                throw new InvalidDataException("Etapa salva com um grupo torto (sem nome ou com dupla que não está no torneio).");
        if (!quadro.All(v => v is null || Conhecida(v)) || (quadro.Count > 1 && quadro.Count != QuadroDoMataMata.MenorPotenciaDe2APartirDe(quadro.Count)))
            throw new InvalidDataException("Etapa salva com o quadro do mata-mata torto.");
        foreach (var jogo in jogos)
        {
            if (jogo is null || !Conhecida(jogo.DuplaA) || !Conhecida(jogo.DuplaB))
                throw new InvalidDataException("Etapa salva com um jogo de dupla que não está no torneio.");
            if (jogo.Jogado && PlacarDoJogo.ProblemaNoPlacar(jogo.Sets, regras.SetsParaVencer) is { } problema)
                throw new InvalidDataException($"Etapa salva com o jogo {jogo.Numero} impossível: {problema}");
        }
        if (estado.RodadaAtual < 0 || estado.RodadaAtual > jogos.Select(j => j.Rodada).DefaultIfEmpty(0).Max())
            throw new InvalidDataException($"Etapa salva na rodada {estado.RodadaAtual + 1}, que não tem jogo nenhum.");

        try
        {
            var pontos = new Dictionary<string, int>(estado.PontosDeRanking ?? new Dictionary<string, int>(), StringComparer.Ordinal);
            return new TorneioDeDuplas(duplas, regras, estado.Semente, pontos, grupos, quadro, jogos, estado.RodadaAtual);
        }
        catch (ArgumentException e)
        {
            throw new InvalidDataException($"Etapa salva inválida: {e.Message}", e);
        }
    }
}

/// <summary>O torneio como vai pro arquivo. Tudo anulável: é o que veio do disco, antes de conferir.</summary>
internal sealed class EstadoDoTorneio
{
    public List<DuplaParticipante>? Duplas { get; set; }
    public RegrasDoTorneio? Regras { get; set; }
    public uint Semente { get; set; }
    public Dictionary<string, int>? PontosDeRanking { get; set; }
    public List<GrupoDoTorneio>? Grupos { get; set; }
    public List<string?>? Quadro { get; set; }
    public List<JogoDoTorneio>? Jogos { get; set; }
    public int RodadaAtual { get; set; }
}
