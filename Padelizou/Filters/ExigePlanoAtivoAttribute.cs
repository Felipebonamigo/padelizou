using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Filters;

// ⚠️ O ENDPOINT SE DECLARA FORA DO BLOQUEIO — e só isso. Sem motivo escrito aqui do lado, e o
// GateDoBloqueioDoProfessorTests reprova: a lista de exceções dele exige uma linha por caso.
//
// Existe porque as telas do professor e as do ALUNO moram nos mesmos controllers: `Solicitar` e
// `CancelarComoAluno` estão no AulasController, e um professor bloqueado que também é aluno de
// alguém não pode perder o direito de marcar a aula DELE — isso não tem nada a ver com o plano.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SemBloqueioDeProfessorAttribute : Attribute
{
}

// A AGENDA FECHA PRO PROFESSOR QUE NÃO SUSTENTA O PLANO (item 2 do desenho de 22/09/2026).
// A régua de QUEM mora em Services/BloqueioDoProfessor; aqui só se aplica.
//
// ⚠️ VAI NA CLASSE, E NÃO ENDPOINT A ENDPOINT. São 51 POSTs do professor espalhados por 11
// arquivos — uma lista escrita à mão envelhece calada, e o 52º nasceria furado. Na classe, o
// endpoint novo já nasce coberto e quem quiser ficar de fora precisa DIZER (e justificar no
// gate). É a mesma escolha do `[Authorize]` de classe que o projeto já faz.
//
// ⚠️ SÓ POST. "Fica apenas a visualização do que já está marcado" (Felipe) é literalmente isto:
// o GET passa reto. Filtrar por verbo aqui, e não por lista de ações, é o que garante que a
// tela de leitura nunca feche por engano.
public sealed class ExigePlanoAtivoAttribute : TypeFilterAttribute
{
    public ExigePlanoAtivoAttribute() : base(typeof(Filtro))
    {
    }

    // Público (e não privado) pra poder ser exercitado direto no teste: o caminho de DI
    // inteiro só pra provar que GET passa reto seria teste de framework, não da nossa regra.
    public sealed class Filtro : IAsyncActionFilter
    {
        private readonly DbPadelContext _context;
        private readonly PlanoProfessorSettings _cfg;

        public Filtro(DbPadelContext context, IOptions<PlanoProfessorSettings> cfg)
        {
            _context = context;
            _cfg = cfg.Value;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
        {
            if (await EstaBloqueadoAsync(ctx) is string motivo)
            {
                ctx.Result = Recusa(ctx, motivo);
                return;
            }

            await next();
        }

        // Devolve o motivo quando o pedido tem que parar aqui; nulo quando pode seguir.
        private async Task<string?> EstaBloqueadoAsync(ActionExecutingContext ctx)
        {
            // Leitura nunca fecha.
            if (!HttpMethods.IsPost(ctx.HttpContext.Request.Method)) return null;

            if (ctx.ActionDescriptor is ControllerActionDescriptor acao
                && acao.MethodInfo.GetCustomAttribute<SemBloqueioDeProfessorAttribute>() != null)
                return null;

            // Sem login não é problema deste filtro — o [Authorize] já recusou antes.
            var claim = ctx.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var jogadorId)) return null;

            var eu = await _context.Jogadores.FindAsync(jogadorId);

            // ⚠️ Quem não é professor passa SEMPRE. É o que deixa o aluno usar as telas de aluno
            // que moram nestes mesmos controllers, sem precisar marcar uma por uma.
            if (eu is not { IsProfessor: true }) return null;

            if (!BloqueioDoProfessor.EstaBloqueado(eu, DateTime.Now, _cfg)) return null;

            return "Seu plano venceu e sua agenda está fechada pra novidades — o que já está "
                 + "marcado continua aparecendo. Assine pra voltar a mexer nela.";
        }

        // ⚠️ REDIRECT PRA TELA DE FORMULÁRIO, 403 PRO RESTO. Devolver HTML de redirect pra um
        // fetch() faz o JS engolir a página inteira como se fosse resposta e falhar calado —
        // o defeito que o silent-failure-hunter caça. O 403 é lido como erro de verdade.
        private static IActionResult Recusa(ActionExecutingContext ctx, string motivo)
        {
            var pedido = ctx.HttpContext.Request;
            var querJson = pedido.Headers.XRequestedWith == "XMLHttpRequest"
                        || (pedido.Headers.Accept.ToString()?.Contains("application/json") ?? false);

            if (querJson) return new ObjectResult(new { erro = motivo }) { StatusCode = 403 };

            if (ctx.Controller is Controller controller) controller.TempData["Erro"] = motivo;

            return new RedirectToActionResult("Index", "PlanoProfessor", null);
        }
    }
}
