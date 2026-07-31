using Padelizou.Models;

namespace Padelizou.Services;

// Troca de horário entre dois jogos, decidida pelo organizador DEPOIS do sorteio.
//
// A grade automática acerta a conta (quadras, expediente, dias), mas não conhece a vida:
// a dupla que avisou que só chega às 10h, o jogo que interessa mais tarde pra ter público.
// Em vez de o organizador pedir "remarca pra mim" no WhatsApp, ele troca o jogo A com o
// jogo B — os dois horários continuam existindo, só mudam de dono, e a grade segue íntegra
// (nenhum buraco novo, nenhuma quadra em dobro).
//
// Troca-se HORÁRIO e QUADRA juntos: o par (hora, quadra) é o slot físico. Trocar só a hora
// deixaria dois jogos na mesma quadra na mesma hora.
public static class TrocaDeHorario
{
    // Null = pode trocar. Texto = o motivo, na língua de quem organiza.
    public static string? MotivoParaNaoTrocar(Partida? a, Partida? b, int torneioId)
    {
        if (a == null || b == null) return "Não encontrei um dos jogos.";
        if (a.Id == b.Id) return "Escolha dois jogos diferentes.";
        if (a.TorneioId != torneioId || b.TorneioId != torneioId)
            return "Os dois jogos precisam ser deste torneio.";

        // Jogo em quadra ou já jogado tem placar acontecendo — o horário dele é história,
        // não agenda. Trocar aqui reescreveria o passado.
        if (a.Status != "Agendada") return $"O jogo {a.Codigo} já começou ou terminou — só se troca jogo agendado.";
        if (b.Status != "Agendada") return $"O jogo {b.Codigo} já começou ou terminou — só se troca jogo agendado.";

        if (a.HorarioPrevisto == null || b.HorarioPrevisto == null)
            return "Um dos jogos ainda está sem horário — não há o que trocar.";

        return null;
    }

    // A troca em si: horário E quadra andam juntos (o slot físico é o par).
    public static void Trocar(Partida a, Partida b)
    {
        (a.HorarioPrevisto, b.HorarioPrevisto) = (b.HorarioPrevisto, a.HorarioPrevisto);
        (a.NomeQuadra, b.NomeQuadra) = (b.NomeQuadra, a.NomeQuadra);
    }
}
