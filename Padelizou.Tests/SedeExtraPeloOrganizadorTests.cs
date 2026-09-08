using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A SUB-ABA "QUADRAS E SEDES" — onde o organizador declara o local alugado (08/09/2026).
//
// A régua da grade está em SedeExtraComHorarioTests/SedeExtraNaGradeTests. Aqui é a FIAÇÃO:
// quem pode gravar, e o que a tela mostra como "quantos jogos cabem lá" — que é CONTA, não
// campo (decisão tomada com o Felipe: dois campos pra mesma informação discordariam).
public class SedeExtraPeloOrganizadorTests
{
    private static readonly DateTime Sabado = new(2026, 8, 15);

    // ── Quantos jogos cabem na janela ─────────────────────────────────────────────────────

    [Fact]
    public void A_capacidade_e_quadras_vezes_rodadas_da_janela()
    {
        // 8h às 12h = 4 horas; jogos de 50 min = 4 rodadas (a 5ª começaria 11h20 e a 6ª 12h10,
        // já fora). Com 2 quadras, 8 jogos.
        Assert.Equal(10, SedesDoTorneio.JogosQueCabemNaJanela(2, Sabado.AddHours(8), Sabado.AddHours(12), 50));
    }

    [Fact]
    public void Sem_janela_nao_da_pra_dizer_quantos_cabem()
    {
        Assert.Null(SedesDoTorneio.JogosQueCabemNaJanela(2, null, Sabado.AddHours(12), 50));
        Assert.Null(SedesDoTorneio.JogosQueCabemNaJanela(2, Sabado.AddHours(8), null, 50));
    }

    [Fact]
    public void Janela_invertida_ou_zerada_cabe_zero()
    {
        Assert.Equal(0, SedesDoTorneio.JogosQueCabemNaJanela(2, Sabado.AddHours(12), Sabado.AddHours(8), 50));
        Assert.Equal(0, SedesDoTorneio.JogosQueCabemNaJanela(2, Sabado.AddHours(8), Sabado.AddHours(8), 50));
    }

    // ── A janela da sede ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_grava_a_janela_em_todas_as_quadras_daquele_clube()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, alugado) = MontarComQuadras(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarJanelaDaSede(torneio.Id, alugado.Id, Sabado.AddHours(8), Sabado.AddHours(12));

        var doAlugado = await ctx.Quadras.Where(q => q.ClubeId == alugado.Id).ToListAsync();
        Assert.Equal(2, doAlugado.Count);
        Assert.All(doAlugado, q =>
        {
            Assert.Equal(Sabado.AddHours(8), q.DisponivelDe);
            Assert.Equal(Sabado.AddHours(12), q.DisponivelAte);
        });
    }

    // ⚠️ As quadras de CASA não podem ser atingidas por engano: a janela nasceu pro lugar
    // ALUGADO, e fechar o clube do próprio organizador tiraria o torneio inteiro da grade.
    [Fact]
    public async Task A_janela_de_um_clube_nao_encosta_nas_quadras_do_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, alugado) = MontarComQuadras(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarJanelaDaSede(torneio.Id, alugado.Id, Sabado.AddHours(8), Sabado.AddHours(12));

        var deCasa = await ctx.Quadras.Where(q => q.ClubeId != alugado.Id).ToListAsync();
        Assert.All(deCasa, q => Assert.Null(q.DisponivelDe));
    }

    [Fact]
    public async Task Limpar_a_janela_devolve_a_quadra_pro_expediente_inteiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, alugado) = MontarComQuadras(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.AlterarJanelaDaSede(torneio.Id, alugado.Id, Sabado.AddHours(8), Sabado.AddHours(12));
        await controller.AlterarJanelaDaSede(torneio.Id, alugado.Id, null, null);

        Assert.All(await ctx.Quadras.Where(q => q.ClubeId == alugado.Id).ToListAsync(),
            q => { Assert.Null(q.DisponivelDe); Assert.Null(q.DisponivelAte); });
    }

    [Fact]
    public async Task So_organizador_mexe_na_janela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, alugado) = MontarComQuadras(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000055" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarJanelaDaSede(torneio.Id, alugado.Id, Sabado.AddHours(8), Sabado.AddHours(12));

        Assert.IsType<ForbidResult>(resultado);
        Assert.All(await ctx.Quadras.ToListAsync(), q => Assert.Null(q.DisponivelDe));
    }

    // ── O transbordo por categoria ────────────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_tira_a_categoria_do_local_externo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, org, _) = MontarComQuadras(ctx);
        var categoria = await ctx.Categorias.FirstAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarTransbordoDaCategoria(categoria.Id, false);

        Assert.False((await ctx.Categorias.FindAsync(categoria.Id))!.PodeJogarNaSedeExtra);
    }

    [Fact]
    public async Task So_organizador_do_torneio_da_categoria_mexe_no_transbordo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, _) = MontarComQuadras(ctx);
        var categoriaDoOutro = await ctx.Categorias.FirstAsync();
        var (_, orgDoMeu, _) = MontarComQuadras(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, orgDoMeu.Id)
            .AlterarTransbordoDaCategoria(categoriaDoOutro.Id, false);

        Assert.IsType<ForbidResult>(resultado);
        Assert.True((await ctx.Categorias.FindAsync(categoriaDoOutro.Id))!.PodeJogarNaSedeExtra);
    }

    // ── "Que a dupla jogue apenas um lá" ──────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_liga_o_evitar_dois_jogos_no_externo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = MontarComQuadras(ctx);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarEvitarDoisJogosNaSedeExtra(torneio.Id, true);

        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.EvitarDoisJogosNaSedeExtra);
    }

    [Fact]
    public async Task So_organizador_liga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = MontarComQuadras(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000044" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarEvitarDoisJogosNaSedeExtra(torneio.Id, true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.False((await ctx.Torneios.FindAsync(torneio.Id))!.EvitarDoisJogosNaSedeExtra);
    }

    private static (Torneio torneio, Jogador organizador, Clube alugado) MontarComQuadras(DbPadelContext ctx)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var casa = new Clube { Nome = "Casa" };
        var alugado = new Clube { Nome = "Alugada" };
        ctx.Clubes.AddRange(casa, alugado);
        ctx.SaveChanges();

        torneio.ClubeId = casa.Id;
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = $"Casa 1-{torneio.Id}", ClubeId = casa.Id },
            new Quadra { TorneioId = torneio.Id, Nome = $"Alugada 1-{torneio.Id}", ClubeId = alugado.Id },
            new Quadra { TorneioId = torneio.Id, Nome = $"Alugada 2-{torneio.Id}", ClubeId = alugado.Id });
        ctx.SaveChanges();

        return (torneio, org, alugado);
    }
}
