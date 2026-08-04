using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O cenário REAL que originou a chave direta: um torneio com categorias normais rodando E,
// em paralelo, um mata-mata de duplas remontadas — os MESMOS jogadores, embaralhados em
// outros pares. Passa pelo GerarChaves de verdade, que é o botão que o organizador aperta.
public class ChaveDiretaNoSorteioTests
{
    [Fact]
    public async Task Sorteio_gera_primeira_rodada_e_nenhum_grupo_na_chave_direta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == chave.Id).ToListAsync();

        Assert.Equal(8, jogos.Count);                                   // 24 duplas, quadro de 32
        Assert.All(jogos, j => Assert.Equal(ChaveamentoMataMata.PrimeiraRodada, j.Fase));
        Assert.All(jogos, j => Assert.False(string.IsNullOrEmpty(j.Codigo)));   // NOT NULL no banco
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));      // entra na grade junto

        // Chave direta não tem grupo — nem GrupoTorneio nem Dupla.Grupo preenchido.
        Assert.Empty(await ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == chave.Id).ToListAsync());
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == chave.Id).ToListAsync();
        Assert.All(duplas, d => Assert.Null(d.Grupo));

        // 8 jogam, 16 esperam a segunda rodada (8 vencedores virão dos jogos + 8 byes).
        var naPrimeiraRodada = jogos.SelectMany(j => new[] { j.Dupla1Id, j.Dupla2Id }).ToHashSet();
        Assert.Equal(16, naPrimeiraRodada.Count);
        Assert.Equal(8, duplas.Count(d => !naPrimeiraRodada.Contains(d.Id)));
    }

    [Fact]
    public async Task Ninguem_e_chamado_pra_duas_quadras_no_mesmo_horario()
    {
        // O ponto mais perigoso da feature: cada pessoa está em DUAS duplas do mesmo torneio
        // (a da categoria dela e a da chave direta). A grade compara PESSOA por causa disso.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id)
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .ToListAsync();

        var conflitos = jogos
            .GroupBy(j => j.HorarioPrevisto)
            .SelectMany(porHorario => porHorario
                .SelectMany(j => new[]
                {
                    j.Dupla1.Jogador1Id, j.Dupla1.Jogador2Id!.Value,
                    j.Dupla2.Jogador1Id, j.Dupla2.Jogador2Id!.Value,
                })
                .GroupBy(jogadorId => jogadorId)
                .Where(g => g.Count() > 1)
                .Select(g => new { porHorario.Key, JogadorId = g.Key }))
            .ToList();

        Assert.Empty(conflitos);
    }

    [Fact]
    public async Task Do_sorteio_ao_campeao_passando_pelos_byes()
    {
        // A chave inteira, jogada até o fim pelo fluxo real: se a ponte dos byes errar, ou o
        // torneio trava numa fase, ou 8 duplas somem sem nunca ter perdido.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, chave) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 24);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var esperado = new[]
        {
            (ChaveamentoMataMata.PrimeiraRodada, 8),
            ("Oitavas de Final", 8),
            ("Quartas de Final", 4),
            ("Semifinal", 2),
            ("Final", 1),
        };

        foreach (var (fase, jogosDaFase) in esperado)
        {
            var jogos = await ctx.Partidas
                .Where(p => p.CategoriaId == chave.Id && p.Fase == fase)
                .ToListAsync();

            Assert.Equal(jogosDaFase, jogos.Count);

            foreach (var jogo in jogos)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 6, 3);   // vence sempre a Dupla1
        }

        // Um campeão só, e ele saiu da Final.
        var final = await ctx.Partidas.SingleAsync(p => p.CategoriaId == chave.Id && p.Fase == "Final");
        Assert.NotNull(final.VencedorId);
        Assert.Equal(23, await ctx.Partidas.CountAsync(p => p.CategoriaId == chave.Id));   // 24 duplas = 23 jogos
    }

    [Fact]
    public async Task Chave_direta_acima_do_teto_e_recusada_antes_de_sortear()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarTorneioComChaveDiretaAsync(ctx, duplasNaChave: 33);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Recusa inteira: nada de torneio sorteado pela metade.
        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync());
        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // ---- cenário ----

    // Um torneio como o Interno: uma categoria normal com 12 duplas e uma chave direta com
    // N duplas montadas a partir dos MESMOS 24 jogadores, remontados em outros pares.
    private static (Torneio torneio, Jogador organizador, Categoria chaveDireta)
        MontarTorneioComChaveDiretaAsync(DbPadelContext ctx, int duplasNaChave)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Interno de Teste",
            Codigo = "INT123",
            Status = "Chaves em Sorteio",
            DataInicio = new DateTime(2026, 8, 5, 8, 0, 0),
            QuantidadeQuadras = 5,
            TempoPrevistoPartidaMinutos = 12,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "6ª Masculina", Codigo = "C6M", Torneio = torneio };
        var chave = new Categoria { Nome = "Mata-Mata", Codigo = "MM", Torneio = torneio, ChaveDireta = true };
        ctx.Categorias.AddRange(categoria, chave);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        // Gente suficiente pra encher as duas: cada pessoa entra 1x na categoria e 1x na chave.
        int pessoas = Math.Max(24, duplasNaChave * 2);
        var jogadores = Enumerable.Range(1, pessoas).Select(TestInfra.NovoJogador).ToList();
        ctx.Jogadores.AddRange(jogadores);
        ctx.SaveChanges();

        for (int i = 0; i < 12; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = categoria,
                Jogador1 = jogadores[i * 2],
                Jogador2 = jogadores[i * 2 + 1],
            });

        // A remontagem: os pares da chave direta são DESLOCADOS em relação aos da categoria,
        // então quase ninguém joga com o mesmo parceiro — é o caso real.
        for (int i = 0; i < duplasNaChave; i++)
            ctx.Duplas.Add(new Dupla
            {
                Categoria = chave,
                Jogador1 = jogadores[(i * 2 + 1) % pessoas],
                Jogador2 = jogadores[(i * 2 + 2) % pessoas],
            });

        ctx.SaveChanges();
        return (torneio, organizador, chave);
    }
}
