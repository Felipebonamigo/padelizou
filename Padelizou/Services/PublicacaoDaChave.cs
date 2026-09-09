using Padelizou.Models;

namespace Padelizou.Services;

// O SORTEIO PUBLICA A CHAVE NA HORA, OU ELA ESPERA APROVAÇÃO?
//
// 🗣️ Felipe, 09/09/2026: "coloque um aviso, que clicando em sortear agora, nao publica a chave,
// fica apenas visivel para o organizador e adm".
//
// ⚠️ ISSO SÓ É VERDADE NO FORMATO PADRÃO, e foi conferido antes de virar texto na tela:
//   • Padrão — `GerarChaves` para em `AprovacaoDeChaves.Pendente`, e só quem organiza enxerga
//     (`ViewBag.PodeAprovarChaves`, a mesma régua de `EhOrganizadorAsync`, que inclui os admins).
//     O aviso "as chaves saíram" só sai pros jogadores no `AprovarChaves`.
//   • Americano (individual e de duplas) — `GerarRodadasAmericano` vai DIRETO pra "Fase de
//     Grupos", que já é público. Está escrito em TorneiosController.Americano, no comentário do
//     `DesfazerRodadasAmericano`.
//
// Um aviso incondicional mentiria justamente no caso mais perigoso: o organizador de um
// Americano clicaria achando que é rascunho e o rodízio sairia pros jogadores na hora. Daí esta
// classe existir em vez de duas frases soltas na view — a régua e o texto andam juntos, e o
// teste que trava um trava o outro.
public static class PublicacaoDaChave
{
    // O sorteio deste torneio já sai público?
    public static bool SaiPublicaNaHora(Torneio torneio) =>
        FormatoDoTorneio.EhAmericano(torneio.Formato);

    // O que a tela diz ANTES de o organizador clicar. Duas frases diferentes de propósito: um
    // texto genérico o bastante pra servir aos dois formatos não avisaria nada.
    public static string Aviso(Torneio torneio) => SaiPublicaNaHora(torneio)
        ? "Atenção: neste formato o sorteio já sai PÚBLICO — as rodadas aparecem pra todo mundo "
          + "assim que você sortear, sem passar por aprovação."
        : "Sortear não publica a chave. Ela fica visível só pra você e pros outros organizadores "
          + "(e admins) até alguém aprovar — o jogador não recebe aviso nenhum antes disso.";
}
