using Padelizou.Models;

namespace Padelizou.Services;

// Quem venceu uma partida — respondido em UM lugar só.
//
// ⚠️ Existiam DUAS respostas pra mesma pergunta, e elas discordavam:
//
//   • FinalizarPartida comparava `GamesDupla1 > GamesDupla2` com os campos NULÁVEIS. Em C#,
//     `4 > null` é **false** — então um jogo 4 x (em branco) dava a vitória à dupla 2, que
//     não marcou nada. Não por regra nenhuma: por acidente da comparação.
//   • ClassificacaoDeGrupos lia `?? 0`, via 4 x 0 e dava a vitória à dupla 1.
//
// Aconteceu no Interno de 05/08/2026, jogo 338 da 6ª Feminina: o sistema registrou uma
// vencedora e classificou a OUTRA. A dupla que o placar dizia ter vencido ficou de fora do
// mata-mata; a que perdeu foi até a semifinal. Duas contas pra mesma pergunta sempre acabam
// assim — só varia quanto tempo leva.
public static class QuemVenceu
{
    // Null = ninguém venceu ainda (empate, ou jogo sem placar). Quem chama decide o que fazer
    // com isso; o que não pode é inventar um vencedor.
    public static int? Da(Partida partida) => PorSets(partida) ?? PorGames(partida);

    // Separados porque quem edita SÓ GAMES precisa saber se o set guardado contradiz o placar
    // novo — set velho decidindo jogo corrigido foi o que deixou uma final com campeão errado.
    public static int? PorSets(Partida partida)
    {
        int sets1 = partida.SetsDupla1 ?? 0, sets2 = partida.SetsDupla2 ?? 0;
        return sets1 == sets2 ? null : sets1 > sets2 ? partida.Dupla1Id : partida.Dupla2Id;
    }

    public static int? PorGames(Partida partida)
    {
        int games1 = partida.GamesDupla1 ?? 0, games2 = partida.GamesDupla2 ?? 0;
        return games1 == games2 ? null : games1 > games2 ? partida.Dupla1Id : partida.Dupla2Id;
    }

    // Dá pra encerrar? Só quando o placar aponta alguém. Finalizar 0 x 0 gravava um "vencedor"
    // tirado do desempate silencioso da comparação — e era assim que um jogo sem placar saía
    // com campeão. A Final do Mata-Mata Geral (jogo 416) foi encerrada 0 x 0 e só depois teve
    // o placar corrigido; o vencedor de 0 x 0 ficou.
    public static string? MotivoParaNaoFinalizar(Partida partida)
    {
        if (partida.GamesDupla1 == null && partida.GamesDupla2 == null)
            return "Marque o placar antes de finalizar — sem games não dá pra dizer quem venceu.";

        return Da(partida) == null
            ? $"O placar está empatado ({partida.GamesDupla1 ?? 0} x {partida.GamesDupla2 ?? 0}) — " +
              "no padel alguém tem que fechar o jogo. Ajuste o placar e finalize."
            : null;
    }
}
