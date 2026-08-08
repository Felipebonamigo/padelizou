using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTO A GRADE CANSA — medido, não afirmado.
//
// O pedido do Felipe (08/08/2026) foi: "que não seja tão seguido e nem tão espaçado, um meio
// termo pra todas não cansarem e nem esperarem muito". Fui mexer no encaixe e MEDI DUAS
// TENTATIVAS PIORES que o que já existia — este arquivo é o resultado disso, e existe pra
// ninguém repetir o caminho.
//
// ⚠️ O QUE APRENDI TENTANDO:
//
// 1. A fila JÁ CHEGA em ordem de rodada. O RodadasAmericano monta rodadas em que cada pessoa
//    joga uma vez, e o guloso de primeira vaga preserva essa ordem — que é quase ótima. As
//    duas tentativas de "melhorar" reordenavam a fila (uma por "quem descansou mais", outra
//    só pulando quem jogou na rodada anterior) e as duas PIORARAM: num Americano de 16 em 2
//    quadras a pior sequência subiu de 2 para 4 e as emendas de 53 para 78. Reordenar
//    empurra os cansados pro fim, onde só sobram eles.
//
// 2. "Ninguém joga duas seguidas" é IMPOSSÍVEL na maioria dos tamanhos, e não por limitação
//    do código. Com 8 pessoas em 2 quadras cabem 8 por rodada: todo mundo joga toda rodada.
//    Mesmo com folga, 12 pessoas em 2 quadras dão 11 jogos por pessoa em 17 rodadas — 11 não
//    cabem em 17 sem encostar. É aritmética, como a do "cada um com cada um".
//
// 3. O que MANDA na qualidade é o ALINHAMENTO entre a rodada e as quadras. 16 pessoas em 2
//    quadras (rodada de 4 jogos = 2 slots exatos) dá pior sequência 2 — praticamente perfeito.
//    As MESMAS 16 pessoas em 3 quadras (4 jogos não dividem 3) desalinham e a sequência sobe.
//    Ou seja: o organizador melhora muito mais escolhendo o número de quadras do que qualquer
//    reordenação faria por ele.
public class DescansoNaGradeTests
{
    private static (List<Partida> Jogos, Dictionary<int, int[]> Ocupantes)
        GradeDeUmAmericano(int pessoas, int quadras, int semente)
    {
        var jogadores = Enumerable.Range(1, pessoas).ToList();
        var rodadas = RodadasAmericano.Montar(jogadores, new Random(semente));

        var jogos = new List<Partida>();
        var ocupantes = new Dictionary<int, int[]>();
        int proximaDupla = 1;

        foreach (var c in rodadas.SelectMany(r => r))
        {
            int d1 = proximaDupla++, d2 = proximaDupla++;
            ocupantes[d1] = new[] { c.A1, c.A2 };
            ocupantes[d2] = new[] { c.B1, c.B2 };
            jogos.Add(new Partida { Dupla1Id = d1, Dupla2Id = d2, Fase = "Americano Rodada 1" });
        }

        int slots = jogos.Count + GradeDeJogos.MargemDeHorarios(quadras);
        var horarios = new List<DateTime>();
        var inicio = new DateTime(2026, 8, 8, 19, 0, 0);
        for (int i = 0; i < slots; i++)
            for (int q = 0; q < quadras; q++)
                horarios.Add(inicio.AddMinutes(20 * i));

        GradeDeJogos.Encaixar(jogos, horarios, ocupantes);
        return (jogos, ocupantes);
    }

    private record Qualidade(int PiorSequencia, int PiorEspera);

