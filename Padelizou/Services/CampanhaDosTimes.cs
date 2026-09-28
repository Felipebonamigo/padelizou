using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

// A campanha de cada time NUM torneio — o que ele fez em quadra: duplas, jogos, V–D, games.
//
// 🗣️ Felipe, 28/09/2026, com o print de um concorrente: *"interessante esse resumo por times
// no torneio — não podemos fazer igual, mas podemos fazer algo parecido"*. A aba Times só
// dizia os PONTOS; o resultado em quadra não estava em lugar nenhum.
//
// A campanha do time é a SOMA DAS CAMPANHAS DAS DUPLAS dele, e a dupla é do time se qualquer
// um dos dois joga por ele. Daí saem as duas regras que parecem estranhas e são de propósito
// (CampanhaDoTimeNoTorneioTests trava as duas):
//   • dupla mista conta pros DOIS times — a mesma régua dos pontos
//     (EstatisticasService.ObterPontosTimesNoTorneioAsync);
//   • confronto interno é uma vitória E uma derrota do time: são duas duplas jogando.
//
// Função pura, sem banco: quem chama entrega a grade JÁ PASSADA pelo portão da chave não
// aprovada (TorneiosController.CarregarViewBagJogosAsync) — é isso que impede o "próximo
// jogo" de vazar uma grade que ainda não foi publicada.
public static class CampanhaDosTimes
{
    public sealed record Campanha(
        int TimeId, string Time, string? Logo,
        int Duplas, int Jogos, int Vitorias, int Derrotas, int GamesPro, int GamesContra,
        int DuplasComJogoMarcado, Partida? ProximoJogo)
    {
        public int Saldo => GamesPro - GamesContra;

        // Sobre os jogos DECIDIDOS: jogo sem vencedor (sem placar) não é derrota. Null quando
        // não há nenhum — "0%" diria que o time perdeu tudo.
        public int? Aproveitamento => Vitorias + Derrotas == 0
            ? null
            : (int)Math.Round(100m * Vitorias / (Vitorias + Derrotas), MidpointRounding.AwayFromZero);
    }

    public static IReadOnlyDictionary<int, Campanha> Montar(IEnumerable<Dupla> duplas, IEnumerable<Partida> jogos)
    {
        // A mesma porta dos pontos: lista de espera e sem parceiro não jogaram, e a dupla-TIME
        // tem o ORGANIZADOR como Jogador1 — contá-la poria a campanha de um time inteiro na
        // conta do time do organizador.
        var duplasDoTime = duplas
            .Where(d => !d.EhTime && InscricaoQueConta.Vale(d))
            .Select(d => (Dupla: d, Times: new[] { d.Jogador1?.Time, d.Jogador2?.Time }
                .OfType<Time>().DistinctBy(t => t.Id).ToList()))
            .Where(x => x.Times.Count > 0)
            .ToList();

        // Jogo com a mesma dupla dos dois lados é vaga ainda não definida, não confronto.
        var validos = jogos.Where(p => p.Dupla1Id != p.Dupla2Id).ToList();
        var finalizados = validos.Where(p => p.Status == "Finalizada").ToList();
        var pelaFrente = validos.Where(p => p.Status is "AoVivo" or "Agendada").ToList();

        return duplasDoTime
            .SelectMany(x => x.Times.Select(t => (Time: t, x.Dupla)))
            .GroupBy(x => x.Time.Id)
            .ToDictionary(g => g.Key, g =>
            {
                var time = g.First().Time;
                var ids = g.Select(x => x.Dupla.Id).ToHashSet();

                int jogosDisputados = 0, vitorias = 0, derrotas = 0, pro = 0, contra = 0;
                foreach (var jogo in finalizados)
                {
                    // Um lado de cada vez: no confronto interno os dois lados são do time.
                    foreach (var lado in new[] { 1, 2 })
                    {
                        int duplaId = lado == 1 ? jogo.Dupla1Id : jogo.Dupla2Id;
                        if (!ids.Contains(duplaId)) continue;

                        jogosDisputados++;
                        pro += (lado == 1 ? jogo.GamesDupla1 : jogo.GamesDupla2) ?? 0;
                        contra += (lado == 1 ? jogo.GamesDupla2 : jogo.GamesDupla1) ?? 0;

                        // A vitória sai de QuemVenceu, a mesma função que grava o VencedorId —
                        // conta própria de `pro > contra` já classificou a dupla errada uma vez.
                        var venceu = QuemVenceu.Da(jogo);
                        if (venceu == duplaId) vitorias++;
                        else if (venceu != null) derrotas++;
                    }
                }

                var doTime = pelaFrente.Where(p => ids.Contains(p.Dupla1Id) || ids.Contains(p.Dupla2Id)).ToList();

                // Em quadra primeiro; depois o horário marcado, e sem horário vai pro fim.
                var proximo = doTime
                    .OrderBy(p => p.Status == "AoVivo" ? 0 : 1)
                    .ThenBy(p => p.HorarioPrevisto ?? DateTime.MaxValue)
                    .ThenBy(p => p.OrdemNoHorario ?? int.MaxValue)
                    .ThenBy(p => p.Id)
                    .FirstOrDefault();

                var comJogo = doTime.SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).Where(ids.Contains).Distinct().Count();

                return new Campanha(time.Id, time.Nome, time.Logo, ids.Count,
                    jogosDisputados, vitorias, derrotas, pro, contra, comJogo, proximo);
            });
    }

    public sealed record LinhaDaTabela(Campanha Campanha, int? Pontos);

    // A tabela da aba Times: pontos primeiro (é o placar oficial entre os times), depois o que
    // se decidiu em quadra. `pontos` vem vazio no torneio que não pontua (restrito, interno de
    // time) — aí a linha leva null, e a tela não inventa um zero.
    public static List<LinhaDaTabela> Tabela(
        IReadOnlyDictionary<int, Campanha> campanhas, IEnumerable<PontosTimeTorneioVM> pontos)
    {
        var porTime = pontos.ToDictionary(p => p.TimeId, p => p.Pontos);

        return campanhas.Values
            .Select(c => new LinhaDaTabela(c, porTime.TryGetValue(c.TimeId, out var p) ? p : null))
            .OrderByDescending(l => l.Pontos ?? 0)
            .ThenByDescending(l => l.Campanha.Vitorias)
            .ThenByDescending(l => l.Campanha.Saldo)
            .ThenBy(l => l.Campanha.Time, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
