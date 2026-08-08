namespace Padelizou.Services;

// A FILA DO AMERICANO: em que ordem os jogos entram em quadra.
//
// Regra do Felipe (08/08/2026): "a próxima rodada é de quem ficou mais tempo parada" — e vale
// SÓ pro Americano, que é o formato em que o mesmo grupo joga várias rodadas seguidas.
//
// ⚠️ O sorteio monta cada grupo por inteiro, um depois do outro: primeiro as 5 rodadas do
// grupo A, depois as 5 do B. Criar os jogos nessa ordem tem dois efeitos, os dois ruins:
//
//   • O grupo B só entra em quadra quando o A tiver terminado o torneio dele. Quem está no B
//     senta e espera o torneio inteiro do vizinho.
//   • Com mais de uma quadra, a grade de horários é posicional — ela dá as duas primeiras
//     faixas pros dois primeiros jogos da lista, que nessa ordem são as rodadas 1 e 2 DO
//     MESMO GRUPO. São as mesmas pessoas marcadas em duas quadras no mesmo horário.
//
// Intercalando por rodada, a rodada 1 de todos os grupos acontece junto, depois a 2, depois a
// 3: quem acabou de jogar volta pro fim da fila e o próximo é sempre quem está parado há mais
// tempo. O NÚMERO da rodada não é reescrito — ele já vem do sorteio, e passa a ser também a
// ordem de leitura da lista.
public static class FilaDoAmericano
{
    // `rodada` é o número que o sorteio deu (1, 2, 3...); `grupo`, a posição do grupo (A = 0,
    // B = 1...). O empate dentro da mesma rodada fica na ordem em que veio — `OrderBy` do LINQ
    // é estável —, que é a ordem das quadras já sorteada.
    public static IEnumerable<T> NaOrdemDeJogar<T>(
        IEnumerable<T> jogos, Func<T, int> rodada, Func<T, int> grupo) =>
        jogos.OrderBy(rodada).ThenBy(grupo);
}
