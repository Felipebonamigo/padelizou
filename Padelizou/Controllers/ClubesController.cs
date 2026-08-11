using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Além do "criar clube inline" original, também é onde o dono/administrador de um clube
    // gerencia seus próprios administradores (atribuir dono é no AdminController, que é
    // exclusivo de quem já é administrador do sistema).
    [Authorize]
    public class ClubesController : Controller
    {
        private readonly DbPadelContext _context;

        public ClubesController(DbPadelContext context)
        {
            _context = context;
        }

        // "Criar clube inline": chamada por fetch de dentro de Avisos/Criar e
        // Grupos/Configuracoes, telas que já exigem login. Tinha [AllowAnonymous], que só
        // alargava a superfície — qualquer um criava clubes sem limite, e clube aparece em
        // dropdown por todo o app (locais de aula, torneio, preferências, busca).
        // ⚠️ `cidade`/`estado` entraram em 11/08/2026 e fecham a ÚLTIMA porta que criava clube
        // sem saber onde ele fica. As outras duas (cadastro/preferências e criação de torneio)
        // já perguntavam; enquanto esta não perguntasse, um local cadastrado pelo grupo
        // continuaria invisível pra qualquer mira geográfica — ver Services/UfDoTorneio.
        //
        // Os dois são OPCIONAIS: aqui o local costuma ser "onde a gente jogou", e exigir
        // endereço no meio de registrar um jogo é atrito na hora errada.
        [HttpPost]
        public async Task<IActionResult> Criar(string nome, string? endereco = null,
            string? cidade = null, string? estado = null)
        {
            nome = (nome ?? "").Trim();

            // ⚠️ A régua passou a ser a do CatalogoLocais (80), e não mais 120: era ele quem
            // CORTAVA silenciosamente o que passava disso. Recusar e avisar é melhor que
            // gravar um nome pela metade e a pessoa nunca entender por que ficou torto.
            if (string.IsNullOrWhiteSpace(nome) || nome.Length > CatalogoLocais.TamanhoMaximoNome)
            {
                return BadRequest();
            }

            // Uma implementação só de achar-ou-criar, compartilhada com as outras duas portas
            // (Services/CatalogoLocais): mesmo nome não vira clube novo, e clube antigo sem
            // cidade recebe a que acabou de ser informada — sem nunca sobrescrever a que já
            // existe. Antes isto era uma segunda cópia da regra, aqui dentro.
            var clube = await CatalogoLocais.AcharOuCriarClubeAsync(_context, nome, cidade, estado, endereco);
            if (clube == null) return BadRequest();

            return Json(new { id = clube.Id, nome = clube.Nome });
        }

        private async Task<bool> EhDonoOuAdminDoClubeAsync(int clubeId, int jogadorId)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return false;
            if (clube.DonoId == jogadorId) return true;

            return await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
        }

        private int? ObterJogadorIdLogado()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }

        [HttpGet]
        public async Task<IActionResult> Gerenciar(int id)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            var clube = await _context.Clubes.Include(c => c.Dono).FirstOrDefaultAsync(c => c.Id == id);
            if (clube == null) return NotFound();
            if (!await EhDonoOuAdminDoClubeAsync(id, meuId)) return Forbid();

            ViewBag.SouDono = clube.DonoId == meuId;
            ViewBag.Administradores = await _context.ClubeAdministradores
                .Include(a => a.Jogador)
                .Where(a => a.ClubeId == id)
                .OrderBy(a => a.Jogador.Nome)
                .ToListAsync();

            return View(clube);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAdministrador(int clubeId, int jogadorId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var jaEAdmin = await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
            if (!jaEAdmin)
            {
                _context.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = clubeId, JogadorId = jogadorId });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtivarMarcacaoHorarios(int clubeId, bool ativa)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return NotFound();

            clube.MarcacaoHorariosAtiva = ativa;
            if (!ativa) clube.NotificarHorariosDiariamente = false;
            await _context.SaveChangesAsync();

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtivarNotificacaoDiaria(int clubeId, bool ativa)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || !clube.MarcacaoHorariosAtiva) return Forbid();

            clube.NotificarHorariosDiariamente = ativa;
            await _context.SaveChangesAsync();

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }

        // Só o dono remove administradores — administradores não removem uns aos outros.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAdministrador(int clubeId, int jogadorId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || clube.DonoId != meuId) return Forbid();

            var vinculo = await _context.ClubeAdministradores
                .FirstOrDefaultAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
            if (vinculo != null)
            {
                _context.ClubeAdministradores.Remove(vinculo);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }
    }
}
