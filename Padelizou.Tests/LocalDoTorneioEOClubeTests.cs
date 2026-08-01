using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// "Local" e "Clube" eram o MESMO dado pedido duas vezes na tela de edição. Pior: podiam
// divergir — o cabeçalho do torneio mostra o LocalTorneio, então dava pra anunciar um lugar e
// ter o torneio marcado em outro. A criação já pedia só o clube; a edição tinha ficado pra trás.
public class LocalDoTorneioEOClubeTests
{
    private static async Task<(Torneio torneio, Jogador organizador, Clube clubeA, Clube clubeB)>
        MontarAsync(DbPadelContext ctx)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        var clubeA = new Clube { Nome = "Volx Padel" };
        var clubeB = new Clube { Nome = "Arena Beira Rio" };
        ctx.Clubes.AddRange(clubeA, clubeB);
        await ctx.SaveChangesAsync();

        torneio.ClubeId = clubeA.Id;
        torneio.LocalTorneio = clubeA.Nome;
        await ctx.SaveChangesAsync();

        return (torneio, organizador, clubeA, clubeB);
    }

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> EditarAsync(
        DbPadelContext ctx, Torneio torneio, int quemEdita, int clubeId, string? localDigitado) =>
        TestInfra.NovoTorneiosController(ctx, quemEdita).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: localDigitado,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao, clubeId: clubeId,
            quantidadeQuadras: torneio.QuantidadeQuadras, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null);

    [Fact]
    public async Task Trocar_de_clube_leva_o_local_junto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, clubeB) = await MontarAsync(ctx);

        await EditarAsync(ctx, torneio, organizador.Id, clubeB.Id, localDigitado: null);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(clubeB.Id, salvo!.ClubeId);
        Assert.Equal("Arena Beira Rio", salvo.LocalTorneio);
    }

    [Fact]
    public async Task Formulario_antigo_nao_consegue_mais_desencontrar_os_dois()
    {
        // Aba aberta antes do deploy ainda manda o campo de texto. Se ele fosse aceito, o
        // torneio anunciaria "Ginásio X" com o clube apontando pra outro lugar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, clubeA, _) = await MontarAsync(ctx);

        await EditarAsync(ctx, torneio, organizador.Id, clubeA.Id, localDigitado: "Ginásio do Outro Lado");

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal("Volx Padel", salvo!.LocalTorneio);
    }

    [Fact]
    public async Task Clube_que_nao_existe_nao_apaga_o_local()
    {
        // Melhor manter o local que estava do que zerar o que aparece no cabeçalho.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, _, _) = await MontarAsync(ctx);

        await EditarAsync(ctx, torneio, organizador.Id, clubeId: 99999, localDigitado: null);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal("Volx Padel", salvo!.LocalTorneio);
    }
}
