using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Padelizou.Filtros;
using Xunit;

namespace Padelizou.Tests;

// 14/09/2026 — VOTAR NO MVP DEVOLVIA "HTTP ERROR 400" CRU. 🗣️ Felipe, com o print de um
// jogador: *"Após votar no melhor do torneio"* / *"Na segunda tentativa deu bom"*.
//
// 🔎 O DIAGNÓSTICO, por eliminação: o `UseStatusCodePagesWithReExecute` só vale pra GET/HEAD
// (decisão de 18/08, pra POST de fora não receber 400 no lugar do status de verdade), então um
// GET com 400 viraria a tela amiga — a tela crua do navegador só sai de um POST. E no
// `POST /Torneios/VotarMvp` a única coisa que devolve 400 de corpo VAZIO é o carimbo
// antifalsificação global: o controller não é `[ApiController]` (sem 400 automático de binding),
// a trava de tentativas devolve 429 COM corpo, e não há `BadRequest()` nenhum nesse caminho.
//
// 🔑 O VOTO NÃO CHEGA A SER GRAVADO: o carimbo é um filtro de AUTORIZAÇÃO, roda antes da ação.
// Ninguém votou duas vezes — valeu o da segunda tentativa.
//
// ⚠️ ISTO NÃO AFROUXA NADA, e é a régua pra ler o diff: o status continua 400 e a ação continua
// não rodando. Muda o CORPO da resposta, não a decisão de segurança. Nenhum
// `[IgnoreAntiforgeryToken]` novo — o `ProtecaoAntifalsificacaoTests` segue com as mesmas 3
// isenções de sempre.
//
// 🔑 E SÓ PRA QUEM NAVEGA. `Sec-Fetch-Mode: navigate` é a MESMA distinção que o
// `RegistroDeAcessoMiddleware` e o `wwwroot/sw.js` já fazem, e o teste do irmão já provou que
// envio de formulário manda `navigate`. Quem chama por `fetch` (o placar ao vivo, a mesa) manda
// `cors`/`same-origin` e precisa continuar recebendo o 400 cru — devolver HTML pra um `fetch`
// que espera status é trocar um defeito visível por um calado.
public class CarimboVencidoNaTelaTests
{
    [Fact]
    public void Quem_navega_recebe_uma_tela_no_lugar_do_400_vazio()
    {
        var contexto = ContextoDe(new AntiforgeryValidationFailedResult(), "navigate");

        new CarimboVencidoNaTela().OnResultExecuting(contexto);

        var tela = Assert.IsType<ViewResult>(contexto.Result);
        Assert.Equal(CarimboVencidoNaTela.NomeDaView, tela.ViewName);

        // ⚠️ O STATUS CONTINUA 400. Trocar por 200 faria monitoramento e buscador lerem
        // "requisição boa"; trocar por 302 desfaria a decisão de 18/08 de POST devolver status
        // honesto. O que muda é só ter corpo em vez de não ter.
        Assert.Equal(StatusCodes.Status400BadRequest, tela.StatusCode);
    }

    [Fact]
    public void Quem_chama_por_fetch_continua_recebendo_o_400_cru()
    {
        // O placar ao vivo e a mesa mandam o formulário por `fetch` com o carimbo no corpo (ver
        // wwwroot/js/placar-ao-vivo.js). Uma página HTML no lugar do status seria lida como
        // "deu certo" por quem só olha `resposta.ok`.
        foreach (var modo in new[] { "cors", "same-origin", "no-cors", null })
        {
            var original = new AntiforgeryValidationFailedResult();
            var contexto = ContextoDe(original, modo);

            new CarimboVencidoNaTela().OnResultExecuting(contexto);

            Assert.Same(original, contexto.Result);
        }
    }

    [Fact]
    public void Resultado_que_nao_e_do_carimbo_passa_batido()
    {
        // A trava que impede o filtro de virar um sequestrador de respostas: ele só tem
        // assunto com a recusa do carimbo, e qualquer outro 400 do sistema continua o que era.
        var qualquerOutro = new BadRequestResult();
        var contexto = ContextoDe(qualquerOutro, "navigate");

        new CarimboVencidoNaTela().OnResultExecuting(contexto);

        Assert.Same(qualquerOutro, contexto.Result);
    }

    [Fact]
    public void O_botao_de_voltar_so_aceita_endereco_daqui()
    {
        // O caminho de volta sai do `Referer` — com `Referrer-Policy: strict-origin-when-cross-
        // origin` (ver infra/vps/Caddyfile) requisição do mesmo site manda a URL inteira, que é
        // exatamente a página onde a pessoa estava.
        Assert.Equal("/Torneios/Mvp/42",
            CarimboVencidoNaTela.VoltarPara("https://padelizou.com.br/Torneios/Mvp/42", "padelizou.com.br"));

        // ⚠️ E RECUSA O RESTO. Sem isto, um site qualquer faz o Padelizou desenhar um botão
        // "voltar" apontando pra ele — dentro do nosso layout, com a nossa logo. O `Referer`
        // é escrito pelo navegador de quem chama, então é entrada de fora como qualquer outra.
        Assert.Null(CarimboVencidoNaTela.VoltarPara("https://outro-site.com/pegadinha", "padelizou.com.br"));
        Assert.Null(CarimboVencidoNaTela.VoltarPara("//outro-site.com/pegadinha", "padelizou.com.br"));
        Assert.Null(CarimboVencidoNaTela.VoltarPara("javascript:alert(1)", "padelizou.com.br"));
        Assert.Null(CarimboVencidoNaTela.VoltarPara(null, "padelizou.com.br"));
        Assert.Null(CarimboVencidoNaTela.VoltarPara("", "padelizou.com.br"));
    }

    [Fact]
    public void A_tela_existe_e_diz_que_nada_foi_gravado()
    {
        // ⚠️ Teste de FONTE: a suíte não renderiza Razor, e o filtro só nomeia a view. Se o
        // arquivo sumir ou trocar de nome, o 400 volta a ser uma tela de erro do navegador —
        // pior ainda, uma exceção. E o texto importa: a pessoa acabou de tocar em "Votar" e
        // precisa saber se o voto foi ou não — sem essa frase ela vota de novo com medo.
        var fonte = File.ReadAllText(Path.Combine(
            PastaDoProjeto(), "Views", "Shared", CarimboVencidoNaTela.NomeDaView + ".cshtml"));

        Assert.Contains("Nada foi gravado", fonte);
    }

    private static ResultExecutingContext ContextoDe(IActionResult resultado, string? secFetchMode)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Host = new HostString("padelizou.com.br");
        if (secFetchMode != null) http.Request.Headers["Sec-Fetch-Mode"] = secFetchMode;

        http.RequestServices = new ServiceCollection()
            .AddSingleton<IModelMetadataProvider, EmptyModelMetadataProvider>()
            .BuildServiceProvider();

        var acao = new ActionContext(http, new RouteData(), new ActionDescriptor());

        return new ResultExecutingContext(acao, new List<IFilterMetadata>(), resultado, controller: null!);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidato = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(candidato)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta do projeto a partir de " + AppContext.BaseDirectory);
    }
}
