using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O SORTEIO DE BRINDES DO TORNEIO (21/11/2026).
//
// 🗣️ Felipe: *"Vamos adicionar uma opção de sorteador, alguns torneios sorteiam brindes e outras
// coisas para quem joga"*. E, nas decisões do desenho:
//   - quem concorre: *"apenas com os que estão inscritos e participam do torneio (estão na chave);
//     se ficou na lista de espera, não participa"* — uma chance por PESSOA;
//   - *"coloca o número de sorteados, pode repetir o sorteio também, adicionando ou
//     substituindo, a critério do organizador"*;
//   - o resultado fica GRAVADO e aparece (página do torneio e telão), com animação;
//   - o ganhador é avisado, e quem saiu num sorteio refeito também (*"sim, avisa"*).
public class SorteioDeBrindesTests
{
    private static readonly DateTime Agora = new(2026, 11, 21, 21, 0, 0);

    // 3 duplas na chave (6 pessoas) + o que NÃO concorre: dupla na lista de espera, inscrição sem
    // parceiro e a linha de TIME (o Jogador1 dela é o organizador). E o Jogador 1 da primeira
    // dupla também joga outra categoria — continua sendo UMA chance.
    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org, HashSet<int> naChave)> TorneioAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, 3, "Fase de Grupos");
        torneio.Nome = "Last Dance";
        var naChave = (await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync())
            .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id ?? 0 }).ToHashSet();

        var outra = new Categoria { Nome = "Mista", Codigo = "MIX", TorneioId = torneio.Id };
        ctx.Categorias.Add(outra);
        var espera1 = TestInfra.NovoJogador(101);
        var espera2 = TestInfra.NovoJogador(102);
        var sozinho = TestInfra.NovoJogador(103);
        var parceiroNaMista = TestInfra.NovoJogador(104);
        ctx.Jogadores.AddRange(espera1, espera2, sozinho, parceiroNaMista);
        await ctx.SaveChangesAsync();

        var jogadorDaPrimeira = naChave.Min();
        ctx.Duplas.AddRange(
            new Dupla { CategoriaId = categoria.Id, Jogador1Id = espera1.Id, Jogador2Id = espera2.Id, EmListaDeEspera = true },
            new Dupla { CategoriaId = categoria.Id, Jogador1Id = sozinho.Id },
            new Dupla { CategoriaId = categoria.Id, Jogador1Id = org.Id, NomeTime = "Os Fortes" },
            new Dupla { CategoriaId = outra.Id, Jogador1Id = jogadorDaPrimeira, Jogador2Id = parceiroNaMista.Id });
        await ctx.SaveChangesAsync();
        naChave.Add(parceiroNaMista.Id);
        return (ctx, torneio, org, naChave);
    }

    private static SorteioDeBrindes Sorteador(DbPadelContext ctx, IPushNotificationService? push = null) =>
        new(ctx, push ?? Substitute.For<IPushNotificationService>());

    // Sorteio que devia ter saído e não saiu é falha do teste, com o motivo da recusa.
    private static int Id(SorteioDeBrindes.Resultado r) =>
        r.Sorteio?.Id ?? throw new InvalidOperationException($"O sorteio não saiu: {r.Erro}");

    private static async Task<List<int>> GanhadoresAsync(DbPadelContext ctx, int sorteioId) =>
        await ctx.GanhadoresDoSorteio.Where(g => g.SorteioId == sorteioId).OrderBy(g => g.Ordem).Select(g => g.JogadorId).ToListAsync();

    // ── Quem concorre ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Concorre_quem_esta_na_chave_uma_vez_so()
    {
        var (ctx, torneio, _, naChave) = await TorneioAsync();

        var participantes = await SorteioDeBrindes.Participantes(ctx, torneio.Id).ToListAsync();

        Assert.Equal(naChave.OrderBy(x => x), participantes.OrderBy(x => x));
        Assert.Equal(participantes.Count, participantes.Distinct().Count());
    }

    // ── Sortear ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sortear_grava_a_quantidade_pedida_de_nomes_diferentes_da_chave()
    {
        var (ctx, torneio, org, naChave) = await TorneioAsync();

        var r = await Sorteador(ctx).SortearAsync(torneio.Id, 3, "Kit Head", false, null, org.Id, Agora);

        Assert.Null(r.Erro);
        var sorteio = await ctx.SorteiosDoTorneio.SingleAsync();
        Assert.Equal((torneio.Id, "Kit Head", 3, Agora, org.Id), (sorteio.TorneioId, sorteio.Premio, sorteio.Quantidade, sorteio.FeitoEm, sorteio.FeitoPorJogadorId));
        var ganhadores = await GanhadoresAsync(ctx, sorteio.Id);
        Assert.Equal(3, ganhadores.Distinct().Count());
        Assert.All(ganhadores, g => Assert.Contains(g, naChave));
        Assert.Equal(new[] { 1, 2, 3 }, await ctx.GanhadoresDoSorteio.OrderBy(g => g.Ordem).Select(g => g.Ordem).ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(8)] // são 7 pessoas na chave
    public async Task Quantidade_fora_do_que_cabe_e_recusada_sem_gravar(int quantidade)
    {
        var (ctx, torneio, org, _) = await TorneioAsync();

        var r = await Sorteador(ctx).SortearAsync(torneio.Id, quantidade, null, false, null, org.Id, Agora);

        Assert.NotNull(r.Erro);
        Assert.Empty(ctx.SorteiosDoTorneio);
    }

    [Fact]
    public async Task Adicionar_deixa_de_fora_quem_ja_ganhou()
    {
        var (ctx, torneio, org, naChave) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        var primeiro = await sorteador.SortearAsync(torneio.Id, 6, null, false, null, org.Id, Agora);
        var jaGanharam = await GanhadoresAsync(ctx, Id(primeiro));

        var segundo = await sorteador.SortearAsync(torneio.Id, 1, "Overgrip", false, null, org.Id, Agora.AddMinutes(1));

        // Sobrou UMA pessoa: é ela, e não há como sair outra.
        Assert.Equal(naChave.Except(jaGanharam), await GanhadoresAsync(ctx, Id(segundo)));
        Assert.NotNull((await sorteador.SortearAsync(torneio.Id, 1, null, false, null, org.Id, Agora.AddMinutes(2))).Erro);
    }

    [Fact]
    public async Task Com_a_caixinha_quem_ja_ganhou_concorre_de_novo()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        await sorteador.SortearAsync(torneio.Id, 7, null, false, null, org.Id, Agora);

        var r = await sorteador.SortearAsync(torneio.Id, 7, null, true, null, org.Id, Agora.AddMinutes(1));

        Assert.Null(r.Erro);
        Assert.Equal(7, (await GanhadoresAsync(ctx, Id(r))).Count);
    }

    [Fact]
    public async Task Substituir_refaz_entre_todos_e_o_antigo_fica_marcado_como_refeito()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        var antigo = await sorteador.SortearAsync(torneio.Id, 7, null, false, null, org.Id, Agora);

        // Os 7 já ganharam no antigo: refazendo, eles voltam a concorrer (o antigo deixou de valer).
        var novo = await sorteador.SortearAsync(torneio.Id, 7, null, false, Id(antigo), org.Id, Agora.AddMinutes(1));

        Assert.Null(novo.Erro);
        var refeito = await ctx.SorteiosDoTorneio.SingleAsync(s => s.Id == Id(antigo));
        Assert.Equal((Id(novo), Agora.AddMinutes(1)), (refeito.SubstituidoPorId, refeito.SubstituidoEm));
        Assert.Equal(7, await ctx.GanhadoresDoSorteio.CountAsync(g => g.SorteioId == Id(antigo))); // não apaga
    }

    [Fact]
    public async Task Nao_substitui_o_que_ja_foi_refeito_nem_sorteio_de_outro_torneio()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        var antigo = await sorteador.SortearAsync(torneio.Id, 2, null, false, null, org.Id, Agora);
        await sorteador.SortearAsync(torneio.Id, 2, null, false, Id(antigo), org.Id, Agora.AddMinutes(1));

        Assert.NotNull((await sorteador.SortearAsync(torneio.Id, 2, null, false, Id(antigo), org.Id, Agora.AddMinutes(2))).Erro);
        Assert.NotNull((await sorteador.SortearAsync(torneio.Id + 1, 2, null, false, Id(antigo), org.Id, Agora.AddMinutes(2))).Erro);
        Assert.Equal(2, await ctx.SorteiosDoTorneio.CountAsync());
    }

    // ── Os avisos ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_ganha_e_avisado_com_o_premio()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        var push = Substitute.For<IPushNotificationService>();

        var r = await Sorteador(ctx, push).SortearAsync(torneio.Id, 2, "Kit Head", false, null, org.Id, Agora);

        foreach (var ganhador in await GanhadoresAsync(ctx, Id(r)))
            await push.Received(1).EnviarParaJogadorAsync(ganhador, "Você ganhou no sorteio! 🎁",
                "Last Dance: você foi sorteado(a) — prêmio: Kit Head.", $"/Torneios/Details/{torneio.Id}", AlcanceDoAviso.SoApp);
        Assert.Equal(2, push.ReceivedCalls().Count());
    }

    [Fact]
    public async Task Quem_sai_num_sorteio_refeito_fica_sabendo()
    {
        var (ctx, torneio, org, naChave) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        var antigo = await sorteador.SortearAsync(torneio.Id, 7, "Raquete", false, null, org.Id, Agora);
        var push = Substitute.For<IPushNotificationService>();

        // Refeito com 6 de 7: exatamente uma pessoa sai.
        var novo = await Sorteador(ctx, push).SortearAsync(torneio.Id, 6, "Raquete", false, Id(antigo), org.Id, Agora.AddMinutes(1));

        var saiu = Assert.Single(naChave.Except(await GanhadoresAsync(ctx, Id(novo))));
        await push.Received(1).EnviarParaJogadorAsync(saiu, "Sorteio refeito",
            "Last Dance: o organizador refez o sorteio de Raquete, e desta vez saiu outro nome.",
            $"/Torneios/Details/{torneio.Id}", AlcanceDoAviso.SoApp);
        await push.DidNotReceive().EnviarParaJogadorAsync(saiu, "Você ganhou no sorteio! 🎁",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O telão ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_telao_mostra_o_sorteio_dos_ultimos_3_minutos_que_vale()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        var sorteador = Sorteador(ctx);
        var refeito = await sorteador.SortearAsync(torneio.Id, 1, "A", false, null, org.Id, Agora);
        var vale = await sorteador.SortearAsync(torneio.Id, 1, "B", false, Id(refeito), org.Id, Agora.AddMinutes(1));

        Assert.Equal(Id(vale), (await SorteioDeBrindes.Recente(ctx, torneio.Id, Agora.AddMinutes(3)).SingleAsync()).Id);
        Assert.Empty(await SorteioDeBrindes.Recente(ctx, torneio.Id, Agora.AddMinutes(5)).ToListAsync());
    }

    // ── As portas ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_e_organizador_nao_sorteia()
    {
        var (ctx, torneio, _, naChave) = await TorneioAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, naChave.Min()).Sortear(torneio.Id, 1, null, false, null);

        Assert.IsType<ForbidResult>(r);
        Assert.Empty(ctx.SorteiosDoTorneio);
        Assert.IsType<ForbidResult>(await TestInfra.NovoTorneiosController(ctx, naChave.Min()).Sorteio(torneio.Id));
    }

    [Fact]
    public async Task O_organizador_sorteia_e_volta_pra_tela_com_a_animacao_daquele_sorteio()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, org.Id).Sortear(torneio.Id, 2, "Kit", false, null);

        var sorteio = await ctx.SorteiosDoTorneio.SingleAsync();
        var volta = Assert.IsType<RedirectToActionResult>(r);
        Assert.Equal("Sorteio", volta.ActionName);
        Assert.Equal(sorteio.Id, volta.RouteValues?["animar"]);
    }

    [Fact]
    public async Task A_pagina_do_torneio_e_o_telao_recebem_os_sorteios()
    {
        var (ctx, torneio, org, _) = await TorneioAsync();
        await Sorteador(ctx).SortearAsync(torneio.Id, 2, "Kit", false, null, org.Id, DateTime.Now);

        var details = TestInfra.NovoTorneiosController(ctx, org.Id);
        await details.Details(torneio.Id, null, null);
        var telao = TestInfra.NovoTorneiosController(ctx, org.Id);
        await telao.Telao(torneio.Id, null);

        Assert.Single((IEnumerable<SorteioDoTorneio>)details.ViewBag.Sorteios);
        Assert.NotNull(telao.ViewBag.SorteioRecente);
    }

    [Fact]
    public void A_consulta_de_quem_concorre_traduz_pro_postgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x").Options;
        using var ctx = new DbPadelContext(options);

        Assert.Contains("SELECT", SorteioDeBrindes.Participantes(ctx, 1).ToQueryString());
        Assert.Contains("SELECT", SorteioDeBrindes.Recente(ctx, 1, Agora).ToQueryString());
    }
}
