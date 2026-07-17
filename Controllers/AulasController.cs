using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize] // Só quem está logado pode acessar as rotas de aula
    public class AulasController : Controller
    {
        private readonly DbPadelContext _context;

        public AulasController(DbPadelContext context)
        {
            _context = context;
        }

        // 1. TELA DE AGENDAMENTO (Busca os professores disponíveis)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Filtra no banco apenas os usuários que possuem a flag de professor ativa
            var professores = await _context.Jogadores
                .Where(j => j.IsProfessor)
                .ToListAsync();

            return View(professores);
        }

        // 2. SALVA O AGENDAMENTO NO BANCO
        [HttpPost]
        public async Task<IActionResult> Create(int professorId, DateTime dataHora, string local, decimal preco)
        {
            var alunoIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(alunoIdValue, out var alunoId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var novaAula = new Aula
            {
                ProfessorId = professorId,
                AlunoId = alunoId,
                DataHora = dataHora,
                Local = local,
                Preco = preco,
                Status = "Agendada"
            };

            _context.Aulas.Add(novaAula);
            await _context.SaveChangesAsync();

            return RedirectToAction("Perfil", "Auth");
        }
        // 3. TELA DE GERENCIAMENTO DO PROFESSOR (Minha Agenda)
        [HttpGet]
        public async Task<IActionResult> MinhaAgenda()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador == null || !jogador.IsProfessor)
            {
                return RedirectToAction("Perfil", "Auth"); // Expulsa se não for prof
            }

            // Traz todas as aulas onde ele é o professor, incluindo o NOME e CELULAR do aluno
            var agenda = await _context.Aulas
                .Include(a => a.Aluno)
                .Where(a => a.ProfessorId == userId)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            return View(agenda);
        }

        // 4. ATUALIZAR STATUS DA AULA (Finalizar ou Cancelar)
        [HttpPost]
        public async Task<IActionResult> AtualizarStatus(int aulaId, string novoStatus)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aula = await _context.Aulas.FindAsync(aulaId);

            // Valida de segurança: garante que a aula existe e pertence a este professor
            if (aula != null && aula.ProfessorId == userId)
            {
                aula.Status = novoStatus;
                await _context.SaveChangesAsync();
            }

            // Recarrega a página da agenda
            return RedirectToAction("MinhaAgenda");
        }
        // 5. TELA DE HISTÓRICO DO ALUNO (Minhas Aulas)
        [HttpGet]
        public async Task<IActionResult> MinhasAulas()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            // Busca todas as aulas onde ele é o ALUNO, incluindo os dados do PROFESSOR
            var minhasAulas = await _context.Aulas
                .Include(a => a.Professor)
                .Where(a => a.AlunoId == userId)
                .OrderByDescending(a => a.DataHora) // Mostra as mais recentes primeiro
                .ToListAsync();

            return View(minhasAulas);
        }
    }
}