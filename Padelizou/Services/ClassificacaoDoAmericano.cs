using Padelizou.Models;

namespace Padelizou.Services;

// A classificação de um Americano individual, QUEBRADA POR GRUPO — que é a única forma certa
// de mostrá-la desde que o formato ganhou divisão (06/08/2026).
//
// ⚠️ Somar a categoria inteira compara gente que NUNCA SE ENFRENTOU. Num Americano de 10 são
// 2 grupos de 5: quem jogou no grupo A não cruzou com ninguém do B, e uma tabela única
// ordenaria os dez por uma soma que não aconteceu — e o corte de quem passa sairia dela.
//
// Mora aqui porque são TRÊS leitores fazendo a mesma pergunta: a aba "Chaves e Grupos" da
// página do torneio, a sub-aba "Classificação" dentro de Jogos e a página avulsa. Cada um
// tinha (ou ia ter) a sua cópia — e a cópia da sub-aba de Jogos somava a categoria inteira,
// então o MESMO torneio mostrava duas classificações diferentes na mesma tela.
public static class ClassificacaoDoAmericano
{
    // Uma tabela: a de um grupo, a do grupo final, ou a do torneio inteiro quando não há
    // divisão (aí `Grupo` é nulo e não existe corte — todo mundo já está no que decide).
    public sealed record Tabela(
        string? Grupo,
        string? Titulo,
        int PassamDaqui,
        bool EhGrupoFinal,
        IReadOnlyList<TabelaDoAmericano.Linha> Linhas);

    // `partidasDaCategoria` pode vir com o torneio inteiro dentro: o que não é Americano e o
    // que não terminou são descartados aqui, e não na consulta de quem chama. Filtrar no
    // chamador é o que faz três telas discordarem sobre o que entra na conta.
    public static List<Tabela> Montar(IEnumerable<Partida> partidasDaCategoria, int passamPorGrupo)
    {
        var doAmericano = partidasDaCategoria
            .Where(p => FaseDoAmericano.EhDoAmericano(p.Fase) && p.Status == "Finalizada")
            .ToList();

        var tabelas = new List<Tabela>();

        // Um nome de grupo por partida da fase de grupos. Nulo = torneio sem divisão, e aí
        // esta lista tem um item só (o nulo), que vira a tabela única.
        var grupos = doAmericano
            .Where(p => FaseDoAmericano.EhDaFaseDeGrupos(p.Fase))
            .Select(p => FaseDoAmericano.GrupoDe(p.Fase))
            .Distinct()
            .OrderBy(g => g, StringComparer.Ordinal)
            .ToList();

        foreach (var grupo in grupos)
        {
            tabelas.Add(new Tabela(
                Grupo: grupo,
                Titulo: grupo == null ? null : $"Grupo {grupo}",
                // Sem divisão não há para onde passar: o corte é ZERO, senão a tabela
                // marcaria "classificados" num torneio que não tem fase seguinte.
                PassamDaqui: grupo == null ? 0 : passamPorGrupo,
                EhGrupoFinal: false,
                Linhas: TabelaDoAmericano.Montar(doAmericano.Where(p => FaseDoAmericano.EhDoGrupo(p.Fase, grupo)))));
        }

        // O grupo final vem por último: é a fase seguinte, e é ele que decide o título.
        var doFinal = doAmericano.Where(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase)).ToList();
        if (doFinal.Count > 0)
        {
            tabelas.Add(new Tabela(
                Grupo: null,
                Titulo: "Grupo final",
                PassamDaqui: 0,
                EhGrupoFinal: true,
                Linhas: TabelaDoAmericano.Montar(doFinal)));
        }

        return tabelas;
    }

    // Qual tabela responde "quem é o campeão": o grupo final quando ele existe, e o grupo
    // único quando o torneio não tem divisão.
    //
    // ⚠️ Com vários grupos e sem grupo final ainda, a resposta é NULA de propósito — a fase
    // classificatória não coroa ninguém, e apontar o líder de um dos grupos como se fosse
    // do torneio é exatamente o erro que a divisão por grupo veio consertar.
    public static Tabela? QueDecideOTitulo(IReadOnlyList<Tabela> tabelas)
    {
        var final = tabelas.FirstOrDefault(t => t.EhGrupoFinal);
        if (final != null) return final;

        var deGrupo = tabelas.Where(t => !t.EhGrupoFinal).ToList();
        return deGrupo.Count == 1 && deGrupo[0].Grupo == null ? deGrupo[0] : null;
    }

    // Este torneio se divide em grupos? Muda o texto da tela: com divisão a tabela do grupo
    // diz quem PASSA; sem ela, diz quem está ganhando o torneio.
    public static bool TemDivisaoEmGrupos(IReadOnlyList<Tabela> tabelas) =>
        tabelas.Any(t => t.Grupo != null);
}
