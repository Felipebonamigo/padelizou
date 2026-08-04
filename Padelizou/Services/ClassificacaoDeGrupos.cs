using Padelizou.Models;

namespace Padelizou.Services;

// Quem classificou em cada grupo, em que posição e com que campanha — a régua única.
//
// Esta conta existia COPIADA em três lugares (o robô da Mesa, o robô do Controle de Placar
// e, agora, a detecção de bye do avanço de fase). Três cópias de um ranking é um convite a
// três campeões diferentes: bastaria um desempate divergir. A regra: vitórias, depois saldo
// de games, dentro de cada grupo; classificam os N primeiros.
public static class ClassificacaoDeGrupos
{
    public static List<ChaveamentoMataMata.Classificado> Calcular(
        IEnumerable<Dupla> duplasComGrupo,
        IReadOnlyList<Partida> partidasDeGrupo,
        int classificamPorGrupo = 2)
    {
        var classificados = new List<ChaveamentoMataMata.Classificado>();
        int passam = Math.Max(1, classificamPorGrupo);

        foreach (var grupo in duplasComGrupo
                     .Where(d => d.Grupo != null)
                     .GroupBy(d => d.Grupo!)
                     .OrderBy(g => g.Key))
        {
            var ranking = grupo
                .Select(dupla =>
                {
                    var jogos = partidasDeGrupo
                        .Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id)
                        .ToList();

                    int vitorias = 0, saldo = 0;
                    foreach (var jogo in jogos)
                    {
                        bool ehDupla1 = jogo.Dupla1Id == dupla.Id;
                        int pro = ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                        int contra = ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);
                        saldo += pro - contra;
                        if (pro > contra) vitorias++;
                    }

                    return (dupla, vitorias, saldo);
                })
                .OrderByDescending(x => x.vitorias).ThenByDescending(x => x.saldo)
                .ToList();

            for (int pos = 0; pos < ranking.Count && pos < passam; pos++)
            {
                classificados.Add(new ChaveamentoMataMata.Classificado(
                    ranking[pos].dupla.Id, grupo.Key, ranking[pos].vitorias, ranking[pos].saldo, pos + 1));
            }
        }

        return classificados;
    }
}
