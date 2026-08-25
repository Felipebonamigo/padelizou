using System.Globalization;

namespace Padelizou.Services;

// A DATA E A HORA QUE VÊM DE DOIS CAMPOS SEPARADOS.
//
// A tela de Adicionar Aula deixou de usar `<input type="datetime-local">` em 25/08/2026, a
// pedido do Felipe: no Android aquele tipo abre a RODINHA de rolagem, onde a data é um número
// solto — e o professor precisa saber QUE DIA DA SEMANA é a aula, que é o motivo de ele marcar
// olhando o calendário. `type="date"` abre o calendário do sistema (com os dias da semana no
// cabeçalho) e `type="time"` abre o relógio.
//
// ⚠️ O PARSING É INVARIANTE, E É A RAZÃO DESTE ARQUIVO EXISTIR EM VEZ DE UM `DateTime` NA
// ASSINATURA DO CONTROLLER. O app roda com cultura pt-BR (Program.cs) e o `<input type="date">`
// manda SEMPRE "yyyy-MM-dd" — lido em pt-BR, "2026-08-18" não é 18 de agosto: é recusa, ou
// pior, outra data. `DatasDaAulaFixa.Ler` já carrega o mesmo aviso, pelo mesmo motivo.
public static class DataEHoraDoFormulario
{
    // Os formatos que os dois campos do HTML mandam. "HH:mm:ss" entra porque um `<input
    // type="time">` com `step` em segundos manda a hora completa — é o mesmo campo, e recusar
    // o valor mais preciso seria recusar por preciosismo.
    private static readonly string[] FormatosDeData = { "yyyy-MM-dd" };

    // ⚠️ `TimeOnly` e não `TimeSpan`: os dois parecem servir, mas o formato customizado de
    // TimeSpan usa OUTROS especificadores (`hh`, com escape próprio pro `:`), e "HH:mm" ali
    // simplesmente não casa — devolve falso pra "14:00", que é o valor mais comum que existe.
    // Custou uma rodada de testes vermelhos pra aparecer.
    private static readonly string[] FormatosDeHora = { "HH:mm", "HH:mm:ss" };

    // Nulo = falta um dos dois, ou o que veio não é o que o campo manda. Quem chama decide o
    // que dizer — aqui não se inventa "hoje" nem "agora": data chutada em cima de formulário
    // torto viraria aula no dia errado, sem ninguém perceber.
    public static DateTime? Juntar(string? data, string? hora)
    {
        if (string.IsNullOrWhiteSpace(data) || string.IsNullOrWhiteSpace(hora)) return null;

        if (!DateTime.TryParseExact(data.Trim(), FormatosDeData, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dia))
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(hora.Trim(), FormatosDeHora, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var horario))
        {
            return null;
        }

        return dia.Date + horario.ToTimeSpan();
    }

    // O caminho de volta: o que a tela escreve nos dois campos. Nos MESMOS formatos do HTML —
    // com "18/08/2026" o navegador considera o valor inválido e abre o campo VAZIO, sem
    // reclamar de nada (a cicatriz está escrita no Editar.cshtml desde 09/08/2026).
    public static string ParaCampoDeData(DateTime quando) =>
        quando.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ParaCampoDeHora(DateTime quando) =>
        quando.ToString("HH\\:mm", CultureInfo.InvariantCulture);
}
