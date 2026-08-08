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
    // `Encerrada` = todo jogo desta tabela já terminou. É o que separa "está ganhando" de
    // "ganhou": sem isso a tela coroaria o líder da segunda rodada de seis.
    public sealed record Tabela(
        string? Grupo,
        string? Titulo,
        int PassamDaqui,
        bool EhGrupoFinal,
        IReadOnlyList<TabelaDoAmericano.Linha> Linhas,
        bool Encerrada = false);

    // `partidasDaCategoria` pode vir com o torneio inteiro dentro: o que não é Americano é
    // descartado aqui, e não na consulta de quem chama. Filtrar no chamador é o que faz três
    // telas discordarem sobre o que entra na conta.
    //
    // ⚠️ DUAS PERGUNTAS DIFERENTES, DUAS LISTAS (Felipe, 08/08/2026: "já deveria aparecer a
    // classificação, mesmo sem nenhum jogo, com tudo zerado e com as participantes"):
    //
    //   • QUEM ESTÁ NA TABELA sai da GRADE INTEIRA — quem foi sorteado já é participante, e
    //     ninguém precisa esperar o primeiro placar pra existir na classificação. Antes a
    //     aba abria com "ninguém pontuou ainda" justamente no começo do torneio, que é
    //     quando mais gente entra pra procurar o próprio nome.
    //   • QUEM PONTUOU sai só do que TERMINOU. Jogo em quadra tem placar parcial na tela,
    //     mas somá-lo aqui faria a classificação subir e descer no meio do game.
    public static List<Tabela> Montar(IEnumerable<Partida> partidasDaCategoria, int passamPorGrupo)
    {
        var doAmericano = partidasDaCategoria
            .Where(p => FaseDoAmericano.EhDoAmericano(p.Fase))
            .ToList();

        var finalizadas = doAmericano.Where(p => p.Status == "Finalizada").ToList();

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
            var doGrupo = doAmericano.Where(p => FaseDoAmericano.EhDoGrupo(p.Fase, grupo)).ToList();
            tabelas.Add(new Tabela(
                Grupo: grupo,
                Titulo: grupo == null ? null : $"Grupo {grupo}",
                // Sem divisão não há para onde passar: o corte é ZERO, senão a tabela
                // marcaria "classificados" num torneio que não tem fase seguinte.
                PassamDaqui: grupo == null ? 0 : passamPorGrupo,
                EhGrupoFinal: false,
                Linhas: TabelaDoAmericano.Montar(
                    finalizadas.Where(p => FaseDoAmericano.EhDoGrupo(p.Fase, grupo)),
                    Participantes(doGrupo)),
                Encerrada: doGrupo.Count > 0 && doGrupo.All(p => p.Status == "Finalizada")));
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
                Linhas: TabelaDoAmericano.Montar(
                    doFinal.Where(p => p.Status == "Finalizada"),
                    Participantes(doFinal)),
                Encerrada: doFinal.All(p => p.Status == "Finalizada")));
        }

        return tabelas;
    }

    // Quem joga estas partidas — a fonte de "as participantes" da tabela zerada. Sai da
    // própria grade porque é ela que diz quem está em qual grupo; a lista de inscritos não
    // sabe disso.
    private static List<Jogador> Participantes(IEnumerable<Partida> partidas) =>
        partidas
            .SelectMany(p => new[] { p.Dupla1?.Jogador1, p.Dupla1?.Jogador2, p.Dupla2?.Jogador1, p.Dupla2?.Jogador2 })
            .Where(j => j != null)
            .GroupBy(j => j!.Id)
            .Select(g => g.First()!)
            .ToList();

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

    // QUEM GANHOU — nula enquanto ninguém ganhou, que é o estado normal de quase toda a
    // vida do torneio. Três condições, e as três importam:
    //
    //  • existe tabela que decide (com vários grupos e sem grupo final, ninguém coroa nada);
    //  • ela ENCERROU (líder da rodada 2 de 6 não é campeão);
    //  • e a liderança não está EMPATADA — no Americano empate no topo do que decide vira
    //    rodada de desempate, então ali ainda não há campeão, há dois candidatos.
    public static TabelaDoAmericano.Linha? Campea(IReadOnlyList<Tabela> tabelas)
    {
        var decide = QueDecideOTitulo(tabelas);
        if (decide is not { Encerrada: true }) return null;

        var primeira = decide.Linhas.FirstOrDefault();
        return primeira is null || primeira.Empatado ? null : primeira;
    }

    // Este torneio se divide em grupos? Muda o texto da tela: com divisão a tabela do grupo
    // diz quem PASSA; sem ela, diz quem está ganhando o torneio.
    public static bool TemDivisaoEmGrupos(IReadOnlyList<Tabela> tabelas) =>
        tabelas.Any(t => t.Grupo != null);
}
