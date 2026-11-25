using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // A FINALÍSSIMA DTN E O TROFÉU (Treta, 25/11/2026) — só o HTTP; a regra mora em
    // Services/FinalissimaDtn. Ver TRETA.md.
    //
    // 🔒 Criar a Finalíssima é de quem organiza (EhOrganizadorAsync, sem régua nova). Registrar o
    // troféu é da dupla vencedora do jogo decisivo OU de quem organiza — o serviço confere quem
    // venceu; aqui só se descobre se é organizador.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarFinalissima(int id, int moduloId, int direitaDaPrimeira, int direitaDaSegunda)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var erro = await new FinalissimaDtn(_context)
                .CriarAsync(id, moduloId, direitaDaPrimeira, direitaDaSegunda, Robo);
            if (erro != null) TempData["Erro"] = erro;
            else TempData["Sucesso"] = "Finalíssima DTN criada.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarTrofeuDtn(int id, int categoriaId, int jogadorId)
        {
            var quem = ObterJogadorIdLogado() ?? 0;
            if (quem <= 0) return Forbid();
            bool ehOrganizador = await EhOrganizadorAsync(id, quem);

            var erro = await new FinalissimaDtn(_context)
                .RegistrarTrofeuAsync(id, categoriaId, jogadorId, quem, ehOrganizador);
            if (erro != null) TempData["Erro"] = erro;
            else TempData["Sucesso"] = "Troféu DTN registrado.";
            return RedirectToAction("Details", new { id });
        }
    }
}
