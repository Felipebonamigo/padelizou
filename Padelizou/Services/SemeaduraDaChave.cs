using Padelizou.Models;

namespace Padelizou.Services;

// A ordem em que as duplas entram no chaveamento — é ela que define os cabeças de chave, quem
// cai no grupo de 2 quando o total não fecha em trincas, e quem encontra quem no ziguezague.
//
// 🗣️ Pedido do Felipe, 07/09/2026: "quando não tiver ranking, por exemplo esse primeiro, é sem
// ranking, faz totalmente aleatório".
//
// 🕳️ O QUE ACONTECIA, medido no banco de produção: dos 114 inscritos no 2ª Etapa ER PADEL TOUR,
// ZERO tinham histórico que conta no ranking — `DuplaContaNoRanking` exclui Americano, e os
// dois únicos torneios finalizados da produção são os dois Americanos das Gurias. Com todos em
// 0 ponto, `OrderByDescending` é ESTÁVEL e devolve a ordem em que as duplas vieram do banco,
// que é aproximadamente a de inscrição. Os cabeças de chave saíam por ordem de chegada.
//
// ⚠️ É o MESMO sintoma que um comentário do `GerarChaves` diz ter consertado ao trocar o campo
// morto `Jogador.PontuacaoGlobal` por pontos de verdade. Ele voltou pela porta dos DADOS, não
// pela do código: o cálculo está certo, só não há campanha nenhuma pra medir ainda.
//
// ⚠️ E a correção é DESEMPATE ALEATÓRIO, não um ramo "se ninguém tem ponto, sorteia". O ramo
// resolveria só o caso extremo; o desempate resolve ele E o caso mais sutil de um torneio COM
// ranking, onde duas duplas empatadas em pontos também saíam por ordem de inscrição. Menos
// código e mais casos cobertos.
public static class SemeaduraDaChave
{
    public static List<Dupla> Ordenar(
        IEnumerable<Dupla> duplas, IReadOnlyDictionary<int, int> pontosPorJogador, Random sorteio)
    {
        // O sorteio é materializado ANTES da ordenação, e isso não é estilo: `OrderBy` com uma
        // chave que muda a cada leitura é comparação instável — o resultado depende de quantas
        // vezes o algoritmo de ordenação encostou em cada item. Um número por dupla, tirado uma
        // vez só, é o que faz o embaralhamento ser embaralhamento.
        var numeroDaSorte = duplas.ToDictionary(d => d.Id, _ => sorteio.Next());

        return duplas
            .OrderByDescending(d => PontosDa(d, pontosPorJogador))
            .ThenBy(d => numeroDaSorte[d.Id])
            .ToList();
    }

    // A soma dos dois, que é como o GerarChaves sempre mediu uma dupla: dois medianos podem
    // valer mais que um forte com um estreante.
    //
    // Quem nunca jogou não aparece no dicionário — `ObterPontosPorJogadorAsync` só devolve quem
    // tem histórico —, e vale zero. É o caso de QUASE TODO MUNDO hoje.
    private static int PontosDa(Dupla dupla, IReadOnlyDictionary<int, int> pontos)
    {
        int total = pontos.GetValueOrDefault(dupla.Jogador1Id);
        if (dupla.Jogador2Id is { } segundo) total += pontos.GetValueOrDefault(segundo);
        return total;
    }
}
