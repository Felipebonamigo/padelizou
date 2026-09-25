namespace Padel.Core.Perfil;

/// <summary>As línguas do jogo (CRONOGRAMA, M3): português do Brasil, inglês e espanhol.</summary>
public enum Idioma { Portugues, Ingles, Espanhol }

public sealed record TextoLocalizado(string Portugues, string Ingles, string Espanhol)
{
    public string Em(Idioma idioma) => idioma switch
    {
        Idioma.Portugues => Portugues,
        Idioma.Ingles => Ingles,
        Idioma.Espanhol => Espanhol,
        _ => throw new ArgumentOutOfRangeException(nameof(idioma), idioma, "Idioma desconhecido."),
    };
}

/// <summary>
/// Uma conquista da Steam. <see cref="Id"/> é o "API name" cadastrado no Steamworks. Secreta = "hidden" na Steam (a
/// descrição só aparece depois de desbloquear). Cumulativa: tem uma estatística da Steam (API name em
/// <see cref="Estatistica"/>) e a <see cref="Meta"/> — a Steam mostra o progresso (ex.: 63/100).
/// </summary>
public sealed record Conquista(string Id, TextoLocalizado Nome, TextoLocalizado Descricao, bool Secreta = false, string? Estatistica = null, int Meta = 0)
{
    public bool Cumulativa => Estatistica is not null;
}

/// <summary>
/// As 20 conquistas (CRONOGRAMA, M4). A condição exata de cada uma está no <see cref="AvaliadorDeConquistas"/> e na
/// tabela de docs/CONQUISTAS.md, que é o que se cadastra no Steamworks.
/// <para>
/// ⚠️ <b>O ID é o "API name" da Steam e NÃO PODE MUDAR depois de publicado.</b> A Steam guarda o desbloqueio de cada
/// jogador por esse nome; renomear é apagar a conquista de quem já tinha e criar outra. Nome e descrição (as três
/// línguas) podem mudar à vontade — o ID não. O mesmo vale pros nomes das estatísticas (BANDEJAS, PARTIDAS). A ordem da
/// lista é a de exibição e pode mudar; conquista nova entra com ID novo.
/// </para>
/// </summary>
public static class CatalogoDeConquistas
{
    public const string PrimeiroPonto = "PRIMEIRO_PONTO";
    public const string PrimeiraVitoria = "PRIMEIRA_VITORIA";
    public const string Pneu = "PNEU";
    public const string PontoDeOuro = "PONTO_DE_OURO";
    public const string TieBreak = "TIE_BREAK";
    public const string Virada = "VIRADA";
    public const string Bandeja100 = "BANDEJA_100";
    public const string ViboraVencedora = "VIBORA_VENCEDORA";
    public const string Por3 = "POR_3";
    public const string Por4 = "POR_4";
    public const string ChiquitaVencedora = "CHIQUITA_VENCEDORA";
    public const string Contrapared = "CONTRAPARED";
    public const string PelaPorta = "PELA_PORTA";
    public const string Rally30 = "RALLY_30";
    public const string VitoriaNoDificil = "VITORIA_NO_DIFICIL";
    public const string SemBolaNaRede = "SEM_BOLA_NA_REDE";
    public const string CampeaoDeEtapa = "CAMPEAO_DE_ETAPA";
    public const string Numero1 = "NUMERO_1";
    public const string VitoriaOnline = "VITORIA_ONLINE";
    public const string Maratona = "MARATONA";

    /// <summary>Estatística da Steam (INT): bandejas batidas, somando todas as partidas e o treino.</summary>
    public const string EstatisticaDeBandejas = "BANDEJAS";
    /// <summary>Estatística da Steam (INT): partidas terminadas fora do treino.</summary>
    public const string EstatisticaDePartidas = "PARTIDAS";

    /// <summary>Games de atraso que a VIRADA pede revertidos num set vencido (0-3, 1-4, 2-5...).</summary>
    public const int DesvantagemDaVirada = 3;
    public const int GolpesDoRallyLongo = 30;

    private static Conquista C(string id, (string pt, string en, string es) nome, (string pt, string en, string es) descricao,
        bool secreta = false, string? estatistica = null, int meta = 0) =>
        new(id, new TextoLocalizado(nome.pt, nome.en, nome.es), new TextoLocalizado(descricao.pt, descricao.en, descricao.es), secreta, estatistica, meta);

