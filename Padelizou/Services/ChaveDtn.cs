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

        return vao
            .OrderBy(l => l.Vitorias)
            .ThenBy(l => l.Saldo)
            .ThenBy(l => l.GamesPro)
            .ThenBy(l => ClassificacaoDeGrupos.SorteioEstavel(l.Dupla))
            .Select(l => l.Dupla.Id)
            .ToList();
    }
}
