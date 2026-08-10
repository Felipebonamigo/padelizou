using System.Globalization;

namespace Padelizou.Services;

// As datas de uma aula fixa semanal.
//
// POR QUE ISTO EXISTE: até 09/08/2026 a aula fixa era só "repetir por N semanas", e o professor
// tinha que adivinhar o N. Mês com 5 sextas, feriado no meio, a semana em que ele viaja — nada
// disso cabe num número. Ele marcava 4, sobrava uma sexta; marcava 5, criava aula no feriado e
// ia apagar uma a uma depois.
//
// Agora a tela GERA a lista e ele desmarca o que não vale. O servidor não confia no que volta:
// a lista vem do formulário, e o navegador manda o que quiser.
public static class DatasDaAulaFixa
{
    // O mesmo teto da geração por contagem (ver AulasController.Cadastro): sem limite, um
    // formulário adulterado criaria mil aulas de uma vez.
    public const int MaxDatas = 26;

    // O passado não entra: aula fixa é combinado pra frente, e criar aula de ontem só suja o
    // financeiro. A data de HOJE vale — a aula pode ser daqui a duas horas.
    public static List<DateTime> Ler(IEnumerable<string>? doFormulario, DateTime agora)
    {
        if (doFormulario == null) return new List<DateTime>();

        var lidas = new List<DateTime>();

        foreach (var texto in doFormulario)
        {
            if (string.IsNullOrWhiteSpace(texto)) continue;

            // "yyyy-MM-ddTHH:mm" é o que o <input type="datetime-local"> manda. Invariante de
            // propósito: a cultura pt-BR do app leria "2026-08-10" como dia/mês/ano e cairia
            // fora em silêncio.
            if (!DateTime.TryParse(texto, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var quando))
            {
                continue;
            }

            if (quando.Date < agora.Date) continue;

            lidas.Add(quando);
        }

        // Distinct porque a mesma data marcada duas vezes viraria duas aulas no mesmo horário —
        // e a trava de conflito do controller só olha o que JÁ ESTÁ no banco, não o lote.
        return lidas.Distinct().OrderBy(d => d).Take(MaxDatas).ToList();
    }

    // A sugestão que a tela mostra: N semanas a partir da primeira data. É só o ponto de
    // partida — quem decide quais ficam é o professor, desmarcando na lista.
    public static List<DateTime> Semanais(DateTime primeira, int quantasSemanas)
    {
        var quantas = Math.Clamp(quantasSemanas, 1, MaxDatas);
        return Enumerable.Range(0, quantas).Select(i => primeira.AddDays(7 * i)).ToList();
    }
}
