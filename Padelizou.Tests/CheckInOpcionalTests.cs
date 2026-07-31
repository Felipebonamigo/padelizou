using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// Nem todo torneio quer lista de chamada: num interno de 8 duplas o organizador conhece todo
// mundo de vista, e a tela vira mais um botão pra ignorar. O interruptor esconde o botão — e
// o servidor recusa junto, porque link antigo e histórico do navegador continuam abrindo a
// tela mesmo sem botão nenhum.
public class CheckInOpcionalTests
{
    private static async Task<(Torneio torneio, Jogador organizador)> MontarAsync(DbPadelContext ctx, bool usaCheckIn)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Chaves em Sorteio");
        torneio.UsaCheckIn = usaCheckIn;
        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    [Fact]
    public async Task Torneio_novo_nasce_com_check_in_ligado()
    {
        // Preserva o que já existia: desligar é escolha, não pegadinha pra quem já tinha
        // torneio no ar.
        Assert.True(new Torneio { Nome = "X", Codigo = "X1" }.UsaCheckIn);
    }

    [Fact]
    public async Task Com_o_check_in_ligado_a_tela_abre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: true);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id);

        Assert.IsType<ViewResult>(resultado);
    }

    [Fact]
    public async Task Desligado_a_tela_recusa_mesmo_pelo_link_direto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: false);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).CheckIn(torneio.Id);

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redir.ActionName);
    }

    [Fact]
    public async Task Desligado_nao_da_pra_marcar_presenca_por_POST_feito_a_mao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: false);
        var dupla = await ctx.Duplas.FirstAsync(d => d.Categoria.TorneioId == torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Id, presente: true);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);
    }

    [Fact]
    public async Task Ligado_a_presenca_e_gravada_e_da_pra_desfazer()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: true);
        var dupla = await ctx.Duplas.FirstAsync(d => d.Categoria.TorneioId == torneio.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.MarcarCheckIn(dupla.Id, presente: true);
        Assert.NotNull((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);

        await controller.MarcarCheckIn(dupla.Id, presente: false);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);
    }

    [Fact]
    public async Task Quem_nao_organiza_continua_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx, usaCheckIn: true);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "22255588896" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, estranho.Id).CheckIn(torneio.Id));
    }
}
