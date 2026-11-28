using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// TRETA (25/11/2026): os papéis de categoria da chave DTN e da Finalíssima — ver TRETA.md.
//
// Categoria com papel é festa, não campanha: fica fora do ranking, do Padelímetro, do MVP e
// das taxas, e ninguém se inscreve nela (quem entra é o robô ou o organizador).
public static class ChaveDtn
{
    public const string Dtn = "DTN";
    public const string Finalissima = "Finalissima";

    // A fase gravada em `Dupla.UltimaFase` de quem perde a final decisiva — o lugar do
    // "Campeao" numa chave invertida.
    public const string Perdedor = "DTN";

    // Nome e código da DTN, cabendo nas colunas (50 cada).
    public static string NomeDaDtn(string modulo) => Cortar("DTN " + modulo, 50);
    public static string CodigoDaDtn(string modulo) => Cortar(modulo, 46) + "-DTN";

    private static string Cortar(string texto, int max) => texto.Length <= max ? texto : texto[..max];

    public static bool TemPapel(Categoria? categoria) => categoria?.PapelDaChave != null;
    public static bool EhDtn(Categoria? categoria) => categoria?.PapelDaChave == Dtn;
    public static bool EhFinalissima(Categoria? categoria) => categoria?.PapelDaChave == Finalissima;

    // QUEM VAI PRO DTN, na ordem do PIOR pro melhor.
    //
    // De cada grupo (ranking completo, do 1º ao último — ClassificacaoDeGrupos.Ordenar), os
    // últimos `porGrupo`, nunca invadindo as vagas do Principal (`classificados`). O meio fica
    // de fora: é o "3º eliminado" do grupo de 4.
    //
    // ⚠️ DO PIOR PRO MELHOR porque é assim que a chave direta distribui o bye: ela dá a folga a
    // quem vem PRIMEIRO na lista. Numa chave invertida, folgar é ir direto pra final — e quem
    // merece isso é o PIOR (o Preto do PDF: "pior terceiro avança direto à final").
    //
    // Entre grupos, a régua de sempre (🗣️ organizador: "sim, sim"): vitórias → saldo → games
    // a favor → sorteio estável (o mesmo do desempate de grupo).
    public static List<int> QuemVaiProDtn(
        IEnumerable<IReadOnlyList<ClassificacaoDeGrupos.Linha>> rankingsPorGrupo, int porGrupo, int classificados)
    {
        if (porGrupo <= 0) return new List<int>();

        var vao = new List<ClassificacaoDeGrupos.Linha>();
        foreach (var ranking in rankingsPorGrupo)
        {
            int primeiro = Math.Max(classificados, ranking.Count - porGrupo);
            for (int i = primeiro; i < ranking.Count; i++) vao.Add(ranking[i]);
        }

        // POR JOGO (27/11/2026): com grupos de tamanhos diferentes (3/3/4) o último do grupo de 4
        // jogou mais e, em número absoluto, seria sempre o "pior". Com grupos iguais, mesma ordem.
        return vao
            .OrderBy(l => ChaveamentoMataMata.PorJogo(l.Vitorias, l.Jogos))
            .ThenBy(l => ChaveamentoMataMata.PorJogo(l.Saldo, l.Jogos))
            .ThenBy(l => ChaveamentoMataMata.PorJogo(l.GamesPro, l.Jogos))
            .ThenBy(l => ClassificacaoDeGrupos.SorteioEstavel(l.Dupla))
            .Select(l => l.Dupla.Id)
            .ToList();
    }

    // A CATEGORIA DTN DO MÓDULO, criada VAZIA se ainda não existe (27/11/2026).
    //
    // 🗣️ Rafael: "os DTN não dá pra mudar horário/quadra". A troca na prévia grava uma reserva
    // presa a (categoria, fase, número); sem a categoria DTN existir, não havia onde prender. Ela
    // nasce vazia no sorteio (ou ao salvar o DTN), a prévia leva o Id dela, e o robô só a
    // PREENCHE quando os grupos fecham (RoboDoChaveamento.Dtn) — e a reserva vale.
    //
    // Vazia ela não aparece em inscrição, dinheiro nem ranking (o papel já tira de tudo).
    public static async Task<Categoria> GarantirCategoriaAsync(DbPadelContext ctx, Categoria modulo)
    {
        var dtn = await ctx.Categorias.FirstOrDefaultAsync(c =>
            c.CategoriaDeOrigemId == modulo.Id && c.PapelDaChave == Dtn);
        if (dtn != null) return dtn;

        dtn = new Categoria
        {
            TorneioId = modulo.TorneioId,
            Nome = NomeDaDtn(modulo.Nome),
            Codigo = CodigoDaDtn(modulo.Codigo),
            ChaveDireta = true,
            PapelDaChave = Dtn,
            CategoriaDeOrigemId = modulo.Id,
            ClubeId = modulo.ClubeId,
            EliminatoriaNoSabadoANoite = modulo.EliminatoriaNoSabadoANoite,
            PodeJogarNaSedeExtra = modulo.PodeJogarNaSedeExtra,
        };
        ctx.Categorias.Add(dtn);
        await ctx.SaveChangesAsync();
        return dtn;
    }

    // O desligar: tira a DTN que ainda está VAZIA (sem dupla, sem jogo, sem reserva gravada nela
    // fica a reserva — ela cai junto por cascade). Com jogo, fica: aí a chave já existe.
    public static async Task TirarCategoriaVaziaAsync(DbPadelContext ctx, Categoria modulo)
    {
        var dtn = await ctx.Categorias.FirstOrDefaultAsync(c =>
            c.CategoriaDeOrigemId == modulo.Id && c.PapelDaChave == Dtn);
        if (dtn == null) return;
        bool temAlgo = await ctx.Duplas.AnyAsync(d => d.CategoriaId == dtn.Id)
                    || await ctx.Partidas.AnyAsync(p => p.CategoriaId == dtn.Id);
        if (temAlgo) return;
        ctx.Categorias.Remove(dtn);
        await ctx.SaveChangesAsync();
    }
}
