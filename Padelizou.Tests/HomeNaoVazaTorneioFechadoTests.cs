using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// A HOME TAMBÉM NÃO ANUNCIA TORNEIO FECHADO (20/09/2026).
//
// 🐛 🗣️ Felipe, depois do deploy que tirou o torneio de time da listagem: *"Usuarios sem a
// bandeira do time ainda esta vendo o torneio"*. E estavam: medido em produção, o torneio
// sumiu de `/Torneios` (0 links) e CONTINUOU na Home (1 link), pra visitante anônimo.
//
// 🕳️ A CAUSA É UMA CÓPIA DA RÉGUA. O `HomeController` escrevia a vitrine À MÃO
// (`!t.Oculto && t.AprovadoEm != null && ...`) em vez de chamar o `PermissaoDeOrganizador`.
// Mudar a régua compartilhada não alcançou a cópia — que é exatamente o que o comentário do
// `ApareceParaOPublico` avisa: *"Regra de visibilidade copiada é como o torneio oculto
// reaparece: a pessoa esconde o torneio, uma das cópias é atualizada, a outra não"*.
//
// ⚠️ E A HOME JÁ TINHA CAÍDO NISSO ANTES: o comentário dela dizia *"antes a home ignorava o
// Oculto e vazava torneio restrito na vitrine"*. Segunda vez, mesma classe, mesmo arquivo —
// por isso a correção aqui não é só somar a condição, é PARAR DE ESCREVER A RÉGUA AQUI.
public class HomeNaoVazaTorneioFechadoTests
{
    private static HomeController NovoHome(DbPadelContext ctx, int? logadoComo)
    {
        var controller = new HomeController(ctx, new EstatisticasService(ctx));
        var user = logadoComo == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, logadoComo.Value.ToString()) }, "Teste"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    private static async Task<List<Torneio>> AbertosNaHomeAsync(DbPadelContext ctx, int? logadoComo)
    {
        var view = (ViewResult)await NovoHome(ctx, logadoComo).Index();
        return ((HomeVM)view.Model!).Abertos;
    }

    private static async Task<Torneio> TorneioDoTimeAsync(DbPadelContext ctx)
    {
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 1);
        ctx.Times.Add(new Time { Id = 9, Nome = "Los Corneteiros" });
        torneio.TimeExclusivoId = 9;
        await ctx.SaveChangesAsync();
        return torneio;
    }

    [Fact]
    public async Task Visitante_ANONIMO_nao_ve_o_torneio_de_time_na_home()
    {
        // É este o caso medido em produção: a Home servia o torneio fechado pra quem não
        // estava nem logado.
        using var ctx = TestInfra.NovoContexto();
        var torneio = await TorneioDoTimeAsync(ctx);

        Assert.DoesNotContain(await AbertosNaHomeAsync(ctx, null), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Logado_SEM_a_camisa_nao_ve_o_torneio_de_time_na_home()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = await TorneioDoTimeAsync(ctx);
        var deFora = TestInfra.NovoJogador(70);
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        Assert.DoesNotContain(await AbertosNaHomeAsync(ctx, deFora.Id), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Quem_VESTE_a_camisa_ve_o_torneio_do_time_dele_na_home()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = await TorneioDoTimeAsync(ctx);
        var doTime = TestInfra.NovoJogador(71);
        doTime.TimeId = 9;
        ctx.Jogadores.Add(doTime);
        await ctx.SaveChangesAsync();

        Assert.Contains(await AbertosNaHomeAsync(ctx, doTime.Id), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task Torneio_RESTRITO_nao_aparece_na_home_pra_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 1);
        torneio.Restrito = true;
        var alguem = TestInfra.NovoJogador(72);
        ctx.Jogadores.Add(alguem);
        await ctx.SaveChangesAsync();

        Assert.DoesNotContain(await AbertosNaHomeAsync(ctx, alguem.Id), t => t.Id == torneio.Id);
        Assert.DoesNotContain(await AbertosNaHomeAsync(ctx, null), t => t.Id == torneio.Id);
    }

    [Fact]
    public async Task O_torneio_ABERTO_continua_na_home_de_todo_mundo()
    {
        // A trava contra consertar demais: se o filtro novo derrubasse o torneio normal, a
        // Home ficaria vazia e ninguém veria — defeito maior que o que se foi corrigir.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 1);
        await ctx.SaveChangesAsync();

        Assert.Contains(await AbertosNaHomeAsync(ctx, null), t => t.Id == torneio.Id);
    }
}