    private static Qualidade Medir(List<Partida> jogos, Dictionary<int, int[]> ocupantes)
    {
        var ordem = jogos.Where(j => j.HorarioPrevisto != null)
            .Select(j => j.HorarioPrevisto!.Value).Distinct().OrderBy(h => h).ToList();

        var porPessoa = new Dictionary<int, List<int>>();
        foreach (var jogo in jogos.Where(j => j.HorarioPrevisto != null))
        {
            int rodada = ordem.IndexOf(jogo.HorarioPrevisto!.Value);
            foreach (var pessoa in ocupantes[jogo.Dupla1Id].Concat(ocupantes[jogo.Dupla2Id]))
            {
                if (!porPessoa.TryGetValue(pessoa, out var lista)) porPessoa[pessoa] = lista = new List<int>();
                lista.Add(rodada);
            }
        }

        int piorSequencia = 1, piorEspera = 0;
        foreach (var lista in porPessoa.Values)
        {
            lista.Sort();
            int sequencia = 1;
            for (int i = 1; i < lista.Count; i++)
            {
                int intervalo = lista[i] - lista[i - 1];
                if (intervalo == 1) { sequencia++; piorSequencia = Math.Max(piorSequencia, sequencia); }
                else sequencia = 1;
                piorEspera = Math.Max(piorEspera, intervalo);
            }
        }
        return new Qualidade(piorSequencia, piorEspera);
    }

    [Theory]
    [InlineData(12, 2)]
    [InlineData(16, 2)]
    [InlineData(16, 3)]
    [InlineData(20, 3)]
    public void A_grade_nao_cansa_nem_entedia_alem_do_aceitavel(int pessoas, int quadras)
    {
        // Tetos folgados de propósito: o alvo é pegar o ABSURDO (dez rodadas coladas, quinze
        // sentado), não engessar o encaixe. Se um dia alguém "otimizar" a grade e estes
        // números estourarem, foi piora — foi exatamente assim que eu descobri as minhas.
        for (int semente = 1; semente <= 5; semente++)
        {
            var (jogos, ocupantes) = GradeDeUmAmericano(pessoas, quadras, semente);
            var q = Medir(jogos, ocupantes);

            var caso = $"{pessoas} pessoas em {quadras} quadras (semente {semente}): "
                     + $"pior sequência {q.PiorSequencia}, pior espera {q.PiorEspera}";

            Assert.True(q.PiorSequencia <= 8, $"jogos demais colados — {caso}");
            Assert.True(q.PiorEspera <= 6, $"espera longa demais — {caso}");
        }
    }

    [Fact]
    public void Rodada_alinhada_com_as_quadras_e_o_que_de_fato_espalha()
    {
        // A descoberta que vale pro organizador, e a razão de este teste existir: 16 pessoas
        // fazem rodadas de 4 jogos. Em 2 quadras a rodada ocupa 2 slots CERTINHOS e ninguém
        // joga mais de 2 vezes seguidas. Nas mesmas 16 pessoas em 3 quadras, 4 jogos não
        // dividem 3, as rodadas desalinham e a sequência piora.
        //
        // Ou seja: escolher bem o número de quadras rende mais que qualquer reordenação — e
        // duas tentativas de reordenar mediram PIOR (ver o cabeçalho deste arquivo).
        var alinhado = Medir(
            GradeDeUmAmericano(16, 2, semente: 1).Jogos,
            GradeDeUmAmericano(16, 2, semente: 1).Ocupantes);

        Assert.True(alinhado.PiorSequencia <= 2,
            $"16 pessoas em 2 quadras deveria ser o caso bom, e deu sequência {alinhado.PiorSequencia}");
    }

    [Fact]
    public void Ninguem_em_duas_quadras_ao_mesmo_tempo()
    {
        // A garantia que não pode ser trocada por conforto nenhum.
        var (jogos, ocupantes) = GradeDeUmAmericano(16, 3, semente: 7);

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));

        foreach (var noMesmoHorario in jogos.GroupBy(j => j.HorarioPrevisto))
        {
            var pessoas = noMesmoHorario
                .SelectMany(j => ocupantes[j.Dupla1Id].Concat(ocupantes[j.Dupla2Id])).ToList();

            Assert.Equal(pessoas.Count, pessoas.Distinct().Count());
        }
    }
}
