using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Quantos jogos um torneio tem AGORA, contados no banco.
//
// Existe por causa de 23/09/2026, quando o pacote "nós registramos os resultados" voltou a ser
// cobrado por jogo: a partir dali a contagem virou dinheiro, e dinheiro contado em dois lugares
// é como um deles fica pra trás. Os dois lugares são o PEDIDO (TorneiosController.Criacao, que
// congela o número do dia) e a RESPOSTA (AdminController.RegistroResultados, que cota pelo
// número de hoje) — e eles precisam contar igual.
//
// ⚠️ Carrega as duplas em vez de contar no SQL de propósito: quem entra no sorteio é
// `ForaDoSorteio.FicaDeFora`, que olha `EhTime` — propriedade [NotMapped], derivada do
// NomeTime. Reescrever a régua dentro do `Where` pra ela virar SQL é exatamente a cópia à mão
// que já deixou o preview da grade prometendo a grade das duplas fechadas enquanto o sorteio
// fazia a de todas (09/09/2026).
public static class JogosDoTorneio
{
    // As três consultas ficam separadas pra poderem ser COMPILADAS num teste
    // (TraducaoDaContagemDeJogosTests): o InMemory da suíte não traduz nada, e as duas que
    // atravessam `Categoria.TorneioId` são navegação que o Postgres precisa virar JOIN.
    public static IQueryable<Categoria> CategoriasDos(
        DbPadelContext ctx, IReadOnlyCollection<int> torneioIds) =>
        ctx.Categorias.Where(c => torneioIds.Contains(c.TorneioId));

    public static IQueryable<Dupla> DuplasDos(
        DbPadelContext ctx, IReadOnlyCollection<int> torneioIds) =>
        ctx.Duplas.Where(d => torneioIds.Contains(d.Categoria.TorneioId));

    // Só o Americano individual inscreve pessoa a pessoa — nos outros formatos esta tabela
    // está vazia, e no individual é ela que diz o tamanho da categoria.
    public static IQueryable<InscricaoAmericana> AmericanasDos(
        DbPadelContext ctx, IReadOnlyCollection<int> torneioIds) =>
        ctx.InscricoesAmericanas.Where(i => torneioIds.Contains(i.Categoria.TorneioId));

    public static async Task<Dictionary<int, int>> ContarAsync(
        DbPadelContext ctx, IReadOnlyCollection<int> torneioIds)
    {
        if (torneioIds.Count == 0) return new Dictionary<int, int>();

        var formatos = await ctx.Torneios
            .Where(t => torneioIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Formato })
            .ToDictionaryAsync(t => t.Id, t => t.Formato);

        var categorias = await CategoriasDos(ctx, torneioIds)
            .Select(c => new { c.Id, c.TorneioId, c.ChaveDireta })
            .ToListAsync();

        var duplas = (await DuplasDos(ctx, torneioIds).ToListAsync())
            .ToLookup(d => d.CategoriaId);

        var americanas = (await AmericanasDos(ctx, torneioIds).ToListAsync())
            .ToLookup(i => i.CategoriaId);

        return torneioIds.Distinct().ToDictionary(id => id, id =>
        {
            var formato = formatos.GetValueOrDefault(id);

            var doTorneio = categorias.Where(c => c.TorneioId == id).Select(c => new CategoriaParaContar(
                c.ChaveDireta,
                formato == FormatoDoTorneio.Americano
                    ? americanas[c.Id].Count(i => !i.EmListaDeEspera)
                    : duplas[c.Id].Count(d => !ForaDoSorteio.FicaDeFora(d))));

            return RegistroDeResultados.JogosPrevistos(formato, doTorneio);
        });
    }

    // Um torneio só — o caminho do pedido, que nasce de dentro da tela do organizador.
    public static async Task<int> ContarAsync(DbPadelContext ctx, int torneioId) =>
        (await ContarAsync(ctx, new[] { torneioId })).GetValueOrDefault(torneioId);
}
