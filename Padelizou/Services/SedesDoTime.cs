using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// As sedes de um time — ler e gravar, num lugar só.
//
// Existe porque a sede deixou de ser uma coluna (`Time.ClubeId`) e virou uma lista
// (Models/TimeSede): quem grava precisa apagar o que saiu e inserir o que entrou, e essa
// dancinha copiada em três telas — o perfil do dono, o painel /Admin/Times e a criação do
// time — seria três chances de fazer diferente.
public static class SedesDoTime
{
    // Quantas sedes cabem. Não é limitação técnica: é para a tela do time não virar uma lista
    // de vinte clubes onde ninguém acha onde o time joga de verdade. Um time que precise de
    // mais do que isto é caso pra conversar, não pra formulário.
    public const int MaximoDeSedes = 6;

    public static Task<List<Clube>> DoTimeAsync(DbPadelContext context, int timeId) =>
        context.TimeSedes
            .Where(s => s.TimeId == timeId)
            .Select(s => s.Clube)
            .OrderBy(c => c.Nome)
            .ToListAsync();

    // As sedes de VÁRIOS times de uma vez — o que a vitrine precisa pra não fazer uma consulta
    // por cartão na tela.
    public static async Task<Dictionary<int, List<string>>> PorTimeAsync(
        DbPadelContext context, IEnumerable<int> timeIds)
    {
        var ids = timeIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, List<string>>();

        var linhas = await context.TimeSedes
            .Where(s => ids.Contains(s.TimeId))
            .Select(s => new { s.TimeId, Nome = s.Clube.Nome })
            .ToListAsync();

        return linhas
            .GroupBy(l => l.TimeId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.Nome).OrderBy(n => n).ToList());
    }

    // Troca a lista inteira pela nova. Null ou vazio = o time fica sem sede declarada, que é
    // um estado legítimo (a maioria dos 44 times importados do ranking está assim).
    //
    // ⚠️ Não chama SaveChanges — quem chama decide quando gravar, igual a
    // AdministracaoTime.Conceder.
    public static async Task DefinirAsync(DbPadelContext context, int timeId, IEnumerable<int>? clubeIds)
    {
        var desejadas = (clubeIds ?? Enumerable.Empty<int>())
            .Distinct()
            .Take(MaximoDeSedes)
            .ToList();

        // Só clubes que existem: o formulário manda id, e id de clube apagado no meio do
        // caminho estouraria a FK no SaveChanges — longe daqui, sem dizer o porquê.
        if (desejadas.Count > 0)
        {
            desejadas = await context.Clubes
                .Where(c => desejadas.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();
        }

        var atuais = await context.TimeSedes.Where(s => s.TimeId == timeId).ToListAsync();

        // Remove o que saiu e insere o que entrou, em vez de apagar tudo e recriar: recriar
        // faria o banco reescrever linhas idênticas a cada salvamento do perfil.
        foreach (var sobrando in atuais.Where(a => !desejadas.Contains(a.ClubeId)))
            context.TimeSedes.Remove(sobrando);

        foreach (var novo in desejadas.Where(d => atuais.All(a => a.ClubeId != d)))
            context.TimeSedes.Add(new TimeSede { TimeId = timeId, ClubeId = novo });
    }
}
