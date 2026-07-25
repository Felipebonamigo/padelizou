using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;
using System.Diagnostics;

namespace Padelizou.Controllers
{
    public class HomeController : Controller
    {
        private readonly DbPadelContext _context;

        // Injetando o banco de dados na Home
        public HomeController(DbPadelContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Torneio oculto não aparece na home (mesma regra da listagem de Torneios —
            // antes a home ignorava o Oculto e vazava torneio restrito na vitrine).
            var ativos = await _context.Torneios
                .Where(t => !t.Oculto && t.Status != "Finalizado")
                .OrderBy(t => t.DataInicio)
                .ToListAsync();

            var vm = new HomeVM
            {
                Abertos = ativos.Where(t => t.Status == "Inscrições Abertas").Take(6).ToList(),
                EmAndamento = ativos.Where(t => t.Status != "Inscrições Abertas").ToList(),
                TotalJogadores = await _context.Jogadores.CountAsync(),
                TorneiosRealizados = await _context.Torneios.CountAsync(t => t.Status == "Finalizado"),
                JogosDisputados = await _context.Partidas.CountAsync(p => p.VencedorId != null),
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
