namespace Padelizou.Services;

// Em que "fatias" o painel de métricas soma cadastros, inscrições e pagamentos.
//
// Nasceu só por semana. Semana é boa pra ver tendência, e péssima pra duas perguntas que o
// admin faz o tempo todo: "o que aconteceu HOJE, depois que eu mandei o link no grupo?" e
// "como está o ano?". Dia responde a primeira, mês responde a segunda.
//
// O quanto de história cada fatia mostra é diferente de propósito: 14 dias cabem numa tela
// e já mostram o ritmo da semana; 12 meses cobrem o ano fiscal do MEI, que é a conta que
// aparece logo acima na mesma página.
public static class FaixasDeMetricas
{
    public const string Dia = "dia";
    public const string Semana = "semana";
    public const string Mes = "mes";

    private const int DiasMostrados = 14;
    private const int SemanasMostradas = 8;
    private const int MesesMostrados = 12;

    // Qualquer coisa fora das três vira semana — que é o que a tela sempre mostrou. Link
    // velho, parâmetro digitado à mão ou vazio não podem virar erro numa tela de leitura.
    public static string Normalizar(string? agrupamento) => (agrupamento ?? "").Trim().ToLowerInvariant() switch
    {
        Dia => Dia,
        Mes => Mes,
        _ => Semana,
    };

    // A segunda-feira da semana de `d`. Domingo é o dia 0 do .NET, mas a semana de quem
    // organiza torneio começa na segunda — o fim de semana é o EVENTO, não o começo dela.
    public static DateTime InicioDaSemana(DateTime d) => d.Date.AddDays(-(((int)d.Date.DayOfWeek + 6) % 7));

    public static DateTime InicioDaFaixa(string? agrupamento, DateTime d) => Normalizar(agrupamento) switch
    {
        Dia => d.Date,
        Mes => new DateTime(d.Year, d.Month, 1),
        _ => InicioDaSemana(d),
    };

    public static DateTime ProximaFaixa(string? agrupamento, DateTime inicio) => Normalizar(agrupamento) switch
    {
        Dia => inicio.AddDays(1),
        Mes => inicio.AddMonths(1),
        _ => inicio.AddDays(7),
    };

    public static int Quantidade(string? agrupamento) => Normalizar(agrupamento) switch
    {
        Dia => DiasMostrados,
        Mes => MesesMostrados,
        _ => SemanasMostradas,
    };

    // Os começos de cada fatia, da mais ANTIGA pra mais recente. A tela inverte na hora de
    // desenhar (o recente em cima), mas somar é mais fácil em ordem crescente.
    public static List<DateTime> Series(string? agrupamento, DateTime agora)
    {
        var tipo = Normalizar(agrupamento);
        var atual = InicioDaFaixa(tipo, agora);

        var series = new List<DateTime>();
        for (var i = Quantidade(tipo) - 1; i >= 0; i--)
        {
            series.Add(tipo switch
            {
                Dia => atual.AddDays(-i),
                Mes => atual.AddMonths(-i),
                _ => atual.AddDays(-7 * i),
            });
        }
        return series;
    }

    // A partir de quando buscar no banco. É o começo da fatia mais antiga — nada anterior
    // a isso entra em fatia nenhuma, então não precisa vir.
    public static DateTime InicioDaBusca(string? agrupamento, DateTime agora) => Series(agrupamento, agora)[0];

    // Como cada linha se apresenta.
    //   dia    → "seg, 03/08"   (o dia da semana importa: sábado de torneio não é terça)
    //   semana → "03/08 – 09/08"
    //   mês    → "agosto/2026"
    public static string Rotulo(string? agrupamento, DateTime inicio) => Normalizar(agrupamento) switch
    {
        Dia => $"{PeriodoAgenda.DiaCurto(inicio.DayOfWeek).ToLowerInvariant()}, {inicio:dd/MM}",
        Mes => $"{PeriodoAgenda.NomeDoMes(inicio.Month).ToLowerInvariant()}/{inicio:yyyy}",
        _ => $"{inicio:dd/MM} – {inicio.AddDays(6):dd/MM}",
    };

    // Título do bloco e subtítulo da página, pra tela não continuar dizendo "semana a
    // semana" enquanto mostra dias.
    public static string Titulo(string? agrupamento) => Normalizar(agrupamento) switch
    {
        Dia => $"Últimos {DiasMostrados} dias",
        Mes => $"Últimos {MesesMostrados} meses",
        _ => $"Últimas {SemanasMostradas} semanas",
    };

    public static string Subtitulo(string? agrupamento) =>
        $"Como o sistema está sendo usado, {FatiaAFatia(agrupamento)}.";

    // "dia a dia", "semana a semana", "mês a mês" — pra frase nenhuma na tela precisar
    // reescrever o mapeamento e depois discordar dele.
    public static string FatiaAFatia(string? agrupamento) => Normalizar(agrupamento) switch
    {
        Dia => "dia a dia",
        Mes => "mês a mês",
        _ => "semana a semana",
    };

    // A mesma janela do `Titulo`, escrita pra caber no meio de uma frase: "15 cadastros nas
    // últimas 8 semanas". O artigo muda com o gênero da fatia — semana é ela, dia e mês são
    // ele —, e é por isso que ele mora aqui, e não solto num `?:` dentro da view.
    public static string NaJanela(string? agrupamento) => Normalizar(agrupamento) switch
    {
        Dia => $"nos últimos {DiasMostrados} dias",
        Mes => $"nos últimos {MesesMostrados} meses",
        _ => $"nas últimas {SemanasMostradas} semanas",
    };
}
