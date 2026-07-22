using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace padelizou.Controllers
{
    // Controller enxuto: só existe para permitir cadastrar um novo clube/local a partir de
    // outros formulários (Cadastro, Preferências, Avisos), sem precisar de uma tela própria.
    [Authorize]
    public class ClubesController : Controller
    {
        private readonly DbPadelContext _context;

        public ClubesController(DbPadelContext context)
        {
            _context = context;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Criar(string nome, string? endereco)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                return BadRequest();
            }

            var clube = new Clube { Nome = nome, Endereco = endereco ?? "", Contato = "" };
            _context.Clubes.Add(clube);
            await _context.SaveChangesAsync();

            return Json(new { id = clube.Id, nome = clube.Nome });
        }
    }
}
