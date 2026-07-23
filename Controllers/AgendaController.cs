using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class AgendaController : Controller
    {
        private const int DuracaoPadraoMinutosAula = 60;

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
                    Titulo = $"Aula para {(a.Aluno != null ? a.Aluno.Nome : a.NomeAlunoAvulso ?? "aluno avulso")}",
                    Subtitulo = $"{a.LocalAula.Nome} — {a.Status}",
                    Icone = "bi-person-video3",
                    LinkController = "Aulas",
                    LinkAction = "MinhaAgenda"
                }));
            }

            if (jogador.AgendaMostrarTorneios)
            {
                // Dupla não tem vínculo direto com Torneio no banco — o caminho real é
                // Dupla -> Categoria -> Torneio.
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
            ViewBag.LinkFeedAgenda = Url.Action("Feed", "Agenda",
                new { id = jogador.Id, token = jogador.AgendaFeedToken }, Request.Scheme);
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

        // ===================== EVENTOS PRA CALENDÁRIO (FullCalendar) =====================

        // Mesmos 4 tipos/toggles e mesmos links do Index() (histórico completo, sem filtro de
        // data) — só que com start/end reais em vez de um único ponto, pra render em grade.
        [HttpGet]
        public async Task<IActionResult> EventosJson()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador == null) return NotFound();

            var eventos = new List<object>();

            if (jogador.AgendaMostrarAulas)
            {
                var aulas = await _context.Aulas
                    .Include(a => a.Professor)
                    .Include(a => a.LocalAula)
                    .Where(a => a.AlunoId == userId)
                    .ToListAsync();

                eventos.AddRange(aulas.Select(a => (object)new
                {
                    title = $"Aula com Prof. {a.Professor.Nome}",
                    start = a.DataHora,
                    end = a.DataHora.AddMinutes(DuracaoPadraoMinutosAula),
                    color = "#1C2742",
                    url = Url.Action("MinhasAulas", "Aulas")
                }));
            }

            if (jogador.AgendaMostrarAlunos && jogador.IsProfessor)
            {
                var aulasDadas = await _context.Aulas
                    .Include(a => a.Aluno)
                    .Include(a => a.LocalAula)
                    .Where(a => a.ProfessorId == userId)
                    .ToListAsync();

                eventos.AddRange(aulasDadas.Select(a => (object)new
                {
                    title = $"Aula para {(a.Aluno != null ? a.Aluno.Nome : a.NomeAlunoAvulso ?? "aluno avulso")}",
                    start = a.DataHora,
                    end = a.DataHora.AddMinutes(DuracaoPadraoMinutosAula),
                    color = "#ffc107",
                    textColor = "#1b2540",
                    url = Url.Action("MinhaAgenda", "Aulas")
                }));
            }

            if (jogador.AgendaMostrarTorneios)
            {
                var duplas = await _context.Duplas
                    .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                    .Where(d => d.Jogador1Id == userId || d.Jogador2Id == userId)
                    .ToListAsync();

                eventos.AddRange(duplas
                    .Where(d => d.Categoria?.Torneio?.DataInicio != null)
                    .Select(d => (object)new
                    {
                        title = d.Categoria.Torneio.Nome,
                        start = d.Categoria.Torneio.DataInicio!.Value.ToString("yyyy-MM-dd"),
                        allDay = true,
                        color = "#198754",
                        url = Url.Action("Details", "Torneios", new { id = d.Categoria.Torneio.Id })
                    }));
            }

            if (jogador.AgendaMostrarJogosSemanais)
            {
                var jogos = await _context.JogosSemanais
                    .Include(j => j.Grupo)
                    .Where(j => j.Dupla1Jogador1Id == userId || j.Dupla1Jogador2Id == userId ||
                                j.Dupla2Jogador1Id == userId || j.Dupla2Jogador2Id == userId)
                    .ToListAsync();

                eventos.AddRange(jogos.Select(j => (object)new
                {
                    title = $"Jogo do grupo {j.Grupo.Nome}",
                    start = j.DataJogo,
                    color = "#0dcaf0",
                    textColor = "#1b2540",
                    url = Url.Action("Detalhes", "Grupos", new { id = j.GrupoId })
                }));
            }

            return Json(eventos);
        }

        // ===================== ASSINATURA DE AGENDA (FEED .ICS) =====================

        // URL fixa por jogador, protegida por id+token (mesmo padrão de Aula.TokenConfirmacao /
        // AulasController.ConfirmarPorEmail) — sem exigir login, pra funcionar com apps de
        // calendário externos (Google/Apple/Outlook) que só sabem fazer um GET simples.
        [AllowAnonymous]
        [HttpGet("Agenda/Feed/{id:int}/{token:guid}.ics")]
        public async Task<IActionResult> Feed(int id, Guid token)
        {
            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == id && j.AgendaFeedToken == token);
            if (jogador == null) return NotFound();

            var eventos = new List<IcsEvent>();

            if (jogador.AgendaMostrarAulas)
            {
                var aulas = await _context.Aulas
                    .Include(a => a.Professor)
                    .Include(a => a.LocalAula)
                    .Where(a => a.AlunoId == jogador.Id
                             && (a.Status == "Pendente" || a.Status == "Confirmada")
                             && a.DataHora >= DateTime.Today)
                    .ToListAsync();

                eventos.AddRange(aulas.Select(a => new IcsEvent(
                    Uid: $"padelizou-aula-{a.Id}-aluno@padelizou.com.br",
                    Inicio: a.DataHora,
                    Fim: a.DataHora.AddMinutes(DuracaoPadraoMinutosAula),
                    DiaInteiro: false,
                    Resumo: $"Aula com Prof. {a.Professor.Nome}",
                    Local: $"{a.LocalAula.Nome}, {a.LocalAula.Endereco}",
                    Descricao: $"Status: {a.Status}")));
            }

            if (jogador.AgendaMostrarAlunos && jogador.IsProfessor)
            {
                var aulasDadas = await _context.Aulas
                    .Include(a => a.Aluno)
                    .Include(a => a.LocalAula)
                    .Where(a => a.ProfessorId == jogador.Id
                             && (a.Status == "Pendente" || a.Status == "Confirmada")
                             && a.DataHora >= DateTime.Today)
                    .ToListAsync();

                eventos.AddRange(aulasDadas.Select(a => new IcsEvent(
                    Uid: $"padelizou-aula-{a.Id}-professor@padelizou.com.br",
                    Inicio: a.DataHora,
                    Fim: a.DataHora.AddMinutes(DuracaoPadraoMinutosAula),
                    DiaInteiro: false,
                    Resumo: $"Aula para {(a.Aluno != null ? a.Aluno.Nome : a.NomeAlunoAvulso ?? "aluno avulso")}",
                    Local: $"{a.LocalAula.Nome}, {a.LocalAula.Endereco}",
                    Descricao: $"Status: {a.Status}")));
            }

            if (jogador.AgendaMostrarTorneios)
            {
                // Reaproveita literalmente a mesma consulta do Index() — sem filtro por status,
                // igual ao comportamento atual, pra manter paridade com a Minha Agenda in-app.
                var duplas = await _context.Duplas
                    .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                    .Where(d => d.Jogador1Id == jogador.Id || d.Jogador2Id == jogador.Id)
                    .ToListAsync();

                eventos.AddRange(duplas
                    .Where(d => d.Categoria?.Torneio?.DataInicio != null)
                    .Select(d => new IcsEvent(
                        Uid: $"padelizou-torneio-{d.Categoria.Torneio.Id}@padelizou.com.br",
                        Inicio: d.Categoria.Torneio.DataInicio!.Value.Date,
                        Fim: null,
                        DiaInteiro: true,
                        Resumo: $"Início do Torneio: {d.Categoria.Torneio.Nome}",
                        Local: d.Categoria.Torneio.LocalTorneio,
                        Descricao: $"{d.Categoria.Nome} — {d.Categoria.Torneio.Status}")));

                // Um evento por partida já agendada (HorarioPrevisto definido), com hora e quadra
                // reais — mais útil que o marcador acima, mas nem toda partida tem isso definido,
                // daí o evento de torneio acima servir de garantia mesmo quando este não aparecer.
                var partidas = await _context.Partidas
                    .Where(p => p.HorarioPrevisto != null
                             && p.Status != "Finalizada"
                             && (p.Dupla1.Jogador1Id == jogador.Id || p.Dupla1.Jogador2Id == jogador.Id ||
                                 p.Dupla2.Jogador1Id == jogador.Id || p.Dupla2.Jogador2Id == jogador.Id))
                    .Select(p => new
                    {
                        p.Id,
                        p.HorarioPrevisto,
                        p.NomeQuadra,
                        p.Fase,
                        CategoriaNome = p.Categoria.Nome,
                        TorneioLocal = p.Categoria.Torneio.LocalTorneio,
                        DuracaoMinutos = p.Categoria.Torneio.TempoPrevistoPartidaMinutos,
                        Dupla1 = p.Dupla1.Jogador1.Nome + "/" + p.Dupla1.Jogador2.Nome,
                        Dupla2 = p.Dupla2.Jogador1.Nome + "/" + p.Dupla2.Jogador2.Nome
                    })
                    .ToListAsync();

                eventos.AddRange(partidas.Select(p => new IcsEvent(
                    Uid: $"padelizou-partida-{p.Id}@padelizou.com.br",
                    Inicio: p.HorarioPrevisto!.Value,
                    Fim: p.HorarioPrevisto.Value.AddMinutes(p.DuracaoMinutos),
                    DiaInteiro: false,
                    Resumo: $"Partida: {p.Dupla1} x {p.Dupla2}",
                    Local: p.NomeQuadra != null ? $"{p.NomeQuadra} — {p.TorneioLocal}" : p.TorneioLocal,
                    Descricao: $"{p.Fase} — {p.CategoriaNome}")));
            }

            // JogoSemanal fica de fora de propósito: é sempre retrospectivo (ver RegistradoPorId/
            // CriadoEm), não existe versão "futura" dele — não faz sentido num feed de calendário.

            var ics = IcsBuilder.BuildCalendar($"Padelizou — {jogador.Nome}", eventos);
            return Content(ics, "text/calendar; charset=utf-8");
        }

        [HttpPost]
        public async Task<IActionResult> RegenerarTokenFeed()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador != null)
            {
                jogador.AgendaFeedToken = Guid.NewGuid();
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Link da agenda atualizado. O link antigo parou de funcionar.";
            }
            return RedirectToAction("Index");
        }
    }
}
