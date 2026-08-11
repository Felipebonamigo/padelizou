using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.ViewComponents;

// Onde os Desafios aparecem fora do próprio módulo: o item do menu Jogos e o cartão do hub de
// ranking.
//
// Existe como componente pelo mesmo motivo do MenuClube: a resposta depende de uma consulta
// (quem é admin) e de uma configuração, e nenhuma das duas cabe numa claim carimbada no login.
//
// ⚠️ Quem decide é PortaDosDesafios — a MESMA régua do controller. Um `if` próprio aqui seria
// a segunda cópia, e a segunda cópia é como um módulo em construção aparece no menu de todo
// mundo no dia em que só uma das duas for atualizada.
//
// `estilo` só troca o DESENHO — a pergunta e a resposta são as mesmas nos dois lugares. É o
// mesmo arranjo do SinoDeAvisos, que aparece na barra e dentro do menu.
public class MenuDesafiosViewComponent : ViewComponent
{
    public const string NoMenu = "Default";
    public const string CartaoDoRanking = "Cartao";

    private readonly PortaDosDesafios _porta;

    public MenuDesafiosViewComponent(PortaDosDesafios porta) => _porta = porta;

    public async Task<IViewComponentResult> InvokeAsync(string estilo = NoMenu)
    {
        var claim = (User as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var jogadorId)) return View(estilo, false);

        return View(estilo, await _porta.MostrarNoMenuAsync(jogadorId));
    }
}
