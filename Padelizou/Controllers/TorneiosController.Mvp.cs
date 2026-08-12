using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // O MVP DO TORNEIO: quem jogou elege o melhor entre os campeões.
    //
    // Regra inteira em Services/MvpDoTorneio — aqui só entra o que é HTTP. A tela esconde o que
    // não cabe, mas quem RECUSA é sempre o serviço: um POST montado à mão não passa por view
    // nenhuma.
    public partial class TorneiosController
    {
        // A tela da votação. Pública: quem não jogou não vota, mas ver quem foi eleito é
        // justamente o que faz a página valer a pena compartilhar.
        [HttpGet]
        public async Task<IActionResult> Mvp(int id)
        {
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            var votacao = await MvpDoTorneio.DoTorneioAsync(_context, id, meuId, DateTime.Now);
            if (votacao == null) return NotFound();

            // ⚠️ 404 e não uma tela vazia: torneio que ainda não acabou não tem votação pra
            // mostrar, e uma página dizendo "nada aqui" é um link que só sabe decepcionar.
            if (!votacao.Aberta && !votacao.Encerrada) return NotFound();

            // A ENQUETE pega carona na página (e no aviso): quem jogou dá nota pro clube e pra
            // organização enquanto a janela está aberta. Ver Services/EnqueteDoTorneio — ela
            // usa a mesma janela do MVP, mas é coleta nossa, pro "Melhor Clube do ano".
            if (meuId != null && votacao.SouEleitor && votacao.Aberta)
            {
                ViewBag.EnqueteAberta = true;
                ViewBag.MinhaAvaliacao = await _context.AvaliacoesDeTorneio
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TorneioId == id && a.JogadorId == meuId.Value);
            }

            return View(votacao);
        }

        // A resposta da enquete. POST, nunca GET — grava; e o id de quem responde é IMPOSTO
        // pela sessão, como no voto logo abaixo.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AvaliarTorneio(int id, int notaClube, int notaOrganizacao)
        {
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var recusa = await EnqueteDoTorneio.AvaliarAsync(
                _context, id, meuId, notaClube, notaOrganizacao, DateTime.Now);

            if (recusa != null) TempData["Erro"] = recusa;
            else TempData["Sucesso"] = "Avaliação registrada — obrigado! Dá pra ajustar enquanto a janela estiver aberta.";

            return RedirectToAction(nameof(Mvp), new { id });
        }

        // ⚠️ POST, nunca GET — votar grava. Link que grava é disparado por pré-carregamento do
        // navegador e por varredor de página, mesma razão do seguir-torneio e do convite pra
        // panelinha.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VotarMvp(int id, int candidatoId)
        {
            // ⚠️ O id do votante é IMPOSTO pela sessão, nunca lido do formulário. É a mesma
            // trava do 5º perfil: aceitar um "votanteId" do POST deixaria qualquer um votar em
            // nome de todo mundo que jogou.
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var recusa = await MvpDoTorneio.VotarAsync(_context, id, meuId, candidatoId, DateTime.Now);

            if (recusa != null) TempData["Erro"] = recusa;
            else TempData["Sucesso"] = "Voto registrado! Dá pra trocar enquanto a votação estiver aberta.";

            return RedirectToAction(nameof(Mvp), new { id });
        }
    }
}
