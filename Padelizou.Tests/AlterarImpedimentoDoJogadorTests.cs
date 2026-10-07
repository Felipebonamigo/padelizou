using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O JOGADOR TROCA O PRÓPRIO IMPEDIMENTO — o POST que o botão "Trocar" da inscrição dispara.
//
// 🗣️ Felipe, 07/10/2026: *"o Andrey me falou que não está ficando salvo o impedimento dele"* —
// ele tentou pelo celular, não viu erro nenhum, e quando o Felipe foi olhar a inscrição não
// tinha impedimento.
//
// ⚠️ ATÉ HOJE ESTE CAMINHO NÃO TINHA TESTE DE CONTROLLER: só o do ORGANIZADOR
// (AlterarImpedimentoOrganizadorTests) chamava a ação, e o do jogador era coberto só pela regra
// pura (AlteracaoDeImpedimentoTests) e por testes de fonte da tela. Ninguém tinha provado que a
// fiação — dono, status, gravar, avisar o parceiro, mensagem — fecha de ponta a ponta.
public class AlterarImpedimentoDoJogadorTests
{
    private static readonly DateTime Sabado = new(2026, 10, 10);

    // Torneio de sábado, inscrição NÃO paga, com o Andrey como Jogador1 — o cenário do relato.
    private static async Task<(Torneio torneio, Dupla dupla, Jogador andrey, Jogador maickel)> MontarAsync(
        DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.DataInicio = Sabado;
        torneio.PermiteImpedimentos = true;
        torneio.PrecoInscricao = 125m;
        torneio.TaxaPorImpedimento = 0m;

        var dupla = await ctx.Duplas.Include(d => d.Jogador1).Include(d => d.Jogador2)
            .FirstAsync(d => d.CategoriaId == categoria.Id);
        await ctx.SaveChangesAsync();

        return (torneio, dupla, dupla.Jogador1, dupla.Jogador2!);
    }

    [Fact]
    public async Task Jogador_troca_o_proprio_impedimento_e_fica_gravado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, dupla, andrey, _) = await MontarAsync(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, andrey.Id)
            .AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SabadoManha);

        Assert.IsType<RedirectToActionResult>(resultado);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(salva!.ImpedimentoSabadoManha);
        Assert.Equal(andrey.Id, salva.ImpedimentoAlteradoPorId);
    }

    [Fact]
    public async Task O_parceiro_tambem_pode_trocar_o_impedimento_da_inscricao()
    {
        // O impedimento é da INSCRIÇÃO, não de quem marcou — vale pros dois da dupla.
        using var ctx = TestInfra.NovoContexto();
        var (_, dupla, _, maickel) = await MontarAsync(ctx);

        await TestInfra.NovoTorneiosController(ctx, maickel.Id)
            .AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SabadoTarde);

        Assert.True((await ctx.Duplas.FindAsync(dupla.Id))!.ImpedimentoSabadoTarde);
    }

    [Fact]
    public async Task Quem_nao_e_da_dupla_nao_troca_e_recebe_o_motivo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, dupla, _, _) = await MontarAsync(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000077" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, intruso.Id);
        await controller.AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SabadoManha);

        Assert.False((await ctx.Duplas.FindAsync(dupla.Id))!.ImpedimentoSabadoManha);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task O_parceiro_e_avisado_da_troca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, dupla, andrey, maickel) = await MontarAsync(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, andrey.Id, push: push)
            .AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SabadoManha);

        await push.Received(1).EnviarParaJogadorAsync(maickel.Id, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task A_troca_deixa_um_aviso_de_sucesso_pra_quem_clicou()
    {
        // O relato foi "não apareceu nada" — nem erro, nem confirmação. Quem salva precisa ver
        // que salvou.
        using var ctx = TestInfra.NovoContexto();
        var (_, dupla, andrey, _) = await MontarAsync(ctx);

        var controller = TestInfra.NovoTorneiosController(ctx, andrey.Id);
        await controller.AlterarImpedimento(dupla.Id, TurnoDoImpedimento.SabadoManha);

        Assert.NotNull(controller.TempData["Sucesso"]);
    }
}
