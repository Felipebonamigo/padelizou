using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Desfazer o clique errado na Mesa. De celular, no balcão, com fila esperando, tocar no play
// do jogo de baixo ou finalizar a partida errada acontece — e não tinha volta.
//
// ⚠️ Reabrir uma finalizada é MUITO mais que trocar o status: a finalização carimba a fase do
// perdedor, move o Padelímetro dos 4 jogadores e manda o robô CRIAR A FASE SEGUINTE. Estes
// testes existem porque desfazer pela metade é pior que não desfazer.
public class DesfazerDoJogoTests
{
    // ---- Ao Vivo → Agendada ----

    [Fact]
    public async Task Play_errado_volta_pra_agendado_sem_perder_hora_nem_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        var horaAntes = jogo.HorarioPrevisto;
        var quadraAntes = jogo.NomeQuadra;

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ColocarNoAr(jogo.Id);
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);

        await controller.VoltarParaAgendado(jogo.Id);

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal("Agendada", depois!.Status);
        Assert.Null(depois.HorarioInicioReal);
        Assert.Equal(horaAntes, depois.HorarioPrevisto);    // a grade não se mexe
        Assert.Equal(quadraAntes, depois.NomeQuadra);
    }

    [Fact]
    public void Jogo_finalizado_nao_volta_pra_agendado_por_esse_caminho()
    {
        // Pra esse existe o REABRIR, que desfaz a cascata. Voltar direto pra agendado deixaria
        // o Padelímetro aplicado e a fase seguinte de pé.
        var jogo = new Partida { Codigo = "AAA", Status = "Finalizada" };

        var motivo = DesfazerDoJogo.MotivoParaNaoVoltarParaAgendado(jogo);

        Assert.NotNull(motivo);
        Assert.Contains("EM QUADRA", motivo);
    }

    // ---- Finalizada → Ao Vivo ----

    [Fact]
    public async Task Reabrir_desfaz_a_fase_que_aquele_resultado_gerou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in grupos)
            await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);

        // O último jogo de grupo fez nascer a semifinal.
        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.NotEmpty(semis);

        var ultimo = grupos.Last();
        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ReabrirPartida(ultimo.Id);

        var reaberto = await ctx.Partidas.FindAsync(ultimo.Id);
        Assert.Equal("AoVivo", reaberto!.Status);
        Assert.Null(reaberto.VencedorId);

        // A semifinal saiu de cena: ela era consequência do resultado que acabou de sumir.
        Assert.Empty(await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync());

        // E nasce de novo ao confirmar o resultado.
        await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, reaberto, 9, 3);
        Assert.NotEmpty(await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync());
    }

    [Fact]
    public async Task Reabrir_e_RECUSADO_se_a_fase_seguinte_ja_comecou()
    {
        // A trava que importa: apagar uma semifinal com gente na quadra seria muito pior que
        // conviver com um placar errado — que se corrige pelo lápis.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in grupos)
            await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);

        // Uma semifinal entra em quadra.
        var semi = await ctx.Partidas
            .FirstAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal");
        await controller.ColocarNoAr(semi.Id);

        var ultimoDeGrupo = grupos.Last();
        await controller.ReabrirPartida(ultimoDeGrupo.Id);

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("Finalizada", (await ctx.Partidas.FindAsync(ultimoDeGrupo.Id))!.Status);
        // E a semifinal continua de pé, com quem está jogando nela.
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(semi.Id))!.Status);
    }

    [Fact]
    public async Task Reabrir_a_final_devolve_o_torneio_pra_andamento_e_tira_o_campeao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        for (int volta = 0; volta < 6; volta++)
        {
            var pendentes = await ctx.Partidas
                .Where(p => p.CategoriaId == categoria.Id && p.Status != "Finalizada").ToListAsync();
            if (pendentes.Count == 0) break;
            foreach (var jogo in pendentes)
                await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);
        }

        var final = await ctx.Partidas.SingleAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(1, await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ReabrirPartida(final.Id);

        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(final.Id))!.Status);
        Assert.NotEqual("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(0, await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));
    }

    [Fact]
    public async Task Reabrir_desfaz_o_Padelimetro_daquele_jogo()
    {
        // O nível dos 4 jogadores volta ao que era, e a linha some do extrato — senão o
        // gráfico do perfil guardaria para sempre um jogo que não aconteceu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.CategoriaId == categoria.Id);
        await TestInfra.FinalizarComPlacarAsync(ctx, doTorneio, jogo, 9, 3);

        var extrato = await ctx.HistoricosDePadelimetro.Where(h => h.PartidaId == jogo.Id).ToListAsync();
        var niveisAntes = extrato.ToDictionary(h => h.JogadorId, h => h.NivelAntes);

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ReabrirPartida(jogo.Id);

        Assert.Empty(await ctx.HistoricosDePadelimetro.Where(h => h.PartidaId == jogo.Id).ToListAsync());
        foreach (var (jogadorId, nivel) in niveisAntes)
            Assert.Equal(nivel, (await ctx.Jogadores.FindAsync(jogadorId))!.Padelimetro);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_desfaz_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);

        var xereta = new Jogador { Nome = "Xereta", Cpf = "99900000095" };
        ctx.Jogadores.Add(xereta);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPartidasController(ctx, xereta.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.VoltarParaAgendado(jogo.Id));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.ReabrirPartida(jogo.Id));
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);
    }

    // ---- a ordem das fases, que decide o que é "depois" ----

    [Fact]
    public void Fase_desconhecida_nunca_e_tratada_como_posterior()
    {
        // Americano e fases de seed antigo não fazem parte da corrente do mata-mata. Apagar
        // um jogo desses por engano seria bem pior que não apagar.
        var grupo = new Partida { Codigo = "G", Fase = "Grupo A", CategoriaId = 1 };
        var americano = new Partida { Id = 2, Codigo = "AM", Fase = "Americano", CategoriaId = 1 };

        Assert.Equal(-1, DesfazerDoJogo.OrdemDaFase("Americano"));
        Assert.Empty(DesfazerDoJogo.GeradosDepois(grupo, new[] { americano }));
    }

    [Fact]
    public void A_corrente_das_fases_vai_do_grupo_ate_a_final()
    {
        Assert.True(DesfazerDoJogo.OrdemDaFase("Grupo A") < DesfazerDoJogo.OrdemDaFase("Quartas de Final"));
        Assert.True(DesfazerDoJogo.OrdemDaFase("Quartas de Final") < DesfazerDoJogo.OrdemDaFase("Semifinal"));
        Assert.True(DesfazerDoJogo.OrdemDaFase("Semifinal") < DesfazerDoJogo.OrdemDaFase("Final"));
    }
}
