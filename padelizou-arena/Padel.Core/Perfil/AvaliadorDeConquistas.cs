namespace Padel.Core.Perfil;

/// <summary>O perfil depois de aplicar, e as conquistas que ele acabou de desbloquear (na ordem do catálogo).</summary>
public sealed record ResultadoDaAvaliacao(PerfilDoJogador Perfil, IReadOnlyList<Conquista> Novas);

/// <summary>
/// Aplica ao perfil o que aconteceu — o resumo de uma partida ou um evento de fora — e diz o que foi desbloqueado agora.
/// Puro: não mexe no perfil de entrada, não lê relógio (o instante entra), não fala com a Steam.
/// <para>
/// <b>Idempotente nas conquistas</b>: o que já estava desbloqueado continua com o instante original e não volta em
/// Novas. <b>Não</b> é idempotente nas estatísticas: aplicar o mesmo resumo duas vezes conta duas partidas — quem chama
/// aplica uma vez por partida.
/// </para>
/// As conquistas cumulativas são conferidas pela estatística do perfil em toda aplicação: um perfil que já passou da meta
/// (vindo de uma versão sem a conquista, ou de uma mesclagem) ganha a conquista na próxima vez que algo for aplicado.
/// </summary>
public static class AvaliadorDeConquistas
{
    /// <summary>
    /// Estatísticas: partida, vitória, vencedores e maior rally só contam fora do treino, e partida/vitória só com a
    /// partida terminada; golpes e tempo contam sempre (treino e abandono incluídos). Conquistas: as condições de
    /// docs/CONQUISTAS.md, nenhuma de partida no treino.
    /// </summary>
    public static ResultadoDaAvaliacao Aplicar(PerfilDoJogador perfil, ResumoDaPartida resumo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(resumo);
        Conferir(resumo);
        bool valeComoPartida = resumo.Modo != ModoDaPartida.Treino;
        bool terminadaValendo = valeComoPartida && resumo.Terminada;
        var depois = perfil with
        {
            Partidas = perfil.Partidas + (terminadaValendo ? 1 : 0),
            Vitorias = perfil.Vitorias + (terminadaValendo && resumo.Venceu ? 1 : 0),
            MaiorRally = valeComoPartida ? Math.Max(perfil.MaiorRally, resumo.MaiorRally) : perfil.MaiorRally,
            SegundosDeJogo = perfil.SegundosDeJogo + resumo.DuracaoEmSegundos,
            GolpesPorTipo = Somar(perfil.GolpesPorTipo, resumo.GolpesPorTipo),
            VencedoresPorTipo = valeComoPartida ? Somar(perfil.VencedoresPorTipo, resumo.VencedoresPorTipo) : perfil.VencedoresPorTipo,
        };
        return Desbloquear(depois, valeComoPartida ? Merecidas(resumo) : [], instante);
    }

    public static ResultadoDaAvaliacao Aplicar(PerfilDoJogador perfil, EventoDeFora evento, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(evento);
        IEnumerable<string> merecidas = evento switch
        {
            EtapaVencida => [CatalogoDeConquistas.CampeaoDeEtapa],
            CircuitoEncerrado { Posicao: 1 } => [CatalogoDeConquistas.Numero1],
            CircuitoEncerrado => [],
            VitoriaOnline { AlgumRivalHumano: true } => [CatalogoDeConquistas.PrimeiraVitoria, CatalogoDeConquistas.VitoriaOnline],
            VitoriaOnline => [CatalogoDeConquistas.PrimeiraVitoria],
            _ => throw new ArgumentException($"Evento de fora desconhecido: {evento.GetType().Name}.", nameof(evento)),
        };
        return Desbloquear(perfil, merecidas, instante);
    }

    /// <summary>
    /// O que do resumo entra no perfil tem de ser possível. Não basta o perfil se defender: somar um negativo num total
    /// que já existe dá um número "válido" e errado.
    /// </summary>
    private static void Conferir(ResumoDaPartida r)
    {
        if (!float.IsFinite(r.DuracaoEmSegundos) || r.DuracaoEmSegundos < 0)
            throw new ArgumentException($"Duração impossível no resumo: {r.DuracaoEmSegundos}.", nameof(r));
        if (r.MaiorRally < 0) throw new ArgumentException($"Maior rally negativo no resumo: {r.MaiorRally}.", nameof(r));
        foreach (var (tipo, n) in r.GolpesPorTipo.Concat(r.VencedoresPorTipo))
            if (n < 0) throw new ArgumentException($"Contagem negativa de {tipo} no resumo: {n}.", nameof(r));
    }

