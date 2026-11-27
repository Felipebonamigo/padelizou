using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TRETA DE PONTA A PONTA, COM OS GRUPOS DE VERDADE (27/11/2026).
//
// 🗣️ Felipe: "isso vai configurar correto depois quem passa também, com as regras que já tinham
// sido ditas?". Branco 8 duplas em 4/4, Preto 10 em 3/3/4 (com folga pros melhores 1ºs), Verde 7
// em 3/4. Sorteio de verdade (GerarChaves), todos os jogos de grupo jogados, e o robô monta o
// resto. As regras (PDF + organizador): os 2 primeiros de cada grupo vão pro Principal, o ÚLTIMO
// vai pro DTN, o meio é eliminado; no Preto os 2 melhores 1ºs (por jogo) folgam até a semi, e na
// DTN os 2 "melhores últimos" fazem a semi e o pior espera na final.
public class TretaPontaAPontaTests
{
    private sealed record Modulo(Categoria Cat, List<List<ClassificacaoDeGrupos.Linha>> Grupos, List<Partida> Abertura);

    private static async Task<Modulo> JogarAsync(int duplas, int grupos, bool campanha)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, cat, org) = TestInfra.MontarTorneio(ctx, duplas);
        var sabado = DateTime.Today.AddDays(14);
        while (sabado.DayOfWeek != DayOfWeek.Saturday) sabado = sabado.AddDays(1);
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 3;
        torneio.TempoPrevistoPartidaMinutos = 30;
        torneio.ForaDoRanking = true;
        foreach (var q in new[] { "Q1", "Q2", "Q3" }) ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = q });
        cat.QuantidadeGrupos = grupos;
        cat.DuplasNoDtnPorGrupo = 1;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        ctx.ChangeTracker.Clear();
        if (campanha)
        {
            var c = await ctx.Categorias.FirstAsync(x => x.Id == cat.Id);
            c.CruzamentoDoMataMata = AberturaPelaCampanha.Marca;
            await ctx.SaveChangesAsync();
            ctx.ChangeTracker.Clear();
        }

        // Todo jogo de grupo: quem tem o MENOR Id vence. O perdedor faz MENOS games quanto mais
        // ao fim do alfabeto está o grupo (A: 4, B: 2, C: 0) — então o 1º do ÚLTIMO grupo tem o
        // melhor saldo por jogo. É o caso que separa "melhores 1ºs" de "1º do A e do B".
        var grupoDaDupla = await ctx.Duplas.Where(d => d.CategoriaId == cat.Id)
            .ToDictionaryAsync(d => d.Id, d => d.Grupo ?? "A");
        foreach (var p in await ctx.Partidas.Where(p => p.CategoriaId == cat.Id).ToListAsync())
        {
            bool venceO1 = p.Dupla1Id < p.Dupla2Id;
            p.Status = "Finalizada";
            p.VencedorId = venceO1 ? p.Dupla1Id : p.Dupla2Id;
            int perdedor = Math.Max(0, 4 - 2 * (grupoDaDupla[p.Dupla1Id][0] - 'A'));
            p.GamesDupla1 = venceO1 ? 6 : perdedor;
            p.GamesDupla2 = venceO1 ? perdedor : 6;
        }
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas()).MontarMataMataDosGruposAsync(cat.Id, torneio.Id);
        ctx.ChangeTracker.Clear();

        // A classificação pela MESMA régua do sistema, pra saber quem é 1º, 2º... e último.
        var gruposDb = await ctx.Set<GrupoTorneio>().Include(g => g.Duplas).Where(g => g.CategoriaId == cat.Id)
            .OrderBy(g => g.Nome).ToListAsync();
        var deGrupo = await ctx.Partidas.AsNoTracking().Where(p => p.CategoriaId == cat.Id && p.Fase.StartsWith("Grupo")).ToListAsync();
        var rankings = gruposDb.Select(g => ClassificacaoDeGrupos.Ordenar(g.Duplas, deGrupo, ClassificacaoDeGrupos.SemPontos)).ToList();
        var abertura = await ctx.Partidas.AsNoTracking().Include(p => p.Categoria)
            .Where(p => p.TorneioId == torneio.Id && !p.Fase.StartsWith("Grupo")).ToListAsync();
        var duplasDtn = await ctx.Duplas.AsNoTracking().Where(d => d.Categoria.PapelDaChave == ChaveDtn.Dtn).ToListAsync();
        // Traduz as cópias da DTN de volta pra dupla original (mesmos jogadores).
        foreach (var p in abertura.Where(p => p.Categoria.PapelDaChave == ChaveDtn.Dtn))
        {
            int Original(int copiaId)
            {
                var copia = duplasDtn.First(d => d.Id == copiaId);
                return gruposDb.SelectMany(g => g.Duplas).First(d => d.Jogador1Id == copia.Jogador1Id && d.Jogador2Id == copia.Jogador2Id).Id;
            }
            p.Dupla1Id = Original(p.Dupla1Id);
            p.Dupla2Id = Original(p.Dupla2Id);
        }
        return new Modulo(cat, rankings, abertura);
    }

    private static int[] Par(Partida p) => new[] { p.Dupla1Id, p.Dupla2Id }.OrderBy(x => x).ToArray();
    private static int[] Par(int a, int b) => new[] { a, b }.OrderBy(x => x).ToArray();
    private static List<Partida> Principal(Modulo m) => m.Abertura.Where(p => p.Categoria.PapelDaChave == null).ToList();
    private static List<Partida> Dtn(Modulo m) => m.Abertura.Where(p => p.Categoria.PapelDaChave == ChaveDtn.Dtn).ToList();
    private static int Id(Modulo m, int grupo, int posicao) => m.Grupos[grupo][posicao - 1].Dupla.Id;
    private static int Ultimo(Modulo m, int grupo) => m.Grupos[grupo][^1].Dupla.Id;

    [Fact]
    public async Task Branco_8_duplas_em_4_e_4()
    {
        var m = await JogarAsync(duplas: 8, grupos: 2, campanha: false);

        Assert.Equal(new[] { 4, 4 }, m.Grupos.Select(g => g.Count).ToArray());
        // Principal: semis 1A×2B e 1B×2A.
        var semis = Principal(m);
        Assert.All(semis, p => Assert.Equal("Semifinal", p.Fase));
        Assert.Equal(2, semis.Count);
        Assert.Contains(semis, p => Par(p).SequenceEqual(Par(Id(m, 0, 1), Id(m, 1, 2))));
        Assert.Contains(semis, p => Par(p).SequenceEqual(Par(Id(m, 1, 1), Id(m, 0, 2))));
        // DTN: final 4ºA × 4ºB. Os 3ºs, fora das duas chaves.
        var dtn = Assert.Single(Dtn(m));
        Assert.Equal("Final", dtn.Fase);
        Assert.Equal(Par(Ultimo(m, 0), Ultimo(m, 1)), Par(dtn));
        Assert.DoesNotContain(m.Abertura, p => Par(p).Contains(Id(m, 0, 3)) || Par(p).Contains(Id(m, 1, 3)));
    }

    [Fact]
    public async Task Verde_7_duplas_em_3_e_4()
    {
        var m = await JogarAsync(duplas: 7, grupos: 2, campanha: false);

        Assert.Equal(new[] { 3, 4 }, m.Grupos.Select(g => g.Count).ToArray());
        var semis = Principal(m);
        Assert.Equal(2, semis.Count);
        Assert.Contains(semis, p => Par(p).SequenceEqual(Par(Id(m, 0, 1), Id(m, 1, 2))));
        Assert.Contains(semis, p => Par(p).SequenceEqual(Par(Id(m, 1, 1), Id(m, 0, 2))));
        // DTN: 3ºA (último do grupo de 3) × 4ºB. O 3º do grupo de 4 fica fora.
        var dtn = Assert.Single(Dtn(m));
        Assert.Equal(Par(Ultimo(m, 0), Ultimo(m, 1)), Par(dtn));
        Assert.DoesNotContain(m.Abertura, p => Par(p).Contains(Id(m, 1, 3)));
    }

    [Fact]
    public async Task Preto_10_duplas_em_3_3_4_com_folga_pros_melhores_1os()
    {
        var m = await JogarAsync(duplas: 10, grupos: 3, campanha: true);

        Assert.Equal(new[] { 3, 3, 4 }, m.Grupos.Select(g => g.Count).ToArray());

        // Principal: quartas com 4 duplas; os 2 melhores 1ºs POR JOGO ficam de fora (folga).
        double PorJogo(ClassificacaoDeGrupos.Linha l, Func<ClassificacaoDeGrupos.Linha, int> v) => (double)v(l) / l.Jogos;
        var lideres = m.Grupos.Select(g => g[0])
            .OrderByDescending(l => PorJogo(l, x => x.Vitorias)).ThenByDescending(l => PorJogo(l, x => x.Saldo))
            .ToList();
        var quartas = Principal(m);
        Assert.Equal(2, quartas.Count);
        Assert.All(quartas, p => Assert.Equal("Quartas de Final", p.Fase));
        var naQuartas = quartas.SelectMany(Par).ToHashSet();
        Assert.DoesNotContain(lideres[0].Dupla.Id, naQuartas);
        Assert.DoesNotContain(lideres[1].Dupla.Id, naQuartas);
        Assert.Contains(lideres[2].Dupla.Id, naQuartas);
        foreach (var g in Enumerable.Range(0, 3)) Assert.Contains(Id(m, g, 2), naQuartas);   // os três 2ºs jogam

        // DTN: os últimos (3ºA, 3ºB, 4ºC); os 2 "melhores" (por jogo) fazem a semi, o pior espera.
        var ultimos = m.Grupos.Select(g => g[^1])
            .OrderBy(l => PorJogo(l, x => x.Vitorias)).ThenBy(l => PorJogo(l, x => x.Saldo)).ThenBy(l => PorJogo(l, x => x.GamesPro))
            .ToList();
        var semiDtn = Assert.Single(Dtn(m));
        Assert.Equal("Semifinal", semiDtn.Fase);
        Assert.Equal(Par(ultimos[1].Dupla.Id, ultimos[2].Dupla.Id), Par(semiDtn));
        // O 3º do grupo de 4 (o "meio") fica fora das duas chaves.
        Assert.DoesNotContain(m.Abertura, p => Par(p).Contains(Id(m, 2, 3)));
    }
}
