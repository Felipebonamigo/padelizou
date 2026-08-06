using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A pergunta "quem venceu?" tinha TRÊS respostas no sistema, e elas discordavam. Estes testes
// existem porque essa divergência eliminou uma dupla errada num torneio de verdade
// (Interno Los Corneteiros, 05/08/2026, jogo 338 da 6ª Feminina).
public class QuemVenceuTests
{
    private static Partida Jogo(int? g1, int? g2, int? s1 = null, int? s2 = null) => new()
    {
        Id = 1, Codigo = "AAA", Dupla1Id = 10, Dupla2Id = 20,
        GamesDupla1 = g1, GamesDupla2 = g2, SetsDupla1 = s1, SetsDupla2 = s2,
    };

    [Fact]
    public void Um_lado_SEM_placar_perde_pra_quem_marcou()
    {
        // O caso real: 4 x (em branco). A conta antiga comparava os campos nuláveis, e
        // `4 > null` é FALSE em C# — a vitória caía pra dupla 2, que não marcou nada.
        Assert.Equal(10, QuemVenceu.Da(Jogo(4, null)));
        Assert.Equal(20, QuemVenceu.Da(Jogo(null, 4)));
    }

    [Fact]
    public void Zero_a_zero_nao_tem_vencedor()
    {
        // Sem isto, o desempate silencioso da comparação escolhia um lado — e foi assim que
        // a Final do Mata-Mata Geral (jogo 416) foi encerrada 0 x 0 com campeão definido.
        Assert.Null(QuemVenceu.Da(Jogo(0, 0)));
        Assert.Null(QuemVenceu.Da(Jogo(null, null)));
        Assert.Null(QuemVenceu.Da(Jogo(3, 3)));
    }

    [Fact]
    public void Sets_mandam_mais_que_games()
    {
        // Quem faz 2 sets a 1 venceu, mesmo perdendo no total de games.
        Assert.Equal(10, QuemVenceu.Da(Jogo(g1: 10, g2: 14, s1: 2, s2: 1)));
    }

    [Fact]
    public void Sets_empatados_caem_pro_placar_de_games()
    {
        Assert.Equal(20, QuemVenceu.Da(Jogo(g1: 4, g2: 6, s1: 1, s2: 1)));
        Assert.Equal(10, QuemVenceu.Da(Jogo(g1: 6, g2: 4, s1: 0, s2: 0)));
    }

    [Fact]
    public void Nao_deixa_finalizar_sem_placar_nem_empatado()
    {
        Assert.NotNull(QuemVenceu.MotivoParaNaoFinalizar(Jogo(null, null)));
        Assert.NotNull(QuemVenceu.MotivoParaNaoFinalizar(Jogo(0, 0)));
        Assert.NotNull(QuemVenceu.MotivoParaNaoFinalizar(Jogo(5, 5)));
        Assert.Null(QuemVenceu.MotivoParaNaoFinalizar(Jogo(6, 4)));
        Assert.Null(QuemVenceu.MotivoParaNaoFinalizar(Jogo(4, null)));
    }

    // ---- a divergência que causou o estrago ----

    [Fact]
    public async Task A_tabela_do_grupo_concorda_com_o_vencedor_gravado()
    {
        // Reprodução do jogo 338: 4 x (em branco). Antes, o VencedorId dizia uma coisa e a
        // classificação contava a vitória pra OUTRA dupla — a que o registro dava como
        // perdedora foi ao mata-mata, e a vencedora ficou de fora.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.CategoriaId == categoria.Id);
        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = null;          // o lado que ninguém preencheu
        jogo.SetsDupla1 = null;
        jogo.SetsDupla2 = null;
        await ctx.SaveChangesAsync();

        await controller.FinalizarPartida(jogo.Id);

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(depois!.Dupla1Id, depois.VencedorId);

        // E a tabela do grupo tem que dizer o MESMO.
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        var deGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Status == "Finalizada").ToListAsync();
        var classificacao = ClassificacaoDeGrupos.Calcular(duplas, deGrupo, 2);

        var vencedora = classificacao.FirstOrDefault(c => c.DuplaId == depois.Dupla1Id);
        var perdedora = classificacao.FirstOrDefault(c => c.DuplaId == depois.Dupla2Id);

        Assert.True((vencedora?.Vitorias ?? 0) > (perdedora?.Vitorias ?? 0),
            "a tabela do grupo tem que dar a vitória pra mesma dupla que o jogo registrou");
    }

    [Fact]
    public async Task Corrigir_o_placar_de_um_jogo_FINALIZADO_troca_o_vencedor_e_refaz_a_fase_seguinte()
    {
        // A raiz do "atualizei o placar errado e ele não corrigiu o chaveamento": o vencedor
        // só era calculado na TRANSIÇÃO pra finalizada. Depois disso, mexer no placar trocava
        // os games e deixava o VencedorId antigo — e a fase seguinte, nascida dele, também.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);

        var semi = await ctx.Partidas
            .FirstAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal");
        await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, semi, 9, 3);

        var venceuAntes = (await ctx.Partidas.FindAsync(semi.Id))!.VencedorId;
        Assert.NotNull(venceuAntes);

        // O placar estava trocado: quem ganhou foi a OUTRA dupla. Corrige com placar que
        // aponta o outro lado, sem depender de qual delas venceu antes.
        bool dupla1TinhaVencido = venceuAntes == semi.Dupla1Id;
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.ControlePlacar(semi.Id, "Finalizada",
            gamesDupla1: dupla1TinhaVencido ? 3 : 9,
            gamesDupla2: dupla1TinhaVencido ? 9 : 3,
            null, null);

        var corrigida = await ctx.Partidas.FindAsync(semi.Id);
        Assert.NotEqual(venceuAntes, corrigida!.VencedorId);   // o vencedor acompanhou o placar
        Assert.Equal(dupla1TinhaVencido ? corrigida.Dupla2Id : corrigida.Dupla1Id, corrigida.VencedorId);

        // E a final, que tinha nascido do vencedor antigo, não pode ter ficado com a dupla
        // que acabou de perder.
        var final = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Final").ToListAsync();
        Assert.All(final, f => Assert.True(f.Dupla1Id != venceuAntes && f.Dupla2Id != venceuAntes,
            "a final continuou com a dupla que perdeu depois da correção"));
    }

    [Fact]
    public async Task Finalizar_sem_placar_e_RECUSADO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.CategoriaId == categoria.Id);

        await controller.FinalizarPartida(jogo.Id);   // sem placar nenhum

        Assert.Equal("Agendada", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }
}
