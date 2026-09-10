using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A MESMA GRADE TEM QUE DAR A MESMA RESPOSTA, VENHA A LISTA NA ORDEM QUE VIER (10/09/2026).
//
// 🕳️ O DEFEITO, medido antes de ser entendido: `GradeDoErMedidaTests.Refazer_grade_sem_nada_mudado_
// reproduz_a_grade_do_sorteio` falhava em ~15% das execuções (5 em 25; 3 em 25 num commit anterior),
// sempre com a mesma assinatura — *"2 de 44 jogos trocaram de horário"*, um PAR trocando entre si.
// O STATUS dava isso como resolvido no PR #118 ("ordem estável dentro do reparo"); não estava.
//
// 🕳️ A CAUSA está em `AuditoriaDaGrade.Conferir`, e não no reparo. Ao montar os "jogos seguidos"
// ela ordena a agenda de cada pessoa por `OrderBy(q => q.Quando)` — e `OrderBy` é ESTÁVEL, então
// dois jogos da mesma pessoa NO MESMO HORÁRIO (que acontece: é o achado "Mesma pessoa em dois
// jogos") ficam na ordem em que vieram na LISTA. A varredura de pares consecutivos então monta
// pares diferentes — (A,B) e (B,C) numa ordem, (B,A) e (A,C) na outra — e o dicionário que
// agrupa por par de jogos passa a ter chaves diferentes.
//
// ⚠️ NÃO É SÓ A ORDEM DOS ACHADOS: é a CONTAGEM. A mesma grade rendia 3 achados numa ordem e 2 na
// outra. Isso vaza para três lugares de uma vez:
//   • o número que o organizador lê no "Conferir grade" muda conforme a ordem que o banco devolveu;
//   • `ReparoDaGrade.Custo` soma pesos diferentes, então uma troca é aceita numa passagem e
//     recusada na outra;
//   • `ReparoDaGrade.Doentes` monta outro `pesoPorHorario`, então o guloso — que aceita a PRIMEIRA
//     troca que melhora — começa por outro jogo.
//
// Daí o par trocado: o sorteio entrega a lista na ordem da fila e o "Refazer" na ordem que o banco
// quiser (`_context.Partidas.Where(...)` sem ORDER BY). Mesmas entradas, duas grades.
//
// A régua: `Conferir` e `Reparar` são funções da GRADE, não da ordem da lista.
public class GradeEstavelIndependenteDaOrdemTests
{
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio() => new()
    {
        Id = 1, Nome = "T", Codigo = "T1",
        DataInicio = Sabado.AddHours(8),
        DataFim = Sabado.AddDays(1),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(8, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static readonly Categoria Cat = new() { Id = 1, Nome = "3ª", Codigo = "C3" };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = Cat };

    private static Partida Jogo(string codigo, int d1, int d2, DateTime quando) => new()
    {
        Id = 0, TorneioId = 1, Codigo = codigo, Status = "Agendada",
        Fase = "Grupo A", CategoriaId = 1, Categoria = Cat,
        Dupla1Id = d1, Dupla2Id = d2, HorarioPrevisto = quando,
    };

    // A pessoa 10 joga em DUAS duplas, e as duas jogam às 10:00 — o empate que a ordenação
    // estável não desempatava. O terceiro jogo, mais tarde, é quem fecha o par consecutivo.
    private static (List<Partida> Jogos, Dupla[] Duplas) Cenario()
    {
        var duplas = new[]
        {
            Dupla(1, 10, 11), Dupla(2, 10, 12), Dupla(3, 30, 31),
            Dupla(4, 40, 41), Dupla(5, 50, 51),
        };

        var jogos = new List<Partida>
        {
            Jogo("AAA", 1, 3, Sabado.AddHours(10)),
            Jogo("BBB", 2, 4, Sabado.AddHours(10)),
            Jogo("CCC", 1, 5, Sabado.AddHours(10).AddMinutes(50)),
        };

        return (jogos, duplas);
    }

    private static string Assinatura(AuditoriaDaGrade.Achado a) => $"{a.Regra}|{a.Quando:O}|{a.Gravidade}";

    // ⚠️ TODAS as permutações, não duas: o defeito aparecia só em algumas, e foi por isso que ele
    // sobreviveu ao PR #118 — quem testou pegou uma ordem que casava.
    private static IEnumerable<List<Partida>> Permutacoes(List<Partida> jogos)
    {
        if (jogos.Count <= 1) { yield return new List<Partida>(jogos); yield break; }

        for (int i = 0; i < jogos.Count; i++)
        {
            var resto = new List<Partida>(jogos);
            resto.RemoveAt(i);
            foreach (var p in Permutacoes(resto))
            {
                p.Insert(0, jogos[i]);
                yield return p;
            }
        }
    }

    [Fact]
    public void Conferir_da_a_mesma_resposta_em_qualquer_ordem_da_lista()
    {
        var (jogos, duplas) = Cenario();
        var torneio = Torneio();

        var esperado = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma)
            .Select(Assinatura).OrderBy(s => s, StringComparer.Ordinal).ToList();

        foreach (var ordem in Permutacoes(jogos))
        {
            var saiu = AuditoriaDaGrade.Conferir(torneio, ordem, duplas, SedesDoTorneio.Nenhuma)
                .Select(Assinatura).OrderBy(s => s, StringComparer.Ordinal).ToList();

            Assert.True(esperado.SequenceEqual(saiu),
                $"a mesma grade rendeu achados diferentes conforme a ordem da lista.\n"
                + $"  ordem  [{string.Join(",", ordem.Select(j => j.Codigo))}]\n"
                + $"  esperado ({esperado.Count}): {string.Join(" ; ", esperado)}\n"
                + $"  saiu     ({saiu.Count}): {string.Join(" ; ", saiu)}");
        }
    }

