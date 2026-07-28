namespace Padelizou.Services;

// A conta de datas da agenda: que fatia do tempo está na tela, e pra onde vão as setas.
//
// Fica separada da view porque é o tipo de coisa que erra em silêncio — semana que começa
// no dia errado, mês que perde o dia 31, "hoje" que cai fora da janela quando o mês vira.
// Aqui dá pra testar sem abrir o navegador.
public static class PeriodoAgenda
{
    public const string Dia = "dia";
    public const string Semana = "semana";
    public const string Mes = "mes";

    public const string VistaCalendario = "calendario";
    public const string VistaLista = "lista";

    private static readonly string[] Meses =
    {
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    };

    private static readonly string[] DiasCurtos = { "dom", "seg", "ter", "qua", "qui", "sex", "sáb" };

    // Nomes escritos à mão em vez de CultureInfo: o servidor não tem cultura pt-BR
    // garantida, e "July" no meio da agenda seria descoberto pelo usuário, não por nós.
    public static string NomeDoMes(int mes) => Meses[mes - 1];
    public static string DiaCurto(DayOfWeek dia) => DiasCurtos[(int)dia];

    public static string Normalizar(string? periodo) => (periodo ?? "").ToLower() switch
    {
        Dia => Dia,
        Mes => Mes,
        _ => Semana,      // padrão: a semana é o que o professor olha no dia a dia
    };

    public static string NormalizarVista(string? vista) =>
        (vista ?? "").ToLower() == VistaLista ? VistaLista : VistaCalendario;

    // Domingo a sábado, como todo calendário brasileiro.
    public static DateTime InicioDaSemana(DateTime d) => d.Date.AddDays(-(int)d.Date.DayOfWeek);

    // Janela [Inicio, Fim) — fim EXCLUSIVO, pra não precisar de "23:59:59" em lugar nenhum
    // e não perder a aula marcada exatamente na virada.
    public static (DateTime Inicio, DateTime Fim) Janela(string? periodo, DateTime referencia)
    {
        var d = referencia.Date;
        switch (Normalizar(periodo))
        {
            case Dia:
                return (d, d.AddDays(1));
            case Mes:
                var primeiro = new DateTime(d.Year, d.Month, 1);
                return (primeiro, primeiro.AddMonths(1));
            default:
                var domingo = InicioDaSemana(d);
                return (domingo, domingo.AddDays(7));
        }
    }

    // Um passo pra trás (-1) ou pra frente (+1). No mês, anda de mês em mês — e o dia 31
    // não pode virar "mês que vem" ao passar por fevereiro, daí ancorar no dia 1º.
    public static DateTime Deslocar(string? periodo, DateTime referencia, int passos)
    {
        var d = referencia.Date;
        return Normalizar(periodo) switch
        {
            Dia => d.AddDays(passos),
            Mes => new DateTime(d.Year, d.Month, 1).AddMonths(passos),
            _ => d.AddDays(7 * passos),
        };
    }

    public static string Titulo(string? periodo, DateTime referencia)
    {
        var (inicio, fim) = Janela(periodo, referencia);
        var ultimo = fim.AddDays(-1);

        switch (Normalizar(periodo))
        {
            case Dia:
                return $"{DiaCurto(inicio.DayOfWeek)}, {inicio.Day} de {NomeDoMes(inicio.Month)} de {inicio.Year}";
            case Mes:
                return $"{NomeDoMes(inicio.Month)} de {inicio.Year}";
            default:
                // "26 de julho – 1 de agosto": só repete o mês quando ele muda no meio.
                return inicio.Month == ultimo.Month
                    ? $"{inicio.Day} – {ultimo.Day} de {NomeDoMes(inicio.Month)} de {inicio.Year}"
                    : $"{inicio.Day} de {NomeDoMes(inicio.Month)} – {ultimo.Day} de {NomeDoMes(ultimo.Month)} de {ultimo.Year}";
        }
    }

    // A grade do mês do Google: começa no domingo da semana do dia 1º e termina no sábado
    // da semana do último dia. Sempre semanas inteiras, senão a tabela fica com buraco.
    public static List<DateTime> GradeDoMes(DateTime referencia)
    {
        var primeiro = new DateTime(referencia.Year, referencia.Month, 1);
        var ultimo = primeiro.AddMonths(1).AddDays(-1);

        var inicio = InicioDaSemana(primeiro);
        var fim = InicioDaSemana(ultimo).AddDays(7);      // exclusivo

        var dias = new List<DateTime>();
        for (var d = inicio; d < fim; d = d.AddDays(1)) dias.Add(d);
        return dias;
    }

    // Faixa de horas da grade de dia/semana. Não é 0–23 fixo: aula de padel é de manhã
    // cedo ou à noite, e mostrar 24 linhas empurraria tudo pra fora da tela do celular.
    // Abre no menor horário do dia com aula (ou 6h) e fecha no maior (ou 22h).
    public static (int Primeira, int Ultima) FaixaDeHoras(IEnumerable<DateTime> horarios)
    {
        int primeira = 6, ultima = 22;

        foreach (var h in horarios)
        {
            if (h.Hour < primeira) primeira = h.Hour;
            if (h.Hour > ultima) ultima = h.Hour;
        }

        return (primeira, ultima);
    }
}
