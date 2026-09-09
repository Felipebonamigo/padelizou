using Padelizou.Models;

namespace Padelizou.Services;

// A inscrição virou participação de verdade? É o que o RANKING, o peso da categoria, o MVP e a
// enquete perguntam — e é a metade que se separou de ForaDoSorteio em 09/09/2026.
//
// Até ali uma régua só respondia duas perguntas: "entra no sorteio?" e "conta pro ranking?".
// Quando a dupla sem parceiro passou a ENTRAR no sorteio (pedido do Felipe: "tem q manter o
// Paulo, ele vai colocar o parceiro dele depois"), as duas perguntas deixaram de ter a mesma
// resposta — e mantê-las juntas teria feito três estragos calados:
//
//   1. PONTO PRA QUEM NÃO JOGOU. `Dupla.UltimaFase` nasce valendo "Grupos" e
//      `PontosDoTorneio.Pontos` paga participação × peso assim que o torneio começa, sem olhar
//      uma única Partida. Quem entrasse na chave sem parceiro e levasse W.O. levaria ponto por
//      não aparecer — o buraco que a RANKING.md fechou em 10/08/2026.
//   2. PONTO A MAIS PRO CAMPEÃO. Esta régua é também o CONTADOR do tamanho da categoria
//      (EstatisticasService), e o tamanho multiplica o ponto de todo mundo: uma categoria de
//      20 com 3 inscrições sozinhas viraria 23, e o título sairia de 250 pra 280.
//   3. RETROATIVO. A régua não tem data de corte: mexer nela reescreveria o ranking de todo
//      torneio que já teve inscrito sozinho, meses atrás.
//
// Por isso a semântica AQUI é exatamente a de sempre — dupla fechada, dentro da vaga (ou time)
// —, e é `ForaDoSorteio` que mudou. Se um dia as duas voltarem a parecer iguais, leia
// InscricaoQueContaTests antes de juntá-las.
public static class InscricaoQueConta
{
    // Time conta: ele não TEM parceiro (Jogador2Id nulo é a construção normal dele), e quem
    // joga por um time jogou de verdade.
    public static bool Vale(Dupla dupla) =>
        dupla.EhTime || (dupla.Completa && !dupla.EmListaDeEspera);

    // A MESMA régua escrita pra rodar NO BANCO.
    //
    // ⚠️ Não dá pra reusar `Vale` numa consulta: ela recebe a entidade e lê `Completa`, que é
    // propriedade calculada (`Jogador2Id != null`). O EF não traduz nenhuma das duas, e a
    // consulta ou explode, ou — pior — vira avaliação em memória depois de trazer a tabela
    // inteira. Por isso aqui as colunas estão escritas na mão.
    //
    // ⚠️ Há teste comparando as duas escritas caso a caso, e ele não é cerimônia: no par irmão
    // `ContaNoRanking`/`DuplaContaNoRanking` a cópia à mão já divergiu em silêncio, e foi assim
    // que o Americano continuou pontuando no ranking oficial. Aqui a divergência PAGA, ou
    // deixa de pagar, ponto.
    public static readonly System.Linq.Expressions.Expression<Func<Dupla, bool>> Expressao =
        d => d.NomeTime != null || (d.Jogador2Id != null && !d.EmListaDeEspera);
}
