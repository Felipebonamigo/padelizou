using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O TORNEIO QUE AINDA NÃO FOI DIVULGADO (Felipe, 18/08/2026: "ainda vamos divulgar, e só aí
// permitir que o pessoal veja").
//
// Duas mudanças moram aqui, e as duas são o motivo de o campo `Oculto` não servir antes:
//   1. Ocultar deixou de depender do torneio ser RESTRITO. Quem precisa esconder é justamente
//      o torneio ABERTO, montado dias antes do anúncio.
//   2. Oculto virou INVISÍVEL: era só "some da listagem", com o link direto abrindo pra
//      qualquer um. Agora a página responde 404 pra quem não cuida do torneio.
//
// ⚠️ O terceiro escape — quem JÁ ESTÁ INSCRITO continua vendo — tem teste próprio e é o que
// impede o estrago da mudança de significado: esconder um torneio com gente dentro não pode
// trancar do lado de fora quem já pagou.
public class TorneioOcultoTests
{
    private static async Task<(Torneio torneio, Categoria categoria, Jogador organizador)> MontarAsync(
        DbPadelContext ctx, bool oculto, string status = "Inscrições Abertas")
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: status);
        torneio.Oculto = oculto;
        // Aprovado: sem isto, o torneio já estaria fora da vitrine por outro motivo, e o teste
        // não estaria falando do oculto.
        torneio.AprovadoEm = new DateTime(2026, 8, 18);
        await ctx.SaveChangesAsync();
        return (torneio, categoria, organizador);
    }

    private static Task<IActionResult> AbrirAsync(DbPadelContext ctx, int torneioId, int jogadorId) =>
        TestInfra.NovoTorneiosController(ctx, jogadorId).Details(torneioId, null, null);

    // ---- Quem entra e quem não entra ----

    [Fact]
    public async Task Torneio_oculto_da_404_pra_quem_chega_de_fora()
    {
        // O ponto da mudança: até 18/08/2026 este mesmo caso abria a página inteira, porque
        // "oculto" queria dizer só "não aparece na lista".
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: true);

        var visitante = TestInfra.NovoJogador(90);
        ctx.Jogadores.Add(visitante);
        await ctx.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await AbrirAsync(ctx, torneio.Id, visitante.Id));
    }

    [Fact]
    public async Task Torneio_oculto_da_404_pra_quem_nem_esta_logado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: true);

        // Id 0 é como o controller enxerga quem não tem conta: sem claim, sem jogador.
        Assert.IsType<NotFoundResult>(await AbrirAsync(ctx, torneio.Id, 0));
    }

    [Fact]
    public async Task Quem_organiza_continua_abrindo_o_proprio_torneio_oculto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: true);

        Assert.IsType<ViewResult>(await AbrirAsync(ctx, torneio.Id, organizador.Id));
    }

    [Fact]
    public async Task Admin_do_Padelizou_abre_qualquer_torneio_oculto()
    {
        // Ele é quem socorre organizador travado — e um torneio que o admin não consegue abrir
        // é um chamado de WhatsApp que só o banco resolve.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: true);

        var admin = TestInfra.NovoJogador(91);
        admin.IsAdminRaiz = true;
        ctx.Jogadores.Add(admin);
        await ctx.SaveChangesAsync();

        Assert.IsType<ViewResult>(await AbrirAsync(ctx, torneio.Id, admin.Id));
    }

    [Fact]
    public async Task Quem_ja_esta_inscrito_NAO_perde_a_pagina_quando_o_torneio_e_escondido()
    {
        // O escape que protege o torneio já em andamento: o organizador esconde hoje, e quem
        // pagou ontem continua vendo chave, horário e a chave de acesso.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: true);

        var inscrito = await ctx.Duplas
            .Where(d => d.Categoria.TorneioId == torneio.Id)
            .Select(d => d.Jogador1Id)
            .FirstAsync();

        Assert.IsType<ViewResult>(await AbrirAsync(ctx, torneio.Id, inscrito));
    }

    [Fact]
    public async Task Torneio_publicado_abre_pra_qualquer_um()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: false);

        Assert.IsType<ViewResult>(await AbrirAsync(ctx, torneio.Id, 0));
    }

    [Fact]
    public async Task A_grade_de_jogos_do_torneio_oculto_tambem_fecha()
    {
        // A página do torneio não é a única porta: a lista de jogos conta nomes, horários e
        // quadras. Deixar UMA aberta é como o torneio escondido reaparece.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: true);

        Assert.IsType<NotFoundResult>(
            await TestInfra.NovoTorneiosController(ctx, 0).Jogos(torneio.Id, null, null));
        Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Jogos(torneio.Id, null, null));
    }

    // ---- O interruptor ----

    [Fact]
    public async Task Organizador_esconde_e_publica_o_torneio_pelo_botao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: false);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.AlternarVisibilidade(torneio.Id, ocultar: true);
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.Oculto);

        await controller.AlternarVisibilidade(torneio.Id, ocultar: false);
        Assert.False((await ctx.Torneios.FindAsync(torneio.Id))!.Oculto);
    }

    [Fact]
    public async Task Esconder_NAO_depende_de_o_torneio_ser_restrito()
    {
        // A regra que caiu: `Oculto = restrito && oculto`. O torneio do Felipe é aberto —
        // qualquer um pode se inscrever quando ele divulgar —, e era exatamente esse que não
        // tinha como ficar escondido.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: false);
        Assert.False(torneio.Restrito);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AlternarVisibilidade(torneio.Id, ocultar: true);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.True(salvo!.Oculto);
        Assert.False(salvo.Restrito);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_publica_nem_esconde()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarAsync(ctx, oculto: true);

        var estranho = TestInfra.NovoJogador(92);
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, estranho.Id)
                .AlternarVisibilidade(torneio.Id, ocultar: false));

        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.Oculto);
    }

    [Fact]
    public async Task Salvar_a_edicao_do_torneio_NAO_publica_o_que_estava_escondido()
    {
        // A pegadinha que a caixa antiga criava: ela vivia no formulão da gestão, então mexer
        // no preço (ou abrir o formulário e salvar) regravava a visibilidade. Agora o Editar
        // não toca no campo.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: true);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null, dataInicio: torneio.DataInicio,
            precoInscricao: 120m, clubeId: torneio.ClubeId,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.True(salvo!.Oculto);
        Assert.Equal(120m, salvo.PrecoInscricao);
    }

    // ---- As duas portas de inscrição ----

    [Fact]
    public async Task Ninguem_de_fora_se_inscreve_em_torneio_oculto_pela_dupla()
    {
        // A tela some pra essa pessoa (o Details devolve 404), então quem chega ao POST é aba
        // velha ou formulário montado à mão — e nos dois casos a inscrição não pode entrar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarAsync(ctx, oculto: true);

        // CPF de verdade (com dígito verificador): o Create recusa CPF inválido ANTES de
        // qualquer outra coisa, e o teste passaria pelo motivo errado.
        var a = TestInfra.NovoJogador(93);
        a.Cpf = "11144477735";
        a.Nome = "Otávio Silva";
        var b = TestInfra.NovoJogador(94);
        b.Cpf = "52998224725";
        b.Nome = "Bruno Costa";
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();

        var antes = await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id);

        var resposta = await DuplasControllerDe(ctx, a.Id).Create(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome1: a.Nome, cpf1: a.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false);

        Assert.IsType<NotFoundResult>(resposta);
        Assert.Equal(antes, await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task Ninguem_de_fora_se_inscreve_em_americano_oculto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = await MontarAsync(ctx, oculto: true);
        torneio.Formato = "Americano";
        await ctx.SaveChangesAsync();

        var resposta = await TestInfra.NovoTorneiosController(ctx, 0).InscreverIndividual(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome: "Fulano de Tal", cpf: "11144477735");

        Assert.IsType<NotFoundResult>(resposta);
        Assert.Empty(await ctx.InscricoesAmericanas.Where(i => i.CategoriaId == categoria.Id).ToListAsync());
    }

    [Fact]
    public async Task O_organizador_continua_inscrevendo_gente_no_balcao_com_o_torneio_oculto()
    {
        // O outro lado da trava acima: é comum montar o torneio escondido JÁ com os inscritos
        // que confirmaram no WhatsApp. Se o balcão fechasse junto, esconder viraria um estado
        // em que não dá pra trabalhar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = await MontarAsync(ctx, oculto: true);
        torneio.Formato = "Americano";
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).InscreverIndividual(
            torneioId: torneio.Id, categoriaId: categoria.Id,
            nome: "Fulano de Tal", cpf: "11144477735");

        Assert.Single(await ctx.InscricoesAmericanas.Where(i => i.CategoriaId == categoria.Id).ToListAsync());
    }

    // ---- O cartaz de divulgação ----

    [Fact]
    public async Task O_cartaz_do_torneio_oculto_nao_sai_pra_quem_esta_de_fora()
    {
        // A borda que menos parece porta: o cartaz é FEITO pra circular fora do site, com nome,
        // data, local e preço na arte. Se ele continuasse saindo, esconder o torneio não
        // esconderia nada — bastaria o link do PNG.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = await MontarAsync(ctx, oculto: true);

        Assert.IsType<NotFoundResult>(await CartoesControllerDe(ctx, 0).Cartaz(torneio.Id));
        Assert.IsType<ViewResult>(await CartoesControllerDe(ctx, organizador.Id).Cartaz(torneio.Id));
    }

    // ---- A régua, sem HTTP no meio ----

    [Fact]
    public void Torneio_visivel_abre_pra_todo_mundo_e_o_oculto_so_pros_tres()
    {
        var visivel = new Torneio { Nome = "x", Codigo = "x" };
        var oculto = new Torneio { Nome = "x", Codigo = "x", Oculto = true };

        Assert.True(VisibilidadeDoTorneio.PodeAbrir(visivel, cuidaDoTorneio: false, estaInscrito: false));
        Assert.False(VisibilidadeDoTorneio.PodeAbrir(oculto, cuidaDoTorneio: false, estaInscrito: false));
        Assert.True(VisibilidadeDoTorneio.PodeAbrir(oculto, cuidaDoTorneio: true, estaInscrito: false));
        Assert.True(VisibilidadeDoTorneio.PodeAbrir(oculto, cuidaDoTorneio: false, estaInscrito: true));
    }

    [Fact]
    public void O_oculto_continua_fora_da_vitrine()
    {
        // A régua velha não foi substituída pela nova: a página fecha E o torneio some da
        // listagem, da Home e do Google. Se alguém "simplificar" uma das duas um dia, o
        // torneio escondido volta a aparecer em algum lugar.
        var oculto = new Torneio { Nome = "x", Codigo = "x", Oculto = true, AprovadoEm = DateTime.Now };

        Assert.False(PermissaoDeOrganizador.ApareceNaVitrine(oculto));
        Assert.False(PermissaoDeOrganizador.ApareceParaOPublico(oculto));
    }

    // ---- Bastidores ----

    private static CartoesController CartoesControllerDe(DbPadelContext ctx, int usuarioLogadoId)
    {
        var ambiente = Substitute.For<IWebHostEnvironment>();
        ambiente.WebRootPath.Returns("");

        // Fonte apontando pra pasta vazia: a PÁGINA do cartaz não depende dela (só o PNG), e o
        // que este teste quer saber é quem entra, não como a arte fica.
        var controller = new CartoesController(ctx, new FonteDoCartao(""), ambiente, new EstatisticasService(ctx));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        return controller;
    }

    private static DuplasController DuplasControllerDe(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            // Ranking RS sem chave — ver AceitarConviteTests.
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance);

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
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }
}
