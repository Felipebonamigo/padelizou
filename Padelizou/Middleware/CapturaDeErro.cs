using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Padelizou.Services;

namespace Padelizou.Middleware;

// A ponte entre o UseExceptionHandler e o RegistroDeErros. O ExceptionHandlerMiddleware chama
// os IExceptionHandler registrados ANTES de reexecutar a página amiga (/Home/Error); este
// devolve sempre false de propósito — registrar não é tratar, e a pessoa continua vendo a
// mesma tela de sempre.
//
// Registrado como singleton pelo framework, por isso os serviços scoped (banco, push) saem do
// RequestServices da requisição, não do construtor.
public class CapturaDeErro : IExceptionHandler
{
    private readonly ILogger<CapturaDeErro> _logger;

    public CapturaDeErro(ILogger<CapturaDeErro> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        // Cliente que desistiu (fechou a aba, perdeu o sinal na quadra) não é erro do sistema.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
            return false;

        try
        {
            int? jogadorId = int.TryParse(
                httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

            var registro = httpContext.RequestServices.GetRequiredService<RegistroDeErros>();
            await registro.RegistrarAsync(exception,
                CaminhoOriginal(httpContext), httpContext.Request.Method, jogadorId);
        }
        catch (Exception falhaDoRegistro)
        {
            // O vigia não pode piorar a queda: se o banco é justamente o que caiu, registrar
            // também falha — e aí o log é o que sobra, como era antes deste arquivo existir.
            _logger.LogError(falhaDoRegistro,
                "Falha ao registrar um erro de produção (o erro original segue no log acima).");
        }

        return false;
    }

    // ⚠️ `Request.Path` aqui NÃO é onde o erro aconteceu — é "/Home/Error".
    //
    // O UseExceptionHandler reescreve o caminho da requisição pra página de erro ANTES de
    // chamar os IExceptionHandler, então ler o Path direto grava o destino no lugar da origem:
    // todo erro do sistema apareceria no registro como "/Home/Error", que é a única informação
    // que não serve pra nada — o registro existe justamente pra dizer ONDE quebrou.
    //
    // Pego na primeira verificação em tela, com 11 erros gravados apontando pro mesmo lugar.
    // O caminho de verdade fica guardado no IExceptionHandlerPathFeature.
    private static string CaminhoOriginal(HttpContext httpContext)
    {
        var original = httpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        return string.IsNullOrEmpty(original) ? httpContext.Request.Path.ToString() : original;
    }
}
