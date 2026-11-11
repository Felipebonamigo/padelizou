using Padelizou.Models;

namespace Padelizou.Services;

// A ORDEM EM QUE AS DUPLAS ENTRAM NO SORTEIO DE GRUPOS — quem é cabeça de chave.
//
// 🗣️ Felipe, 11/11/2026: "permita antes do sorteio, escolher os cabeças de chave caso seja uma
// escolha do organizador".
//
// 1. Quem o organizador escolheu (`Dupla.CabecaDeChave`), na ordem dele: o 1 abre o Grupo A.
// 2. O resto pelo ranking (soma dos pontos da dupla), como sempre foi.
// 3. Empate de ranking: sorteio (`Guid`). ⚠️ Sem ele não há sorteio nenhum — ver o comentário
//    de 09/09/2026 em TorneiosController.GerarChaves.
//
// Ninguém escolhido = exatamente a ordem de antes.
public static class CabecasDeChave
{
    public static List<Dupla> Ordenar(IEnumerable<Dupla> duplas, Func<Dupla, int> pontos) =>
        duplas
            .OrderBy(d => d.CabecaDeChave is null)
            .ThenBy(d => d.CabecaDeChave ?? 0)
            .ThenByDescending(pontos)
            .ThenBy(_ => Guid.NewGuid())
            .ToList();

    // Até onde o organizador pode numerar: uma posição por dupla que vai ao sorteio.
    public static int PosicaoMaxima(int duplasNoSorteio) => duplasNoSorteio;
}
