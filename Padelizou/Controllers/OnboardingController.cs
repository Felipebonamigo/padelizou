using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers;

// Tirar (e repor) passo dos "Primeiros passos" — 14/09/2026. Ver PassoPuladoDoOnboarding.
[Authorize]
public class OnboardingController : Controller
{
    private readonly DbPadelContext _context;

    public OnboardingController(DbPadelContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Pular(string passo, string? de = null)
    {
        var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (!Enum.TryParse<PassoDoOnboarding>(passo, ignoreCase: false, out var chave))
        {
            return DeVoltaPara(de);
        }

        // ⚠️ A RÉGUA NO SERVIDOR, e não só no `if` da view: o botão só existe nos dois passos
        // com saída, mas este POST é montado à mão em três segundos. Sem esta linha, "pular"
        // viraria a porta de fundo pra apagar o perfil da cobrança.
        if (!PulosDoOnboarding.PodeSerPulado(chave))
        {
            return DeVoltaPara(de);
        }

        // ⚠️ IDEMPOTENTE: o toque duplo manda dois POSTs. A trava DE VERDADE é a chave composta
        // (JogadorId, Passo) — este `if` é o que evita o erro na cara de quem conseguiu
        // exatamente o que queria. Mesma forma do ReacaoService.ReagirAsync.
        bool jaPulei = await _context.PassosPuladosDoOnboarding
            .AnyAsync(p => p.JogadorId == jogadorId && p.Passo == chave);

        if (!jaPulei)
        {
            _context.PassosPuladosDoOnboarding.Add(new PassoPuladoDoOnboarding
            {
                JogadorId = jogadorId,
                Passo = chave,
            });
            await _context.SaveChangesAsync();
        }

        return DeVoltaPara(de);
    }

    [HttpPost]
    public async Task<IActionResult> MostrarTodos(string? de = null)
    {
        var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        _context.PassosPuladosDoOnboarding.RemoveRange(
            _context.PassosPuladosDoOnboarding.Where(p => p.JogadorId == jogadorId));
        await _context.SaveChangesAsync();

        return DeVoltaPara(de);
    }

    // Lista branca de destinos. O partial vive em duas telas e o botão de repor numa terceira,
    // então o POST precisa saber pra onde volta — mas um destino vindo do formulário, usado
    // como URL, é redirect aberto. Aqui o que chega é uma PALAVRA, e o que não estiver na
    // lista cai na Home.
    private IActionResult DeVoltaPara(string? de) => de switch
    {
        "perfil" => RedirectToAction("Perfil", "Auth"),
        "preferencias" => RedirectToAction("Preferencias", "Auth"),
        _ => RedirectToAction("Index", "Home"),
    };
}
