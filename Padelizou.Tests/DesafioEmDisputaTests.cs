using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Tests;

// ⚖️ RESOLVER O PLACAR CONTESTADO (/Admin/DesafiosEmDisputa).
//
// ⚠️ Antes desta tela, contestar era um beco sem saída: o desafio parava em `Em disputa`,
// ninguém pontuava e não havia botão em lugar nenhum. Estes testes guardam a saída — e as três
// coisas que ela NÃO pode fazer: decidir sozinha, apagar o que a dupla alegou, e pontuar um
// jogo anulado.
public class DesafioEmDisputaTests
{
    private const int Categoria = 1;

    private static (DbPadelContext ctx, int adminId) Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        var admin = TestInfra.NovoJogador(9);
        admin.IsAdminGeral = true;
        ctx.Jogadores.Add(admin);
        for (int i = 1; i <= 4; i++) ctx.Jogadores.Add(TestInfra.NovoJogador(i));

        ctx.CategoriasPadrao.Add(new CategoriaPadrao
        {
            Id = Categoria, Nome = "4ª Categoria Masculina", Codigo = "C4M", Tipo = "Masculina",
        });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Golden Point" });
        ctx.SaveChanges();

        return (ctx, admin.Id);
    }

    private static AdminController Controller(DbPadelContext ctx, int usuarioLogadoId,
        IPushNotificationService? push = null)
    {
        var controller = new AdminController(
            ctx,
            push ?? Substitute.For<IPushNotificationService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Options.Create(new RegistroResultadosSettings()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }

    // Um desafio jogado, com placar lançado e CONTESTADO pela outra dupla.
    private static Desafio Contestado(DbPadelContext ctx, int games1 = 6, int games2 = 4)
    {
        var desafio = new Desafio
        {
            DesafianteJogador1Id = 2, DesafianteJogador2Id = 3,
            DesafiadoJogador1Id = 4, DesafiadoJogador2Id = 5,
            CategoriaPadraoId = Categoria, ClubeId = 1,
            Formato = FormatoDoDesafio.UmSet,
            Status = Desafio.EmDisputa,
            DataHora = DateTime.Now.AddDays(-1),
            PropostoEm = DateTime.Now.AddDays(-2),
            GamesDesafiante = games1, GamesDesafiado = games2,
            LancadoPorId = 2, LancadoEm = DateTime.Now.AddHours(-5),
            MotivoDaDisputa = "foi 6x4 pra gente, nao pra eles",
        };
        ctx.Desafios.Add(desafio);
        ctx.SaveChanges();
        return desafio;
    }

    private static FechamentoDoDesafio Fechamento(DbPadelContext ctx) =>
        TestInfra.FechamentoDeDesafioDe(ctx);

    // ── Quem entra ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Jogador_comum_nao_abre_nem_resolve()
    {
        var (ctx, _) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, usuarioLogadoId: 2);   // um dos que jogaram

        Assert.IsType<RedirectToActionResult>(await controller.DesafiosEmDisputa());
        Assert.IsType<ForbidResult>(await controller.ResolverDisputa(
            desafio.Id, "validar", "eu venci", null, null, 6, 4, Fechamento(ctx)));

        // E nada mudou: continua em disputa, sem pontuar.
        var depois = await ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.EmDisputa, depois.Status);
        Assert.Null(depois.PontosDesafiante);
    }

    // ── Validar o placar ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validar_fecha_pelo_caminho_normal_e_pontua()
    {
        // ⚠️ Fecha pelo FechamentoDoDesafio — o mesmo do botão da outra dupla e do relógio das
        // 72h. Fechar "na mão" aqui seria a segunda cópia da conta de pontos e do cinturão.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "validar",
            "falei com os quatro; os dois confirmaram 6x4", null, null, null, null, Fechamento(ctx));

        var fechado = await ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Confirmado, fechado.Status);
        Assert.Equal(11, fechado.PontosDesafiante);   // sem Padelímetro: 10 de vitória + 1
        Assert.Equal(1, fechado.PontosDesafiado);
        Assert.Equal(adminId, fechado.ConfirmadoPorId);
        Assert.Equal(adminId, fechado.ResolvidoPorId);
        Assert.StartsWith("Placar validado:", fechado.ResolucaoDaDisputa);

        // ⚠️ O que a dupla alegou NÃO é apagado: sem os dois lados, não dá pra reler a história.
        Assert.Equal("foi 6x4 pra gente, nao pra eles", fechado.MotivoDaDisputa);
    }

    [Fact]
    public async Task Validar_ganha_o_cinturao_igual_a_um_placar_normal()
    {
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "validar", "conferido no video",
            null, null, null, null, Fechamento(ctx));

        // Cinturão estava vago: o vencedor leva, pelo mesmo caminho de sempre.
        var dono = await ctx.ReinadosNoCinturao.SingleAsync(r => r.TerminouEm == null);
        Assert.Equal(2, dono.Jogador1Id);
        Assert.Equal(3, dono.Jogador2Id);
    }

    [Fact]
    public async Task Corrigir_o_placar_inverte_o_vencedor()
    {
        // O caso que dá razão a quem contestou: o placar lançado estava errado.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx, games1: 6, games2: 4);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "validar", "o placar certo era 4x6",
            null, null, gamesDesafiante: 4, gamesDesafiado: 6, Fechamento(ctx));

        var fechado = await ctx.Desafios.SingleAsync();
        Assert.Equal(4, fechado.GamesDesafiante);
        Assert.Equal(6, fechado.GamesDesafiado);
        Assert.Equal(2, EstadoDoDesafio.LadoVencedor(fechado));
        Assert.Equal(1, fechado.PontosDesafiante);
        Assert.Equal(11, fechado.PontosDesafiado);
    }

    // ── Anular ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Anular_nao_paga_ninguem_e_nao_e_cancelamento()
    {
        // ⚠️ `Anulado`, e não `Cancelado`: cancelado é jogo que NÃO aconteceu. Misturar os dois
        // faria o histórico dizer que a pessoa furou um compromisso que ela na verdade jogou.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "anular", "cada um conta uma historia",
            null, null, null, null, Fechamento(ctx));

        var anulado = await ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Anulado, anulado.Status);
        Assert.Equal(0, anulado.PontosDesafiante);
        Assert.Equal(0, anulado.PontosDesafiado);
        Assert.StartsWith("Desafio anulado:", anulado.ResolucaoDaDisputa);

        // E não move cinturão nenhum.
        Assert.Empty(ctx.ReinadosNoCinturao);
    }

    [Fact]
    public async Task Anulado_fica_fora_do_ranking()
    {
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        await Controller(ctx, adminId).ResolverDisputa(desafio.Id, "anular", "sem acordo",
            null, null, null, null, Fechamento(ctx));

        var contam = await RankingDeDesafios.QueContam(ctx.Desafios, DateTime.Now).ToListAsync();
        Assert.Empty(contam);
    }

    // ── O que a regra recusa ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_justificativa_nao_resolve()
    {
        // ⚠️ A justificativa é o que responde "por que meu placar virou 6x4?" três semanas
        // depois. Sem ela, a decisão vira "o admin mudou" — e o ranking perde a confiança.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "validar", "   ", null, null, null, null, Fechamento(ctx));

        var intacto = await ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.EmDisputa, intacto.Status);
        Assert.Null(intacto.ResolvidoPorId);
    }

    [Fact]
    public async Task Placar_empatado_nao_pode_ser_validado()
    {
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        await controller.ResolverDisputa(desafio.Id, "validar", "empatou",
            null, null, gamesDesafiante: 6, gamesDesafiado: 6, Fechamento(ctx));

        Assert.Equal(Desafio.EmDisputa, (await ctx.Desafios.SingleAsync()).Status);
    }

    [Fact]
    public async Task Desafio_que_nao_esta_em_disputa_nao_volta_pra_mesa()
    {
        // Reabrir jogo fechado por esta porta seria reescrever ranking sem ninguém ver.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);
        desafio.Status = Desafio.Confirmado;
        desafio.PontosDesafiante = 11;
        desafio.PontosDesafiado = 1;
        await ctx.SaveChangesAsync();

        await Controller(ctx, adminId).ResolverDisputa(desafio.Id, "anular", "mudei de ideia",
            null, null, null, null, Fechamento(ctx));

        var intacto = await ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Confirmado, intacto.Status);
        Assert.Equal(11, intacto.PontosDesafiante);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tela_lista_o_contestado_com_os_dois_lados_e_guarda_o_resolvido()
    {
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);

        var controller = Controller(ctx, adminId);
        var vm = Assert.IsType<DesafiosEmDisputaVM>(
            Assert.IsType<ViewResult>(await controller.DesafiosEmDisputa()).Model);

        var linha = Assert.Single(vm.EmDisputa);
        Assert.Equal("6 x 4", linha.Placar);
        Assert.Equal("foi 6x4 pra gente, nao pra eles", linha.Desafio.MotivoDaDisputa);
        Assert.NotNull(linha.QuemLancou);
        Assert.Empty(vm.Resolvidas);

        // Depois de resolver, ele sai da fila e entra no histórico da mesma tela.
        await controller.ResolverDisputa(desafio.Id, "validar", "conferido", null, null, null, null,
            Fechamento(ctx));

        var depois = Assert.IsType<DesafiosEmDisputaVM>(
            Assert.IsType<ViewResult>(await controller.DesafiosEmDisputa()).Model);

        Assert.Empty(depois.EmDisputa);
        Assert.Single(depois.Resolvidas);
    }

    [Fact]
    public async Task Os_quatro_sao_avisados_do_desfecho()
    {
        // Inclusive quem "ganhou": decisão sobre briga que ninguém comunica é a próxima
        // reclamação, e o silêncio faz o placar parecer ter mudado sozinho.
        var (ctx, adminId) = Cenario();
        using var _ctx = ctx;
        var desafio = Contestado(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, adminId, push).ResolverDisputa(desafio.Id, "validar", "conferido",
            null, null, null, null, Fechamento(ctx));

        foreach (var id in new[] { 2, 3, 4, 5 })
        {
            await push.Received(1).EnviarParaJogadorAsync(id, Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.SoApp);
        }
    }
}
