using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Services;

namespace Padelizou.Tests;

// A tela de "essa bola saiu" é a única do site cujo STATUS vem de fora: quem preenche o
// `codigo` é o UseStatusCodePagesWithReExecute, passando na query string o número que o
// pipeline devolveu — e query string é a barra de endereço, onde qualquer um digita o que
// quiser.
//
// Sem régua, `?codigo=99999` fazia o servidor escrever o número cru na linha de status e
// `?codigo=200` fazia uma URL do site responder 200 mostrando a tela de erro: buscador e
// monitoramento passariam a ler "página boa" onde não tem página nenhuma. Nada grava, mas o
// status é o que o robô do Google acredita.
public class StatusDaTelaDeErroTests
{
    // O caminho normal — 404 de link velho, 401 de quem não logou, 429 do rate limiter —
    // não pode mudar de comportamento por causa da trava.
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    [InlineData(410)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(599)]
    public void Erro_de_verdade_chega_intacto_na_tela_e_no_status(int codigo)
    {
        var controller = NovoHome();

        controller.NaoEncontrado(codigo);

        Assert.Equal(codigo, controller.Response.StatusCode);
        Assert.Equal(codigo, (int)controller.ViewBag.Codigo);
    }

    // Fora da faixa de erro só existe URL digitada à mão: o pipeline nunca reexecuta a tela
    // com 200, com 302 nem com 99999.
    [Theory]
    [InlineData(200)]     // o pior deles: página inexistente respondendo "tudo certo"
    [InlineData(302)]
    [InlineData(399)]
    [InlineData(600)]
    [InlineData(999)]
    [InlineData(99999)]   // fazia o servidor escrever 999 na linha de status
    [InlineData(0)]
    [InlineData(-1)]
    public void Codigo_fora_da_faixa_de_erro_cai_pro_404(int codigo)
    {
        var controller = NovoHome();

        controller.NaoEncontrado(codigo);

        Assert.Equal(404, controller.Response.StatusCode);
        Assert.Equal(404, (int)controller.ViewBag.Codigo);   // o texto da tela também não repete o número inventado
    }

    // Quem preenche o `codigo` é o Program.cs. Se essa linha mudar de forma, a action passa a
    // responder sempre o 404 padrão e ninguém percebe — 401 e 429 virariam "não encontrada".
    [Fact]
    public void O_Program_continua_entregando_o_codigo_pela_query_string()
    {
        var program = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Program.cs"));

        Assert.Matches(
            @"UseStatusCodePagesWithReExecute\(\s*""/Home/NaoEncontrado""\s*,\s*""\?codigo=\{0\}""\s*\)",
            program);
    }

    private static HomeController NovoHome()
    {
        var ctx = TestInfra.NovoContexto();
        return new HomeController(ctx, new EstatisticasService(ctx))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
