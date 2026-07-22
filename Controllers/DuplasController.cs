using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Controllers
{
    public class DuplasController : Controller
    {
        private readonly DbPadelContext _context;

        public DuplasController(DbPadelContext context)
        {
            _context = context;
        }

        // Abre a tela de Inscrição
        public IActionResult Create()
        {
            return View();
        }

        // Recebe os dados do formulário
        [HttpPost]
        public async Task<IActionResult> Create(
            int torneioId, int categoriaId,
            string nome1, string cpf1, string? celular1, string? cidade1, string? estado1,
            string nome2, string cpf2, string? celular2, string? cidade2, string? estado2,
            bool impSextaNoite, bool impSabadoManha, bool impSabadoTarde)
        {
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // 1. Verifica ou cadastra o JOGADOR 1
            var jogador1 = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf1);
            if (jogador1 == null)
            {
                jogador1 = new Jogador { Nome = nome1, Cpf = cpf1 };
                _context.Jogadores.Add(jogador1);
            }
            jogador1.Celular = string.IsNullOrWhiteSpace(jogador1.Celular) ? celular1?.Trim() : jogador1.Celular;
            jogador1.Cidade = string.IsNullOrWhiteSpace(jogador1.Cidade) ? cidade1?.Trim() : jogador1.Cidade;
            jogador1.Estado = string.IsNullOrWhiteSpace(jogador1.Estado) ? estado1?.Trim() : jogador1.Estado;

            // 2. Verifica ou cadastra o JOGADOR 2
            var jogador2 = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf2);
            if (jogador2 == null)
            {
                jogador2 = new Jogador { Nome = nome2, Cpf = cpf2 };
                _context.Jogadores.Add(jogador2);
            }
            jogador2.Celular = string.IsNullOrWhiteSpace(jogador2.Celular) ? celular2?.Trim() : jogador2.Celular;
            jogador2.Cidade = string.IsNullOrWhiteSpace(jogador2.Cidade) ? cidade2?.Trim() : jogador2.Cidade;
            jogador2.Estado = string.IsNullOrWhiteSpace(jogador2.Estado) ? estado2?.Trim() : jogador2.Estado;

            // Salva os jogadores (se forem novos) para gerar os IDs que usaremos na dupla
            await _context.SaveChangesAsync();

            // 3. Monta a DUPLA e vincula à Categoria
            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = jogador1.Id,
                Jogador2Id = jogador2.Id,
                ImpedimentoSextaNoite = impSextaNoite,
                ImpedimentoSabadoManha = impSabadoManha,
                ImpedimentoSabadoTarde = impSabadoTarde
            };

            _context.Duplas.Add(dupla);
            await _context.SaveChangesAsync(); // Inscrição finalizada!

            TempData["Sucesso"] = "Inscrição confirmada com sucesso!";
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }
    }
}
