using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// O ORGANIZADOR CRAVA O CAMPEÃO quando a partida de desempate não serve (Felipe, 08/08/2026).
//
// Com 3+ empatados no título — ou com o desempate desligado — a tela dizia "o critério fica com
// o organizador" e acabava ali: um beco. O torneio ficava sem campeão, sem ponto no ranking e
// sem conquista no perfil de ninguém, e a única saída era mexer no banco.
//
// Coroar é irreversível na prática (distribui ponto, entra no perfil de quem jogou), então o
// que este arquivo protege são as TRAVAS: só entre empatados, só com a lista fechada, e uma vez.
public class CampeaoCravadoPeloOrganizadorTests
{
    // Um Americano de grupo único já jogado, com o empate no topo desenhado de propósito.
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria categoria,
                               Jogador org, List<Jogador> jogadores)>
        MontarEmpateNoTopoAsync(bool deixarUmJogoAberto = false)
    {
        var ctx = TestInfra.NovoContexto();

        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "Americano", Codigo = "AM1", Status = "Fase de Grupos",
            Formato = "Americano", QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = 20,
            DataInicio = new DateTime(2026, 8, 20, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });

        var jogadores = new List<Jogador>();
        for (int i = 1; i <= 4; i++)
        {
            var j = new Jogador { Nome = $"Jogadora {i}", Cpf = $"888000000{i:00}" };
            ctx.Jogadores.Add(j);
            jogadores.Add(j);
        }
        await ctx.SaveChangesAsync();

        // Duas partidas 4x4: os quatro terminam com os mesmos games, empatados no topo.
        // É o cenário que uma partida de desempate não resolve.
        await AdicionarJogoAsync(ctx, torneio, categoria, jogadores[0], jogadores[1], 4,
                                 jogadores[2], jogadores[3], 4, "Finalizada");
        await AdicionarJogoAsync(ctx, torneio, categoria, jogadores[0], jogadores[2], 4,
                                 jogadores[1], jogadores[3], 4,
                                 deixarUmJogoAberto ? "AoVivo" : "Finalizada");

        return (ctx, torneio, categoria, org, jogadores);
    }

    private static async Task AdicionarJogoAsync(DbPadelContext ctx, Torneio torneio, Categoria categoria,
        Jogador a1, Jogador a2, int games1, Jogador b1, Jogador b2, int games2, string status)
    {
        var d1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a1.Id, Jogador2Id = a2.Id };
        var d2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = b1.Id, Jogador2Id = b2.Id };
        ctx.Duplas.AddRange(d1, d2);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Fase = "Americano Rodada 1",
            Status = status,
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
            GamesDupla1 = games1,
            GamesDupla2 = games2,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task O_escolhido_vira_campeao_e_o_torneio_encerra()
    {
        var (ctx, torneio, categoria, org, jogadores) = await MontarEmpateNoTopoAsync();
        var escolhida = jogadores[2];

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .CoroarCampeaoAmericano(torneio.Id, categoria.Id, escolhida.Id);

        var campea = await ctx.Duplas.SingleAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao");
        Assert.Equal(escolhida.Id, campea.Jogador1Id);
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Nao_da_pra_coroar_quem_nao_terminou_empatado_na_lideranca()
    {
        // ⚠️ Coroar quem não está na liderança não é critério de desempate: é reescrever o
        // resultado. Só faltava alguém montar o POST à mão.
        var (ctx, torneio, categoria, org, _) = await MontarEmpateNoTopoAsync();

        var deFora = new Jogador { Nome = "Não jogou", Cpf = "77700000011" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .CoroarCampeaoAmericano(torneio.Id, categoria.Id, deFora.Id);

        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));
        Assert.NotEqual("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Com_jogo_em_aberto_nao_coroa_ninguem()
    {
        // A liderança ainda pode mudar — coroar agora seria coroar uma foto que vai mudar.
        var (ctx, torneio, categoria, org, jogadores) = await MontarEmpateNoTopoAsync(deixarUmJogoAberto: true);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .CoroarCampeaoAmericano(torneio.Id, categoria.Id, jogadores[0].Id);

        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));
    }

    [Fact]
    public async Task Coroar_duas_vezes_nao_cria_dois_campeoes()
    {
        // O segundo carimbo não apaga o primeiro: ele SOMA, e a categoria fica com dois.
        var (ctx, torneio, categoria, org, jogadores) = await MontarEmpateNoTopoAsync();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.CoroarCampeaoAmericano(torneio.Id, categoria.Id, jogadores[0].Id);
        await controller.CoroarCampeaoAmericano(torneio.Id, categoria.Id, jogadores[1].Id);

        var campeoes = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao").ToListAsync();

        Assert.Single(campeoes);
        Assert.Equal(jogadores[0].Id, campeoes[0].Jogador1Id);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_coroa()
    {
        var (ctx, torneio, categoria, _, jogadores) = await MontarEmpateNoTopoAsync();

        var estranho = new Jogador { Nome = "Estranho", Cpf = "77700000022" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var resposta = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .CoroarCampeaoAmericano(torneio.Id, categoria.Id, jogadores[0].Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resposta);
        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));
    }
}