    /// <summary>As conquistas de partida (não cumulativas) que este resumo, fora do treino, merece.</summary>
    private static IEnumerable<string> Merecidas(ResumoDaPartida r)
    {
        bool vitoria = r.Terminada && r.Venceu;
        if (r.PontosVencidos >= 1) yield return CatalogoDeConquistas.PrimeiroPonto;
        if (vitoria) yield return CatalogoDeConquistas.PrimeiraVitoria;
        if (r.Sets.Any(s => s.Pneu)) yield return CatalogoDeConquistas.Pneu;
        if (r.PontosDeOuroVencidos >= 1) yield return CatalogoDeConquistas.PontoDeOuro;
        if (r.SetsVencidosNoTieBreak >= 1) yield return CatalogoDeConquistas.TieBreak;
        if (r.MaiorDesvantagemRevertida >= CatalogoDeConquistas.DesvantagemDaVirada) yield return CatalogoDeConquistas.Virada;
        if (Venceu(r, TipoDeGolpe.Vibora)) yield return CatalogoDeConquistas.ViboraVencedora;
        if (Venceu(r, TipoDeGolpe.SmashPor3)) yield return CatalogoDeConquistas.Por3;
        if (Venceu(r, TipoDeGolpe.SmashPor4)) yield return CatalogoDeConquistas.Por4;
        if (Venceu(r, TipoDeGolpe.Chiquita)) yield return CatalogoDeConquistas.ChiquitaVencedora;
        if (Venceu(r, TipoDeGolpe.Contrapared)) yield return CatalogoDeConquistas.Contrapared;
        if (r.SaidasPelaPorta >= 1) yield return CatalogoDeConquistas.PelaPorta;
        if (r.MaiorRally >= CatalogoDeConquistas.GolpesDoRallyLongo) yield return CatalogoDeConquistas.Rally30;
        // As duas se excluem numa mesma partida: "contra a IA" é RivalDaIA em qualquer modo (a sala online sem rival humano
        // é a IA das mesmas Opções do local); "online" pede ao menos um rival humano desde o começo (Opcoes.Humanos).
        if (vitoria && r.Dificuldade == Dificuldade.Dificil && r.RivalDaIA) yield return CatalogoDeConquistas.VitoriaNoDificil;
        if (vitoria && r.GolpesNaRede == 0) yield return CatalogoDeConquistas.SemBolaNaRede;
        if (vitoria && r.Modo == ModoDaPartida.Online && !r.RivalDaIA) yield return CatalogoDeConquistas.VitoriaOnline;
    }

    private static bool Venceu(ResumoDaPartida r, TipoDeGolpe tipo) => r.VencedoresPorTipo.GetValueOrDefault(tipo) >= 1;

    private static ResultadoDaAvaliacao Desbloquear(PerfilDoJogador perfil, IEnumerable<string> merecidas, DateTimeOffset instante)
    {
        var ids = new HashSet<string>(merecidas, StringComparer.Ordinal);
        foreach (var c in CatalogoDeConquistas.Todas)
            if (c.Estatistica is string estatistica && CatalogoDeConquistas.ValorDaEstatistica(perfil, estatistica) >= c.Meta)
                ids.Add(c.Id);

        var novas = CatalogoDeConquistas.Todas.Where(c => ids.Contains(c.Id) && !perfil.Desbloqueou(c.Id)).ToList();
        if (novas.Count == 0) return new ResultadoDaAvaliacao(perfil, []);
        var conquistas = new Dictionary<string, DateTimeOffset>(perfil.Conquistas, StringComparer.Ordinal);
        foreach (var c in novas) conquistas[c.Id] = instante;
        return new ResultadoDaAvaliacao(perfil with { Conquistas = conquistas }, novas);
    }

    private static Dictionary<TipoDeGolpe, int> Somar(IReadOnlyDictionary<TipoDeGolpe, int> a, IReadOnlyDictionary<TipoDeGolpe, int> b)
    {
        var soma = new Dictionary<TipoDeGolpe, int>(a);
        foreach (var (tipo, n) in b) soma[tipo] = soma.GetValueOrDefault(tipo) + n;
        return soma;
    }
}
