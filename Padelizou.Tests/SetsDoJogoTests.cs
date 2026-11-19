using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Formato = Padelizou.Services.FormatoDaPartida.Formato;

namespace Padelizou.Tests;

// JOGO DE MAIS DE UM SET NO PLACAR AO VIVO (19/11/2026). 🗣️ Felipe, na final do Chakra (2 sets de
// 6): *"Como que eu coloco o segundo set em jogo? E não esqueças que se for 1-1 em sets tem o tie
// break"*. Decisão: "Fechar set" guarda o placar do set e zera os games; no 1x1, super tie-break
// de 10 (vence por 2); o Finalizar só aceita quando alguém venceu os sets.
public class SetsDoJogoTests
{
    private static readonly Formato DoisSetsDe6 = new(2, 6, PontosTieBreak: TieBreakDoJogo.PontosPadrao);
    private static readonly Formato UmSetDe9 = new(1, 9);

    private static Partida Jogo(int s1, int s2, int g1, int g2, int p1 = 0, int p2 = 0, string? historico = null) => new()
    {
        Dupla1Id = 1, Dupla2Id = 2, Codigo = "J", Status = "AoVivo",
        SetsDupla1 = s1, SetsDupla2 = s2, GamesDupla1 = g1, GamesDupla2 = g2,
        PontosTieBreak1 = p1, PontosTieBreak2 = p2, PlacarDosSets = historico,
    };

    // ── A RÉGUA ────────────────────────────────────────────────────────────────────────
    [Fact]
    public void Dois_sets_precisa_vencer_dois_e_decide_no_super_tie_break()
    {
        Assert.Equal(2, SetsDoJogo.SetsParaVencer(DoisSetsDe6));
        Assert.Equal(1, SetsDoJogo.SetsParaVencer(UmSetDe9));
        Assert.True(SetsDoJogo.EmSuperTieBreak(DoisSetsDe6, 1, 1));
        Assert.False(SetsDoJogo.EmSuperTieBreak(DoisSetsDe6, 1, 0));
        Assert.False(SetsDoJogo.EmSuperTieBreak(UmSetDe9, 0, 0));
    }

    [Theory]
    [InlineData(6, 3, true)]
    [InlineData(6, 5, false)]
    [InlineData(7, 5, true)]
    [InlineData(5, 5, false)]
    [InlineData(3, 2, false)]
    public void Fecha_o_set_quando_ele_terminou(int g1, int g2, bool fecha)
    {
        Assert.Equal(fecha, SetsDoJogo.PodeFecharSet(DoisSetsDe6, Jogo(0, 0, g1, g2)));
    }

    [Fact]
    public void Jogo_de_um_set_nao_tem_fechar_set()
    {
        Assert.False(SetsDoJogo.PodeFecharSet(UmSetDe9, Jogo(0, 0, 9, 3)));
    }

    [Fact]
    public void Fechar_o_primeiro_set_guarda_o_placar_e_zera_os_games()
    {
        var jogo = Jogo(0, 0, 6, 3);

        SetsDoJogo.Fechar(DoisSetsDe6, jogo);

        Assert.Equal((1, 0, 0, 0), (jogo.SetsDupla1, jogo.SetsDupla2, jogo.GamesDupla1, jogo.GamesDupla2));
        Assert.Equal("6-3", jogo.PlacarDosSets);
    }

    // O set que DECIDE o jogo fica com os games na tela: é o placar final que o Finalizar lê.
    [Fact]
    public void Fechar_o_set_que_decide_mantem_os_games_e_o_jogo_fica_decidido()
    {
        var jogo = Jogo(1, 0, 6, 4, historico: "6-3");

        SetsDoJogo.Fechar(DoisSetsDe6, jogo);

        Assert.Equal((2, 0, 6, 4), (jogo.SetsDupla1, jogo.SetsDupla2, jogo.GamesDupla1, jogo.GamesDupla2));
        Assert.Equal("6-3 6-4", jogo.PlacarDosSets);
        Assert.Equal(1, QuemVenceu.LadoJaDecidido(DoisSetsDe6, jogo.SetsDupla1, jogo.SetsDupla2, jogo.GamesDupla1, jogo.GamesDupla2));
    }

