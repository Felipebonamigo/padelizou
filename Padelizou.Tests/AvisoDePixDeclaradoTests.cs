using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 27/10/2026 — "JÁ PAGUEI" NO PIX DIRETO TEM QUE AVISAR QUEM CONFIRMA.
//
// 🗣️ O Jônatas (professor) pagou a mensalidade por Pix às 14h11, clicou "já paguei" e mandou no
// WhatsApp do Felipe às 19h53: *"paguei o mês lá, me libera depois"*. 🗣️ Felipe: *"o professor
// pagou e não liberou"*. O Pix direto não tem confirmação automática (Services/PixDireto): quem
// libera é o admin raiz, conferindo o extrato. Só que o "já paguei" só mudava o status — ninguém
// era avisado, e a cobrança esperava calada na fila até alguém lembrar de abrir /Admin/PixDireto.
//
// Agora o "já paguei" avisa o ADMIN RAIZ — só ele, porque só ele confirma (AdminController.
// ConfirmarPix exige raiz). App + WhatsApp: pessoal, urgente (tem gente travada) e acionável.
// ⚠️ O aviso é acessório: falhar o push não pode desfazer o "já paguei".
public class AvisoDePixDeclaradoTests
{
    private sealed record Cenario(DbPadelContext Ctx, Jogador Raiz, Jogador AdminGeral, Jogador Professor, Pagamento Cobranca);

    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var raiz = new Jogador { Nome = "Felipe", Cpf = "11100000001", IsAdminRaiz = true };
        var geral = new Jogador { Nome = "Admin Geral", Cpf = "11100000002", IsAdminGeral = true };
        var professor = new Jogador { Nome = "Jônatas Portal", Cpf = "11100000003", IsProfessor = true };
        ctx.Jogadores.AddRange(raiz, geral, professor);
        ctx.SaveChanges();

        var cobranca = new Pagamento
        {
            Tipo = PixDireto.TipoAssinatura, JogadorId = professor.Id, Valor = 49.90m, Comissao = 49.90m,
            MetodoPagamento = PixDireto.Metodo, Status = "Pendente",
        };
        ctx.Pagamentos.Add(cobranca);
        ctx.SaveChanges();
        return new Cenario(ctx, raiz, geral, professor, cobranca);
    }

    private static PagamentosController Controller(DbPadelContext ctx, int logado, IPushNotificationService push)
    {
        var controller = new PagamentosController(
            ctx, Microsoft.Extensions.Options.Options.Create(new AsaasSettings()),
            Substitute.For<IPagamentoInscricaoService>(),
            Substitute.For<IAsaasService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PagamentosController>.Instance,
            push);
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, logado.ToString()) }, "Teste")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Ja_paguei_avisa_o_admin_raiz_com_quem_e_quanto()
    {
        var c = Montar();
        var push = Substitute.For<IPushNotificationService>();

        await Controller(c.Ctx, c.Professor.Id, push).DeclararPix(c.Cobranca.Id);

        await push.Received(1).EnviarParaJogadorAsync(c.Raiz.Id,
            Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null && corpo.Contains("Jônatas Portal") && corpo.Contains("49,90")),
            "/Admin/PixDireto",
            AlcanceDoAviso.AppEWhatsApp);
    }

    [Fact]
    public async Task So_o_raiz_e_avisado_nem_o_admin_geral_nem_quem_pagou()
    {
        // Só o raiz confirma Pix (AdminController.ConfirmarPix): avisar quem não pode agir é ruído.
        var c = Montar();
        var push = Substitute.For<IPushNotificationService>();

        await Controller(c.Ctx, c.Professor.Id, push).DeclararPix(c.Cobranca.Id);

        await push.DidNotReceive().EnviarParaJogadorAsync(c.AdminGeral.Id,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.DidNotReceive().EnviarParaJogadorAsync(c.Professor.Id,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Falha_no_aviso_nao_desfaz_o_ja_paguei()
    {
        var c = Montar();
        var push = Substitute.For<IPushNotificationService>();
        push.EnviarParaJogadorAsync(default, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("push fora do ar")));

        var controller = Controller(c.Ctx, c.Professor.Id, push);
        await controller.DeclararPix(c.Cobranca.Id);

        c.Ctx.ChangeTracker.Clear();
        var gravada = await c.Ctx.Pagamentos.SingleAsync(p => p.Id == c.Cobranca.Id);
        Assert.Equal(PixDireto.AguardandoConfirmacao, gravada.Status);
        Assert.NotNull(controller.TempData["Sucesso"]);
    }

    [Fact]
    public async Task Declaracao_recusada_nao_avisa_ninguem()
    {
        // "Já avisou", cobrança de outro, já confirmada: nada mudou, nada a avisar.
        var c = Montar();
        var push = Substitute.For<IPushNotificationService>();

        await Controller(c.Ctx, c.AdminGeral.Id, push).DeclararPix(c.Cobranca.Id);   // não é dele

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }
}
