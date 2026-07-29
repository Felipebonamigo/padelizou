using Padelizou.Models;

namespace Padelizou.Services;

// Aviso de quadra atrasada: o jogo estava marcado, o horário passou e nada aconteceu.
//
// O aviso de "seu jogo é o próximo" é disparado pelo FIM do jogo anterior, de propósito —
// aviso de vez por relógio chega na hora errada. Este aqui é o complemento: ATRASO é um
// fato de relógio por definição, e quem está esperando fora do ginásio não tem como saber
// que a grade escorregou. O jogador que sabe do atraso não fica dando volta na quadra —
// e não vai embora achando que perdeu a vez.
public static class QuadraAtrasada
{
    // Só é atraso depois disto. Grade de torneio escorrega minutos o tempo todo; avisar no
    // primeiro minuto ensinaria todo mundo a ignorar o aviso.
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(15);

    // Acima disto o aviso perde o sentido: com horas de atraso não é mais "fique por
    // perto", é um torneio com problema — e isso quem resolve é o organizador, não um push.
    // Também protege o caso do torneio de vários dias com jogo de ontem nunca lançado.
    public static readonly TimeSpan Teto = TimeSpan.FromHours(3);

    // Quais partidas do torneio merecem o aviso AGORA. Recebe todas as partidas do torneio
    // porque a decisão depende do conjunto: um jogo atrasado num torneio onde nada rolou
    // hoje não é atraso, é dia que ainda não começou.
    public static List<Partida> Atrasadas(IEnumerable<Partida> partidasDoTorneio, DateTime agora)
    {
        var todas = partidasDoTorneio.ToList();

        // Sem bola rolando HOJE, ninguém recebe push de atraso: o organizador ainda não
        // colocou o dia no ar (ou desistiu do dia), e avisar "atrasado" pra todo mundo às
        // 8h da manhã seria alarme falso em massa.
        if (!TorneioEmAtividade(todas, agora)) return new List<Partida>();

        return todas
            .Where(p => p.Status == "Agendada")
            .Where(p => p.AvisoAtrasoEnviadoEm == null)      // um aviso por partida, e só um
            // Quem já ouviu "seu jogo é o próximo" já está por perto — pra ele a quadra não
            // está atrasada, está vagando. Os dois avisos juntos se contradizem.
            .Where(p => p.AvisoProximoEnviadoEm == null)
            .Where(p => p.HorarioPrevisto != null)
            .Where(p => agora - p.HorarioPrevisto!.Value >= Tolerancia)
            .Where(p => agora - p.HorarioPrevisto!.Value <= Teto)
            .OrderBy(p => p.HorarioPrevisto)
            .ToList();
    }

    // "Hoje tem torneio acontecendo": alguma partida ao vivo agora, ou alguma que começou
    // ou terminou HOJE. Final de ontem não conta — senão o segundo dia de um torneio
    // dispararia atraso antes de o portão abrir.
    public static bool TorneioEmAtividade(IEnumerable<Partida> partidas, DateTime agora)
    {
        return partidas.Any(p =>
            p.Status == "AoVivo"
            || (p.HorarioInicioReal != null && p.HorarioInicioReal.Value.Date == agora.Date)
            || (p.HorarioFimReal != null && p.HorarioFimReal.Value.Date == agora.Date));
    }

    public static int MinutosDeAtraso(Partida partida, DateTime agora) =>
        (int)(agora - partida.HorarioPrevisto!.Value).TotalMinutes;

    // O corpo diz o que a pessoa decide com ele: quanto esperar e que não perdeu a vez.
    public static string Corpo(Partida partida, DateTime agora)
    {
        var horario = partida.HorarioPrevisto!.Value.ToString("HH:mm");
        var atraso = MinutosDeAtraso(partida, agora);
        var onde = string.IsNullOrWhiteSpace(partida.NomeQuadra) ? "" : $" na {partida.NomeQuadra}";

        return $"Seu jogo das {horario}{onde} ainda não começou (~{atraso} min de atraso). " +
               "Você não perdeu a vez — avisamos quando a quadra vagar.";
    }
}
