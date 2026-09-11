using Padelizou.Models;

namespace Padelizou.Services;

// QUEM ESTÁ SACANDO — as duas regras da bolinha, num lugar só.
//
// Elas nasceram espalhadas e foi isso que apagou a bolinha do site inteiro: o único lugar que
// gravava `DuplaSacandoId` era o POST do Controle de Partida, e o campo simplesmente nunca era
// preenchido no caminho que o organizador usa de verdade (o play, o −/+ do card, a Mesa).
// Feature no ar desde 05/08/2026, invisível desde 05/08/2026.
//
// Agora são três chamadores da largada (ColocarNoAr, o <select> de status do ControlePlacar e
// o ReabrirPartida) e dois da validação (ControlePlacar e TrocarSaque). Cinco cópias da mesma
// frase é como a autorização de organizador virou três réguas que precisam andar juntas.
public static class SaqueDoJogo
{
    // JOGO QUE ENTRA EM QUADRA SAI DAQUI COM ALGUÉM SACANDO.
    //
    // 🗣️ Felipe, 11/09/2026: *"tanto faz em quem começar a bolinha, mas tem q ter em alguem"*.
    // Qual das duas é indiferente de propósito — quem saca primeiro é sorteio na quadra, e o
    // servidor não tem como saber. O que não pode é o card nascer sem bolinha nenhuma, que é
    // o estado em que ele viveu até hoje.
    //
    // ⚠️ `??=`: saque que JÁ tinha dono não é tocado. Jogo que voltou pra agendado e foi
    // chamado de novo mantém a bolinha onde o organizador pôs.
    public static void DefinirNaLargada(Partida jogo) => jogo.DuplaSacandoId ??= jogo.Dupla1Id;

    // Quem saca só pode ser uma das duas duplas DESTE jogo. Sem esta checagem, um POST montado
    // à mão apontaria a bolinha pra uma dupla de outra partida, e a tela mostraria em quadra
    // um nome que não está em quadra.
    public static bool EhDesteJogo(Partida jogo, int? duplaId) =>
        duplaId == jogo.Dupla1Id || duplaId == jogo.Dupla2Id;
}
