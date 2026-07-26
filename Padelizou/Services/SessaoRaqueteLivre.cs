using Padelizou.Models;

namespace Padelizou.Services;

// Regras de exibição do Raquete Livre, num lugar só porque listagem, detalhe e
// notificação precisam concordar sobre "ainda está rolando?" e sobre como escrever
// um horário que pode não ter fim.
public static class SessaoRaqueteLivre
{
    // Quanto tempo uma sessão sem hora de fim continua aparecendo depois de começar.
    // Seis horas cobre o caso real (começa 19h, acaba quando o pessoal cansa) sem deixar
    // o aviso da terça pendurado na lista de quinta.
    public static readonly TimeSpan DuracaoPresumida = TimeSpan.FromHours(6);

    // Sessões ativas que ainda valem a pena mostrar.
    public static IQueryable<AvisoRaqueteLivre> EmCartaz(IQueryable<AvisoRaqueteLivre> query, DateTime agora)
    {
        var limiteSemFim = agora - DuracaoPresumida;

        return query.Where(a => a.Status == "Ativo" &&
            ((a.DataHoraFim != null && a.DataHoraFim >= agora) ||
             (a.DataHoraFim == null && a.DataHoraInicio >= limiteSemFim)));
    }

    // "19:00 às 22:00" quando há previsão; "a partir das 19:00" quando não há.
    public static string DescreverHorario(AvisoRaqueteLivre aviso) =>
        aviso.DataHoraFim.HasValue
            ? $"{aviso.DataHoraInicio:HH:mm} às {aviso.DataHoraFim.Value:HH:mm}"
            : $"a partir das {aviso.DataHoraInicio:HH:mm}";

    public static bool SemHoraPraAcabar(AvisoRaqueteLivre aviso) => !aviso.DataHoraFim.HasValue;
}