    // A ORDEM DOS ACHADOS também é estável — é a ordem em que o "Conferir grade" lista os pontos
    // na tela, e uma lista que se reembaralha a cada F5 faz o organizador achar que algo mudou.
    [Fact]
    public void A_ordem_dos_achados_na_tela_nao_depende_da_ordem_da_lista()
    {
        var (jogos, duplas) = Cenario();
        var torneio = Torneio();

        var esperado = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma)
            .Select(Assinatura).ToList();

        foreach (var ordem in Permutacoes(jogos))
            Assert.Equal(esperado, AuditoriaDaGrade.Conferir(torneio, ordem, duplas, SedesDoTorneio.Nenhuma)
                .Select(Assinatura).ToList());
    }

    // E o reparo, que é guloso e aceita a PRIMEIRA troca que melhora, tem que chegar na MESMA
    // grade — é literalmente o que o "Refazer grade" promete quando nada mudou.
    [Fact]
    public void Reparar_chega_na_mesma_grade_em_qualquer_ordem_da_lista()
    {
        var torneio = Torneio();
        var (_, duplas) = Cenario();

        Dictionary<string, DateTime?> Rodar(Func<List<Partida>, List<Partida>> ordenar)
        {
            var (jogos, _) = Cenario();
            ReparoDaGrade.Reparar(torneio, ordenar(jogos), duplas, SedesDoTorneio.Nenhuma);
            return jogos.ToDictionary(j => j.Codigo, j => j.HorarioPrevisto);
        }

        var referencia = Rodar(j => j);

        foreach (var permutar in new Func<List<Partida>, List<Partida>>[]
                 {
                     j => Enumerable.Reverse(j).ToList(),
                     j => j.OrderBy(x => x.Codigo, StringComparer.Ordinal).ToList(),
                     j => j.OrderByDescending(x => x.Codigo, StringComparer.Ordinal).ToList(),
                 })
        {
            var saiu = Rodar(permutar);
            Assert.True(referencia.All(par => saiu[par.Key] == par.Value),
                "o reparo chegou em grades diferentes conforme a ordem da lista: "
                + string.Join(", ", referencia.Where(p => saiu[p.Key] != p.Value)
                    .Select(p => $"{p.Key}: {p.Value:dd HH:mm} → {saiu[p.Key]:dd HH:mm}")));
        }
    }
}
