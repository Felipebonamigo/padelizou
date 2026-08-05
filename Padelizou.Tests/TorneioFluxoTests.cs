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

    // A fase seguinte não encosta na anterior: quem joga a semifinal é quem disputa a final,
    // e marcar a final para o minuto em que a semi termina é chamar a mesma dupla de volta pra
    // quadra sem descanso. Uma rodada de folga entre as fases (GradeDeJogos.AberturaDaProximaFase)
    // — a mesma conta que a projeção mostra na tela, pra grade e previsão não divergirem.
    [Fact]
    public async Task Cada_fase_deixa_uma_rodada_de_folga_pra_seguinte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 4;   // sobram quadras: é aí que a conta corrida furava
        torneio.TempoPrevistoPartidaMinutos = 30;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        var fimDosGrupos = grupos.Max(p => p.HorarioPrevisto!.Value);

        foreach (var jogo in grupos)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.All(semis, p => Assert.True(p.HorarioPrevisto >= fimDosGrupos.AddMinutes(60),
            $"semifinal às {p.HorarioPrevisto:HH:mm} colada no último jogo de grupo ({fimDosGrupos:HH:mm})"));

        foreach (var semi in semis)
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, semi, 9, 5);

        var fimDasSemis = semis.Max(p => p.HorarioPrevisto!.Value);
        var final = await ctx.Partidas
            .SingleAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");

        Assert.True(final.HorarioPrevisto >= fimDasSemis.AddMinutes(60),
            $"final às {final.HorarioPrevisto:HH:mm} colada na semifinal ({fimDasSemis:HH:mm})");
    }

    // Duas categorias avançando ao mesmo tempo dividem as quadras do mesmo horário.
    //
    // ⚠️ Antes, cada rodada nova era ancorada no último jogo marcado do TORNEIO INTEIRO, e o
    // efeito era enfileirar as categorias: com 5 quadras e 5 categorias, cada semifinal
    // esperava a semifinal alheia acabar e quatro quadras ficavam paradas — o torneio varava
    // a madrugada com a quadra vazia. Era o preço de o encaixe não saber o que já estava
    // marcado; agora ele sabe, e emenda em paralelo.
    [Fact]
    public async Task Duas_categorias_avancam_dividindo_as_quadras_do_mesmo_horario()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, primeira, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 4;
        torneio.TempoPrevistoPartidaMinutos = 30;

        // Uma segunda categoria com gente DIFERENTE: quem joga uma não joga a outra, então
        // nada impede as duas de ocuparem o mesmo horário.
        var segunda = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "CAT3M", TorneioId = torneio.Id };
        ctx.Categorias.Add(segunda);
        await ctx.SaveChangesAsync();

        for (int i = 0; i < 6; i++)
        {
            var j1 = TestInfra.NovoJogador(100 + i * 2);
            var j2 = TestInfra.NovoJogador(101 + i * 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = segunda, Jogador1 = j1, Jogador2 = j2 });
        }
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var semis = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && p.Fase == "Semifinal").ToListAsync();

        Assert.Equal(4, semis.Count);                                  // 2 por categoria
        Assert.All(semis, s => Assert.NotNull(s.HorarioPrevisto));

        // As quatro cabem nas 4 quadras: as duas categorias jogam a semifinal JUNTAS.
        Assert.Single(semis.Select(s => s.HorarioPrevisto).Distinct());
        Assert.Equal(2, semis.Select(s => s.CategoriaId).Distinct().Count());
    }

    // A garantia que a mudança acima NÃO pode perder: ninguém é chamado pra duas quadras ao
    // mesmo tempo. Enfileirar as categorias garantia isso de graça; emendar em paralelo só é
    // seguro porque o encaixe recebe o que já está marcado e confere PESSOA por PESSOA.
    [Fact]
    public async Task Emendar_em_paralelo_nao_poe_ninguem_em_duas_quadras_ao_mesmo_tempo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, primeira, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 5;
        torneio.TempoPrevistoPartidaMinutos = 11;

        // A segunda categoria repete OS MESMOS jogadores em outros pares — é o desenho da
        // chave direta (mata-mata paralelo), onde cada um disputa duas coisas no mesmo dia.
        var segunda = new Categoria { Nome = "Mata-Mata Geral", Codigo = "MMG", TorneioId = torneio.Id };
        ctx.Categorias.Add(segunda);
        await ctx.SaveChangesAsync();

        var jogadores = await ctx.Jogadores.Where(j => j.Nome.StartsWith("Jogador")).OrderBy(j => j.Id).ToListAsync();
        for (int i = 0; i < 6; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = segunda,
                Jogador1 = jogadores[i],
                Jogador2 = jogadores[jogadores.Count - 1 - i],
            });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        // Finaliza tudo o que aparecer, fase após fase, até o torneio acabar.
        for (int volta = 0; volta < 10; volta++)
        {
            var pendentes = await ctx.Partidas
                .Where(p => p.TorneioId == torneio.Id && p.Status != "Finalizada").ToListAsync();
            if (pendentes.Count == 0) break;

            foreach (var jogo in pendentes)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);
        }

        var todas = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id && p.HorarioPrevisto != null)
            .ToListAsync();

        foreach (var noHorario in todas.GroupBy(p => p.HorarioPrevisto!.Value))
        {
            var pessoas = noHorario
                .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
                .Where(id => id != null)
                .ToList();

            Assert.Equal(pessoas.Count, pessoas.Distinct().Count());

            var quadras = noHorario.Select(p => p.NomeQuadra).Where(q => q != null).ToList();
            Assert.Equal(quadras.Count, quadras.Distinct().Count());
        }
    }

    [Fact]
    public async Task Mata_mata_respeita_o_expediente_e_vira_o_dia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        // Uma quadra só, dia curto: os 6 jogos de grupo enchem o dia (9h..14h, teto de 14h)
        // e o mata-mata tem que cair no dia seguinte na abertura dos DEMAIS dias.
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 60;
        torneio.HoraInicioDoDia = new TimeSpan(9, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(7, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(14, 0, 0);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();

        // 6 jogos de 1h numa quadra enchem 9h..14h; o próximo passaria do teto, então o
        // mata-mata abre o dia seguinte às 7h — a abertura dos DEMAIS dias, não as 9h da
        // sexta nem as 15h em que a fase de grupos parou.
        Assert.Equal(new DateTime(2026, 7, 2, 7, 0, 0), semis.Min(p => p.HorarioPrevisto));
        Assert.All(semis, p => Assert.Equal(new DateTime(2026, 7, 2), p.HorarioPrevisto!.Value.Date));
        Assert.All(semis, p => Assert.True(p.HorarioPrevisto!.Value.TimeOfDay <= new TimeSpan(14, 0, 0)));
    }

    // Motor genérico: QUALQUER nº de grupos fecha o mata-mata. Regra de 05/08/2026: TODO
    // classificado avança — o quadro cresce pra caber todo mundo e os melhores pegam BYE
    // (pulam a primeira rodada), em vez de os piores 2ºs serem cortados sem jogar.
    [Theory]
    [InlineData(9, "Quartas de Final", 2)]  // 3 grupos → 6 no quadro de 8: 2 jogos + 2 byes
    [InlineData(15, "Oitavas de Final", 2)] // 5 grupos → 10 no quadro de 16: 2 jogos + 6 byes
    [InlineData(18, "Oitavas de Final", 4)] // 6 grupos → 12 no quadro de 16: 4 jogos + 4 byes
    [InlineData(24, "Oitavas de Final", 8)] // 8 grupos → 16 no quadro de 16: cheio, sem bye
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
