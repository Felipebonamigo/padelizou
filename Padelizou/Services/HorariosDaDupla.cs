using Padelizou.Models;

namespace Padelizou.Services;

// Como os horários que o organizador combinou com a dupla aparecem na tela (Models/HorarioDaDupla).
// Um texto só pra etiqueta da lista, a linha do painel e a mensagem de sucesso.
public static class HorariosDaDupla
{
    private static readonly System.Globalization.CultureInfo PtBr = new("pt-BR");

    public static string Rotulo(bool podeJogar, DateTime inicio, DateTime fim)
    {
        var dia = inicio.ToString("ddd dd/MM", PtBr).Replace(".", "");
        bool diaInteiro = inicio.TimeOfDay == TimeSpan.Zero && fim == inicio.Date.AddDays(1);
        var faixa = diaInteiro ? "dia inteiro" : $"{inicio:HH:mm}–{(fim == inicio.Date.AddDays(1) ? "24:00" : fim.ToString("HH:mm"))}";
        return $"{dia} {faixa}";
    }

    public static string Rotulo(HorarioDaDupla h) => Rotulo(h.PodeJogar, h.Inicio, h.Fim);

    // Os dias que a tela oferece: do início ao fim do torneio (sem fim marcado, três dias — o
    // mesmo alcance que os turnos já usam).
    public static IReadOnlyList<DateTime> DiasDoTorneio(Torneio torneio)
    {
        if (torneio.DataInicio is not DateTime inicio) return Array.Empty<DateTime>();
        var ultimo = torneio.DataFim?.Date is DateTime fim && fim >= inicio.Date ? fim : inicio.Date.AddDays(2);
        var dias = new List<DateTime>();
        for (var d = inicio.Date; d <= ultimo && dias.Count < 14; d = d.AddDays(1)) dias.Add(d);
        return dias;
    }
}
