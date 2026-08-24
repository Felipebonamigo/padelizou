using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// CANCELAR UM TORNEIO: ele não vai acontecer.
//
// Faltava a terceira saída. Finalizar diz que o torneio ACONTECEU (procura campeão, distribui
// ponto); esconder diz que ele existe mas não aparece. Choveu, o clube perdeu a quadra, não deu
// gente — e o organizador só tinha as duas erradas: deixar "Inscrições Abertas" pendurado pra
// sempre, ou marcar como finalizado um torneio sem um jogo sequer.
//
// Pedido do Felipe em 07/08/2026, com duas decisões dele: o sistema NÃO estorna (mostra quem
// pagou e quanto, a devolução é por fora) e o torneio SOME da listagem.
public class CancelarTorneioTests
{
    [Fact]
    public async Task Cancelar_marca_o_torneio_e_guarda_o_motivo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .CancelarTorneio(torneio.Id, "Choveu o fim de semana inteiro");

        Assert.IsType<RedirectToActionResult>(resultado);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(CancelamentoDoTorneio.Status, salvo!.Status);
        Assert.Equal("Choveu o fim de semana inteiro", salvo.MotivoCancelamento);
        Assert.NotNull(salvo.CanceladoEm);
    }

    [Fact]
    public async Task Todo_inscrito_e_avisado_do_cancelamento()
    {
        // O aviso mais caro de não chegar: sem ele a pessoa sai de casa e vai pra quadra.
        // Mesmo assim ele saiu do WhatsApp em 21/08/2026, com o resto da família de torneio —
        // o e-mail continua indo, e é ele que alcança quem não instalou o app.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 3, status: "Inscrições Abertas");
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push)
            .CancelarTorneio(torneio.Id, "Sem quadra");

        // 3 duplas = 6 pessoas, cada uma avisada uma vez.
        await push.Received(6).EnviarParaJogadorAsync(
            Arg.Any<int>(), "Torneio cancelado",
            Arg.Is<string>(c => c != null && c.Contains("Sem quadra")),
            Arg.Any<string?>(), AlcanceDoAviso.SoApp);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_cancela_o_torneio_dos_outros()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var estranho = new Jogador { Nome = "Estranho", Cpf = "22255588896" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id)
            .CancelarTorneio(torneio.Id, "quero cancelar o dos outros");

        Assert.IsType<ForbidResult>(resultado);
        Assert.NotEqual(CancelamentoDoTorneio.Status, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Torneio_que_ja_aconteceu_nao_se_cancela()
    {
        // Finalizado tem campeão, ponto de ranking distribuído e conquista no perfil de gente.
        // Cancelar isso não é cancelar, é reescrever o passado.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.CancelarTorneio(torneio.Id, null);

        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Cancelado_nao_volta_a_andar_por_um_POST_feito_a_mao()
    {
        // A tela esconde o botão de encerrar inscrições, mas aba velha e POST montado à mão
        // ressuscitariam o torneio em "Chaves em Sorteio" — com os inscritos já avisados de
        // que ele não vai acontecer.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.CancelarTorneio(torneio.Id, null);
        await controller.EncerrarInscricoes(torneio.Id);

        Assert.Equal(CancelamentoDoTorneio.Status, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Cancelado_sai_da_listagem_publica_mas_o_organizador_continua_vendo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        await TestInfra.NovoTorneiosController(ctx, organizador.Id).CancelarTorneio(torneio.Id, null);

        // Quem passa na listagem sem ser organizador não vê mais.
        var deFora = new Jogador { Nome = "Curioso", Cpf = "22255588896" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var viewDeFora = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, deFora.Id).Index());
        var abertosDeFora = (List<Torneio>)viewDeFora.ViewData["Abertos"]!;
        var emAndamentoDeFora = (List<Torneio>)viewDeFora.ViewData["EmAndamento"]!;
        Assert.DoesNotContain(abertosDeFora, t => t.Id == torneio.Id);
        Assert.DoesNotContain(emAndamentoDeFora, t => t.Id == torneio.Id);

        // O organizador continua vendo — é lá que ele resolve as devoluções.
        var viewDono = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, organizador.Id).Index());
        var canceladosDoDono = (List<Torneio>)viewDono.ViewData["Cancelados"]!;
        Assert.Contains(canceladosDoDono, t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Quem_pagou_aparece_na_lista_de_devolucao_com_o_valor_da_dupla()
    {
        // Preço é POR PESSOA: a dupla pagou o dobro, e é o dobro que tem que voltar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.PrecoInscricao = 80m;

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        duplas[0].Pago = true;   // esta pagou
        duplas[1].Pago = false;  // esta não
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.CancelarTorneio(torneio.Id, null);
        await controller.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null);

        var devolver = (List<Padelizou.ViewModels.DevolucaoPendenteVM>)controller.ViewData["PagosParaDevolver"]!;
        var linha = Assert.Single(devolver);
        Assert.Equal(160m, linha.Valor);   // 80 por pessoa × 2
    }

    [Fact]
    public async Task Cancelar_libera_o_nome_pra_remarcar()
    {
        // O caso mais comum de cancelar é justamente remarcar. Se o nome continuasse preso,
        // o organizador teria que inventar "Amigos do Eder 2" pro mesmo torneio.
        var meus = new[] { ("Amigos do Eder", (string?)CancelamentoDoTorneio.Status) };

        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, "Amigos do Eder"));
    }
}
