using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quantos cliques cada patrocinador levou. `NoMes` são os últimos 30 dias corridos.
public record ContagemDeCliques(string Patrocinador, int Total, int NoMes);

public static class CliquesDoPatrocinio
{
    // ⚠️ O NOME DO PATROCINADOR VIRA PARTE DA URL, então ele precisa de uma forma canônica:
    // "Grand Padel", "grand padel" e "GRAND PADEL" são o mesmo patrocinador. Comparar cru
    // transformaria um link copiado de e-mail (que chega em caixa baixa) em 404 — e o
    // patrocinador ficaria sem destino sem ninguém entender por quê.
    public static bool MesmoNome(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    // Resolve o patrocinador pelo nome, na NOSSA lista. Nulo quando não existe.
    //
    // ⚠️ É ESTA FUNÇÃO QUE IMPEDE O OPEN REDIRECT. O destino nunca vem do pedido: chega um
    // nome, e a URL sai de PatrocinadoresSettings. Um endpoint que aceitasse `?url=` daria a
    // qualquer um um link de phishing saindo de `padelizou.com.br` — o domínio em que as
    // pessoas confiam.
    public static Patrocinador? Achar(PatrocinadoresSettings cfg, string? nome) =>
        string.IsNullOrWhiteSpace(nome)
            ? null
            : cfg.ParaExibir().FirstOrDefault(p => MesmoNome(p.Nome, nome));

    public static async Task<IReadOnlyList<ContagemDeCliques>> ContarAsync(
        DbPadelContext context, DateTime agora, PatrocinadoresSettings? cfg = null)
    {
        var desde = agora.AddDays(-30);

        // atalho: carrega a tabela inteira e conta em memória. Cabe porque a linha nasce de um
        // CLIQUE NO RODAPÉ — não de visita: quase todo visitante nunca clica num logo de
        // patrocinador, então isto é ordens de grandeza menor que o AcessoAoSite, que ganha uma
        // linha por página vista. Agregar no SQL acima de ~50 mil linhas (GroupBy + Count), e aí
        // conferindo a tradução com ToQueryString contra Npgsql — é `Where` depois de projeção,
        // do tipo que passa liso no InMemory e estoura no Postgres.
        var cliques = await context.CliquesNoPatrocinador
            .AsNoTracking()
            .ToListAsync();

        // ⚠️ PATROCINADOR EM CARTAZ SEM CLIQUE APARECE COM ZERO. "Não aparece na lista" e "teve
        // zero clique" são conclusões opostas, e uma tela que some com o segundo faz o Felipe
        // concluir a primeira. Por isso a lista de EXIBIÇÃO entra junto, e não só o banco.
        var emCartaz = (cfg ?? new PatrocinadoresSettings()).ParaExibir().Select(p => p.Nome);

        var nomes = cliques.Select(c => c.Patrocinador)
            .Concat(emCartaz)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return nomes
            .Select(nome =>
            {
                var dele = cliques.Where(c => MesmoNome(c.Patrocinador, nome)).ToList();
                return new ContagemDeCliques(nome, dele.Count, dele.Count(c => c.Quando >= desde));
            })
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Patrocinador)
            .ToList();
    }
}
