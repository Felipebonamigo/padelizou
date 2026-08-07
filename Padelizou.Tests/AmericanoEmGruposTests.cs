using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O Americano dividido em grupos, do sorteio ao campeão (Felipe, 06/08/2026).
//
// A regra: cada um joga com cada um DO SEU GRUPO sem repetir parceiro, todos jogam a mesma
// quantidade de jogos, os primeiros de cada grupo formam um GRUPO FINAL, e o título se decide
// lá — nunca na soma dos grupos, que juntaria gente que nunca se enfrentou.
public class AmericanoEmGruposTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador org)
        MontarAmericano(int inscritos)
    {
        var ctx = TestInfra.NovoContexto();

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "Americano", Codigo = "AM1", Status = "Chaves em Sorteio",
            Formato = "Americano", QuantidadeQuadras = 4, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 8, 20, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        for (int i = 1; i <= inscritos; i++)
        {
            var j = new Jogador { Nome = $"Jogador {i:00}", Cpf = $"888000000{i:00}" };
            ctx.Jogadores.Add(j);
            ctx.SaveChanges();
            ctx.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = j.Id });
        }
        ctx.SaveChanges();

        return (ctx, torneio, categoria, org);
    }

    // Joga TODAS as partidas abertas, uma leva por vez. Cada leva pode fazer nascer a próxima
    // (o grupo final), então repete até o torneio parar de criar jogo.
    private static async Task JogarTudoAsync(DbPadelContext ctx, Jogador org, int torneioId)
    {
        var push = Substitute.For<IPushNotificationService>();

        for (int volta = 0; volta < 60; volta++)
        {
            var abertas = await ctx.Partidas
                .Where(p => p.TorneioId == torneioId && p.Status != "Finalizada")
                .OrderBy(p => p.Id).ToListAsync();
            if (abertas.Count == 0) return;

            foreach (var partida in abertas)
            {
                // Placar variado e determinístico: sem variação, todo mundo empata e o teste
                // cairia sempre no caminho do empate em vez do caminho normal.
                int g1 = 9, g2 = partida.Id % 8;
                await TestInfra.NovoPartidasController(ctx, org.Id, push: push)
                    .ControlePlacar(partida.Id, "Finalizada", g1, g2, partida.NomeQuadra, null);
            }
        }
    }

    private static Task SortearAsync(DbPadelContext ctx, Jogador org, int torneioId, int grupos) =>
        TestInfra.NovoTorneiosController(ctx, org.Id)
            .GerarRodadasAmericano(torneioId, new DateTime(2026, 8, 20, 19, 0, 0), grupos);

    // ── O SORTEIO ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dez_pessoas_em_dois_grupos_de_cinco()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);

        var salva = await ctx.Categorias.FindAsync(categoria.Id);
        Assert.Equal(2, salva!.GruposAmericano);
        Assert.Equal(2, salva.PassamPorGrupo);

        // Cinco em cada grupo, e ninguém sem grupo.
        var porGrupo = await ctx.InscricoesAmericanas
            .Where(i => i.CategoriaId == categoria.Id)
            .GroupBy(i => i.Grupo)
            .Select(g => new { Grupo = g.Key, Quantos = g.Count() })
            .ToListAsync();

        Assert.Equal(2, porGrupo.Count);
        Assert.All(porGrupo, g => Assert.Equal(5, g.Quantos));
        Assert.All(porGrupo, g => Assert.NotNull(g.Grupo));
    }

    // Dentro de cada grupo a garantia do formato continua valendo.
    [Fact]
    public async Task Cada_um_joga_com_cada_um_DO_SEU_grupo()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);

        var partidas = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id).ToListAsync();

        var deA = await ctx.InscricoesAmericanas
            .Where(i => i.CategoriaId == categoria.Id && i.Grupo == "A")
            .Select(i => i.JogadorId).ToListAsync();

        // Toda partida do grupo A só tem gente do grupo A.
        foreach (var p in partidas.Where(p => FaseDoAmericano.GrupoDe(p.Fase) == "A"))
        {
            foreach (var jogador in new[] { p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value,
                                            p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value })
            {
                Assert.Contains(jogador, deA);
            }
        }

        // E os 10 pares possíveis dentro do grupo aconteceram.
        var pares = partidas.Where(p => FaseDoAmericano.GrupoDe(p.Fase) == "A")
            .SelectMany(p => new[]
            {
                (Math.Min(p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value), Math.Max(p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value)),
                (Math.Min(p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value), Math.Max(p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value)),
            })
            .Distinct().Count();
        Assert.Equal(10, pares);   // C(5,2)
    }

    // ⚠️ O NÚMERO QUE NÃO FECHA É RECUSADO, e o torneio não é sorteado pela metade.
    [Fact]
    public async Task Numero_que_nao_fecha_e_recusado_sem_sortear_nada()
    {
        var (ctx, torneio, _, org) = MontarAmericano(14);
        using var _c = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarRodadasAmericano(torneio.Id, new DateTime(2026, 8, 20, 19, 0, 0));

        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync());
        var erro = controller.TempData["Erro"] as string;
        Assert.NotNull(erro);
        Assert.Contains("15", erro);   // a saída: chame mais 1
    }

    // ── O GRUPO FINAL ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_grupo_final_nasce_quando_os_grupos_terminam_e_leva_os_classificados()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);

        // Enquanto os grupos rolam, o grupo final não existe.
        Assert.False(await ctx.Partidas.AnyAsync(p =>
            p.TorneioId == torneio.Id && p.Fase.StartsWith("Americano Final")));

        await JogarTudoAsync(ctx, org, torneio.Id);

        var doFinal = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id && p.Fase.StartsWith("Americano Final"))
            .ToListAsync();

        Assert.NotEmpty(doFinal);

        // 2 grupos × 2 que passam = 4 finalistas, e um grupo de 4 são 3 partidas.
        var finalistas = doFinal.SelectMany(p => new[]
        {
            p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value,
            p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value,
        }).Distinct().ToList();

        Assert.Equal(4, finalistas.Count);
        Assert.Equal(3, doFinal.Count);
    }

    // Quem passa é o primeiro de CADA grupo, pela tabela DAQUELE grupo — e não os melhores do
    // torneio somado, que juntaria gente que nunca se enfrentou.
    [Fact]
    public async Task Passam_os_primeiros_de_cada_grupo()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);
        await JogarTudoAsync(ctx, org, torneio.Id);

        var finalistas = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id && p.Fase.StartsWith("Americano Final"))
            .ToListAsync();

        var idsFinalistas = finalistas.SelectMany(p => new[]
        {
            p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value,
            p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value,
        }).Distinct().ToHashSet();

        // Dois de cada grupo: o conjunto de finalistas tem gente dos DOIS grupos.
        var grupoDe = await ctx.InscricoesAmericanas
            .Where(i => i.CategoriaId == categoria.Id)
            .ToDictionaryAsync(i => i.JogadorId, i => i.Grupo);

        Assert.Equal(2, idsFinalistas.Count(id => grupoDe[id] == "A"));
        Assert.Equal(2, idsFinalistas.Count(id => grupoDe[id] == "B"));
    }

    // ── O CAMPEÃO ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_campeao_sai_do_grupo_final_e_e_uma_pessoa_so()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);
        await JogarTudoAsync(ctx, org, torneio.Id);

        var carimbo = await ctx.Duplas
            .SingleOrDefaultAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao");

        Assert.NotNull(carimbo);
        // Sem parceiro: o título é individual, senão vaza pra quem jogou junto na última rodada.
        Assert.Null(carimbo!.Jogador2Id);

        // E é alguém que disputou o grupo final.
        var doFinal = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id && p.Fase.StartsWith("Americano Final"))
            .ToListAsync();
        var finalistas = doFinal.SelectMany(p => new[]
        {
            p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value,
            p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value,
        }).ToHashSet();

        Assert.Contains(carimbo.Jogador1Id, finalistas);
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // ⚠️ Ninguém pode ser coroado antes do grupo final acabar. Era o risco de o robô olhar só
    // "as rodadas acabaram" sem perguntar se ainda falta a fase decisiva.
    [Fact]
    public async Task Ninguem_e_coroado_com_a_fase_de_grupos_apenas()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(10);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 2);

        // Joga SÓ a fase de grupos.
        var push = Substitute.For<IPushNotificationService>();
        var deGrupo = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && !p.Fase.StartsWith("Americano Final"))
            .OrderBy(p => p.Id).ToListAsync();
        foreach (var p in deGrupo)
        {
            await TestInfra.NovoPartidasController(ctx, org.Id, push: push)
                .ControlePlacar(p.Id, "Finalizada", 9, p.Id % 8, p.NomeQuadra, null);
        }

        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));
        Assert.NotEqual("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // ── GRUPO ÚNICO NÃO MUDOU ─────────────────────────────────────────────────────────────

    // Sem divisão, a fase continua "Americano Rodada N" — a MESMA grafia dos torneios
    // sorteados antes desta mudança. Duas grafias pra mesma coisa é como as telas passam a
    // discordar uma da outra.
    [Fact]
    public async Task Grupo_unico_mantem_a_fase_de_sempre_e_coroa_o_lider()
    {
        var (ctx, torneio, categoria, org) = MontarAmericano(8);
        using var _ = ctx;

        await SortearAsync(ctx, org, torneio.Id, grupos: 1);

        var fases = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Select(p => p.Fase).Distinct().ToListAsync();
        Assert.All(fases, f => Assert.StartsWith("Americano Rodada ", f));
        Assert.Equal(1, (await ctx.Categorias.FindAsync(categoria.Id))!.GruposAmericano);

        await JogarTudoAsync(ctx, org, torneio.Id);

        // Nenhum grupo final nasce num torneio sem divisão.
        Assert.False(await ctx.Partidas.AnyAsync(p =>
            p.TorneioId == torneio.Id && p.Fase.StartsWith("Americano Final")));
    }
}
