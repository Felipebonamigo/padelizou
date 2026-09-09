using Padelizou.Models;

namespace Padelizou.Services;

// Quem NÃO entra no sorteio das chaves. Desde 09/09/2026, uma razão só: não ter vaga.
//
// A regra sempre existiu dentro do GerarChaves, só que em silêncio — o organizador sorteava
// sem saber que ia deixar gente na porta. Mora aqui pra ter UM dono: a tela avisa antes de
// sortear e o sorteio filtra, os dois lendo a mesma linha.
//
// ⚠️ SEM PARCEIRO DEIXOU DE FICAR DE FORA (09/09/2026). 🗣️ Felipe: "tem q manter o Paulo, ele
// vai colocar o parceiro dele depois". Quem se inscreve sozinho entra na chave ocupando a vaga
// dele, com a segunda posição em aberto; o segundo nome entra até a dupla ter o primeiro jogo
// com placar. Se nunca entrar, aquela dupla leva W.O. — risco assumido, e por isso
// `ComVagaEmAberto` existe: o organizador precisa ver quantas são ANTES de apertar o botão.
//
// ⚠️ ESTA RÉGUA JÁ RESPONDEU DUAS PERGUNTAS AO MESMO TEMPO — "entra no sorteio?" e "conta pro
// ranking?" — e essa é a coisa mais importante a se saber aqui. A segunda mudou de casa pra
// Services/InscricaoQueConta, com a semântica intacta. Se as duas voltarem a parecer a mesma
// coisa, leia InscricaoQueContaTests antes de juntá-las: era ponto de ranking pago a quem não
// jogou, peso de categoria inflado e efeito retroativo sobre a história toda.
public static class ForaDoSorteio
{
    // Time (categoria de times) nunca fica de fora por "estar sem parceiro": ele não TEM
    // parceiro — Jogador2Id nulo é a construção normal dele, não uma pendência.
    public static bool FicaDeFora(Dupla dupla) =>
        !dupla.EhTime && dupla.EmListaDeEspera;

    public static List<Dupla> Listar(IEnumerable<Dupla> duplas) =>
        duplas.Where(FicaDeFora).ToList();

    // Quem ENTRA no sorteio com a segunda vaga em aberto — o aviso que o organizador precisa
    // ler antes de sortear, porque cada uma destas é um W.O. provável se o parceiro não
    // aparecer até o primeiro jogo.
    //
    // Quem está na espera não aparece aqui: ela não entra no sorteio de jeito nenhum, e
    // listá-la mandaria o organizador atrás de um parceiro que não resolveria nada.
    public static List<Dupla> ComVagaEmAberto(IEnumerable<Dupla> duplas) =>
        duplas.Where(d => !d.EhTime && !d.Completa && !d.EmListaDeEspera).ToList();

    // Por que essa dupla ficou de fora. Sobrou uma razão só — e ela é do organizador, não do
    // jogador: chamar da lista de espera é ele quem faz.
    public static string Motivo(Dupla dupla) => "na lista de espera";
}
