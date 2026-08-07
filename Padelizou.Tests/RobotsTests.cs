using Microsoft.AspNetCore.Http;
using Padelizou.Middleware;

namespace Padelizou.Tests;

// O dev ficou público junto com o lançamento — e sem nada disto o Google indexaria o
// ambiente de teste ao lado do site real. O perigo simétrico é pior: um noindex vazando
// pro site público apagaria o Padelizou da busca na semana do lançamento. Os dois lados
// têm teste.
public class RobotsTests
{
    private static async Task<(HttpContext Contexto, bool ChamouProximo)> Passar(string host, string caminho)
    {
        var chamouProximo = false;
        var middleware = new RobotsMiddleware(_ => { chamouProximo = true; return Task.CompletedTask; });

        var contexto = new DefaultHttpContext();
        contexto.Request.Host = new HostString(host);
        contexto.Request.Path = caminho;
        contexto.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(contexto);
        return (contexto, chamouProximo);
    }

    private static string Corpo(HttpContext contexto)
    {
        contexto.Response.Body.Position = 0;
        return new StreamReader(contexto.Response.Body).ReadToEnd();
    }

    [Theory]
    [InlineData("dev.padelizou.com.br")]
    [InlineData("admin.padelizou.com.br")]
    public async Task Dev_e_admin_dizem_ao_buscador_pra_nao_entrar(string host)
    {
        var (contexto, _) = await Passar(host, "/robots.txt");

        Assert.Contains("Disallow: /", Corpo(contexto));
    }

    [Fact]
    public async Task Site_publico_e_indexavel_e_o_robots_diz_isso()
    {
        var (contexto, _) = await Passar("padelizou.com.br", "/robots.txt");

        var corpo = Corpo(contexto);
        Assert.Contains("Allow: /", corpo);
        Assert.DoesNotContain("Disallow", corpo);
    }

    [Theory]
    [InlineData("dev.padelizou.com.br")]
    [InlineData("admin.padelizou.com.br")]
    public async Task Toda_resposta_do_dev_e_do_admin_leva_o_cabecalho_noindex(string host)
    {
        // robots.txt impede rastrear; página já linkada ainda entraria no índice "às cegas".
        // É o cabeçalho que mantém o host fora da busca — então tem que estar em TODA resposta.
        var (contexto, chamouProximo) = await Passar(host, "/Torneios");

        Assert.Equal("noindex, nofollow", contexto.Response.Headers["X-Robots-Tag"]);
        Assert.True(chamouProximo, "a página em si continua sendo servida normalmente");
    }

    [Fact]
    public async Task Site_publico_JAMAIS_leva_noindex()
    {
        // O acidente que este teste impede: apagar padelizou.com.br do Google no lançamento.
        var (contexto, _) = await Passar("padelizou.com.br", "/Torneios");

        Assert.False(contexto.Response.Headers.ContainsKey("X-Robots-Tag"));
    }

    [Fact]
    public async Task Robots_responde_direto_sem_passar_pelo_resto_do_pipeline()
    {
        // É o que garante que o portão de acesso antecipado, quando religado, não transforme
        // o robots.txt num redirect pra tela de senha (a ordem no Program.cs põe este
        // middleware na frente do portão).
        var (contexto, chamouProximo) = await Passar("dev.padelizou.com.br", "/robots.txt");

        Assert.False(chamouProximo);
        Assert.Equal("text/plain; charset=utf-8", contexto.Response.ContentType);
    }
}
