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
    //
    // `sedes` é o torneio em mais de um clube: com ele, a troca recusa mandar a categoria que fica
    // em casa (a 3ª, a 4ª do Er) pro slot do local alugado — a grade automática não faria isso, e
    // a troca na mão também não. Sem `sedes` (chamador antigo, torneio de uma sede), nada muda.
    public static string? MotivoParaNaoTrocar(Partida? a, Partida? b, int torneioId, SedesDoTorneio? sedes = null)
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

        if (sedes != null && sedes.MaisDeUmClube)
            return NaoPodeIrPraVagaDe(a, b, sedes) ?? NaoPodeIrPraVagaDe(b, a, sedes);

        return null;
    }

    // O clube do SLOT: o carimbo do jogo (Partida.ClubeId, que o "por ordem" guarda depois de
    // apagar a quadra) ou, sem carimbo, o clube da quadra.
    private static int? ClubeDaVaga(Partida p, SedesDoTorneio sedes) =>
        p.ClubeId ?? sedes.ClubeDaQuadra(p.NomeQuadra);

    // `quem` pode tomar a vaga de `dono`? Mesmas duas réguas de GradeDeJogos.Encaixar: categoria
    // presa a um clube (`ClubeDaCategoria`) e categoria tirada do externo (`PodeIrPraSedeExtra`).
    private static string? NaoPodeIrPraVagaDe(Partida quem, Partida dono, SedesDoTorneio sedes)
    {
        if (ClubeDaVaga(dono, sedes) is not int clube) return null;

        bool presa = sedes.ClubeDaCategoria(quem.CategoriaId) is int fixo
            ? fixo != clube
            : !sedes.PodeIrPraSedeExtra(quem.CategoriaId) && sedes.EhSedeExtra(clube);
        if (!presa) return null;

        var categoria = quem.Categoria?.Nome ?? "essa categoria";
        var nomeDoClube = sedes.NomeDoClube(clube) ?? "outro clube";
        return $"O jogo {quem.Codigo} é da {categoria}, que não joga no {nomeDoClube} — e o horário "
             + $"{dono.HorarioPrevisto:dd/MM HH:mm} do jogo {dono.Codigo} é lá. O clube é do horário, não do jogo.";
    }

    // A troca em si: horário, quadra E CLUBE andam juntos — o slot físico é o trio. 🗣️ *"quando eu
    // trocar aqui, tem q cuidar para nao trocar o clube, por que o clube é pelo horario"* (Felipe,
    // 10/09/2026). No "por ordem" a quadra é nula e o clube carimbado é tudo que diz ONDE é o jogo;
    // trocar só hora e quadra deixava o jogo com a hora do Radar e o nome do Er Padel.
    public static void Trocar(Partida a, Partida b)
    {
        (a.HorarioPrevisto, b.HorarioPrevisto) = (b.HorarioPrevisto, a.HorarioPrevisto);
        (a.NomeQuadra, b.NomeQuadra) = (b.NomeQuadra, a.NomeQuadra);
        (a.ClubeId, b.ClubeId) = (b.ClubeId, a.ClubeId);
    }
}
