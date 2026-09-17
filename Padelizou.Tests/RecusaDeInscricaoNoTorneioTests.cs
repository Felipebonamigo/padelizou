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

// RECUSAR UMA INSCRIÇÃO QUE OUTRA PESSOA FEZ POR VOCÊ.
//
// 🗣️ Felipe, 16/09/2026: *"'Você foi inscrito para um torneio por Maickel' — para quando alguem
// inscrever um parceiro no torneio, avisar o parceiro e permitir recusar, ao recusar o primeiro
// fica sozinho no torneio e o avisa"*.
//
// ⚠️ O QUE ESTE ARQUIVO SEGURA, e que nenhum teste pegava antes: a inscrição em dupla é a única
// porta do sistema em que uma pessoa entra num compromisso — com data, lugar e DINHEIRO — sem
// ter clicado em nada. O aviso já existia desde 31/07/2026; o que faltava era a saída nomeada,
// e uma saída que ninguém acha não é saída (é a mesma lição do mural dos Desafios, onde "poder
// sair" só existe porque o aviso conta que você entrou).
//
// ⚠️ A RECUSA REUSA O EFEITO DA DESISTÊNCIA de propósito (Services/DesistenciaDeInscricao): sai
// um, o outro fica inscrito e sem parceiro, e a vaga NÃO abre. Duas réguas escritas separadas
// pra "tirar alguém de uma dupla" é como uma delas acaba deixando sair depois do sorteio.
public class RecusaDeInscricaoNoTorneioTests
{
    private const string CpfA = "11144477735";
    private const string CpfB = "22255588846";
    private const string CpfC = "33366699957";

    private static async Task<(DbPadelContext ctx, Categoria cat, Jogador a, Jogador b, Jogador c)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Copa de Verão", Codigo = "CV01", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var a = new Jogador { Nome = "Maickel Souza", Cpf = CpfA };
        var b = new Jogador { Nome = "Otávio Wunsch", Cpf = CpfB };
        var c = new Jogador { Nome = "Geovani Batista", Cpf = CpfC };
        ctx.Jogadores.AddRange(a, b, c);
        await ctx.SaveChangesAsync();

