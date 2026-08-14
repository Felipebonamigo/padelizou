using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers;

// A moldura da foto: a vitrine das conquistas que viaja com o jogador (13/08/2026).
//
// Controller próprio, e não uma action no JogadoresController, por dois motivos: o assunto é
// fechado (uma tela, um POST), e o JogadorsController.cs estava em obra na sessão paralela —
// arquivo compartilhado no meio de trabalho alheio é como um commit leva metade do trabalho
// do outro junto.
[Authorize]
public class MoldurasController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;

    public MoldurasController(DbPadelContext context, IEstatisticasService estatisticas)
    {
        _context = context;
        _estatisticas = estatisticas;
    }

    // GET /Molduras — o guarda-roupa: TODAS as molduras em volta da foto da própria pessoa,
    // as destravadas escolhíveis e as bloqueadas em cinza dizendo como destravar (a mesma
    // regra dos badges: bloqueada sem explicação é decoração, não incentivo).
    public async Task<IActionResult> Index()
    {
        var jogadorId = ObterUserId();
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return RedirectToAction("Perfil", "Auth");

        ViewBag.Conquistas = await _estatisticas.ObterConquistasAsync(jogadorId);
        return View(jogador);
    }

    [HttpPost]
    public async Task<IActionResult> Escolher(string? codigo)
    {
        var jogadorId = ObterUserId();
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return RedirectToAction("Perfil", "Auth");

        // ⚠️ A TRAVA É AQUI, não na tela: a tela cinza a moldura bloqueada, mas o POST chega
        // com qualquer string — sem esta conferência, um formulário montado à mão vestiria a
        // moldura de Decacampeão em quem nunca jogou. A régua é a MESMA que calcula os badges
        // do perfil (ObterConquistasAsync), não uma segunda conta.
        var conquistas = await _estatisticas.ObterConquistasAsync(jogadorId);
        var impedimento = CatalogoMolduras.MotivoParaNaoUsar(codigo, conquistas);
        if (impedimento != null)
        {
            TempData["Erro"] = impedimento;
            return RedirectToAction("Index");
        }

        jogador.MolduraEscolhida = string.IsNullOrWhiteSpace(codigo) ? null : codigo;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = jogador.MolduraEscolhida == null
            ? "Foto limpa, sem moldura."
            : $"Moldura “{CatalogoMolduras.PorCodigo(codigo)!.Nome}” vestida! Ela aparece em todo lugar onde sua foto aparece.";
        return RedirectToAction("Index");
    }

    private int ObterUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