    public static IReadOnlyList<Conquista> Todas { get; } =
    [
        C(PrimeiroPonto,
            ("Primeiro ponto", "First Point", "Primer punto"),
            ("Ganhe o seu primeiro ponto numa partida.", "Win your first point in a match.", "Gana tu primer punto en un partido.")),
        C(PrimeiraVitoria,
            ("Primeira vitória", "First Win", "Primera victoria"),
            ("Vença uma partida.", "Win a match.", "Gana un partido.")),
        C(Pneu,
            ("Pneu", "Bagel", "Rosco"),
            ("Vença um set por 6-0.", "Win a set 6-0.", "Gana un set 6-0.")),
        C(PontoDeOuro,
            ("Ponto de ouro", "Golden Point", "Punto de oro"),
            ("Vença um ponto de ouro, no 40-40.", "Win a golden point at 40-40.", "Gana un punto de oro con 40-40.")),
        C(TieBreak,
            ("Tie-break", "Tiebreak", "Tie-break"),
            ("Vença um set no tie-break.", "Win a set in a tiebreak.", "Gana un set en el tie-break.")),
        C(Virada,
            ("Virada", "Comeback", "Remontada"),
            ("Vença um set em que esteve 3 games atrás.", "Win a set after trailing by 3 games.", "Gana un set tras ir 3 juegos por detrás.")),
        C(Bandeja100,
            ("Cem bandejas", "100 Bandejas", "Cien bandejas"),
            ("Bata 100 bandejas, somando todas as partidas.", "Hit 100 bandejas across all your matches.", "Pega 100 bandejas, sumando todos los partidos."),
            estatistica: EstatisticaDeBandejas, meta: 100),
        C(ViboraVencedora,
            ("Víbora vencedora", "Winning Víbora", "Víbora ganadora"),
            ("Ganhe um ponto com uma víbora.", "Win a point with a víbora.", "Gana un punto con una víbora.")),
        C(Por3,
            ("Por 3", "Por 3", "Por 3"),
            ("Ganhe um ponto com um remate por 3, pra fora pela lateral.", "Win a point with a smash out over the side wall (por 3).", "Gana un punto con un remate por 3, fuera por la lateral.")),
        C(Por4,
            ("Por 4", "Por 4", "Por 4"),
            ("Ganhe um ponto com um remate por 4, pra fora por cima do fundo.", "Win a point with a smash out over the back wall (por 4).", "Gana un punto con un remate por 4, fuera por encima del fondo."),
            secreta: true),
        C(ChiquitaVencedora,
            ("Chiquita vencedora", "Winning Chiquita", "Chiquita ganadora"),
            ("Ganhe um ponto com uma chiquita.", "Win a point with a chiquita.", "Gana un punto con una chiquita.")),
        C(Contrapared,
            ("Contrapared", "Off the Back Glass", "Contrapared"),
            ("Ganhe um ponto com uma contrapared, batendo no seu próprio vidro.", "Win a point with a contrapared, hitting off your own back glass.", "Gana un punto con una contrapared, golpeando contra tu propio cristal."),
            secreta: true),
        C(PelaPorta,
            ("Pela porta", "Through the Door", "Por la puerta"),
            ("Ganhe um ponto com a bola saindo pela porta.", "Win a point with the ball going out through the door.", "Gana un punto con la bola saliendo por la puerta.")),
        C(Rally30,
            ("Rally de 30", "30-Shot Rally", "Peloteo de 30"),
            ("Jogue um ponto de 30 golpes ou mais.", "Play a point of 30 or more shots.", "Juega un punto de 30 golpes o más.")),
        C(VitoriaNoDificil,
            ("Vitória no difícil", "Hard Win", "Victoria en difícil"),
            ("Vença uma partida contra a IA no difícil.", "Beat the AI on Hard.", "Gana un partido contra la IA en difícil.")),
        C(SemBolaNaRede,
            ("Sem bola na rede", "Clean Net", "Sin bola a la red"),
            ("Vença uma partida sem mandar nenhuma bola na rede.", "Win a match without hitting a single ball into the net.", "Gana un partido sin mandar ninguna bola a la red.")),
        C(CampeaoDeEtapa,
            ("Campeão de etapa", "Stage Champion", "Campeón de etapa"),
            ("Vença uma etapa do circuito na carreira.", "Win a stage of the career circuit.", "Gana una etapa del circuito en la carrera.")),
        C(Numero1,
            ("Número 1", "Number 1", "Número 1"),
            ("Termine o circuito da carreira em 1º no ranking.", "Finish the career circuit ranked 1st.", "Termina el circuito de la carrera en el 1.º puesto del ranking.")),
        C(VitoriaOnline,
            ("Vitória online", "Online Win", "Victoria en línea"),
            ("Vença uma partida online contra outro jogador.", "Win an online match against another player.", "Gana un partido en línea contra otro jugador.")),
        C(Maratona,
            ("Maratona", "Marathon", "Maratón"),
            ("Jogue 50 partidas até o fim.", "Finish 50 matches.", "Termina 50 partidos."),
            estatistica: EstatisticaDePartidas, meta: 50),
    ];

    /// <summary>
    /// A forma de um ID (MAIÚSCULAS_COM_SUBLINHADO: letras A–Z, dígitos, sublinhado só entre eles). Vale também pra ID que
    /// este jogo não conhece (de uma versão mais nova): o perfil guarda qualquer ID nessa forma.
    /// </summary>
    public static bool IdValido(string id) =>
        !string.IsNullOrEmpty(id) && id[0] != '_' && id[^1] != '_' && !id.Contains("__", StringComparison.Ordinal)
        && id.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');

    private static readonly Dictionary<string, Conquista> PorId = Todas.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public static Conquista Por(string id) =>
        PorId.TryGetValue(id, out var conquista) ? conquista : throw new ArgumentException($"Conquista desconhecida: {id}.", nameof(id));

    /// <summary>O valor atual de uma estatística da Steam no perfil — o que a camada Steam grava com SetStat.</summary>
    public static int ValorDaEstatistica(PerfilDoJogador perfil, string estatistica)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        return estatistica switch
        {
            EstatisticaDeBandejas => perfil.GolpesPorTipo.GetValueOrDefault(TipoDeGolpe.Bandeja),
            EstatisticaDePartidas => perfil.Partidas,
            _ => throw new ArgumentException($"Estatística desconhecida: {estatistica}.", nameof(estatistica)),
        };
    }
}
