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
    // A trava só existe onde há taxa a cobrar: torneio gratuito não deve nada, e as formas
    // online cobram a comissão dentro de cada inscrição, muito antes das chaves.
    public static bool SeAplica(Torneio torneio) =>
        torneio.FormaPagamento == "Externo" && torneio.PrecoInscricao > 0;

    public static bool ChavesLiberadas(Torneio torneio) =>
        !SeAplica(torneio)
        || torneio.TaxaExternoPagaEm != null
        || torneio.TaxaExternoNegociadaEm != null;

    // Base da taxa: gente que existe na lista na hora do fechamento. Dupla completa são 2
    // pessoas, dupla ainda sem parceiro é 1 (cobrar por alguém que ainda não foi definido
    // seria cobrar por fantasma). Lista de espera fica fora — ela não joga e o organizador
    // não recebeu dela. TIME também fica fora: ele é cadastrado pelo organizador, não paga
    // inscrição pelo sistema — mesma cortesia dos impedimentos, errar pra menos.
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