    [Fact]
    public void No_um_a_um_o_super_tie_break_fecha_com_10_e_dois_de_frente()
    {
        Assert.False(SetsDoJogo.PodeFecharSet(DoisSetsDe6, Jogo(1, 1, 0, 0, 10, 9)));
        Assert.False(SetsDoJogo.PodeFecharSet(DoisSetsDe6, Jogo(1, 1, 0, 0, 9, 7)));
        Assert.True(SetsDoJogo.PodeFecharSet(DoisSetsDe6, Jogo(1, 1, 0, 0, 12, 10)));

        var jogo = Jogo(1, 1, 0, 0, 8, 10, historico: "6-3 4-6");
        SetsDoJogo.Fechar(DoisSetsDe6, jogo);

        Assert.Equal((1, 2), (jogo.SetsDupla1, jogo.SetsDupla2));
        Assert.Equal("6-3 4-6 [8-10]", jogo.PlacarDosSets);
        Assert.Equal(2, QuemVenceu.LadoJaDecidido(DoisSetsDe6, jogo.SetsDupla1, jogo.SetsDupla2, jogo.GamesDupla1, jogo.GamesDupla2));
    }

    // O 6x4 do segundo set NÃO dá o jogo a ninguém com os sets em 1x0 — antes o verde e o
    // Finalizar liam "sets diferentes" e entregavam o jogo a quem venceu o primeiro set.
    [Fact]
    public void Com_os_sets_em_aberto_ninguem_venceu_ainda()
    {
        Assert.Null(QuemVenceu.LadoJaDecidido(DoisSetsDe6, 1, 0, 4, 6));
        Assert.NotNull(SetsDoJogo.MotivoParaNaoFinalizar(DoisSetsDe6, Jogo(1, 0, 4, 6)));
        Assert.Null(SetsDoJogo.MotivoParaNaoFinalizar(DoisSetsDe6, Jogo(2, 0, 6, 4)));
        Assert.Null(SetsDoJogo.MotivoParaNaoFinalizar(UmSetDe9, Jogo(0, 0, 9, 3)));
    }

    [Fact]
    public void A_linha_dos_sets_mostra_os_sets_fechados()
    {
        // Ao vivo, a linha mostra só os sets FECHADOS: o set em jogo está nos contadores, e
        // repeti-lo aqui deixava um número velho na tela a cada toque (visto rodando, 19/11).
        Assert.Equal("6-3", SetsDoJogo.Texto(Jogo(1, 0, 2, 1, historico: "6-3")));
        Assert.Equal("6-3 4-6", SetsDoJogo.Texto(Jogo(1, 1, 0, 0, 3, 2, historico: "6-3 4-6")));
        Assert.Equal("6-3 6-4", SetsDoJogo.Texto(new Partida { Status = "Finalizada", PlacarDosSets = "6-3 6-4", GamesDupla1 = 6, GamesDupla2 = 4, Codigo = "x" }));
        Assert.Null(SetsDoJogo.Texto(Jogo(0, 0, 3, 2)));
    }

    // ── O CAMINHO DA TELA ──────────────────────────────────────────────────────────────
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida jogo, Jogador org)> FinalNoArAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SetsFaseGrupos = 2;
        torneio.GamesFaseGrupos = 6;
        torneio.PontosTieBreakGrupos = TieBreakDoJogo.PontosPadrao;
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);
        return (ctx, torneio, jogo, org);
    }

    [Fact]
    public async Task O_botao_fechar_set_grava_o_set_e_zera_os_games()
    {
        var (ctx, torneio, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.GamesDupla1 = 6; jogo.GamesDupla2 = 3;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).FecharSet(jogo.Id, "Jogos");

        var depois = await ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id);
        Assert.Equal((1, 0, 0, 0, "6-3"), (depois.SetsDupla1, depois.SetsDupla2, depois.GamesDupla1, depois.GamesDupla2, depois.PlacarDosSets));
    }

    [Fact]
    public async Task Quem_nao_opera_o_dia_nao_fecha_set()
    {
        var (ctx, _, jogo, _) = await FinalNoArAsync();
        using var __ = ctx;
        var estranho = new Jogador { Nome = "Estranho", Cpf = "52998224725" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        Assert.IsType<ForbidResult>(await TestInfra.NovoTorneiosController(ctx, estranho.Id).FecharSet(jogo.Id, "Jogos"));
    }

    [Fact]
    public async Task Finalizar_com_os_sets_em_aberto_e_recusado()
    {
        var (ctx, _, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.SetsDupla1 = 1; jogo.GamesDupla1 = 4; jogo.GamesDupla2 = 6;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.FinalizarPartida(jogo.Id, "Jogos");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal("AoVivo", (await ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id)).Status);
    }

    [Fact]
    public async Task No_um_a_um_o_lote_grava_os_pontos_do_super_tie_break()
    {
        var (ctx, torneio, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.SetsDupla1 = 1; jogo.SetsDupla2 = 1; jogo.GamesDupla1 = 0; jogo.GamesDupla2 = 0;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).SalvarPlacaresAoVivo(torneio.Id, new[] { jogo.Id },
            games1: new[] { 0 }, games2: new[] { 0 }, voltarPara: null, pontos1: new[] { 4 }, pontos2: new[] { 2 });

        var depois = await ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id);
        Assert.Equal((4, 2), (depois.PontosTieBreak1, depois.PontosTieBreak2));
    }
}

