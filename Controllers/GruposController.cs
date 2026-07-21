using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class GruposController : Controller
    {
        private readonly DbPadelContext _context;

        public GruposController(DbPadelContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = ObterUserId();

            var grupos = await _context.JogadoresGrupo
                .Include(jg => jg.GrupoPrivado)
                .Where(jg => jg.JogadorId == userId)
                .Select(jg => jg.GrupoPrivado)
                .ToListAsync();

            return View(grupos);
        }

        [HttpGet]
        public IActionResult Criar() => View();

        [HttpPost]
        public async Task<IActionResult> Criar(string nome)
        {
            var userId = ObterUserId();

            var grupo = new GrupoPrivado { Nome = nome, CodigoConvite = await GerarCodigoUnicoAsync(), AdministradorId = userId };
            _context.GruposPrivados.Add(grupo);
            await _context.SaveChangesAsync();

            _context.JogadoresGrupo.Add(new JogadorGrupo { JogadorId = userId, GrupoId = grupo.Id, PontuacaoInterna = 0 });
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalhes", new { id = grupo.Id });
        }

        [HttpGet]
        public IActionResult Entrar() => View();

        [HttpPost]
        public async Task<IActionResult> Entrar(string codigo)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.CodigoConvite == codigo.Trim().ToUpper());

            if (grupo == null)
            {
                TempData["Erro"] = "Código inválido.";
                return RedirectToAction("Entrar");
            }

            var jaMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupo.Id && jg.JogadorId == userId);
            if (!jaMembro)
            {
                _context.JogadoresGrupo.Add(new JogadorGrupo { JogadorId = userId, GrupoId = grupo.Id, PontuacaoInterna = 0 });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Detalhes", new { id = grupo.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id, int? mes, int? ano)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == id && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo == null) return NotFound();

            var ranking = await _context.JogadoresGrupo
                .Include(jg => jg.Jogador)
                .Where(jg => jg.GrupoId == id)
                .OrderByDescending(jg => jg.PontuacaoInterna)
                .ToListAsync();

            var mesConsulta = mes ?? DateTime.Today.Month;
            var anoConsulta = ano ?? DateTime.Today.Year;

            var jogosDoMes = await _context.JogosSemanais
                .Where(j => j.GrupoId == id && j.DataJogo.Month == mesConsulta && j.DataJogo.Year == anoConsulta)
                .ToListAsync();

            var pontosMes = new Dictionary<int, int>();
            foreach (var jogo in jogosDoMes)
            {
                AplicarPontos(pontosMes, jogo);
            }

            var jogosRecentes = await _context.JogosSemanais
                .Include(j => j.Dupla1Jogador1).Include(j => j.Dupla1Jogador2)
                .Include(j => j.Dupla2Jogador1).Include(j => j.Dupla2Jogador2)
                .Where(j => j.GrupoId == id)
                .OrderByDescending(j => j.DataJogo)
                .Take(15)
                .ToListAsync();

            ViewBag.Ranking = ranking;
            ViewBag.RankingMes = ranking
                .Select(r => new RankingMesItem { Jogador = r.Jogador, Pontos = pontosMes.GetValueOrDefault(r.JogadorId) })
                .OrderByDescending(x => x.Pontos)
                .ToList();
            ViewBag.MesConsulta = mesConsulta;
            ViewBag.AnoConsulta = anoConsulta;
            ViewBag.JogosRecentes = jogosRecentes;
            ViewBag.EhAdmin = grupo.AdministradorId == userId;

            return View(grupo);
        }

        [HttpGet]
        public async Task<IActionResult> RegistrarJogo(int grupoId)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            ViewBag.Membros = await _context.JogadoresGrupo
                .Include(jg => jg.Jogador)
                .Where(jg => jg.GrupoId == grupoId)
                .Select(jg => jg.Jogador)
                .ToListAsync();
            ViewBag.GrupoId = grupoId;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarJogo(
            int grupoId, DateTime dataJogo,
            int dupla1Jogador1Id, int dupla1Jogador2Id, int dupla2Jogador1Id, int dupla2Jogador2Id,
            int gamesDupla1, int gamesDupla2)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            var jogo = new JogoSemanal
            {
                GrupoId = grupoId,
                DataJogo = dataJogo,
                Dupla1Jogador1Id = dupla1Jogador1Id,
                Dupla1Jogador2Id = dupla1Jogador2Id,
                Dupla2Jogador1Id = dupla2Jogador1Id,
                Dupla2Jogador2Id = dupla2Jogador2Id,
                GamesDupla1 = gamesDupla1,
                GamesDupla2 = gamesDupla2,
                RegistradoPorId = userId
            };
            _context.JogosSemanais.Add(jogo);
            await _context.SaveChangesAsync();

            var pontos = new Dictionary<int, int>();
            AplicarPontos(pontos, jogo);
            foreach (var (jogadorId, pts) in pontos)
            {
                var registro = await _context.JogadoresGrupo.FirstOrDefaultAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == jogadorId);
                if (registro != null)
                {
                    registro.PontuacaoInterna += pts;
                }
            }
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Jogo registrado! Ranking atualizado.";
            return RedirectToAction("Detalhes", new { id = grupoId });
        }

        // Vitória = 3 pts, derrota = 1 pt (participação), empate = 2 pts pra cada lado.
        private static void AplicarPontos(Dictionary<int, int> pontos, JogoSemanal jogo)
        {
            int pontosDupla1, pontosDupla2;
            if (jogo.VencedorLado == 1) { pontosDupla1 = 3; pontosDupla2 = 1; }
            else if (jogo.VencedorLado == 2) { pontosDupla1 = 1; pontosDupla2 = 3; }
            else { pontosDupla1 = 2; pontosDupla2 = 2; }

            Somar(pontos, jogo.Dupla1Jogador1Id, pontosDupla1);
            Somar(pontos, jogo.Dupla1Jogador2Id, pontosDupla1);
            Somar(pontos, jogo.Dupla2Jogador1Id, pontosDupla2);
            Somar(pontos, jogo.Dupla2Jogador2Id, pontosDupla2);
        }

        private static void Somar(Dictionary<int, int> dict, int id, int pts)
        {
            dict[id] = dict.GetValueOrDefault(id) + pts;
        }

        private int ObterUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<string> GerarCodigoUnicoAsync()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem caracteres ambíguos (O/0, I/1)
            var rnd = new Random();
            string codigo;
            do
            {
                codigo = new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
            }
            while (await _context.GruposPrivados.AnyAsync(g => g.CodigoConvite == codigo));

            return codigo;
        }
    }

    public class RankingMesItem
    {
        public Jogador Jogador { get; set; } = null!;
        public int Pontos { get; set; }
    }
}
