using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Middleware;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O CABEÇALHO `Host` NÃO DECIDE A POSTURA DA INSTÂNCIA.
//
// 🐛 O DEFEITO QUE ESTE ARQUIVO PRENDE (medido em produção, 01/09/2026): o middleware decidia
// "esta é a instância de teste, libera tudo, inclusive /Admin" comparando
// `Request.Host.Host` com `dev.padelizou.com.br` — e `Host` vem do CLIENTE. Contra o app de
// produção, sem passar pelo Caddy:
//
//   Host: padelizou.com.br       -> /Admin = 404   (certo)
//   Host: dev.padelizou.com.br   -> /Admin = 302   (a produção servindo o painel)
//
// ⚠️ Isso NUNCA deu poder de admin: o AdminController continua exigindo IsAdminGeral /
// IsAdminRaiz. O que caía era a camada de separação de host — e ela caía por um cabeçalho que
// qualquer um escreve. Não era alcançável de fora porque o Kestrel só escuta em localhost e
// quem roteia por Host é o Caddy; ou seja, a proteção inteira morava num arquivo que nem está
// neste repositório.
//
// A régua nova é `Beta:AmbienteDeTeste`, que já existia e nasce FALSA — "quem tem que se
// declarar é a cópia, não o original" (ver Services/BetaSettings). A instância diz quem é; a
// requisição não opina.
public class HostForjadoNaoAbreOAdminTests
{
    private static AdminHostMiddleware Middleware(bool ambienteDeTeste, bool ehDesenvolvimento = false)
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns(ehDesenvolvimento ? "Development" : "Production");

        return new AdminHostMiddleware(
            _ => Task.CompletedTask,
            env,
            Options.Create(new BetaSettings { AmbienteDeTeste = ambienteDeTeste }));
    }

    private static DefaultHttpContext Requisicao(string host, string caminho)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString(host);
        ctx.Request.Path = caminho;
        return ctx;
    }

    [Fact]
    public async Task Producao_com_Host_de_dev_forjado_NAO_serve_o_Admin()
    {
        // O caso exato medido em produção. Antes: 302. Depois: 404, como no host público.
        var ctx = Requisicao("dev.padelizou.com.br", "/Admin/Index");

        await Middleware(ambienteDeTeste: false).InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task A_instancia_de_teste_continua_servindo_tudo()
    {
        // O dev existe pra testar qualquer funcionalidade, /Admin inclusive. Quem se declarou
        // cópia (Beta__AmbienteDeTeste=true no systemd do dev) segue liberado.
        var ctx = Requisicao("dev.padelizou.com.br", "/Admin/Index");

        await Middleware(ambienteDeTeste: true).InvokeAsync(ctx);

        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task O_site_publico_continua_escondendo_o_Admin()
    {
        // Regressão da regra original, que não mudou.
        var ctx = Requisicao("padelizou.com.br", "/Admin/Index");

        await Middleware(ambienteDeTeste: false).InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task O_subdominio_do_painel_continua_servindo_o_Admin()
    {
        // `admin.padelizou.com.br` aponta pra PRÓPRIA produção — ela precisa honrar esse host.
        // E forjá-lo não ganha nada: é um endereço público, qualquer um o visita direto.
        var ctx = Requisicao("admin.padelizou.com.br", "/Admin/Index");

        await Middleware(ambienteDeTeste: false).InvokeAsync(ctx);

        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Fora_do_painel_o_subdominio_do_painel_continua_dando_404()
    {
        // A outra metade da separação: o host do painel serve SÓ /Admin, /Auth e assets.
        var ctx = Requisicao("admin.padelizou.com.br", "/Torneios/Details/1");

        await Middleware(ambienteDeTeste: false).InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task No_localhost_nada_disso_vale()
    {
        // Em Development o middleware sai na primeira linha: esses hosts não existem
        // localmente, e testar /Admin na máquina é o caso normal.
        var ctx = Requisicao("localhost", "/Admin/Index");

        await Middleware(ambienteDeTeste: false, ehDesenvolvimento: true).InvokeAsync(ctx);

        Assert.NotEqual(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
    }
}
