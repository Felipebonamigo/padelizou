using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Diagnostics;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    public class HomeController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEstatisticasService _estatisticas;

        public HomeController(DbPadelContext context, IEstatisticasService estatisticas)
        {
            _context = context;
            _estatisticas = estatisticas;
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

            if (User.Identity?.IsAuthenticated == true
                && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var jogadorId))
            {
                await PreencherParteLogadaAsync(vm, jogadorId);
            }

            return View(vm);
        }

        // "Hoje no seu padel": tudo consulta pequena e indexada por jogador — a home é a
        // página mais aberta do sistema, não pode pesar.
        private async Task PreencherParteLogadaAsync(HomeVM vm, int jogadorId)
        {
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return; // cookie válido de conta que não existe mais

            vm.PrimeiroNome = jogador.Nome.Split(' ')[0];
            vm.Onboarding = await _estatisticas.ObterOnboardingAsync(jogadorId);

            // Próximo jogo de torneio com hora e quadra definidas. A margem de 2h pra trás
            // cobre o jogo atrasado do dia: ainda é "o próximo" até alguém finalizar.
            var corte = DateTime.Now.AddHours(-2);
            vm.ProximoJogo = await _context.Partidas
                .Where(p => p.HorarioPrevisto != null && p.HorarioPrevisto >= corte
                         && p.Status != "Finalizada"
                         && (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId ||
                             p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId))
                .OrderBy(p => p.HorarioPrevisto)
                .Select(p => new ProximoJogoVM
                {
                    TorneioId = p.Categoria.TorneioId,
                    Torneio = p.Categoria.Torneio.Nome,
                    Fase = p.Fase,
                    Categoria = p.Categoria.Nome,
                    Horario = p.HorarioPrevisto!.Value,
                    Quadra = p.NomeQuadra,
                    Adversarios = (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId)
                        ? p.Dupla2.Jogador1.Nome + " e " + p.Dupla2.Jogador2.Nome
                        : p.Dupla1.Jogador1.Nome + " e " + p.Dupla1.Jogador2.Nome,
                })
                .FirstOrDefaultAsync();

            // Compromissos: aula que vou ter, aula que vou dar e quadra reservada.
            // (Jogo semanal de grupo fica de fora — é registrado depois que acontece.)
            var agora = DateTime.Now;
            var compromissos = new List<CompromissoVM>();

            compromissos.AddRange(await _context.Aulas
                .Where(a => a.AlunoId == jogadorId && a.DataHora >= agora
                         && (a.Status == "Pendente" || a.Status == "Confirmada"))
                .Select(a => new CompromissoVM
                {
                    Data = a.DataHora,
                    Titulo = "Aula com Prof. " + a.Professor.Nome,
                    Subtitulo = a.LocalAula.Nome + " — " + a.Status,
                    Icone = "bi-mortarboard",
                    Controller = "Aulas", Action = "MinhasAulas",
                })
                .ToListAsync());

            if (jogador.IsProfessor)
            {
                compromissos.AddRange(await _context.Aulas
                    .Where(a => a.ProfessorId == jogadorId && a.DataHora >= agora
                             && (a.Status == "Pendente" || a.Status == "Confirmada"))
                    .Select(a => new CompromissoVM
                    {
                        Data = a.DataHora,
                        Titulo = "Aula para " + (a.Aluno != null ? a.Aluno.Nome : a.NomeAlunoAvulso ?? "aluno avulso"),
                        Subtitulo = a.LocalAula.Nome,
                        Icone = "bi-person-video3",
                        Controller = "Aulas", Action = "MinhaAgenda",
                    })
                    .ToListAsync());
            }

            compromissos.AddRange(await _context.MarcacoesJogo
                .Where(m => m.JogadorId == jogadorId && m.Status == "Confirmada" && m.DataHora >= agora)
                .Select(m => new CompromissoVM
                {
                    Data = m.DataHora,
                    Titulo = "Quadra em " + m.Clube.Nome,
                    Subtitulo = m.QuadraClube.Nome + " — " + m.DuracaoMinutos + " min",
                    Icone = "bi-calendar2-week",
                    Controller = "MarcarJogo", Action = "MinhasMarcacoes",
                })
                .ToListAsync());

            vm.Compromissos = compromissos.OrderBy(c => c.Data).Take(3).ToList();

            // Torneios em que estou dentro (dupla ou americano) e que ainda não terminaram.
            var meusTorneios = await _context.Duplas
                .Where(d => (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                         && d.Categoria.Torneio.Status != "Finalizado")
                .Select(d => new MeuTorneioVM
                {
                    Torneio = d.Categoria.Torneio,
                    Categoria = d.Categoria.Nome,
                    ListaDeEspera = d.EmListaDeEspera,
                })
                .ToListAsync();

            meusTorneios.AddRange(await _context.InscricoesAmericanas
                .Where(i => i.JogadorId == jogadorId && i.Categoria.Torneio.Status != "Finalizado")
                .Select(i => new MeuTorneioVM
                {
                    Torneio = i.Categoria.Torneio,
                    Categoria = i.Categoria.Nome,
                    ListaDeEspera = i.EmListaDeEspera,
                })
                .ToListAsync());

            vm.MeusTorneios = meusTorneios
                .GroupBy(m => m.Torneio.Id).Select(g => g.First())
                .OrderBy(m => m.Torneio.Status == "Inscrições Abertas") // em andamento primeiro
                .ThenBy(m => m.Torneio.DataInicio)
                .ToList();

            // O que eu já acompanho não repete na vitrine geral.
            var meusIds = vm.MeusTorneios.Select(m => m.Torneio.Id).ToHashSet();
            vm.EmAndamento = vm.EmAndamento.Where(t => !meusIds.Contains(t.Id)).ToList();
            vm.Abertos = vm.Abertos.Where(t => !meusIds.Contains(t.Id)).ToList();
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
