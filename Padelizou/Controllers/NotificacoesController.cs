using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.Controllers;

// A CAIXA DE ENTRADA do jogador.
//
// Existe porque, até aqui, aviso era só entrega: push, e-mail e WhatsApp saíam e o sistema
// esquecia. Quem estava sem sinal, apagou a notificação sem querer ou não instalou o app —
// a maioria, 4 aparelhos em 128 jogadores — não tinha onde descobrir o que perdeu.
//
// ⚠️ É o único canal que não depende de entrega nenhuma: é só abrir. Os outros três já
// falharam num dia só (chip despareado, cota do Gmail estourada, inscrição de push fantasma).
[Authorize]
public class NotificacoesController : Controller
{
    // Quantos avisos aparecem na tela. Não é paginação de verdade porque ninguém rola uma
    // caixa de entrada de padel procurando o aviso de três meses atrás — o que interessa é o
    // de agora. Se um dia interessar, aqui é onde a paginação entra.
    private const int QuantosMostrar = 60;

    private readonly DbPadelContext _context;

    public NotificacoesController(DbPadelContext context) => _context = context;

    private int JogadorId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var eu = JogadorId();

        var avisos = await _context.AvisosDoJogador
            .Where(a => a.JogadorId == eu)
            .OrderByDescending(a => a.CriadoEm)
            .ThenByDescending(a => a.Id)
            .Take(QuantosMostrar)
            .ToListAsync();

        // ⚠️ Quais estavam NÃO LIDOS é decidido ANTES de marcar — senão a tela abriria com
        // tudo já cinza e a pessoa não veria o que chegou desde a última visita. O destaque
        // é justamente a razão de ela ter aberto.
        ViewBag.NaoLidosAgora = avisos.Where(a => a.LidaEm == null).Select(a => a.Id).ToHashSet();

        // Abrir a lista É ler. Um botão "marcar como lida" em cada linha seria trabalho pra
        // pessoa fazer o que o sistema já sabe.
        var agora = DateTime.Now;
        foreach (var a in avisos.Where(a => a.LidaEm == null)) a.LidaEm = agora;
        if (avisos.Any(a => a.LidaEm == agora)) await _context.SaveChangesAsync();

        return View(avisos);
    }
}
