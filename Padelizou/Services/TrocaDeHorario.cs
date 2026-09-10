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

    // ═══ A TROCA COM UMA ELIMINATÓRIA QUE AINDA NÃO NASCEU (10/09/2026) ═══
    //
    // 🗣️ *"permita também trocar de horário as eliminatórias, não apenas as de chave"*. Um lado
    // da troca pode ser um jogo PREVISTO (ProximasFasesDaChave.JogoQueVem): ele não tem linha no
    // banco, então o slot que ele recebe vira uma RESERVA (Models/ReservaDeHorario), e é o robô
    // que a transforma em jogo quando a rodada nascer.

    // Um lado da troca: o jogo real OU o previsto, reduzido ao slot dele.
    public sealed record Lado(ReferenciaDoJogo Referencia, Partida? Real, ProximasFasesDaChave.JogoQueVem? Previsto)
    {
        public bool Existe => Real != null || Previsto != null;
        public DateTime? Horario => Real != null ? Real.HorarioPrevisto : Previsto?.Horario;
        public string? Quadra => Real != null ? Real.NomeQuadra : Previsto?.Quadra;

        // Como a mensagem chama o jogo: o real pelo código, como sempre; o previsto pela fase
        // numerada, que é como a tela o mostra ("4ª Masculina · Final").
        public string Rotulo => Real != null
            ? $"o jogo {Real.Codigo}"
            : Previsto != null ? $"{Previsto.Categoria} · {Previsto.FaseNumerada}" : "o jogo";
    }

    // Null = pode trocar. Texto = o motivo. As regras do jogo real são as mesmas de sempre (acima);
    // o previsto só precisa existir na prévia de agora e ter hora.
    public static string? MotivoParaNaoTrocar(Lado a, Lado b, int torneioId)
    {
        if (!a.Existe || !b.Existe)
            return "Não encontrei um dos jogos — a prévia pode ter mudado desde que a página abriu. Recarregue e tente de novo.";
        if (a.Referencia == b.Referencia) return "Escolha dois jogos diferentes.";

        foreach (var real in new[] { a.Real, b.Real })
        {
            if (real == null) continue;
            if (real.TorneioId != torneioId) return "Os dois jogos precisam ser deste torneio.";
            if (real.Status != "Agendada") return $"O jogo {real.Codigo} já começou ou terminou — só se troca jogo agendado.";
        }

        if (a.Horario == null || b.Horario == null)
            return "Um dos jogos ainda está sem horário — não há o que trocar.";

        return null;
    }
}
