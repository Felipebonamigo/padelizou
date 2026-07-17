using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Create(int categoriaId, string nome1, string cpf1, string nome2, string cpf2)
        {
            // 1. Verifica ou cadastra o JOGADOR 1
            // Busca no banco de dados se já existe alguém com esse CPF
            var jogador1 = _context.Jogadores.FirstOrDefault(j => j.Cpf == cpf1);

            if (jogador1 == null)
            {
                // Se não existe, cria um novo jogador
                jogador1 = new Jogador { Nome = nome1, Cpf = cpf1 };
                _context.Jogadores.Add(jogador1);
            }

            // 2. Verifica ou cadastra o JOGADOR 2
            var jogador2 = _context.Jogadores.FirstOrDefault(j => j.Cpf == cpf2);

            if (jogador2 == null)
            {
                jogador2 = new Jogador { Nome = nome2, Cpf = cpf2 };
                _context.Jogadores.Add(jogador2);
            }

            // Salva os jogadores (se forem novos) para gerar os IDs que usaremos na dupla
            await _context.SaveChangesAsync();

            // 3. Monta a DUPLA e vincula à Categoria
            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = jogador1.Id,
                Jogador2Id = jogador2.Id
            };

            _context.Duplas.Add(dupla);
            await _context.SaveChangesAsync(); // Inscrição finalizada!

            // Por enquanto, volta para a home após o sucesso
            return RedirectToAction("Index", "Home");
        }
    }
}