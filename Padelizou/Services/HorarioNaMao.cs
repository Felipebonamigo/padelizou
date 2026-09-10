using Padelizou.Models;

namespace Padelizou.Services;

// Definir o horário de UM jogo na mão, digitando a hora — sem trocar de lugar com ninguém.
//
// 🗣️ Felipe, na véspera do Er (10/09/2026): *"permita tambem, trocar o horario na mão, na
// lista de jogos, para nós organizadores"*. A troca ⇄ (Services/TrocaDeHorario) exige OUTRO
// jogo pra trocar de slot; aqui só este jogo muda. A grade não é recalculada — quem aponta
// gente em dois jogos no mesmo horário é o Conferir grade, como na troca.
public static class HorarioNaMao
{
    // Null = pode. Texto = o motivo, na língua de quem organiza.
    public static string? MotivoParaNaoDefinir(Partida? jogo, int torneioId, DateTime? horario,
        IEnumerable<Partida> doTorneio)
    {
        if (jogo == null) return "Não encontrei o jogo.";
        if (jogo.TorneioId != torneioId) return "O jogo precisa ser deste torneio.";

        // Jogo em quadra ou já jogado tem placar acontecendo — o horário dele é história.
        if (jogo.Status != "Agendada")
            return $"O jogo {jogo.Codigo} já começou ou terminou — só se marca hora de jogo agendado.";

        if (horario == null) return "Escolha um horário.";

        // Duas quadras iguais no mesmo horário é a única coisa que a grade não pode produzir.
        // A troca ⇄ resolve isso trocando com o dono; uma hora digitada não tem com quem
        // trocar — recusa e diz o caminho. No "por ordem" a quadra é nula e isto não entra.
        if (!string.IsNullOrWhiteSpace(jogo.NomeQuadra))
        {
            var ocupante = doTorneio.FirstOrDefault(p => p.Id != jogo.Id
                && p.Status != "Finalizada"
                && p.HorarioPrevisto == horario
                && string.Equals(p.NomeQuadra?.Trim(), jogo.NomeQuadra.Trim(), StringComparison.OrdinalIgnoreCase));
            if (ocupante != null)
                return $"A {jogo.NomeQuadra} já tem o jogo {ocupante.Codigo} às {horario:dd/MM HH:mm} — " +
                       "mude a quadra antes, ou use a troca ⇄.";
        }

        return null;
    }

    // O CLUBE ACOMPANHA O HORÁRIO (🗣️ *"o clube é pelo horário"*, PR #120). Se o clube em que o
    // jogo está tem alguma quadra aberta na hora nova, fica; se não tem (o Radar só abre sábado
    // de manhã, e o jogo foi pra sábado à noite), o jogo volta pro clube principal do torneio.
    // Null = fica onde está — inclusive sem carimbo, em torneio de uma sede só.
    public static int? ClubeParaOHorario(Partida jogo, DateTime horario, Torneio torneio, IReadOnlyCollection<Quadra> quadras)
    {
        if (quadras.Count == 0) return null;

        int atual = jogo.ClubeId ?? torneio.ClubeId;
        bool atualAberto = quadras.Any(q => (q.ClubeId ?? torneio.ClubeId) == atual && Aberta(q, horario));
        return atualAberto || atual == torneio.ClubeId ? null : torneio.ClubeId;
    }

    // Quadra sem janela abre o torneio inteiro; com janela, o "até" é o último INÍCIO de jogo
    // (a convenção do planejador).
    private static bool Aberta(Quadra quadra, DateTime horario) =>
        quadra.DisponivelDe == null || quadra.DisponivelAte == null
        || (quadra.DisponivelDe <= horario && horario <= quadra.DisponivelAte);

    public static void Definir(Partida jogo, DateTime horario, int? clubeNovo)
    {
        jogo.HorarioPrevisto = horario;
        if (clubeNovo != null) jogo.ClubeId = clubeNovo;
    }
}
