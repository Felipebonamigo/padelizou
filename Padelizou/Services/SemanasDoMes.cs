using System.Globalization;

namespace Padelizou.Services;

// Como um mês vira barras no card de semanas do Financeiro.
//
// 🗣️ Pedido do Felipe, 01/09/2026: *"aonde diz (Ultimas 6 semanas), permita tambem escolher ali
// o mês, separando as semanas, como padrão vem o mês atual"*.
//
// Antes o card era uma janela ROLANTE de 6 semanas, que atravessava a virada do mês — a última
// barra do print dele era "31/08–06/09". Uma barra assim não pertence a mês nenhum: nem a soma
// das barras batia com agosto, nem com setembro.
//
// ⚠️ A SEMANA CONTINUA SENDO DE SEGUNDA A DOMINGO — ela só é RECORTADA nas pontas do mês. É o
// recorte que faz a soma das barras ser exatamente o faturamento daquele mês, que é o número
// que o card "Últimos 6 meses" mostra logo abaixo, na mesma tela. Sem ele, a mesma página
// diria dois valores diferentes pro mesmo mês — e é assim que o professor conclui que o
// sistema perdeu dinheiro dele.
//
// Função pura, sem EF: as pontas do mês são onde este tipo de conta erra calado (um dia fora
// de todas as fatias é dinheiro que some da tela; um dia em duas é dinheiro contado duas
// vezes), e aqui isso é testável sem montar banco.
public static class SemanasDoMes
{
    // O mês viaja na URL neste formato, e não como data solta: é o que o `<input type="month">`
    // e o parâmetro do link falam.
    public const string Formato = "yyyy-MM";

    // ⚠️ InvariantCulture na leitura. "2026-08" lido na cultura pt-BR do app é a armadilha que
    // já mordeu este projeto uma vez (ver Services/DatasDaAulaFixa).
    //
    // Parâmetro perdido, vazio ou impossível cai no mês atual em vez de dar erro: URL editada
    // na mão não pode quebrar a tela inteira do financeiro.
    public static DateTime Escolhido(string? texto, DateTime hoje) =>
        DateTime.TryParseExact((texto ?? "").Trim(), Formato, CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out var mes)
            ? new DateTime(mes.Year, mes.Month, 1)
            : new DateTime(hoje.Year, hoje.Month, 1);

    // As fatias do mês, em ordem. `Fim` é INCLUSIVO — é o último dia que aparece no rótulo da
    // barra, e quem consulta soma até o fim dele.
    public static IReadOnlyList<(DateTime Inicio, DateTime Fim)> Fatiar(DateTime mes)
    {
        var primeiro = new DateTime(mes.Year, mes.Month, 1);
        var ultimo = primeiro.AddMonths(1).AddDays(-1);

        var fatias = new List<(DateTime Inicio, DateTime Fim)>();

        var inicio = primeiro;
        while (inicio <= ultimo)
        {
            // O domingo daquela semana. Segunda = 0 … domingo = 6, que é a mesma conta de
            // segunda-a-domingo que o Financeiro já faz pro filtro "Esta semana".
            var domingo = inicio.AddDays(6 - ((int)inicio.DayOfWeek + 6) % 7);

            var fim = domingo > ultimo ? ultimo : domingo;
            fatias.Add((inicio, fim));
            inicio = fim.AddDays(1);
        }

        return fatias;
    }

    // "setembro de 2026". Nome à mão, pelo PeriodoAgenda: o servidor não tem cultura pt-BR
    // garantida, e "September" no meio do Financeiro seria descoberto pelo professor.
    public static string Rotulo(DateTime mes) => $"{PeriodoAgenda.NomeDoMes(mes.Month)} de {mes.Year}";

    public static string Chave(DateTime mes) => mes.ToString(Formato, CultureInfo.InvariantCulture);
}
