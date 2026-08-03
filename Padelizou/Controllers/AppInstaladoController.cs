using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.Controllers;

// "Esse jogador instalou o app?" — pergunta que só o navegador dele sabe responder. O
// servidor recebe a mesma requisição de quem abriu pelo Chrome e de quem abriu pelo ícone na
// tela de início; a diferença aparece só no cliente, em `display-mode: standalone`.
//
// Existe porque os Primeiros Passos mediam a coisa errada: olhavam se havia inscrição de
// notificação (PushSubscription). Quem instalava o app e não liberava aviso continuava sendo
// cobrado, tela após tela, pra instalar o que já tinha instalado — foi a primeira reclamação
// do primeiro professor de verdade, em 03/08/2026.
[Authorize]
public class AppInstaladoController : Controller
{
    private readonly DbPadelContext _context;

    public AppInstaladoController(DbPadelContext context)
    {
        _context = context;
    }

    // Chamado pelo wwwroot/js/instalar-app.js na primeira página que abre instalado.
    // Idempotente: só carimba quem ainda não tinha data, então reabrir o app não reescreve
    // a data e não gasta escrita à toa.
    [HttpPost]
    public async Task<IActionResult> Registrar()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var jogadorId))
        {
            return Unauthorized();
        }

        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return NotFound();

        if (jogador.InstalouAppEm == null)
        {
            jogador.InstalouAppEm = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        return Ok();
    }
}
