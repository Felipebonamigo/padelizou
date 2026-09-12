using Padelizou.Models;

namespace Padelizou.Services;

// A ABA "CHAVES E GRUPOS" EXISTE PRA QUEM ESTÁ OLHANDO? — uma pergunta, uma régua.
//
// 🗣️ Deivid Santos, 10/09/2026, com o print de /Torneios/Jogos aberto: *"Eu to nessa tela"* …
// *"Ta ruim de achar o chaveamento"*. A página dedicada de jogos é o destino de quatro avisos
// diferentes (ver Torneio.ChavesAvisadasEm e QuadraAtrasadaBackgroundService): quem toca no
// push cai nela, e ela não tinha saída nenhuma — nem pro torneio, nem pras chaves. As abas mãe
// só existem no /Torneios/Details, e isso não está escrito em lugar nenhum da tela.
//
// ⚠️ A RÉGUA MORA AQUI, e não em cada view, porque agora são DUAS telas fazendo a mesma
// pergunta: o `@if` da aba no Details e o botão da lista de jogos. Duas cópias da lista de
// status é como o botão passaria a prometer uma aba que o Details não desenha.
public static class AbaDeChavesEGrupos
{
    // Chave sorteada e pública. "Chaves em Aprovação" é o caso de fronteira: os jogos já
    // existem no banco, mas pro jogador é como se o sorteio não tivesse saído — só quem
    // aprova enxerga a aba, e por isso só pra ele o caminho pode ser oferecido.
    //
    // ⚠️ A LISTA DE STATUS NÃO MORA AQUI: o primeiro termo é `AprovacaoDeChaves.ChavePublicada`,
    // a mesma régua que decide onde ficam as ferramentas do organizador na página do torneio
    // (10/09/2026). As duas nasceram no mesmo dia com a lista escrita duas vezes — cópias que
    // concordam hoje divergem na primeira mudança, e aí este botão promete uma aba que o
    // Details não desenha. O que é SÓ daqui é o segundo termo.
    public static bool Existe(Torneio torneio, bool podeAprovarChaves) =>
        AprovacaoDeChaves.ChavePublicada(torneio)
        || (torneio.Status == AprovacaoDeChaves.Pendente && podeAprovarChaves);
}
