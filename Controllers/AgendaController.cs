using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class AgendaController : Controller
    {
        private readonly DbPadelContext _context;

        public AgendaController(DbPadelContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador == null) return NotFound();

            var itens = new List<AgendaItem>();

            if (jogador.AgendaMostrarAulas)
            {
                var aulas = await _context.Aulas
                    .Include(a => a.Professor)
                    .Include(a => a.LocalAula)
                    .Where(a => a.AlunoId == userId)
                    .ToListAsync();

                itens.AddRange(aulas.Select(a => new AgendaItem
                {
                    Data = a.DataHora,
                    Tipo = "Aula",
                    Titulo = $"Aula com Prof. {a.Professor.Nome}",
                    Subtitulo = $"{a.LocalAula.Nome} — {a.Status}",
                    Icone = "bi-calendar-check",
                    LinkController = "Aulas",
                    LinkAction = "MinhasAulas"
                }));
            }

            if (jogador.AgendaMostrarAlunos && jogador.IsProfessor)
            {
                var aulasDadas = await _context.Aulas
                    .Include(a => a.Aluno)
                    .Include(a => a.LocalAula)
                    .Where(a => a.ProfessorId == userId)
                    .ToListAsync();

                itens.AddRange(aulasDadas.Select(a => new AgendaItem
                {
                    Data = a.DataHora,
                    Tipo = "Aluno",
                    Titulo = $"Aula para {a.Aluno.Nome}",
                    Subtitulo = $"{a.LocalAula.Nome} — {a.Status}",
                    Icone = "bi-person-video3",
                    LinkController = "Aulas",
                    LinkAction = "MinhaAgenda"
                }));
            }

            if (jogador.AgendaMostrarTorneios)
            {
                // Dupla não tem vínculo direto com Torneio no banco — o caminho real é
                // Dupla -> Categoria -> Torneio (Dupla.Torneio/TorneioId existem no C# mas não
                // têm coluna correspondente na tabela; ver task sinalizada separadamente).
                var duplas = await _context.Duplas
                    .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                    .Where(d => d.Jogador1Id == userId || d.Jogador2Id == userId)
                    .ToListAsync();

                itens.AddRange(duplas.Where(d => d.Categoria?.Torneio != null).Select(d => new AgendaItem
                {
                    Data = d.Categoria.Torneio.DataInicio ?? DateTime.MinValue,
                    Tipo = "Torneio",
                    Titulo = d.Categoria.Torneio.Nome,
                    Subtitulo = $"{d.Categoria.Nome} — {d.Categoria.Torneio.Status}",
                    Icone = "bi-trophy",
                    LinkController = "Torneios",
                    LinkAction = "Details",
                    LinkId = d.Categoria.Torneio.Id
                }));
            }

            if (jogador.AgendaMostrarJogosSemanais)
            {
                var jogos = await _context.JogosSemanais
                    .Include(j => j.Grupo)
                    .Where(j => j.Dupla1Jogador1Id == userId || j.Dupla1Jogador2Id == userId ||
                                j.Dupla2Jogador1Id == userId || j.Dupla2Jogador2Id == userId)
                    .ToListAsync();

                itens.AddRange(jogos.Select(j => new AgendaItem
                {
                    Data = j.DataJogo,
                    Tipo = "Jogo Semanal",
                    Titulo = $"Jogo do grupo {j.Grupo.Nome}",
                    Subtitulo = $"Placar {j.GamesDupla1} x {j.GamesDupla2}",
                    Icone = "bi-shield-lock-fill",
                    LinkController = "Grupos",
                    LinkAction = "Detalhes",
                    LinkId = j.GrupoId
                }));
            }

            ViewBag.Jogador = jogador;
            return View(itens.OrderByDescending(i => i.Data).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarPreferencias(
            bool mostrarJogosSemanais, bool mostrarTorneios, bool mostrarAulas, bool mostrarAlunos)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador != null)
            {
                jogador.AgendaMostrarJogosSemanais = mostrarJogosSemanais;
                jogador.AgendaMostrarTorneios = mostrarTorneios;
                jogador.AgendaMostrarAulas = mostrarAulas;
                jogador.AgendaMostrarAlunos = mostrarAlunos;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
