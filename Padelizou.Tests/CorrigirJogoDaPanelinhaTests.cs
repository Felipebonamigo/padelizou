using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// CORRIGIR E APAGAR UM JOGO DE PANELINHA.
//
// O que está sendo protegido aqui não é a tela, é o ranking. O mesmo jogo alimenta DUAS contas:
// o ranking do mês e o da semana são refeitos na hora a partir dos jogos, mas o ranking GERAL
// mora gravado em `JogadorGrupo.PontuacaoInterna`. Enquanto só existia "registrar", os dois
// batiam. Assim que passa a existir apagar, um total somado à mão fica com pontos de um jogo que
// não existe mais — e o jeito de descobrir isso é a pessoa reclamar do próprio placar.
public class CorrigirJogoDaPanelinhaTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
    {
        var controller = new GruposController(
            ctx, Substitute.For<ISessaoGrupoService>(),
            Substitute.For<IPushNotificationService>(), NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // Uma panelinha com 4 jogadores, o primeiro deles administrador.
    private static async Task<(GrupoPrivado grupo, List<Jogador> jogadores)> MontarAsync(DbPadelContext ctx)
    {
        var jogadores = new List<Jogador>();
        for (int i = 1; i <= 4; i++)
        {
            jogadores.Add(new Jogador { Nome = $"Jogador {i}", Cpf = $"7770000000{i}", Login = $"jog{i}" });
        }
        ctx.Jogadores.AddRange(jogadores);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado { Nome = "Quartas no OK", CodigoConvite = "7C7BKL", AdministradorId = jogadores[0].Id };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        foreach (var j in jogadores)
        {
            ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = j.Id, PontuacaoInterna = 0 });
        }
        await ctx.SaveChangesAsync();

        return (grupo, jogadores);
    }

    private static async Task<JogoSemanal> RegistrarAsync(
        GruposController controller, DbPadelContext ctx, GrupoPrivado grupo, List<Jogador> j,
        int games1, int games2, int registradoPor)
    {
        await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 11),
            j[0].Id, j[1].Id, j[2].Id, j[3].Id,
            games1, games2, clubeId: null);

        return await ctx.JogosSemanais.OrderByDescending(x => x.Id).FirstAsync();
    }

    private static async Task<int> PontosAsync(DbPadelContext ctx, int grupoId, int jogadorId) =>
        await ctx.JogadoresGrupo.Where(jg => jg.GrupoId == grupoId && jg.JogadorId == jogadorId)
            .Select(jg => jg.PontuacaoInterna).FirstAsync();

    [Fact]
    public void Vitoria_vale_3_derrota_vale_1_e_empate_vale_2_pra_cada()
    {
        var vitoriaDaUm = new JogoSemanal
        {
            Dupla1Jogador1Id = 1, Dupla1Jogador2Id = 2, Dupla2Jogador1Id = 3, Dupla2Jogador2Id = 4,
            GamesDupla1 = 6, GamesDupla2 = 4,
        };
        var empate = new JogoSemanal
        {
            Dupla1Jogador1Id = 1, Dupla1Jogador2Id = 2, Dupla2Jogador1Id = 3, Dupla2Jogador2Id = 4,
            GamesDupla1 = 5, GamesDupla2 = 5,
        };

        var so = PontuacaoDaPanelinha.Totais(new[] { vitoriaDaUm });
        Assert.Equal(3, so[1]);
        Assert.Equal(3, so[2]);
        Assert.Equal(1, so[3]);
        Assert.Equal(1, so[4]);

        var comEmpate = PontuacaoDaPanelinha.Totais(new[] { vitoriaDaUm, empate });
        Assert.Equal(5, comEmpate[1]);
        Assert.Equal(3, comEmpate[3]);
    }

    [Fact]
    public async Task Apagar_o_jogo_TIRA_os_pontos_dele_do_ranking_geral()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);
        var controller = Controller(ctx, j[0].Id);

        var jogo = await RegistrarAsync(controller, ctx, grupo, j, 6, 4, j[0].Id);
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[0].Id));
        Assert.Equal(1, await PontosAsync(ctx, grupo.Id, j[2].Id));

        await controller.ApagarJogo(jogo.Id);

        // O jogo não existe mais: NINGUÉM pode continuar pontuado por ele. Este é exatamente o
        // fantasma que aparecia quando o total era somado de pouco em pouco.
        Assert.Empty(ctx.JogosSemanais.Where(x => x.Id == jogo.Id));
        Assert.Equal(0, await PontosAsync(ctx, grupo.Id, j[0].Id));
        Assert.Equal(0, await PontosAsync(ctx, grupo.Id, j[1].Id));
        Assert.Equal(0, await PontosAsync(ctx, grupo.Id, j[2].Id));
        Assert.Equal(0, await PontosAsync(ctx, grupo.Id, j[3].Id));
    }

    [Fact]
    public async Task Apagar_um_jogo_deixa_de_pe_os_pontos_dos_OUTROS_jogos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);
        var controller = Controller(ctx, j[0].Id);

        var primeiro = await RegistrarAsync(controller, ctx, grupo, j, 6, 4, j[0].Id);
        await RegistrarAsync(controller, ctx, grupo, j, 6, 2, j[0].Id);
        Assert.Equal(6, await PontosAsync(ctx, grupo.Id, j[0].Id));

        await controller.ApagarJogo(primeiro.Id);

        // Sobrou um jogo, e é ele que manda: 3 pra dupla 1, 1 pra dupla 2.
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[0].Id));
        Assert.Equal(1, await PontosAsync(ctx, grupo.Id, j[2].Id));
    }

    [Fact]
    public async Task Corrigir_o_placar_VIRA_o_vencedor_no_ranking_em_vez_de_somar_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);
        var controller = Controller(ctx, j[0].Id);

        var jogo = await RegistrarAsync(controller, ctx, grupo, j, 6, 4, j[0].Id);
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[0].Id));

        // Placar tinha sido digitado invertido: quem ganhou foi a dupla 2.
        await controller.EditarJogo(
            jogo.Id, new DateTime(2026, 8, 11),
            j[0].Id, j[1].Id, j[2].Id, j[3].Id,
            gamesDupla1: 4, gamesDupla2: 6, clubeId: null);

        Assert.Equal(1, await PontosAsync(ctx, grupo.Id, j[0].Id));
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[2].Id));
    }

    [Fact]
    public async Task Corrigir_quem_jogou_tira_os_pontos_de_quem_saiu_da_partida()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);
        var controller = Controller(ctx, j[0].Id);

        var jogo = await RegistrarAsync(controller, ctx, grupo, j, 6, 4, j[0].Id);
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[1].Id));

        // Quem jogou de parceiro não era o 2, era o 4 — e o 2 não jogou nada.
        await controller.EditarJogo(
            jogo.Id, new DateTime(2026, 8, 11),
            j[0].Id, j[3].Id, j[2].Id, j[1].Id,
            gamesDupla1: 6, gamesDupla2: 4, clubeId: null);

        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[3].Id));
        Assert.Equal(1, await PontosAsync(ctx, grupo.Id, j[1].Id));
    }

    [Fact]
    public async Task Quem_nao_administra_nem_registrou_NAO_apaga_o_jogo_dos_outros()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);

        // O administrador (j[0]) lança o jogo.
        var jogo = await RegistrarAsync(Controller(ctx, j[0].Id), ctx, grupo, j, 6, 4, j[0].Id);

        // Um membro qualquer tenta apagar.
        await Controller(ctx, j[2].Id).ApagarJogo(jogo.Id);

        Assert.NotEmpty(ctx.JogosSemanais.Where(x => x.Id == jogo.Id));
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[0].Id));
    }

    [Fact]
    public async Task Quem_REGISTROU_pode_corrigir_o_proprio_jogo_sem_ser_administrador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);

        // Quem lança é o j[2], que NÃO administra a panelinha.
        var quemLancou = Controller(ctx, j[2].Id);
        var jogo = await RegistrarAsync(quemLancou, ctx, grupo, j, 6, 4, j[2].Id);
        Assert.Equal(j[2].Id, jogo.RegistradoPorId);

        await quemLancou.EditarJogo(
            jogo.Id, new DateTime(2026, 8, 11),
            j[0].Id, j[1].Id, j[2].Id, j[3].Id,
            gamesDupla1: 4, gamesDupla2: 6, clubeId: null);

        var salvo = await ctx.JogosSemanais.FirstAsync(x => x.Id == jogo.Id);
        Assert.Equal(4, salvo.GamesDupla1);
        Assert.Equal(6, salvo.GamesDupla2);
    }

    [Fact]
    public async Task Recalcular_conserta_o_total_que_JA_estava_torto_no_banco()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, j) = await MontarAsync(ctx);
        var controller = Controller(ctx, j[0].Id);

        // Um total herdado de antes da correção — pontos de jogo que ninguém mais encontra.
        var registro = await ctx.JogadoresGrupo.FirstAsync(jg => jg.GrupoId == grupo.Id && jg.JogadorId == j[0].Id);
        registro.PontuacaoInterna = 99;
        await ctx.SaveChangesAsync();

        await RegistrarAsync(controller, ctx, grupo, j, 6, 4, j[0].Id);

        // Refazer não soma em cima do 99: ele some, porque não tem jogo que o sustente.
        Assert.Equal(3, await PontosAsync(ctx, grupo.Id, j[0].Id));
    }
}
