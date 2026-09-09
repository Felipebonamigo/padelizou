using Padelizou.Models;

namespace Padelizou.Services;

// A condição do torneio "por fora" (5%): as chaves só saem depois que a taxa do Padelizou
// foi paga — ou que o Padelizou registrou uma negociação com o organizador.
//
// Existe porque no Externo o dinheiro nunca passa pelo sistema: não há split, não há webhook
// de inscrição, não há NADA que cobre a taxa sozinho. A única moeda de troca que o sistema
// tem é o sorteio das chaves, que é o momento em que o organizador mais precisa dele. A
// condição está escrita na própria opção desde a criação do torneio — aqui ela vira trava.
public static class TaxaDoTorneioExterno
{
    // A trava só existe onde há taxa a cobrar: torneio gratuito não deve nada, as formas
    // online cobram a comissão dentro de cada inscrição (muito antes das chaves), e o
    // Americano que não compra o Ranking Americano é isento de taxa em qualquer forma —
    // ver CobrancaDoTorneio.IsentoDeTaxa.
    public static bool SeAplica(Torneio torneio) =>
        torneio.FormaPagamento == "Externo" && torneio.PrecoInscricao > 0
        && !CobrancaDoTorneio.IsentoDeTaxa(torneio);

    public static bool ChavesLiberadas(Torneio torneio) =>
        !SeAplica(torneio)
        || torneio.TaxaExternoPagaEm != null
        || torneio.TaxaExternoNegociadaEm != null
        // O fiado (08/09/2026): o organizador destrava sozinho e o torneio passa a DEVER.
        // A trava não sumiu — virou dívida registrada, que aparece no /Admin/Financeiro.
        // Sem este terceiro caso, a única saída pra quem ia pagar depois era esperar um admin.
        || torneio.TaxaExternoAdiadaEm != null;

    // Este torneio está DEVENDO a taxa: o organizador pegou fiado e ninguém deu baixa ainda.
    //
    // ⚠️ Não é o mesmo que "chave travada". Torneio que nunca chegou a adiar também não pagou,
    // mas está parado ANTES do sorteio — não deve nada, porque não levou nada. Devedor é quem
    // já ficou com as chaves.
    //
    // ⚠️ E não é o mesmo que "negociado". Negociar é o Padelizou abrindo mão; aí não há o que
    // cobrar. Por isso os dois carimbos encerram a dívida e o de adiar, sozinho, a mantém.
    public static bool EstaDevendo(Torneio torneio) =>
        SeAplica(torneio)
        && torneio.TaxaExternoAdiadaEm != null
        && torneio.TaxaExternoPagaEm == null
        && torneio.TaxaExternoNegociadaEm == null;

    // O FIADO AINDA ESTÁ EM ABERTO — ou seja, dá pra voltar atrás nele.
    //
    // 🗣️ Pedido do Felipe (09/09/2026), num print do painel com o torneio de volta em
    // "Inscrições Abertas": *"aqui está dizendo que já sorteou a chave, mas a gente voltou,
    // deveria ter sumido aquela mensagem"*. Quem devolve as chaves devolve a dívida.
    //
    // ⚠️ PARECE `EstaDevendo` E NÃO É: falta o `SeAplica` de propósito. Aquela pergunta é "há o
    // que cobrar deste torneio HOJE?"; esta é "existe uma promessa em aberto pra desfazer?". As
    // duas divergem no torneio que carimbou o fiado e depois virou gratuito: `SeAplica` responde
    // não, `EstaDevendo` responde não junto — e o carimbo ficaria pra trás, esperando o preço
    // voltar a subir pra destravar a chave de graça.
    //
    // ⚠️ PAGO e NEGOCIADO ficam de fora, e é a linha inteira desta regra: pagar é dinheiro que
    // entrou, negociar é o Padelizou tendo aberto mão. Só a PROMESSA volta atrás.
    public static bool FiadoEmAberto(Torneio torneio) =>
        torneio.TaxaExternoAdiadaEm != null
        && torneio.TaxaExternoPagaEm == null
        && torneio.TaxaExternoNegociadaEm == null;

    // AINDA DÁ PRA REGISTRAR A CORTESIA (a "negociação")?
    //
    // 🗣️ Pergunta do Felipe, 09/09/2026, olhando o torneio do Er: "aonde eu coloco q foi
    // cortesia?". Não tinha onde — o formulário do admin vivia atrás de "a chave NÃO está
    // liberada", e isso estava certo enquanto só havia dois jeitos de liberar (pago ou
    // negociado). O FIADO de 08/09 abriu um terceiro, e `ChavesLiberadas` passou a responder
    // `true` pra ele — sumindo com o caminho da cortesia justamente de quem pegou fiado. O
    // torneio ficava DEVENDO pra sempre, e a única saída era pagar mesmo com o Padelizou já
    // tendo aberto mão.
    //
    // ⚠️ A PERGUNTA CERTA NÃO É "a chave está liberada?", É "a taxa ainda está em aberto?".
    // As duas coincidiam antes do fiado; agora não coincidem mais.
    public static bool PodeRegistrarNegociacao(Torneio torneio) =>
        SeAplica(torneio)
        && torneio.TaxaExternoPagaEm == null
        && torneio.TaxaExternoNegociadaEm == null;

    // Base da taxa: gente que existe na lista na hora do fechamento. Dupla completa são 2
    // pessoas, dupla ainda sem parceiro é 1 (cobrar por alguém que ainda não foi definido
    // seria cobrar por fantasma). Lista de espera fica fora — ela não joga e o organizador
    // não recebeu dela. TIME também fica fora: ele é cadastrado pelo organizador, não paga
    // inscrição pelo sistema — mesma cortesia dos impedimentos, errar pra menos.
    //
    // ⚠️ A CHAVE DIRETA também fica fora, mas o filtro dela NÃO está aqui: é a categoria que
    // carrega a flag, e quem chama nem sempre traz a Categoria carregada — uma propriedade
    // calculada devolveria `false` calada e cobraria a mais. Por isso ela é filtrada na
    // QUERY de quem chama (TorneiosController.TaxaExterno e .DiaDoJogo). A razão é a mesma
    // do time: quem joga a chave direta já foi contado na categoria em que se inscreveu.
    public static int PessoasInscritas(IEnumerable<Dupla> duplas, IEnumerable<InscricaoAmericana> americanas)
    {
        int pessoas = 0;
        foreach (var dupla in duplas)
        {
            if (dupla.EmListaDeEspera || dupla.EhTime) continue;
            pessoas += dupla.Jogador2Id.HasValue ? 2 : 1;
        }
        foreach (var inscricao in americanas)
        {
            if (!inscricao.EmListaDeEspera) pessoas++;
        }
        return pessoas;
    }

    // Impedimentos ficam de fora da base de propósito: são receita acessória e opcional do
    // organizador — errar pra menos aqui é a cortesia certa.
    public static decimal Valor(int pessoasInscritas, decimal precoPorPessoa, decimal percentual) =>
        Math.Round(pessoasInscritas * precoPorPessoa * percentual / 100m, 2);
}
