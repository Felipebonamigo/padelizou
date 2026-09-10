using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// NADA SOBRE A CHAVE CHEGA AO JOGADOR ANTES DE O ORGANIZADOR APROVAR — por NENHUMA porta.
//
// 🗣️ Felipe, 10/09/2026: *"temos outra emergencia, chegou a notificação de horarios para as
// pessoas, e nao poderia chegar, lembra que pedi para nao chegar as notificações e nem nada até
// publicar?"*.
//
// 🕳️ O `AprovarChaves` era o único que AVISAVA — mas três leitores da grade entregavam o horário
// sem olhar a aprovação: a AGENDA/ICS do jogador (o Google Calendar notifica evento novo — é a
// notificação que chegou), o "seu próximo jogo" da HOME, e o push "sua quadra está atrasada" do
// robô do dia de jogo. A lição é a mesma da aba Jogos: régua de visibilidade precisa morar no
// DADO, num predicado só, e cada leitor usa o mesmo — `AprovacaoDeChaves.Publicada`.
public class NadaVazaAntesDeAprovarTests
{
    private static HomeController Home(DbPadelContext ctx, int jogadorId)
    {
        var controller = new HomeController(ctx, new EstatisticasService(ctx));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, jogadorId.ToString()) }, "Teste")),
            },
        };
        return controller;
    }

    // O predicado é UM só e é traduzível pro banco — é ele que cada leitor usa.
    [Fact]
    public async Task O_predicado_esconde_jogo_de_torneio_pendente_e_mostra_o_aprovado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        Assert.Equal(0, await ctx.Partidas.Where(AprovacaoDeChaves.Publicada).CountAsync());

        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        Assert.True(await ctx.Partidas.Where(AprovacaoDeChaves.Publicada).AnyAsync());
    }

    [Fact]
    public async Task A_home_nao_mostra_proximo_jogo_de_chave_pendente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.DataInicio = DateTime.Today.AddDays(1);
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var inscrito = (await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id)).Jogador1Id;

        var antes = Assert.IsType<ViewResult>(await Home(ctx, inscrito).Index());
        Assert.Null(((HomeVM)antes.Model!).ProximoJogo);

        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var depois = Assert.IsType<ViewResult>(await Home(ctx, inscrito).Index());
        Assert.NotNull(((HomeVM)depois.Model!).ProximoJogo);
    }
}
