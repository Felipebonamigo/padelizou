using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTO A FASE DE GRUPOS CANSA — o gêmeo do DescansoNaGradeTests, que só mede Americano.
//
// Relato do Felipe (07/09/2026), olhando a grade do torneio do Er: *"tem jogos seguidos dos
// mesmos jogadores, isso temos q evitar"*. Medido no dev, num torneio gerado pelo app: 20
// casos de dupla jogando em horários vizinhos, em 31 jogos.
//
// ⚠️ POR QUE O AMERICANO NÃO TINHA ESSE PROBLEMA E A FASE DE GRUPOS TINHA. O DescansoNaGradeTests
// explica: a fila do Americano JÁ CHEGA EM ORDEM DE RODADA (o RodadasAmericano monta rodadas
// em que cada pessoa joga uma vez), e o guloso de primeira vaga preserva essa ordem. A fase de
// grupos chegava GRUPO A GRUPO:
//
//     Grupo A: a×b, a×c, b×c     ← as três coladas, entre as mesmas três duplas
//     Grupo B: d×e, d×f, e×f
//
// que é a pior ordem possível pra descanso. E ninguém media este caminho — foi por isso que o
// problema chegou até o organizador.
//
// ⚠️ E POR QUE NÃO SE CONSERTA NO ENCAIXE. O mesmo DescansoNaGradeTests registra duas tentativas
// de reordenar a fila por "quem descansou mais", as duas MEDIDAS PIORES. Uma terceira foi
// tentada em 07/09 e reprovou igual (pior espera 7, teto 6). O conserto é dar à fase de grupos
// a mesma ordem de rodada que o Americano já tem — na origem, não no guloso.
public class DescansoNaFaseDeGruposTests
{
    private record Qualidade(int PiorSequencia, int PiorEspera);

    // A MESMA medida do DescansoNaGradeTests, de propósito: dois jeitos de medir a mesma coisa
    // divergem no dia em que só um for atualizado.
    private static Qualidade Medir(IReadOnlyList<Partida> jogos, IReadOnlyDictionary<int, int[]> ocupantes)
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

    private static async Task<Qualidade> GradeDoTorneioAsync(int duplas, int quadras)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: duplas);
        torneio.QuantidadeQuadras = quadras;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Include(p => p.Dupla1).Include(p => p.Dupla2).ToListAsync();
        var ocupantes = jogos
            .SelectMany(j => new[] { j.Dupla1!, j.Dupla2! })
            .DistinctBy(d => d.Id)
            .ToDictionary(d => d.Id, d => new[] { d.Jogador1Id, d.Jogador2Id!.Value });

        return Medir(jogos, ocupantes);
    }

    [Theory]
    [InlineData(12, 2)]
    [InlineData(15, 2)]
    [InlineData(18, 3)]
    [InlineData(24, 3)]
    public async Task A_fase_de_grupos_nao_cansa_nem_entedia_alem_do_aceitavel(int duplas, int quadras)
    {
        // Os MESMOS tetos do DescansoNaGradeTests. Não são apertados de propósito: o alvo é o
        // absurdo, não engessar o encaixe. Com a fila saindo grupo a grupo, a sequência
        // estourava aqui.
        var q = await GradeDoTorneioAsync(duplas, quadras);

        var caso = $"{duplas} duplas em {quadras} quadras: "
                 + $"pior sequência {q.PiorSequencia}, pior espera {q.PiorEspera}";

        Assert.True(q.PiorSequencia <= 8, $"jogos demais colados — {caso}");
        Assert.True(q.PiorEspera <= 6, $"espera longa demais — {caso}");
    }

    // Quantas vezes uma dupla voltou à quadra com MENOS de `folga` horários de intervalo.
    //
    // ⚠️ É esta a medida do pedido, e não a `PiorSequencia` do teste acima — elas parecem a
    // mesma e não são. Sequência 2 já quer dizer "jogou, e jogou de novo no horário seguinte",
    // que é exatamente o que o Felipe pediu pra evitar; o teto de 8 do outro teste pega o
    // absurdo, não a emenda. Medir sequência aqui deixaria o teste verde com o defeito dentro.
    private static int Emendas(IReadOnlyList<Partida> jogos, IReadOnlyDictionary<int, int[]> ocupantes, int folga)
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

        int emendas = 0;
        foreach (var lista in porPessoa.Values)
        {
            lista.Sort();
            for (int i = 1; i < lista.Count; i++)
                if (lista[i] - lista[i - 1] < folga) emendas++;
        }
        return emendas;
    }

    [Fact]
    public async Task Numa_grade_folgada_ninguem_volta_pra_quadra_no_horario_seguinte()
    {
        // 12 duplas = 4 grupos de 3 = 12 jogos; 2 quadras = 6 horários; cada dupla joga 2
        // vezes. Espaçar de 2 CABE com sobra aqui — quatro grupos dão quatro jogos diferentes
        // pra alternar. É o caso que o Felipe viu quebrado.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);
        torneio.QuantidadeQuadras = 2;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Include(p => p.Dupla1).Include(p => p.Dupla2).ToListAsync();
        var ocupantes = jogos
            .SelectMany(j => new[] { j.Dupla1!, j.Dupla2! })
            .DistinctBy(d => d.Id)
            .ToDictionary(d => d.Id, d => new[] { d.Jogador1Id, d.Jogador2Id!.Value });

        Assert.Equal(0, Emendas(jogos, ocupantes, folga: GradeDeJogos.HorariosDeDescanso));
    }
}
