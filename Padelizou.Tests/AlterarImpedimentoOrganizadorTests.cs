using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O ORGANIZADOR TROCA O IMPEDIMENTO DE OUTRA PESSOA — aba Pagamentos (07/09/2026).
//
// A regra (quem pode, quando, e o que acontece com o dinheiro) tem trava própria em
// AlteracaoDeImpedimentoTests (MotivoParaOrganizadorNaoAlterar). Este arquivo trava a FIAÇÃO
// do controller: autorização, gravação de verdade, e o aviso pros DOIS jogadores da dupla —
// diferente do fluxo do próprio jogador, que avisa só o parceiro (ele mesmo já sabe que mexeu).
public class AlterarImpedimentoOrganizadorTests
{
    [Fact]
    public async Task Organizador_troca_o_impedimento_de_qualquer_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(salva!.ImpedimentoSextaNoite);
    }

    [Fact]
    public async Task So_organizador_troca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.False((await ctx.Duplas.FindAsync(dupla.Id))!.ImpedimentoSextaNoite);
    }

    [Fact]
    public async Task Nao_troca_depois_do_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Chaves em Sorteio");
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P1",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Agendada",
        });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(duplas[0].Id, TurnoDoImpedimento.SextaNoite);

        Assert.False((await ctx.Duplas.FindAsync(duplas[0].Id))!.ImpedimentoSextaNoite);
    }

    // ⚠️ O CORAÇÃO DA DECISÃO DO FELIPE: dupla PAGA pode ter o impedimento trocado — o valor
    // congelado muda (mesma régua de Aplicar), mas `Pago` continua exatamente como estava.
    // Nada cobra nem estorna sozinho.
    [Fact]
    public async Task Troca_dupla_ja_paga_sem_mexer_no_status_de_pago()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        torneio.TaxaPorImpedimento = 20m;
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        dupla.Pago = true;
        dupla.ValorInscricao = 300m;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        var salva = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(salva!.ImpedimentoSextaNoite);
        Assert.True(salva.Pago);
        Assert.Equal(320m, salva.ValorInscricao);
    }

    // Diferente do jogador (AlterarImpedimento avisa só o PARCEIRO): o organizador mexeu por
    // fora, então os DOIS precisam saber — nenhum dos dois clicou em nada.
    [Fact]
    public async Task Avisa_os_dois_jogadores_da_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, org.Id, push: push)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador1Id, "O organizador mudou seu impedimento",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(dupla.Jogador2Id!.Value, "O organizador mudou seu impedimento",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Time_nao_tem_impedimento_pra_organizador_trocar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = org.Id, NomeTime = "Time A" };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarImpedimentoOrganizador(dupla.Id, TurnoDoImpedimento.SextaNoite);

        Assert.False((await ctx.Duplas.FindAsync(dupla.Id))!.ImpedimentoSextaNoite);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(resultado);
    }
}
