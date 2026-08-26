using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // O RANKING DE PALPITEIROS do torneio: quem mais acertou no Palpitrômetro.
    //
    // A régua mora em Services/PontosDoPalpite e a apuração em Services/PalpiteService — aqui
    // só entra o que é HTTP. Tudo é derivado do que já estava gravado: nenhuma coluna nova.
    public partial class TorneiosController
    {
        // Pública, como a do MVP: ver quem cravou a zebra é justamente o que faz a página
        // valer o compartilhamento no grupo do WhatsApp.
        [HttpGet]
        public async Task<IActionResult> Palpiteiros(int id)
        {
            var torneio = await _context.Torneios
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();
            // Mesma porta do Details: pública sim, mas não a de torneio escondido.
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();

            var ranking = await _palpites.ObterRankingDoTorneioAsync(id);

            // ⚠️ 404 e não uma tela vazia, mesma régua da votação de MVP: torneio sem nenhum
            // palpite respondido não tem ranking pra mostrar, e uma página dizendo "nada aqui"
            // é um link que só sabe decepcionar. Quem esconde o link é o Details.
            if (ranking.Count == 0) return NotFound();

            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            ViewBag.Torneio = torneio;
            ViewBag.MeuJogadorId = meuId;
            return View(ranking);
        }
    }
}