        return (ctx, cat, a, b, c);
    }

    private static DuplasController Duplas(DbPadelContext ctx, int usuarioLogadoId,
        IPushNotificationService? push = null)
    {
        push ??= Substitute.For<IPushNotificationService>();

        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            push,
            Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, push),
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

    // Inscreve uma dupla pela porta de verdade — o formulário do torneio. Passar pelo
    // controller (e não semear a Dupla na mão) é o que prova que a pergunta NASCE junto com a
    // inscrição: semeada à mão, o teste passaria mesmo com o gancho esquecido.
    private static Task InscreverAsync(DuplasController controller, Categoria cat,
        Jogador jogador1, Jogador? jogador2) =>
        controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: jogador1.Nome, cpf1: jogador1.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: jogador2?.Nome, cpf2: jogador2?.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            semParceiro: jogador2 == null);

    // ── A PERGUNTA NASCE COM A INSCRIÇÃO ─────────────────────────────────────────────────

    [Fact]
    public async Task Inscrever_um_parceiro_deixa_a_pergunta_pra_ELE_e_nao_pra_quem_clicou()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);

        var pergunta = await ctx.InscritosPorOutro.SingleAsync();
        Assert.Equal(b.Id, pergunta.JogadorId);
        Assert.Equal(a.Id, pergunta.InscritoPorId);
        Assert.Null(pergunta.ConfirmadoEm);
    }

    [Fact]
    public async Task Organizador_que_inscreve_a_dupla_inteira_pergunta_aos_DOIS()
    {
        // Ninguém dos dois clicou — é o caso em que a inscrição pode ser notícia pros dois.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, c.Id), cat, a, b);

        var perguntas = await ctx.InscritosPorOutro.OrderBy(p => p.JogadorId).ToListAsync();
        Assert.Equal(new[] { a.Id, b.Id }, perguntas.Select(p => p.JogadorId));
        Assert.All(perguntas, p => Assert.Equal(c.Id, p.InscritoPorId));
    }

    [Fact]
    public async Task Quem_se_inscreve_sozinho_nao_recebe_pergunta_nenhuma()
    {
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, null);

        Assert.Empty(await ctx.InscritosPorOutro.ToListAsync());
    }

    [Fact]
    public async Task O_aviso_diz_QUEM_inscreveu_e_leva_pra_tela_de_recusa()
    {
        // Sem o nome, o aviso é um comunicado sem responsável; sem o link, a recusa fica a
        // três telas de distância de quem precisa dela.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await InscreverAsync(Duplas(ctx, a.Id, push), cat, a, b);

        await push.Received(1).EnviarParaJogadorAsync(b.Id,
            Arg.Is<string>(t => t != null && t.Contains("Você foi inscrito para um torneio por Maickel")),
            Arg.Any<string>(),
            Arg.Is<string?>(u => u != null && u.Contains("RecusarInscricao")),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_clicou_continua_recebendo_o_aviso_de_sempre()
    {
        // Quem se inscreveu não foi inscrito por ninguém: mandar "você foi inscrito por você
        // mesmo" com um botão de recusar é o tipo de aviso que ensina a ignorar o canal.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await InscreverAsync(Duplas(ctx, a.Id, push), cat, a, b);

        await push.Received(1).EnviarParaJogadorAsync(a.Id,
            Arg.Is<string>(t => t != null && t.Contains("Inscrição confirmada")),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── A RECUSA ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recusar_deixa_quem_inscreveu_SOZINHO_no_torneio()
    {
        // É a frase do pedido: "ao recusar o primeiro fica sozinho no torneio".
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, b.Id).RecusarInscricao(duplaId, confirmo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(a.Id, dupla.Jogador1Id);
        Assert.Null(dupla.Jogador2Id);
        // A pergunta some junto: respondida é respondida, e uma linha órfã traria a faixa de
        // volta pra quem já saiu da inscrição.
        Assert.Empty(await ctx.InscritosPorOutro.ToListAsync());
    }

    [Fact]
    public async Task Quem_ficou_sozinho_e_avisado_da_recusa()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, b.Id, push: push)
            .RecusarInscricao(duplaId, confirmo: true);

        await push.Received(1).EnviarParaJogadorAsync(a.Id,
            Arg.Is<string>(t => t != null && t.Contains("recusou")),
            Arg.Is<string>(c => c != null && c.Contains("Copa de Verão")),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_inscreveu_de_fora_da_dupla_tambem_e_avisado()
    {
        // O organizador que montou a dupla não está nela. Sem este aviso ele descobriria a
        // recusa só na hora de montar a chave — que é tarde pra chamar outra pessoa.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await InscreverAsync(Duplas(ctx, c.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, b.Id, push: push)
            .RecusarInscricao(duplaId, confirmo: true);

        await push.Received(1).EnviarParaJogadorAsync(c.Id,
            Arg.Is<string>(t => t != null && t.Contains("recusou")),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_fez_a_propria_inscricao_nao_entra_pela_porta_da_recusa()
    {
        // Quem clicou tem a porta de sempre (desistir), que PERGUNTA se sai só ele ou os dois.
        // Deixar a recusa responder por ele seria decidir a pergunta no lugar da pessoa.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, a.Id).RecusarInscricao(duplaId, confirmo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(b.Id, dupla.Jogador2Id);
        Assert.Single(await ctx.InscritosPorOutro.ToListAsync());
    }

    [Fact]
    public async Task Estranho_nao_recusa_inscricao_alheia()
    {
        // Sem esta trava, a recusa vira um botão de tirar qualquer um do torneio.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, c.Id).RecusarInscricao(duplaId, confirmo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(b.Id, dupla.Jogador2Id);
    }

    [Fact]
    public async Task Depois_do_sorteio_a_recusa_e_assunto_do_organizador()
    {
        // A dupla já está numa chave, com jogos marcados e adversários contando com ela.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        var torneio = await ctx.Torneios.FindAsync(cat.TorneioId);
        torneio!.Status = "Chaves em Sorteio";
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, b.Id).RecusarInscricao(duplaId, confirmo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(b.Id, dupla.Jogador2Id);
    }

    [Fact]
    public async Task Quem_recusa_sendo_o_unico_da_inscricao_devolve_a_vaga()
    {
        // O parceiro já tinha saído: não sobra ninguém pra segurar a vaga, e ela volta pra
        // fila — mesma regra da desistência de quem está sozinho.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, b.Id), cat, a, null);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, a.Id).RecusarInscricao(duplaId, confirmo: true);

        Assert.Empty(await ctx.Duplas.ToListAsync());
        Assert.Empty(await ctx.InscritosPorOutro.ToListAsync());
    }

    [Fact]
    public async Task A_inscricao_paga_continua_paga_pra_quem_ficou()
    {
        // Recusar não devolve dinheiro nem tira o que já foi pago de quem continua inscrito —
        // a devolução é botão do organizador (ESTORNO.md), e quem fica não pediu nada.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var dupla = await ctx.Duplas.SingleAsync();
        dupla.Pago = true;
        dupla.PagoEm = DateTime.Now;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, b.Id).RecusarInscricao(dupla.Id, confirmo: true);

        var depois = await ctx.Duplas.SingleAsync();
        Assert.True(depois.Pago);
        Assert.Equal(a.Id, depois.Jogador1Id);
    }

    // ── "ESTÁ CERTO" ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirmar_tira_a_pergunta_sem_mexer_na_inscricao()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, b.Id).ConfirmarInscricao(duplaId);

        var pergunta = await ctx.InscritosPorOutro.SingleAsync();
        Assert.NotNull(pergunta.ConfirmadoEm);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(b.Id, dupla.Jogador2Id);
    }

    [Fact]
    public async Task Ninguem_confirma_a_pergunta_do_outro()
    {
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await TestInfra.NovoTorneiosController(ctx, c.Id).ConfirmarInscricao(duplaId);

        Assert.Null((await ctx.InscritosPorOutro.SingleAsync()).ConfirmadoEm);
    }

    // ── A TROCA DE PARCEIRO É A SEGUNDA PORTA ────────────────────────────────────────────

    [Fact]
    public async Task Trocar_o_parceiro_pergunta_pro_NOVO_e_tira_a_pergunta_do_antigo()
    {
        // Quem entra por uma troca também não clicou em nada; e quem saiu não tem mais o que
        // responder — a linha dele traria a faixa de volta numa inscrição que não é mais dele.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _1 = ctx;

        await InscreverAsync(Duplas(ctx, a.Id), cat, a, b);
        var duplaId = (await ctx.Duplas.SingleAsync()).Id;

        await Duplas(ctx, a.Id).TrocarParceiro(duplaId, cpfNovoParceiro: c.Cpf,
            nomeNovoParceiro: c.Nome);

        var pergunta = await ctx.InscritosPorOutro.SingleAsync();
        Assert.Equal(c.Id, pergunta.JogadorId);
        Assert.Equal(a.Id, pergunta.InscritoPorId);
    }

    // ── A TERCEIRA PORTA: A INSCRIÇÃO QUE NASCE DO PAGAMENTO ─────────────────────────────
    //
    // ⚠️ No torneio que EXIGE pagamento na inscrição, a dupla não nasce no Create: ela nasce
    // quando o webhook do Asaas confirma (PagamentoInscricaoService). Sem o gancho aqui, esses
    // torneios — justamente os que envolvem dinheiro — ficariam com o parceiro inscrito, pago,
    // sem aviso nenhum e sem saída.

    [Fact]
    public async Task A_inscricao_que_nasce_do_pagamento_tambem_pergunta_ao_parceiro()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        var pagamento = new Pagamento
        {
            Tipo = "TorneioDupla",
            Status = "Confirmado",
            Valor = 200m,
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(new DadosInscricaoTorneio(
                cat.TorneioId, cat.Id, a.Id, b.Id, false, false, false, false, false,
                InscritoPorId: a.Id)),
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await ServicoDePagamento(ctx).EfetivarAsync(pagamento);

        var pergunta = await ctx.InscritosPorOutro.SingleAsync();
        Assert.Equal(b.Id, pergunta.JogadorId);
        Assert.Equal(a.Id, pergunta.InscritoPorId);
    }

    [Fact]
    public async Task Cobranca_antiga_sem_autor_gravado_nao_pergunta_nada()
    {
        // Pagamento criado ANTES desta coluna existir: o JSON não tem `InscritoPorId`, e a
        // desserialização traz nulo. A inscrição nasce como sempre nasceu — sem faixa, sem
        // aviso de recusa, sem nada quebrado.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _1 = ctx;

        var pagamento = new Pagamento
        {
            Tipo = "TorneioDupla",
            Status = "Confirmado",
            Valor = 200m,
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(new DadosInscricaoTorneio(
                cat.TorneioId, cat.Id, a.Id, b.Id, false, false, false, false, false)),
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await ServicoDePagamento(ctx).EfetivarAsync(pagamento);

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Empty(await ctx.InscritosPorOutro.ToListAsync());
    }

    private static PagamentoInscricaoService ServicoDePagamento(DbPadelContext ctx,
        IPushNotificationService? push = null)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Microsoft.Extensions.Options.Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            push ?? Substitute.For<IPushNotificationService>(),
            Microsoft.Extensions.Options.Options.Create(new TaxasExibicao()),
            Microsoft.Extensions.Options.Options.Create(new PlanoProfessorSettings()));
    }
}
