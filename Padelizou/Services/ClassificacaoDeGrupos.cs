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
    // Uma linha da tabela do grupo, já com a campanha somada. A TELA de classificação também
    // come daqui desde 13/08/2026: ela montava a própria ordenação (vitórias + saldo, sem
    // terceiro critério e com a vitória contada por `meusGames > gamesAdversario` em vez do
    // QuemVenceu) — era a quarta cópia desta regra, viva justamente onde o jogador olha.
    public record Linha(Dupla Dupla, int Jogos, int Vitorias, int Derrotas, int Saldo, int GamesPro, int GamesContra);

    // A tabela COMPLETA de um grupo, na ordem oficial. `Calcular` corta os N primeiros disto.
    public static List<Linha> Ordenar(IEnumerable<Dupla> duplasDoGrupo, IReadOnlyList<Partida> partidasDeGrupo) =>
        duplasDoGrupo
            .Select(dupla =>
            {
                var jogos = partidasDeGrupo
                    .Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id)
                    .ToList();

                int vitorias = 0, derrotas = 0, gamesPro = 0, gamesContra = 0;
                foreach (var jogo in jogos)
                {
                    bool ehDupla1 = jogo.Dupla1Id == dupla.Id;
                    gamesPro += ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                    gamesContra += ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);

                    // ⚠️ A vitória sai de Services/QuemVenceu, a MESMA função que grava o
                    // VencedorId ao finalizar. Aqui existia a conta em separado (`pro >
                    // contra`), e ela divergia da outra quando um lado estava sem placar:
                    // a tabela dizia que a dupla A venceu e o registro do jogo dizia que
                    // foi a B. No Interno de 05/08 isso classificou a dupla errada.
                    var venceu = QuemVenceu.Da(jogo);
                    if (venceu == dupla.Id) vitorias++;
                    else if (venceu != null) derrotas++;
                }

                return new Linha(dupla, jogos.Count, vitorias, derrotas, gamesPro - gamesContra, gamesPro, gamesContra);
            })
            // ⚠️ A ORDEM PRECISA SER TOTAL — não pode sobrar empate nenhum no fim.
            //
            // Vitórias e saldo não bastam, e não é caso raro: no Interno de 05/08/2026 o
            // grupo A dos TIMES terminou com Target.it, Valandro e Argentus empatados em
            // 1 vitória e −2 de saldo. Sem terceiro critério, quem fica em 2º sai da ORDEM
            // EM QUE AS DUPLAS CHEGARAM na consulta — e cada chamador monta essa consulta
            // do seu jeito. A mesma tabela então respondia coisas diferentes conforme quem
            // perguntasse, e a chave saiu com um time que não tinha vencido a semifinal.
            //
            // Confronto direto NÃO entra: naquele grupo ele é circular (Target.it ganhou
            // da Valandro, a Valandro da Argentus e a Argentus da Target.it), então além
            // de mais caro ele não resolveria justamente o empate que apareceu.
            //
            // O Id no fim não é critério esportivo — é a garantia de que a régua sempre
            // responde a mesma coisa. Empate que sobrevive a games pró é sorteio de
            // qualquer jeito; melhor um sorteio ESTÁVEL do que um que muda de ideia entre
            // duas telas.
            .OrderByDescending(x => x.Vitorias)
            .ThenByDescending(x => x.Saldo)
            .ThenByDescending(x => x.GamesPro)
            .ThenBy(x => x.Dupla.Id)
            .ToList();

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
            var ranking = Ordenar(grupo, partidasDeGrupo);

            for (int pos = 0; pos < ranking.Count && pos < passam; pos++)
            {
                classificados.Add(new ChaveamentoMataMata.Classificado(
                    ranking[pos].Dupla.Id, grupo.Key, ranking[pos].Vitorias, ranking[pos].Saldo, pos + 1));
            }
        }

        return classificados;
    }
}
