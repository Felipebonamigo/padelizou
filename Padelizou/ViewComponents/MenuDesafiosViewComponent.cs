using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.ViewComponents;

// O item "Desafios" dentro do menu Jogos.
//
// Existe como componente pelo mesmo motivo do MenuClube: a resposta depende de uma consulta
// (quem é admin) e de uma configuração, e nenhuma das duas cabe numa claim carimbada no login.
//
// ⚠️ Quem decide é PortaDosDesafios — a MESMA régua do controller. Um `if` próprio aqui seria
// a segunda cópia, e a segunda cópia é como um módulo em construção aparece no menu de todo
// mundo no dia em que só uma das duas for atualizada.
public class MenuDesafiosViewComponent : ViewComponent
{
    private readonly PortaDosDesafios _porta;

    public MenuDesafiosViewComponent(PortaDosDesafios porta) => _porta = porta;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var claim = (User as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var jogadorId)) return View(false);

        return View(await _porta.MostrarNoMenuAsync(jogadorId));
    }
}
