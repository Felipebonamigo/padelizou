using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
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

        public IActionResult Index()
        {
            // Busca todos os torneios no banco de dados para montar a vitrine
            var torneios = _context.Torneios.ToList();
            return View(torneios);
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