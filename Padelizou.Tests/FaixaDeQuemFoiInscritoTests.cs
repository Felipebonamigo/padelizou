using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Xunit;

namespace Padelizou.Tests;

// A FAIXA "VOCÊ FOI INSCRITO POR FULANO — ESTÁ CERTO?" NA TELA DO TORNEIO.
//
// ⚠️ ELA EXISTE PORQUE O AVISO NÃO BASTA, e isso está medido neste projeto: push alcança 5
// aparelhos em 154, e a caixa de avisos alcança quem ABRIR o app. A frase já está escrita no
// próprio código do torneio, no gancho dos chamados do mural (17/08/2026): *"aviso é lembrete,
// não deve ser a única porta: o que existe no sistema precisa estar visível de dentro dele"*.
// Quem apagou a notificação sem ler continua achando a saída pela tela do torneio.
public class FaixaDeQuemFoiInscritoTests
{
    private static async Task<(DbPadelContext ctx, Categoria cat, Jogador a, Jogador b, Jogador c)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Copa de Verão", Codigo = "CV02", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "5ª Masculina", Codigo = "C5M2", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var a = new Jogador { Nome = "Maickel Souza", Cpf = "11144477735" };
        var b = new Jogador { Nome = "Otávio Wunsch", Cpf = "22255588846" };
        var c = new Jogador { Nome = "Geovani Batista", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(a, b, c);
        await ctx.SaveChangesAsync();

        return (ctx, cat, a, b, c);
    }

    private static async Task<Dupla> InscreverAsync(DbPadelContext ctx, Categoria cat,
        Jogador jogador1, Jogador jogador2, Jogador autor)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, autor.Id.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();

        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: jogador1.Nome, cpf1: jogador1.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: jogador2.Nome, cpf2: jogador2.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false);

        return await ctx.Duplas.SingleAsync();
    }

    [Fact]
    public async Task A_tela_do_torneio_diz_QUEM_me_inscreveu()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        var dupla = await InscreverAsync(ctx, cat, a, b, autor: a);

        var controller = TestInfra.NovoTorneiosController(ctx, b.Id);
        await controller.Details(cat.TorneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var faixas = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.PerguntasDeInscricao);
        // Primeiro e último nome (NomeBonito.Curto) — a régua deste projeto pra rótulo que
        // divide linha com botão no celular. O aviso do push, que tem o título inteiro pra ele,
        // usa o nome com apelido.
        Assert.Equal("Maickel Souza", faixas[dupla.Id]);
    }

    [Fact]
    public async Task Quem_inscreveu_nao_ve_faixa_nenhuma()
    {
        // A faixa é pergunta, e quem clicou já respondeu ao clicar.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        await InscreverAsync(ctx, cat, a, b, autor: a);

        var controller = TestInfra.NovoTorneiosController(ctx, a.Id);
        await controller.Details(cat.TorneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var faixas = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.PerguntasDeInscricao);
        Assert.Empty(faixas);
    }

    [Fact]
    public async Task A_pergunta_de_um_nao_aparece_pro_outro()
    {
        // Sem este filtro, o titular veria "você foi inscrito por você mesmo" na própria
        // inscrição — e, pior, um estranho veria a pergunta de terceiro.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;
        await InscreverAsync(ctx, cat, a, b, autor: a);

        var controller = TestInfra.NovoTorneiosController(ctx, c.Id);
        await controller.Details(cat.TorneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var faixas = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.PerguntasDeInscricao);
        Assert.Empty(faixas);
    }

    [Fact]
    public async Task Quem_ja_disse_que_esta_certo_nao_ve_mais_a_faixa()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        var dupla = await InscreverAsync(ctx, cat, a, b, autor: a);

        await TestInfra.NovoTorneiosController(ctx, b.Id).ConfirmarInscricao(dupla.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, b.Id);
        await controller.Details(cat.TorneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var faixas = Assert.IsType<Dictionary<int, string>>(controller.ViewBag.PerguntasDeInscricao);
        Assert.Empty(faixas);
    }

    // ── A MARCAÇÃO ───────────────────────────────────────────────────────────────────────
    // Teste de FONTE porque a suíte não renderiza Razor (mesma razão escrita em
    // AcoesDaMinhaInscricaoTests): o que dá pra provar aqui é que a tela consulta o fato certo
    // e oferece as duas saídas.

    private static string Tela(params string[] caminho)
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (Directory.Exists(Path.Combine(tentativa, "Views")))
                return File.ReadAllText(Path.Combine(new[] { tentativa, "Views" }.Concat(caminho).ToArray()));
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir do bin.");
    }

    [Fact]
    public void A_tela_do_torneio_desenha_a_faixa_com_as_duas_saidas()
    {
        var tela = Tela("Torneios", "Details.cshtml");

        Assert.Contains("PerguntasDeInscricao", tela);
        Assert.Contains("ConfirmarInscricao", tela);
        Assert.Contains("RecusarInscricao", tela);
    }

    [Fact]
    public void A_tela_da_recusa_pergunta_antes_de_tirar_alguem_do_torneio()
    {
        // Recusar tira a pessoa do torneio e não tem desfazer: a tela precisa dizer o que vai
        // acontecer com quem fica ANTES do clique, e ter as duas respostas — recusar e manter.
        var tela = Tela("Torneios", "RecusarInscricao.cshtml");

        Assert.Contains("QuemInscreveu", tela);
        Assert.Contains("ConfirmarInscricao", tela);
        Assert.Contains("confirmo", tela);
        // O POST que tira alguém do torneio sem token é o que uma aba maliciosa dispara.
        Assert.Contains("asp-action=\"RecusarInscricao\"", tela);
    }
}