// A MESMA COISA NA TELA DO LÁPIS (Partidas/ControlePlacar). 🗣️ Felipe, no Controle de Partida da
// final do Chakra: *"Aqui também não deixa"*.
public class SetsNoControleDePartidaTests
{
    private static async Task<(DbPadelContext ctx, Partida jogo, Jogador org)> FinalNoArAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SetsFaseGrupos = 2;
        torneio.GamesFaseGrupos = 6;
        torneio.PontosTieBreakGrupos = TieBreakDoJogo.PontosPadrao;
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);
        return (ctx, jogo, org);
    }

    private static Task<Partida> Ler(DbPadelContext ctx, Partida jogo) =>
        ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id);

    [Fact]
    public async Task Salvar_e_fechar_o_set_grava_os_games_digitados_e_fecha()
    {
        var (ctx, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;

        var c = TestInfra.NovoPartidasController(ctx, org.Id);
        var resultado = await c.ControlePlacar(jogo.Id, "AoVivo", 6, 3, jogo.NomeQuadra, null, fecharSet: true);

        var depois = await Ler(ctx, jogo);
        Assert.Equal((1, 0, 0, 0, "6-3"), (depois.SetsDupla1, depois.SetsDupla2, depois.GamesDupla1, depois.GamesDupla2, depois.PlacarDosSets));
        // Volta pra própria tela: o próximo set se marca dali mesmo.
        var volta = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("ControlePlacar", volta.ActionName);
    }

    // ⚠️ A tela apagava os sets quando "sets" e "games" apontavam pra lados diferentes — que é
    // exatamente o 2º set de quem perdeu o 1º.
    [Fact]
    public async Task Salvar_no_meio_do_segundo_set_nao_apaga_os_sets()
    {
        var (ctx, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.SetsDupla1 = 1; jogo.SetsDupla2 = 0; jogo.PlacarDosSets = "6-3";
        await ctx.SaveChangesAsync();

        await TestInfra.NovoPartidasController(ctx, org.Id).ControlePlacar(jogo.Id, "AoVivo", 2, 4, jogo.NomeQuadra, null);

        var depois = await Ler(ctx, jogo);
        Assert.Equal((1, 0, 2, 4), (depois.SetsDupla1, depois.SetsDupla2, depois.GamesDupla1, depois.GamesDupla2));
    }

    [Fact]
    public async Task Finalizar_pelo_status_com_os_sets_em_aberto_e_recusado()
    {
        var (ctx, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.SetsDupla1 = 1; jogo.SetsDupla2 = 0;
        await ctx.SaveChangesAsync();

        var c = TestInfra.NovoPartidasController(ctx, org.Id);
        await c.ControlePlacar(jogo.Id, "Finalizada", 4, 6, jogo.NomeQuadra, null);

        Assert.NotNull(c.TempData["Erro"]);
        Assert.Equal("AoVivo", (await Ler(ctx, jogo)).Status);
    }

    [Fact]
    public async Task No_um_a_um_grava_os_pontos_do_super_tie_break()
    {
        var (ctx, jogo, org) = await FinalNoArAsync();
        using var _ = ctx;
        jogo.SetsDupla1 = 1; jogo.SetsDupla2 = 1; jogo.GamesDupla1 = 0; jogo.GamesDupla2 = 0;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoPartidasController(ctx, org.Id).ControlePlacar(jogo.Id, "AoVivo", 0, 0, jogo.NomeQuadra, null,
            pontosTieBreak1: 7, pontosTieBreak2: 5);

        var depois = await Ler(ctx, jogo);
        Assert.Equal((7, 5), (depois.PontosTieBreak1, depois.PontosTieBreak2));
    }
}
