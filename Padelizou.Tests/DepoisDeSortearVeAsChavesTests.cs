using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;

namespace Padelizou.Tests;

// SORTEAR AS CHAVES CAI NA ABA PADRÃO (07/09/2026).
//
// Pedido do Felipe, testando o torneio do Er: *"quando clicar em sortear as chaves, [abrir]
// uma tela de todas chaves"*.
//
// É o gêmeo do que aconteceu no encerrar inscrições. O organizador aperta o botão que é o
// clímax da montagem do torneio — os grupos saem, os jogos nascem — e a página recarrega na
// aba que estava, sem levar ninguém pra ver o que acabou de ser sorteado. A aba "Chaves e
// Grupos" (`#grupos`) existe e é exatamente essa tela.
//
// São DUAS saídas de sucesso no GerarChaves, e as duas precisam levar pro mesmo lugar: o
// torneio "por ordem de liberação" (sem horário previsto) sai por um `return` antes, e o que
// tem grade sai pelo de baixo. Uma só consertada deixaria o buraco na metade dos torneios.
public class DepoisDeSortearVeAsChavesTests
{
    [Fact]
    public async Task Sortear_leva_o_organizador_pra_aba_de_chaves_e_grupos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var resposta = await controller.GerarChaves(torneio.Id);

        // O sorteio de fato rodou: sem isto o teste passaria com o torneio intocado.
        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotEmpty(ctx.Partidas);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal("grupos", redirect.Fragment);
    }

    [Fact]
    public async Task Torneio_por_ordem_de_liberacao_tambem_cai_nas_chaves()
    {
        // ⚠️ O caminho do `SemHorarioPrevisto` TINHA um `return` próprio, antes do encaixe na
        // grade — e este teste nasceu porque ele era o que ficava de fora quando só o return de
        // baixo era consertado. Desde 09/09/2026 o atalho não existe mais: o por-ordem passa
        // pela grade igual e só apaga a quadra no fim (Services/OrdemDeLiberacao). O teste fica,
        // porque o destino do redirect continua sendo dele.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var resposta = await controller.GerarChaves(torneio.Id);

        Assert.NotEmpty(ctx.Partidas);
        // No modo por ordem o que falta é a QUADRA, não a hora.
        Assert.All(ctx.Partidas, p => Assert.True(string.IsNullOrEmpty(p.NomeQuadra)));

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("grupos", redirect.Fragment);
    }
}
