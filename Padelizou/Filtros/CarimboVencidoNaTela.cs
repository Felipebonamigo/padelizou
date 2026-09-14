using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Padelizou.Filtros;

// O CARIMBO ANTIFALSIFICAÇÃO RECUSOU — E A PESSOA PRECISA DE UM CAMINHO, NÃO DE UM BECO.
//
// 🗣️ 14/09/2026, print de um jogador mandado pelo Felipe: *"Após votar no melhor do torneio"*,
// com a tela crua do Chrome ("This page isn't working — HTTP ERROR 400"), e em seguida *"Na
// segunda tentativa deu bom"*.
//
// 🔎 O QUE ACONTECE. O carimbo (`AutoValidateAntiforgeryTokenAttribute`, global desde o
// build-93) recusa o POST e o MVC devolve um 400 SEM CORPO. O navegador não tem o que desenhar
// e mostra a tela dele: sem menu, sem identidade, sem volta — o mesmo beco que a tela de 404
// existe pra não deixar acontecer. Abrir a página de novo emite carimbo novo, e aí vai: é por
// isso que "na segunda tentativa deu bom".
//
// ⚠️ E NÃO DÁ PRA RESOLVER PELO `UseStatusCodePagesWithReExecute`, que é onde a mão coça. Ele é
// só GET/HEAD DE PROPÓSITO desde 18/08: o re-execute preserva o método, então um POST com erro
// virava um POST na tela de erro, o carimbo recusava ESSE também, e o 400 dele substituía o
// status de verdade na resposta (o webhook de pagamento chegou a responder 400 no lugar de
// 401). Voltar atrás naquilo pra consertar isto aqui seria trocar um defeito por outro pior.
//
// 🔒 O QUE ESTE FILTRO NÃO FAZ, e é a régua pra ler o diff: não muda o status (continua 400),
// não deixa a ação rodar (o carimbo é filtro de AUTORIZAÇÃO, corta antes) e não cria isenção
// nenhuma. Muda o CORPO da resposta. Quem grava continua sendo só quem tem carimbo válido.
//
// 🔑 SÓ PRA QUEM NAVEGA. `Sec-Fetch-Mode: navigate` é a mesma distinção que o
// `RegistroDeAcessoMiddleware` e o `wwwroot/sw.js` já fazem, e envio de formulário manda
// `navigate` (provado em `RegistroDeAcessoMiddlewareTests`). Quem chama por `fetch` — o placar
// ao vivo, a Mesa — manda `cors`/`same-origin` e tem que continuar recebendo o 400 CRU: devolver
// HTML pra quem só olha `resposta.ok` trocaria um defeito visível por um calado.
//
// ⚠️ `IAlwaysRunResultFilter`, e não `IResultFilter`: quando um filtro de autorização corta a
// requisição, os filtros de resultado COMUNS não rodam. Esta interface existe exatamente pro
// caso de querer alcançar o resultado de uma requisição cortada — que é o nosso.
public class CarimboVencidoNaTela : IAlwaysRunResultFilter
{
    // Mora em Views/Shared porque a recusa pode vir de qualquer um dos 69 controllers.
    public const string NomeDaView = "CarimboVencido";

    // Onde a view procura o caminho de volta.
    public const string ChaveDoVoltar = "Voltar";

    public void OnResultExecuting(ResultExecutingContext context)
    {
        // O marcador público do próprio framework. Preferido ao tipo concreto
        // (`AntiforgeryValidationFailedResult`) porque é ele que a documentação promete.
        if (context.Result is not IAntiforgeryValidationFailedResult) return;

        if (context.HttpContext.Request.Headers["Sec-Fetch-Mode"] != "navigate") return;

        var voltar = VoltarPara(
            context.HttpContext.Request.Headers.Referer.ToString(),
            context.HttpContext.Request.Host.Host);

        var metadados = context.HttpContext.RequestServices.GetRequiredService<IModelMetadataProvider>();
        var dados = new ViewDataDictionary(metadados, new ModelStateDictionary())
        {
            [ChaveDoVoltar] = voltar,
        };

        context.Result = new ViewResult
        {
            ViewName = NomeDaView,
            ViewData = dados,
            StatusCode = StatusCodes.Status400BadRequest,
        };
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    // O caminho de volta, extraído do `Referer` — e SÓ quando ele aponta pra cá.
    //
    // ⚠️ O `Referer` é escrito pelo navegador de quem chama, então é entrada de fora como
    // qualquer outra: sem esta peneira, um site de terceiro faria o Padelizou desenhar um botão
    // "voltar" apontando pra ele, dentro do nosso layout e com a nossa logo. Devolve caminho
    // RELATIVO de propósito — assim o que a view escreve no `href` nunca é um domínio.
    //
    // Com `Referrer-Policy: strict-origin-when-cross-origin` (ver infra/vps/Caddyfile), o
    // navegador manda a URL inteira quando a origem é a mesma, que é justamente o nosso caso:
    // o formulário está numa página nossa.
    public static string? VoltarPara(string? referer, string hostDaRequisicao)
    {
        if (string.IsNullOrWhiteSpace(referer)) return null;

        if (!Uri.TryCreate(referer, UriKind.Absolute, out var endereco)) return null;

        if (endereco.Scheme != Uri.UriSchemeHttp && endereco.Scheme != Uri.UriSchemeHttps) return null;

        if (!string.Equals(endereco.Host, hostDaRequisicao, StringComparison.OrdinalIgnoreCase)) return null;

        return endereco.PathAndQuery;
    }
}
