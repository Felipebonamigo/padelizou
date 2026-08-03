using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "Instalou o app" e "aceitou notificação" são duas coisas diferentes, e os Primeiros Passos
// tratavam como se fossem uma só: mediam a inscrição de push. Quem instalava o app e não
// liberava aviso continuava sendo cobrado, tela após tela, pra instalar o que já tinha
// instalado — a primeira reclamação do primeiro professor de verdade, em 03/08/2026.
public class AppInstaladoTests
{
    private static async Task<(DbPadelContext ctx, Jogador jogador)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var jogador = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        return (ctx, jogador);
    }

    private static async Task<bool> PassoDeInstalarEstaConcluidoAsync(DbPadelContext ctx, int jogadorId)
    {
        var onboarding = await new EstatisticasService(ctx).ObterOnboardingAsync(jogadorId);
        return onboarding.Passos.Single(p => p.Titulo.Contains("Instale o app")).Concluido;
    }

    [Fact]
    public async Task Quem_nao_instalou_continua_com_o_passo_pendente()
    {
        var (ctx, jogador) = await MontarAsync();
        using var _ = ctx;

        Assert.False(await PassoDeInstalarEstaConcluidoAsync(ctx, jogador.Id));
    }

    [Fact]
    public async Task Instalar_sem_liberar_notificacao_ja_conclui_o_passo()
    {
        // Este é o caso do Jonatas: instalou pelo Android, não mexeu em notificação, e o
        // cartão insistia em pedir a instalação.
        var (ctx, jogador) = await MontarAsync();
        using var _ = ctx;

        jogador.InstalouAppEm = DateTime.Now;
        await ctx.SaveChangesAsync();

        Assert.True(await PassoDeInstalarEstaConcluidoAsync(ctx, jogador.Id));
    }

    [Fact]
    public async Task Liberar_notificacao_sem_carimbo_tambem_conclui_o_passo()
    {
        // O caminho antigo continua valendo: no iPhone, push só existe com o app instalado,
        // então a inscrição de push é prova de instalação.
        var (ctx, jogador) = await MontarAsync();
        using var _ = ctx;

        ctx.PushSubscriptionsJogador.Add(new PushSubscriptionJogador
        {
            JogadorId = jogador.Id,
            Endpoint = "https://exemplo/push/1",
            P256dh = "chave",
            Auth = "auth",
        });
        await ctx.SaveChangesAsync();

        Assert.True(await PassoDeInstalarEstaConcluidoAsync(ctx, jogador.Id));
    }

    [Fact]
    public async Task O_carimbo_de_instalacao_nao_e_reescrito_a_cada_abertura()
    {
        // O JS avisa em toda aba nova. Reescrever a data faria a primeira instalação parecer
        // de hoje pra sempre — e gastaria escrita no banco por nada.
        var (ctx, jogador) = await MontarAsync();
        using var _ = ctx;

        var controller = new Padelizou.Controllers.AppInstaladoController(ctx);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.NameIdentifier, jogador.Id.ToString()) }, "Teste")),
            },
        };

        await controller.Registrar();
        var primeiraVez = (await ctx.Jogadores.FindAsync(jogador.Id))!.InstalouAppEm;

        await controller.Registrar();
        var depois = (await ctx.Jogadores.FindAsync(jogador.Id))!.InstalouAppEm;

        Assert.NotNull(primeiraVez);
        Assert.Equal(primeiraVez, depois);
    }
}
