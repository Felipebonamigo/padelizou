using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// "Destas pessoas, quais JÁ têm inscrição neste torneio?" — a pergunta que decide quem paga o
// preço da segunda inscrição (ver Services/PrecoDaInscricao).
//
// ⚠️ Olha as DUAS tabelas de inscrição, porque cada formato grava na sua: `Dupla` no Oficial e
// no Americano de Duplas, `InscricaoAmericana` no Americano individual. Perguntar só à
// primeira daria desconto indevido a quem já está num Americano individual — e essa metade
// esquecida já custou caro duas vezes neste repo (o Ranking Americano de duplas e a trava da
// forma de recebimento).
//
// Fica num lugar só porque a pergunta é feita em três: inscrição de dupla, inscrição de
// americano e o "pagar agora". Respostas diferentes = preços diferentes pra mesma pessoa.
public static class QuemJaEstaNoTorneio
{
    // `ignorarDuplaIds` são as inscrições que estão SAINDO no mesmo movimento — as
    // inscrições sozinhas que uma dupla que fecha absorve (ver Services/InscricaoRepetida).
    // Sem isto, quem junta a própria inscrição solo é contado como "já está no torneio" por
    // causa de uma linha que vai deixar de existir, e paga o preço de SEGUNDA inscrição numa
    // categoria em que, no fim, ele está uma vez só.
    public static async Task<HashSet<int>> DentreAsync(
        DbPadelContext context, int torneioId, IEnumerable<int> jogadorIds,
        IEnumerable<int>? ignorarDuplaIds = null)
    {
        var procurados = jogadorIds.Where(id => id > 0).Distinct().ToList();
        if (procurados.Count == 0) return new HashSet<int>();

        var ignoradas = ignorarDuplaIds?.Distinct().ToList() ?? new List<int>();

        // ⚠️ Lista de espera CONTA como já estar no torneio: a pessoa vai ser chamada, e
        // cobrar dela o preço cheio na categoria seguinte só porque a fila ainda não andou
        // seria cobrar pela ordem do acaso.
        //
        // TIME fica de fora (`NomeTime == null`): ele é cadastrado pelo organizador e não paga
        // inscrição pelo sistema — a mesma exceção de TaxaDoTorneioExterno.
        // ⚠️ As duas colunas de jogador se consultam SEPARADAS. Juntá-las num
        // `SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })` parece mais limpo e o EF
        // não traduz: estoura em tempo de execução com "could not be translated" — pego pelos
        // testes, não pelo compilador.
        var comoJogador1 = await context.Duplas
            .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null
                     && !ignoradas.Contains(d.Id)
                     && procurados.Contains(d.Jogador1Id))
            .Select(d => d.Jogador1Id)
            .Distinct()
            .ToListAsync();

        var comoJogador2 = await context.Duplas
            .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null
                     && !ignoradas.Contains(d.Id)
                     && d.Jogador2Id != null && procurados.Contains(d.Jogador2Id.Value))
            .Select(d => d.Jogador2Id!.Value)
            .Distinct()
            .ToListAsync();

        var deDuplas = comoJogador1.Concat(comoJogador2);

        var deAmericanas = await context.InscricoesAmericanas
            .Where(i => i.Categoria.TorneioId == torneioId && procurados.Contains(i.JogadorId))
            .Select(i => i.JogadorId)
            .Distinct()
            .ToListAsync();

        return deDuplas.Concat(deAmericanas).ToHashSet();
    }

    // Atalho pra uma pessoa só.
    public static async Task<bool> EstaAsync(DbPadelContext context, int torneioId, int jogadorId,
        IEnumerable<int>? ignorarDuplaIds = null) =>
        (await DentreAsync(context, torneioId, new[] { jogadorId }, ignorarDuplaIds)).Contains(jogadorId);
}
