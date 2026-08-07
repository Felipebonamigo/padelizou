using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace padelizou.Controllers
{
    // Quem pode criar torneio, e a fila de torneios esperando o OK.
    //
    // As duas telas nasceram da mesma preocupação do Felipe (07/08/2026): *"tenho medo que
    // qualquer pessoa chegue, crie torneio e lote de torneios"*. E o estrago não seria a lista
    // suja — cada torneio criado dispara aviso pra base inteira, então torneio inventado é
    // spam no celular de todo mundo.
    //
    // Duas travas, duas perguntas diferentes: QUEM pode criar (perfil, uma vez por pessoa) e
    // QUAL torneio aparece (aprovação, todo torneio, sempre).
    public partial class AdminController
    {
        // ── 1. O perfil de organizador ────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Organizadores(string? busca)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.ComPerfil = await _context.Jogadores
                .Where(j => j.IsOrganizadorTorneio && j.ExcluidoEm == null)
                .OrderBy(j => j.Nome)
                .ToListAsync();

            // A busca usa a MESMA régua do resto do site (nome, apelido ou CPF completo) —
            // uma terceira regra de busca aqui divergiria das outras duas em um mês.
            ViewBag.Achados = string.IsNullOrWhiteSpace(busca) || busca.Trim().Length < 3
                ? new List<Padelizou.Models.Jogador>()
                : await BuscaJogador.BuscarAsync(_context, busca, limite: 10);

            ViewBag.Busca = busca;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DarPerfilDeOrganizador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.IsOrganizadorTorneio = true;
            await _context.SaveChangesAsync();

            // Avisa a pessoa: ela pediu e está esperando. Descobrir sozinha, tentando de novo
            // dias depois, é perder o organizador no meio do caminho.
            try
            {
                await _pushNotificationService.EnviarParaJogadorAsync(jogador.Id,
                    "Você já pode criar torneios",
                    "Seu perfil de organizador foi liberado no Padelizou. Bons jogos!",
                    Url.Action("Create", "Torneios"));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Falha ao avisar o jogador {JogadorId} do perfil de organizador.", jogador.Id);
            }

            TempData["Sucesso"] = $"{NomeBonito.Formatar(jogador.Nome)} já pode criar torneios.";
            return RedirectToAction(nameof(Organizadores));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TirarPerfilDeOrganizador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.IsOrganizadorTorneio = false;
            await _context.SaveChangesAsync();

            // ⚠️ Tirar o perfil NÃO derruba os torneios que ela já criou: eles têm gente
            // inscrita, e apagar evento por causa de uma permissão revogada seria punir os
            // jogadores por algo que eles não fizeram. O que ela perde é abrir torneio novo.
            TempData["Sucesso"] = $"{NomeBonito.Formatar(jogador.Nome)} não cria mais torneios. "
                + "Os torneios que já existem continuam de pé, e ela segue organizando os dela.";
            return RedirectToAction(nameof(Organizadores));
        }

        // ── 2. A fila de aprovação ────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> TorneiosParaAprovar()
        {
            if (await ObterJogadorAdminRaizAsync() == null) return RedirectToAction("Perfil", "Auth");

            // Esperando primeiro, aprovados depois: a tela existe pra resolver a fila, e a
            // fila é o que está em cima.
            var torneios = await _context.Torneios
                .Include(t => t.Clube)
                .Where(t => t.Status != "Finalizado" && t.Status != CancelamentoDoTorneio.Status)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            var criadores = await _context.TorneioOrganizadores
                .Where(o => o.NivelAcesso == "Criador")
                .Include(o => o.Jogador)
                .ToDictionaryAsync(o => o.TorneioId, o => o.Jogador);

            ViewBag.Esperando = torneios.Where(t => t.AprovadoEm == null).ToList();
            ViewBag.Aprovados = torneios.Where(t => t.AprovadoEm != null).Take(20).ToList();
            ViewBag.Criadores = criadores;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AprovarTorneio(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Já aprovado sai fora ANTES de qualquer coisa: sem isto, dois cliques no mesmo
            // botão mandariam o aviso pra base inteira duas vezes.
            if (torneio.AprovadoEm != null)
            {
                TempData["Erro"] = $"\"{torneio.Nome}\" já estava aprovado.";
                return RedirectToAction(nameof(TorneiosParaAprovar));
            }

            torneio.AprovadoEm = DateTime.Now;
            torneio.AprovadoPorId = admin.Id;
            await _context.SaveChangesAsync();

            // O aviso "novo torneio aberto" mora AQUI, e não na criação: é neste momento que o
            // torneio passa a existir pra quem está de fora. Só enfileira — a entrega sai pela
            // FilaDeAvisos, por fora da requisição.
            //
            // Torneio oculto/restrito não avisa ninguém: quem escolheu não aparecer na vitrine
            // não quer um push pra base inteira anunciando o evento dele.
            if (!torneio.Oculto)
            {
                var elegiveis = await _context.Jogadores
                    .Where(j => j.NotificarTorneiosAbertos && j.ExcluidoEm == null)
                    .Select(j => j.Id)
                    .ToListAsync();

                var url = Url.Action("Details", "Torneios", new { id = torneio.Id });
                foreach (var jogadorId in elegiveis)
                {
                    await _pushNotificationService.EnviarParaJogadorAsync(
                        jogadorId, "Novo torneio aberto", torneio.Nome, url);
                }
            }

            TempData["Sucesso"] = $"\"{torneio.Nome}\" aprovado — já está na listagem"
                + (torneio.Oculto ? "." : " e o aviso saiu pra quem quer saber de torneio novo.");
            return RedirectToAction(nameof(TorneiosParaAprovar));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TirarAprovacaoDoTorneio(int id)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.AprovadoEm = null;
            torneio.AprovadoPorId = null;
            await _context.SaveChangesAsync();

            // ⚠️ O aviso que já saiu não volta. Tirar da vitrine tira da listagem daqui pra
            // frente; quem já recebeu o push tem o link e continua conseguindo abrir a página.
            // Pra sumir de verdade existe o cancelamento, que avisa os inscritos.
            TempData["Sucesso"] = $"\"{torneio.Nome}\" saiu da listagem. "
                + "Quem já recebeu o aviso continua com o link — pra encerrar de verdade, use o cancelamento.";
            return RedirectToAction(nameof(TorneiosParaAprovar));
        }
    }
}
