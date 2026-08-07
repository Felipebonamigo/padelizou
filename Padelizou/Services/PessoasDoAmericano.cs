using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quantas pessoas jogaram um Americano. É a base de DUAS coisas: o PESO do ponto no Ranking
// Americano e o PREÇO de R$ 5 por pessoa que o organizador paga pra valer ponto.
//
// ⚠️ Os dois formatos guardam a inscrição em lugares DIFERENTES. O individual grava uma linha
// por PESSOA em `InscricaoAmericana`; o de duplas grava uma linha por PAR em `Dupla`, porque
// ele herdou o caminho de inscrição do Padrão. Uma contagem que olhasse só a primeira devolve
// ZERO pro Americano de Duplas — e zero, abaixo do piso de 8, significa "não pontua e não se
// cobra". O formato inteiro sumia dos dois lugares sem erro nenhum aparecer.
//
// Mora num lugar só porque são dois leitores fazendo a MESMA pergunta: o ranking e o acerto do
// admin. Contagens separadas divergiriam um dia, e aí a gente cobraria por um tamanho e
// pontuaria por outro.
public static class PessoasDoAmericano
{
    // Pessoas por CATEGORIA — é a unidade do peso, porque a colocação também é por categoria.
    public static async Task<Dictionary<int, int>> PorCategoriaAsync(
        DbPadelContext context, IReadOnlyCollection<int> torneioIds)
    {
        var total = new Dictionary<int, int>();
        if (torneioIds.Count == 0) return total;

        // ⚠️ CADA FORMATO OLHA A SUA TABELA, e nunca as duas somadas. No Americano INDIVIDUAL a
        // tabela `Dupla` também tem linhas — uma por PARCERIA DE RODADA, e não por inscrição
        // (um Americano de 8 chega a ter dezenas). Somar os dois lados contaria a mesma pessoa
        // uma vez por rodada que ela jogou, e o peso do ponto explodiria em silêncio.
        var formatos = await context.Torneios
            .Where(t => torneioIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Formato })
            .ToListAsync();

        var idsIndividuais = formatos
            .Where(t => t.Formato == FormatoDoTorneio.Americano).Select(t => t.Id).ToList();
        var idsDeDuplas = formatos
            .Where(t => FormatoDoTorneio.EhAmericanoDeDuplas(t.Formato)).Select(t => t.Id).ToList();

        // Lista de espera fica de fora nos dois formatos: quem esperou não jogou, e o preço
        // também não a cobra.
        if (idsIndividuais.Count > 0)
        {
            var individuais = await context.InscricoesAmericanas
                .Where(i => idsIndividuais.Contains(i.Categoria.TorneioId) && !i.EmListaDeEspera)
                .GroupBy(i => i.CategoriaId)
                .Select(g => new { CategoriaId = g.Key, Quantos = g.Count() })
                .ToListAsync();

            foreach (var linha in individuais) total[linha.CategoriaId] = linha.Quantos;
        }

        if (idsDeDuplas.Count > 0)
        {
            // Cada dupla são DUAS pessoas — o piso e o preço se contam por PESSOA, não por
            // inscrição. ⚠️ Menos a que ainda está sem parceiro (`Jogador2Id` nulo): ela já
            // ocupa vaga e já pagou, mas é UMA pessoa, e contá-la como duas inflaria o piso e
            // o preço com gente que não existe.
            var deDuplas = await context.Duplas
                .Where(d => idsDeDuplas.Contains(d.Categoria.TorneioId) && !d.EmListaDeEspera)
                .GroupBy(d => d.CategoriaId)
                .Select(g => new { CategoriaId = g.Key, Quantos = g.Sum(d => d.Jogador2Id != null ? 2 : 1) })
                .ToListAsync();

            foreach (var linha in deDuplas) total[linha.CategoriaId] = linha.Quantos;
        }

        return total;
    }

    // Pessoas por TORNEIO — é a unidade do preço, que se acerta torneio a torneio.
    public static async Task<Dictionary<int, int>> PorTorneioAsync(
        DbPadelContext context, IReadOnlyCollection<int> torneioIds)
    {
        var porCategoria = await PorCategoriaAsync(context, torneioIds);
        if (porCategoria.Count == 0) return new Dictionary<int, int>();

        var torneioDaCategoria = await context.Categorias
            .Where(c => torneioIds.Contains(c.TorneioId))
            .Select(c => new { c.Id, c.TorneioId })
            .ToListAsync();

        var total = new Dictionary<int, int>();
        foreach (var categoria in torneioDaCategoria)
        {
            if (!porCategoria.TryGetValue(categoria.Id, out var pessoas)) continue;

            total[categoria.TorneioId] = total.TryGetValue(categoria.TorneioId, out var jaTinha)
                ? jaTinha + pessoas
                : pessoas;
        }

        return total;
    }
}
