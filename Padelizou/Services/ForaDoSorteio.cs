using Padelizou.Models;

namespace Padelizou.Services;

// Quem NÃO entra no sorteio das chaves.
//
// A regra sempre existiu dentro do GerarChaves (`d.Completa && !d.EmListaDeEspera`), só que em
// silêncio: o jogador que se inscreveu "ainda não tenho parceiro", pagou e esqueceu de fechar a
// dupla descobria que ficou de fora quando a chave saía — sem tempo de resolver. E o
// organizador sorteava sem saber que ia deixar gente na porta.
//
// Mora aqui pra ter UM dono: a tela avisa antes de sortear e o sorteio filtra, os dois lendo a
// mesma linha. Duas cópias divergiriam no dia em que a regra mudasse, e aí a tela prometeria
// uma coisa e o sorteio faria outra.
public static class ForaDoSorteio
{
    // Time (categoria de times) nunca fica de fora por "estar sem parceiro": ele não TEM
    // parceiro — Jogador2Id nulo é a construção normal dele, não uma pendência.
    public static bool FicaDeFora(Dupla dupla) =>
        !dupla.EhTime && (!dupla.Completa || dupla.EmListaDeEspera);

    public static List<Dupla> Listar(IEnumerable<Dupla> duplas) =>
        duplas.Where(FicaDeFora).ToList();

    // Por que essa dupla ficou de fora — o organizador precisa saber se é coisa que ele
    // resolve (chamar da espera) ou coisa que o jogador resolve (achar parceiro).
    public static string Motivo(Dupla dupla) =>
        !dupla.Completa ? "sem parceiro" : "na lista de espera";
}
