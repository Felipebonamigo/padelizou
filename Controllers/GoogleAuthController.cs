using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class GoogleAuthController : Controller
    {
        private readonly IGoogleCalendarService _googleCalendarService;

        public GoogleAuthController(IGoogleCalendarService googleCalendarService)
        {
            _googleCalendarService = googleCalendarService;
        }

        [HttpGet]
        public IActionResult Conectar()
        {
            var professorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var url = _googleCalendarService.GetAuthorizationUrl(professorId);
            return Redirect(url);
        }

        [HttpGet]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            var professorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (error != null || string.IsNullOrEmpty(code) || state != professorId.ToString())
            {
                TempData["Erro"] = "Não foi possível conectar sua Google Agenda. Tente novamente.";
                return RedirectToAction("MinhaAgenda", "Aulas");
            }

            await _googleCalendarService.ExchangeCodeAsync(professorId, code);

            TempData["Sucesso"] = "Google Agenda conectada com sucesso!";
            return RedirectToAction("MinhaAgenda", "Aulas");
        }
    }
}
