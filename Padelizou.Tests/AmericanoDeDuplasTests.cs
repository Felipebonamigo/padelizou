using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O AMERICANO DE DUPLAS (Felipe, 07/08/2026): a dupla é fixa, como no formato padrão, e cada
// uma enfrenta cada uma das outras exatamente uma vez. Vence a DUPLA que somar mais games —
// a régua do Americano individual, com a dupla no lugar da pessoa.
//
// As consequências que estes testes trancam:
//   • qualquer quantidade de duplas fecha (diferente do individual, que só fecha em certos
//     números) — com ímpar, uma descansa por rodada e o descanso reveza;
//   • o campeão é a DUPLA DE VERDADE, carimbada nela mesma (não a linha solo do individual);
//   • empate de DUAS na liderança com desempate previsto vira partida criada SOZINHA pelo
//     robô — as duas duplas já existem, não há parceiro a escolher como no individual;
//   • e o desfecho tem que ser o mesmo pelas duas telas que finalizam partida, porque todo
//     defeito grave deste projeto veio de uma segunda cópia que ninguém exercitava.
public class AmericanoDeDuplasTests
{
    // ── O SORTEIO (Services/RodadasAmericanoDeDuplas) ─────────────────────────────────────

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(15)]
    [InlineData(16)]
    public void Cada_dupla_enfrenta_cada_uma_exatamente_uma_vez(int n)
    {
        var ids = Enumerable.Range(101, n).ToList();
        var rodadas = RodadasAmericanoDeDuplas.Montar(ids, new Random(42));

        Assert.Equal(RodadasAmericanoDeDuplas.Rodadas(n), rodadas.Count);

        // Todos os pares possíveis, cada um uma vez só.
        var pares = rodadas.SelectMany(r => r)
            .Select(c => (Math.Min(c.DuplaA, c.DuplaB), Math.Max(c.DuplaA, c.DuplaB)))
            .ToList();
        Assert.Equal(n * (n - 1) / 2, pares.Count);
        Assert.Equal(pares.Count, pares.Distinct().Count());

        // Ninguém joga duas vezes na mesma rodada.
        foreach (var rodada in rodadas)
        {
            var naRodada = rodada.SelectMany(c => new[] { c.DuplaA, c.DuplaB }).ToList();
            Assert.Equal(naRodada.Count, naRodada.Distinct().Count());
        }
    }

    // Com número ímpar o descanso REVEZA: cada dupla fica de fora exatamente uma rodada.
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    public void Impar_descansa_exatamente_uma_rodada_cada(int n)
    {
        var ids = Enumerable.Range(1, n).ToList();
        var rodadas = RodadasAmericanoDeDuplas.Montar(ids, new Random(7));

        Assert.Equal(n, rodadas.Count);
        foreach (var id in ids)
        {
            int jogou = rodadas.Count(r => r.Any(c => c.DuplaA == id || c.DuplaB == id));
            Assert.Equal(n - 1, jogou);
        }
    }

    [Fact]
    public void Menos_de_duas_duplas_nao_sorteia()
    {
        Assert.False(RodadasAmericanoDeDuplas.Aceita(1));
        Assert.Empty(RodadasAmericanoDeDuplas.Montar(new[] { 1 }));
    }

    // ── A TABELA (Services/TabelaDoAmericanoDeDuplas) ─────────────────────────────────────

    private static Dupla D(int id) => new()
    {
        Id = id,
        Jogador1Id = id * 10,
        Jogador2Id = id * 10 + 1,
        Jogador1 = new Jogador { Id = id * 10, Nome = $"J{id}A", Cpf = $"7770000{id:0000}" },
        Jogador2 = new Jogador { Id = id * 10 + 1, Nome = $"J{id}B", Cpf = $"7780000{id:0000}" },
    };

    private static Partida P(Dupla a, Dupla b, int gamesA, int gamesB) => new()
    {
        Dupla1 = a, Dupla1Id = a.Id, GamesDupla1 = gamesA,
        Dupla2 = b, Dupla2Id = b.Id, GamesDupla2 = gamesB,
        Status = "Finalizada", Fase = "Americano Rodada 1", Codigo = "TESTE1",
    };

    [Fact]
    public void A_soma_e_por_dupla_e_a_lider_isolada_nao_fica_empatada()
    {
        var (a, b, c) = (D(1), D(2), D(3));
        var tabela = TabelaDoAmericanoDeDuplas.Montar(new[]
        {
            P(a, b, 6, 2), P(a, c, 6, 3), P(b, c, 6, 4),
        });

        Assert.Equal(new[] { 1, 2, 3 }, tabela.Select(l => l.Dupla.Id));   // A=12, B=8, C=7
        Assert.Equal(new[] { 12, 8, 7 }, tabela.Select(l => l.TotalGames));
        Assert.All(tabela, l => Assert.False(l.Empatada));
        Assert.Empty(TabelaDoAmericanoDeDuplas.EmpatadasNaLideranca(tabela));
    }

    // A ordem tem que ser TOTAL: empate desempata por Id, não pela ordem em que as partidas
    // chegaram — cada chamador monta a consulta do seu jeito (a lição do Interno).
    [Fact]
    public void Empate_na_lideranca_e_marcado_e_a_ordem_e_estavel()
    {
        var (a, b, c) = (D(1), D(2), D(3));

        // A=10, B=10, C=5 — e as partidas entram fora de ordem de propósito.
        var partidas = new[] { P(b, c, 6, 3), P(a, c, 4, 2), P(a, b, 6, 4) };
        var tabela = TabelaDoAmericanoDeDuplas.Montar(partidas);

        Assert.Equal(new[] { 1, 2, 3 }, tabela.Select(l => l.Dupla.Id));
        Assert.True(tabela[0].Empatada);
        Assert.True(tabela[1].Empatada);
        Assert.False(tabela[2].Empatada);

        var empatadas = TabelaDoAmericanoDeDuplas.EmpatadasNaLideranca(tabela);
        Assert.Equal(new[] { 1, 2 }, empatadas.Select(d => d.Id));
    }

    // ── DO SORTEIO AO CAMPEÃO, pelas duas telas ───────────────────────────────────────────

    public enum Tela { Mesa, TelaCheia }

    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador org, List<Dupla> duplas)
        MontarTorneio(int quantasDuplas, bool desempate = false, int incompletas = 0)
    {
        var ctx = TestInfra.NovoContexto();

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000098" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "Americano de Duplas", Codigo = "AD1", Status = "Chaves em Sorteio",
            Formato = "AmericanoDuplas", QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 8, 20, 19, 0, 0),
            DesempateAmericano = desempate,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        var duplas = new List<Dupla>();
        for (int i = 1; i <= quantasDuplas + incompletas; i++)
        {
            var j1 = new Jogador { Nome = $"Jogador {i:00}A", Cpf = $"6660000{i:0000}" };
            ctx.Jogadores.Add(j1);
            Jogador? j2 = null;
            if (i <= quantasDuplas)
            {
                j2 = new Jogador { Nome = $"Jogador {i:00}B", Cpf = $"6670000{i:0000}" };
                ctx.Jogadores.Add(j2);
            }
            ctx.SaveChanges();

            var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = j1.Id, Jogador2Id = j2?.Id };
            ctx.Duplas.Add(dupla);
            duplas.Add(dupla);
        }
        ctx.SaveChanges();

        return (ctx, torneio, categoria, org, duplas.Where(d => d.Completa).ToList());
    }

    private static Task SortearAsync(DbPadelContext ctx, Jogador org, int torneioId) =>
        TestInfra.NovoTorneiosController(ctx, org.Id)
            .GerarRodadasAmericanoDuplas(torneioId, new DateTime(2026, 8, 20, 19, 0, 0));

    // Finaliza o jogo entre as duas duplas, com o placar visto do lado de `duplaX` — pela
    // tela pedida, porque o desfecho não pode depender de por onde o placar foi lançado.
    private static async Task JogarAsync(DbPadelContext ctx, Tela tela, Jogador org,
        int torneioId, int duplaX, int duplaY, int gamesX, int gamesY)
    {
        // Só as abertas: depois do desempate criado existem DUAS partidas entre as mesmas
        // duplas (a da rodada, já finalizada, e a do título).
        var partida = await ctx.Partidas.SingleAsync(p => p.TorneioId == torneioId
            && p.Status != "Finalizada"
            && ((p.Dupla1Id == duplaX && p.Dupla2Id == duplaY) ||
                (p.Dupla1Id == duplaY && p.Dupla2Id == duplaX)));

        var (g1, g2) = partida.Dupla1Id == duplaX ? (gamesX, gamesY) : (gamesY, gamesX);

        if (tela == Tela.Mesa)
        {
            partida.GamesDupla1 = g1;
            partida.GamesDupla2 = g2;
            await ctx.SaveChangesAsync();
            await TestInfra.NovoTorneiosController(ctx, org.Id).FinalizarPartida(partida.Id);
        }
        else
        {
            await TestInfra.NovoPartidasController(ctx, org.Id)
                .ControlePlacar(partida.Id, "Finalizada", g1, g2, partida.NomeQuadra, null);
        }
    }

    [Fact]
    public async Task O_sorteio_gera_todos_contra_todos_e_pula_dupla_incompleta()
    {
        var (ctx, torneio, _, org, duplas) = MontarTorneio(4, incompletas: 1);
        using var _1 = ctx;

        await SortearAsync(ctx, org, torneio.Id);

        var partidas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.Equal(6, partidas.Count);   // 4 duplas = 6 jogos
        Assert.All(partidas, p => Assert.StartsWith("Americano", p.Fase));
        Assert.All(partidas, p => Assert.NotNull(p.HorarioPrevisto));

        // A incompleta não entra em jogo nenhum; as completas jogam 3 vezes cada.
        var incompleta = await ctx.Duplas.SingleAsync(d => d.Jogador2Id == null);
        Assert.DoesNotContain(partidas, p => p.Dupla1Id == incompleta.Id || p.Dupla2Id == incompleta.Id);
        foreach (var dupla in duplas)
        {
            Assert.Equal(3, partidas.Count(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id));
        }

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // A grade nunca chama a mesma PESSOA pra duas quadras no mesmo horário — é a vantagem de
    // as duplas existirem antes dos jogos (no individual o encaixe é posicional).
    [Fact]
    public async Task A_grade_nao_poe_ninguem_em_duas_quadras_ao_mesmo_tempo()
    {
        var (ctx, torneio, _, org, _) = MontarTorneio(7);
        using var _1 = ctx;

        await SortearAsync(ctx, org, torneio.Id);

        var partidas = await ctx.Partidas
            .Include(p => p.Dupla1).Include(p => p.Dupla2)
            .Where(p => p.TorneioId == torneio.Id).ToListAsync();

        foreach (var mesmoHorario in partidas.GroupBy(p => p.HorarioPrevisto))
        {
            var pessoas = mesmoHorario
                .SelectMany(p => new[] { p.Dupla1.Jogador1Id, p.Dupla1.Jogador2Id!.Value,
                                         p.Dupla2.Jogador1Id, p.Dupla2.Jogador2Id!.Value })
                .ToList();
            Assert.Equal(pessoas.Count, pessoas.Distinct().Count());
        }
    }

    [Fact]
    public async Task Uma_dupla_so_nao_sorteia_e_avisa()
    {
        var (ctx, torneio, _, org, _) = MontarTorneio(1);
        using var _1 = ctx;

        await SortearAsync(ctx, org, torneio.Id);

        Assert.Empty(await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync());
        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // O caso normal: líder isolada é campeã, carimbada NELA MESMA — o título é dos dois
    // jogadores da dupla, como no mata-mata, e não numa linha solo como no individual.
    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task A_dupla_lider_em_games_e_coroada_pelas_duas_telas(Tela tela)
    {
        var (ctx, torneio, _, org, duplas) = MontarTorneio(3);
        using var _1 = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var (a, b, c) = (duplas[0].Id, duplas[1].Id, duplas[2].Id);

        // A = 12, B = 8, C = 7.
        await JogarAsync(ctx, tela, org, torneio.Id, a, b, 6, 2);
        await JogarAsync(ctx, tela, org, torneio.Id, a, c, 6, 3);
        await JogarAsync(ctx, tela, org, torneio.Id, b, c, 6, 4);

        var campea = await ctx.Duplas.SingleAsync(d => d.UltimaFase == "Campeao");
        Assert.Equal(a, campea.Id);
        Assert.NotNull(campea.Jogador2Id);   // dupla de verdade, não linha solo
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        // E nenhuma partida extra: sem empate não existe rodada final.
        Assert.Equal(3, await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id));
    }

    // Empate de DUAS com desempate previsto: o robô cria a partida SOZINHO, entre as duas —
    // não há parceiro a escolher, diferente do individual (onde quem monta é o organizador).
    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task Empate_de_duas_vira_desempate_criado_pelo_robo(Tela tela)
    {
        var (ctx, torneio, _, org, duplas) = MontarTorneio(3, desempate: true);
        using var _1 = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var (a, b, c) = (duplas[0].Id, duplas[1].Id, duplas[2].Id);

        // A = 10, B = 10, C = 5.
        await JogarAsync(ctx, tela, org, torneio.Id, a, b, 6, 4);
        await JogarAsync(ctx, tela, org, torneio.Id, a, c, 4, 2);
        await JogarAsync(ctx, tela, org, torneio.Id, b, c, 6, 3);

        var desempate = await ctx.Partidas.SingleAsync(p =>
            p.TorneioId == torneio.Id && p.Fase == TabelaDoAmericano.FaseDesempate);
        Assert.Contains(desempate.Dupla1Id, new[] { a, b });
        Assert.Contains(desempate.Dupla2Id, new[] { a, b });

        // Ninguém coroado ainda: o título sai do desempate.
        Assert.False(await ctx.Duplas.AnyAsync(d => d.UltimaFase == "Campeao"));

        // A dupla que vencer o desempate leva — carimbada nela mesma.
        await JogarAsync(ctx, tela, org, torneio.Id, desempate.Dupla2Id, desempate.Dupla1Id, 7, 5);

        var campea = await ctx.Duplas.SingleAsync(d => d.UltimaFase == "Campeao");
        Assert.Equal(desempate.Dupla2Id, campea.Id);
        Assert.NotNull(campea.Jogador2Id);
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // Sem desempate previsto o sistema NÃO inventa critério nem campeão: encerra e deixa o
    // título com o organizador — a mesma postura do individual.
    [Fact]
    public async Task Empate_sem_desempate_previsto_encerra_sem_campea()
    {
        var (ctx, torneio, _, org, duplas) = MontarTorneio(3, desempate: false);
        using var _1 = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var (a, b, c) = (duplas[0].Id, duplas[1].Id, duplas[2].Id);

        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, a, b, 6, 4);
        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, a, c, 4, 2);
        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, b, c, 6, 3);

        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.False(await ctx.Duplas.AnyAsync(d => d.UltimaFase == "Campeao"));
        Assert.Equal(3, await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id));
    }

    // Empate TRIPLO nem com desempate previsto vira partida: três não cabem em dois lados de
    // uma quadra — a régua é a mesma do individual (TabelaDoAmericano.ProblemaParaDesempatar).
    [Fact]
    public async Task Empate_triplo_fica_com_o_organizador_mesmo_com_desempate_previsto()
    {
        var (ctx, torneio, _, org, duplas) = MontarTorneio(3, desempate: true);
        using var _1 = ctx;
        await SortearAsync(ctx, org, torneio.Id);
        var (a, b, c) = (duplas[0].Id, duplas[1].Id, duplas[2].Id);

        // Todas com 6: A 6×0 B, C 6×0 A, B 6×0 C.
        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, a, b, 6, 0);
        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, c, a, 6, 0);
        await JogarAsync(ctx, Tela.TelaCheia, org, torneio.Id, b, c, 6, 0);

        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.False(await ctx.Duplas.AnyAsync(d => d.UltimaFase == "Campeao"));
        Assert.Equal(3, await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id));
    }

    // A inscrição individual é do OUTRO Americano: aqui a inscrição é em dupla, e um POST
    // montado à mão não pode criar uma inscrição que nenhum sorteio lê.
    [Fact]
    public async Task Inscricao_individual_e_recusada_no_formato_de_duplas()
    {
        var (ctx, torneio, categoria, _, _) = MontarTorneio(2);
        using var _1 = ctx;
        torneio.Status = "Inscrições Abertas";
        await ctx.SaveChangesAsync();

        var jogador = new Jogador { Nome = "Fulano Beltrano", Cpf = "52998224725" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, jogador.Id)
            .InscreverIndividual(torneio.Id, categoria.Id, jogador.Nome, jogador.Cpf);

        Assert.Empty(await ctx.InscricoesAmericanas.Where(i => i.CategoriaId == categoria.Id).ToListAsync());
    }
}
