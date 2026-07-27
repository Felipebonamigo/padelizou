using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Tests;

// Fluxo REAL do torneio, do sorteio ao campeão, passando pelos robôs de avanço.
// Este arquivo existe por causa de dois bugs que chegaram a produção sem ninguém ver:
//   1. GerarChaves gravava Fase="Grupo X" mas o gatilho do mata-mata só aceitava
//      "Fase de Grupos" — o torneio travava ao fim dos grupos (corrigido 25/07/2026).
//   2. Os robôs criavam Partida sem Codigo (coluna NOT NULL) — INSERT falharia
//      no primeiro avanço automático real (corrigido 25/07/2026).
public class TorneioFluxoTests
{
    [Fact]
    public async Task GerarChaves_com_6_duplas_cria_2_grupos_de_3_e_6_jogos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var grupos = await ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == categoria.Id).ToListAsync();
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        var partidas = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        Assert.Equal(2, grupos.Count);
        Assert.All(duplas, d => Assert.False(string.IsNullOrEmpty(d.Grupo)));       // Grupo string (mata-mata usa)
        Assert.All(duplas, d => Assert.NotNull(d.GrupoTorneioId));                  // FK (classificação usa)
        Assert.Equal(6, partidas.Count);                                            // 2 grupos de 3 → 3 jogos cada
        Assert.All(partidas, p => Assert.StartsWith("Grupo ", p.Fase));
        Assert.All(partidas, p => Assert.False(string.IsNullOrEmpty(p.Codigo)));    // NOT NULL no banco
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // Regra do resto: total não múltiplo de 3 fecha os melhores em grupos de 2.
    [Theory]
    [InlineData(13, 5, 11)] // resto 1 → 2 grupos de 2 + 3 grupos de 3 → 2*1 + 3*3 = 11 jogos
    [InlineData(14, 5, 13)] // resto 2 → 1 grupo de 2 + 4 grupos de 3 → 1 + 12 = 13 jogos
    [InlineData(12, 4, 12)] // múltiplo de 3 → 4 grupos de 3 → 12 jogos
    [InlineData(2, 1, 1)]   // mínimo: 2 duplas → 1 grupo único com 1 jogo
    public async Task GerarChaves_regra_do_resto(int qtdDuplas, int gruposEsperados, int jogosEsperados)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        Assert.Equal(gruposEsperados, await ctx.Set<GrupoTorneio>().CountAsync(g => g.CategoriaId == categoria.Id));
        Assert.Equal(jogosEsperados, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task GerarChaves_exige_organizador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, 6);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000098" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, intruso.Id);
        var resultado = await controller.GerarChaves(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal(0, await ctx.Partidas.CountAsync());
    }

    [Fact]
    public async Task Fluxo_completo_grupos_semifinal_final_campeao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // 1. Finaliza TODOS os jogos de grupo — dupla1 sempre vence.
        var jogosDeGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in jogosDeGrupo)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        // 2. O robô deve ter criado a SEMIFINAL sozinho (2 grupos → semi direto).
        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.Equal(2, semis.Count);
        Assert.All(semis, p => Assert.False(string.IsNullOrEmpty(p.Codigo))); // regressão: robô sem Codigo

        // Perdedor de jogo de grupo NÃO pode ser carimbado com "Grupo X".
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.DoesNotContain(duplas, d => d.UltimaFase != null && d.UltimaFase.StartsWith("Grupo "));

        // 3. Finaliza as semis → robô cria a FINAL.
        foreach (var semi in semis)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, semi, 9, 5);

        var final = await ctx.Partidas
            .SingleOrDefaultAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");
        Assert.NotNull(final);
        Assert.False(string.IsNullOrEmpty(final!.Codigo));

        // Perdedores das semis carimbados com "Semifinal".
        duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.Equal(2, duplas.Count(d => d.UltimaFase == "Semifinal"));

        // 4. Finaliza a final → campeão coroado e torneio encerrado.
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, final, 9, 7);

        duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.Equal(1, duplas.Count(d => d.UltimaFase == "Campeao"));
        Assert.Equal(1, duplas.Count(d => d.UltimaFase == "Final"));
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // TODO jogo do torneio nasce com horário previsto. Os do mata-mata só existem depois que a
    // fase de grupos acaba, e nasciam sem hora nenhuma: o jogador via "a definir" justo na fase
    // que mais importa.
    [Fact]
    public async Task Todo_jogo_do_torneio_nasce_com_horario_previsto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        Assert.All(grupos, p => Assert.NotNull(p.HorarioPrevisto));
        var fimDosGrupos = grupos.Max(p => p.HorarioPrevisto!.Value);

        foreach (var jogo in grupos)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        // Semifinal criada pelo robô: com hora, e DEPOIS do último jogo de grupo.
        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.All(semis, p => Assert.NotNull(p.HorarioPrevisto));
        Assert.All(semis, p => Assert.True(p.HorarioPrevisto > fimDosGrupos));
        // 2 quadras: as duas semis rodam juntas.
        Assert.Single(semis.Select(p => p.HorarioPrevisto).Distinct());

        foreach (var semi in semis)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, semi, 9, 5);

        var final = await ctx.Partidas
            .SingleAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");
        Assert.NotNull(final.HorarioPrevisto);
        Assert.True(final.HorarioPrevisto > semis.Max(p => p.HorarioPrevisto));

        // Nenhum jogo do torneio, em fase nenhuma, ficou sem hora.
        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto == null).ToListAsync());
    }

    [Fact]
    public async Task Mata_mata_respeita_o_expediente_e_vira_o_dia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        // Uma quadra só, expediente curto: os 6 jogos de grupo enchem o dia e o mata-mata
        // tem que cair no dia seguinte no horário de abertura, não às 3h da manhã.
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 60;
        torneio.HoraInicioDoDia = new TimeSpan(9, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(15, 0, 0);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();

        // 6 jogos de 1h numa quadra enchem 9h..14h; às 15h não cabe mais (terminaria 16h),
        // então o mata-mata abre o dia seguinte às 9h — o horário de abertura, não 15h.
        Assert.Equal(new DateTime(2026, 7, 2, 9, 0, 0), semis.Min(p => p.HorarioPrevisto));
        Assert.All(semis, p => Assert.Equal(new DateTime(2026, 7, 2), p.HorarioPrevisto!.Value.Date));
        Assert.All(semis, p => Assert.True(p.HorarioPrevisto!.Value.TimeOfDay < new TimeSpan(15, 0, 0)));
    }

    // Motor genérico (25/07/2026): antes o mata-mata só fechava com 1/2/4/8 grupos.
    // Agora QUALQUER nº de grupos fecha: todos os 1ºs + melhores 2ºs completam o quadro.
    [Theory]
    [InlineData(9, "Semifinal", 2)]         // 3 grupos → quadro de 4 (3 primeiros + melhor 2º)
    [InlineData(15, "Quartas de Final", 4)] // 5 grupos → quadro de 8 (5 primeiros + 3 melhores 2ºs)
    [InlineData(18, "Quartas de Final", 4)] // 6 grupos → quadro de 8 (6 primeiros + 2 melhores 2ºs)
    [InlineData(24, "Oitavas de Final", 8)] // 8 grupos → quadro de 16 (todos 1ºs e 2ºs)
    public async Task Qualquer_numero_de_grupos_fecha_o_mata_mata(int qtdDuplas, string primeiraFase, int jogosEsperados)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogosDeGrupo = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in jogosDeGrupo)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var primeiraFaseJogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == primeiraFase).ToListAsync();
        Assert.Equal(jogosEsperados, primeiraFaseJogos.Count);

        // Nenhuma dupla aparece 2x na primeira fase do mata-mata.
        var ids = primeiraFaseJogos.SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        // E o fluxo segue até coroar um campeão.
        string? fase = primeiraFase;
        while (fase != null)
        {
            var jogos = await ctx.Partidas
                .Where(p => p.CategoriaId == categoria.Id && p.Fase == fase && p.Status != "Finalizada").ToListAsync();
            foreach (var jogo in jogos)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 4);
            fase = Padelizou.Services.ChaveamentoMataMata.ProximaFase(fase);
            if (fase != null)
                Assert.True(await ctx.Partidas.AnyAsync(p => p.CategoriaId == categoria.Id && p.Fase == fase),
                    $"A fase '{fase}' deveria ter sido gerada pelo robô.");
        }

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.Equal(1, duplas.Count(d => d.UltimaFase == "Campeao"));
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // Categoria mínima: 1 grupo só também fecha com Final e campeão (antes travava pra sempre).
    [Theory]
    [InlineData(3)] // 1 grupo de 3 → Final 1º x 2º
    [InlineData(2)] // 1 grupo de 2 (chave direta) → Final entre as duas
    public async Task Um_grupo_so_tambem_coroa_campeao(int qtdDuplas)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogosDeGrupo = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in jogosDeGrupo)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var final = await ctx.Partidas
            .SingleOrDefaultAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");
        Assert.NotNull(final);

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, final!, 9, 6);

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.Equal(1, duplas.Count(d => d.UltimaFase == "Campeao"));
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Fluxo_com_4_grupos_gera_quartas_depois_semis_depois_final()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12); // 4 grupos de 3
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogosDeGrupo = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        foreach (var jogo in jogosDeGrupo)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        // 4 grupos → 4 jogos de Quartas (1º de cada grupo x 2º do grupo oposto).
        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final").ToListAsync();
        Assert.Equal(4, quartas.Count);

        foreach (var q in quartas)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, q, 9, 4);
        Assert.Equal(2, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal"));

        var semis = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();
        foreach (var s in semis)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, s, 9, 4);
        Assert.Equal(1, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final"));

        // Perdedores das quartas carimbados corretamente.
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.Equal(4, duplas.Count(d => d.UltimaFase == "Quartas de Final"));
    }
}
