using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // A tela /Admin/Times: criar, apagar e dizer quem manda em cada time.
    //
    // Por que aqui e não na vitrine (/Times/Detalhes), que já designa administrador: dentro de
    // admin.padelizou.com.br o AdminHostMiddleware serve SÓ /Admin e /Auth — de lá, qualquer
    // caminho /Times dá 404. Sem esta tela, colocar o primeiro administrador de um time exigia
    // sair do painel e voltar pro site público.
    //
    // ⚠️ As REGRAS não são reescritas aqui: quem decide se pode conceder, se pode remover e o
    // que acontece ao conceder é Services/AdministracaoTime, o mesmo que a vitrine usa. A única
    // diferença é que aqui quem pede é sempre admin do sistema.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> Times()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var times = await _context.Times
                .Include(t => t.Clube)
                .OrderBy(t => t.Nome)
                .ToListAsync();

            // Uma consulta pra cada contagem, e não uma por linha: são 44 times importados do
            // ranking mais os criados por jogador — por linha seriam ~90 idas ao banco só pra
            // desenhar a tabela.
            var membrosPorTime = await _context.Jogadores
                .Where(j => j.TimeId != null && j.ExcluidoEm == null)
                .GroupBy(j => j.TimeId!.Value)
                .Select(g => new { TimeId = g.Key, Quantos = g.Count() })
                .ToDictionaryAsync(x => x.TimeId, x => x.Quantos);

            var duplasPorTime = await _context.Duplas
                .Where(d => d.TimeId != null)
                .GroupBy(d => d.TimeId!.Value)
                .Select(g => new { TimeId = g.Key, Quantos = g.Count() })
                .ToDictionaryAsync(x => x.TimeId, x => x.Quantos);

            var administradores = await _context.TimeAdministradores
                .Include(a => a.Jogador)
                .ToListAsync();

            // Nome de quem concedeu, numa consulta só — igual à vitrine.
            var idsConcedentes = administradores
                .Where(a => a.ConcedidoPorId != null)
                .Select(a => a.ConcedidoPorId!.Value)
                .Distinct()
                .ToList();

            var concedentes = await _context.Jogadores
                .Where(j => idsConcedentes.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            var linhas = times.Select(t => new TimeNoAdminVM
            {
                Time = t,
                Membros = membrosPorTime.GetValueOrDefault(t.Id),
                DuplasEmTorneios = duplasPorTime.GetValueOrDefault(t.Id),
                Administradores = administradores
                    .Where(a => a.TimeId == t.Id)
                    .Select(a => new AdministradorTimeVM
                    {
                        Jogador = a.Jogador,
                        ConcedidoEm = a.ConcedidoEm,
                        ConcedidoPor = a.ConcedidoPorId != null
                            ? concedentes.GetValueOrDefault(a.ConcedidoPorId.Value)
                            : null,
                    })
                    .OrderBy(a => a.ConcedidoEm)
                    .ToList(),
            }).ToList();

            ViewBag.Clubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

            return View(linhas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarTime(string nome, int? clubeId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            nome = (nome ?? "").Trim();

            if (nome.Length == 0)
            {
                TempData["Erro"] = "Diga o nome do time.";
                return RedirectToAction("Times");
            }

            if (nome.Length > CatalogoLocais.TamanhoMaximoNome)
            {
                TempData["Erro"] = $"O nome do time tem no máximo {CatalogoLocais.TamanhoMaximoNome} caracteres.";
                return RedirectToAction("Times");
            }

            // Nome repetido não pode, pela mesma razão do clube: no cadastro, quem digita o nome
            // de um time que já existe ENTRA nele (AuthController.DefinirTimeAsync casa por nome,
            // sem diferenciar maiúsculas). Com dois "SINDAQUA" na base, cada pessoa cai num dos
            // dois pelo acaso da consulta — e metade do time fica pendurada no cadastro errado,
            // em silêncio.
            var alvo = nome.ToLower();
            if (await _context.Times.AnyAsync(t => t.Nome.ToLower() == alvo))
            {
                TempData["Erro"] = $"Já existe um time chamado \"{nome}\". Quem digitar esse nome no cadastro entra no que já existe — criar um segundo só dividiria o time em dois.";
                return RedirectToAction("Times");
            }

            // Clube sede é opcional, e um id que não existe vira "sem sede" em vez de estourar
            // a gravação com erro de chave estrangeira.
            var sede = clubeId is > 0 && await _context.Clubes.AnyAsync(c => c.Id == clubeId) ? clubeId : null;

            _context.Times.Add(new Time { Nome = nome, ClubeId = sede });
            await _context.SaveChangesAsync();

            // Nasce SEM administrador, de propósito — igual aos 44 importados do ranking. Quem
            // vai mandar é escolhido no passo seguinte, pela busca da própria tela: criar o time
            // não faz do admin do sistema o dono dele.
            TempData["Sucesso"] = $"Time \"{nome}\" criado. Agora escolha quem administra.";
            return RedirectToAction("Times");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirTime(int timeId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var time = await _context.Times.FindAsync(timeId);
            if (time == null) return NotFound();

            // O que morre junto está declarado no DbPadelContext, e é de propósito:
            // - Jogador.TimeId → SetNull: quem vestia a camisa fica sem time, e só. A conta,
            //   os jogos e os pontos continuam inteiros.
            // - Dupla.TimeId → SetNull: a dupla-time de um torneio antigo perde o escudo na
            //   tela, mas o resultado do torneio não é tocado (a lição do Torneio.ClubeId em
            //   cascade, que apagou torneio junto com clube, já custou caro uma vez).
            // - TimeAdministrador → Cascade: sem o time, o cargo não quer dizer nada.
            var nome = time.Nome;
            _context.Times.Remove(time);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Time \"{nome}\" apagado. Quem vestia a camisa ficou sem time — nenhuma conta, jogo ou resultado foi tocado.";
            return RedirectToAction("Times");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IncluirAdministradorDoTime(int timeId, int jogadorId)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return Forbid();

            var time = await _context.Times.FindAsync(timeId);
            if (time == null) return NotFound();

            var novo = await _context.Jogadores.FindAsync(jogadorId);
            if (novo == null)
            {
                TempData["Erro"] = "Jogador não encontrado.";
                return RedirectToAction("Times");
            }

            // A regra é a mesma da vitrine — inclusive a recusa de duplicata. Aqui quem pede é
            // sempre admin do sistema, então a única resposta possível é "essa pessoa já
            // administra o time".
            var problema = AdministracaoTime.ProblemaParaConceder(
                quemPedeEhAdminDoSistema: true,
                quemPedeEhAdminDoTime: false,
                alvoJaEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId));

            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Times");
            }

            AdministracaoTime.Conceder(_context, timeId, novo, admin.Id);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{novo.ComoChamar} agora administra o {time.Nome}.";
            return RedirectToAction("Times");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAdministradorDoTime(int timeId, int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var quantos = await _context.TimeAdministradores.CountAsync(a => a.TimeId == timeId);

            // Admin do sistema PODE deixar o time sem nenhum administrador — é o estado dos 44
            // importados do ranking, e é ele quem destrava de volta. A trava de "não fique sem
            // ninguém" existe pro administrador do próprio time, na vitrine.
            var problema = AdministracaoTime.ProblemaParaRemover(
                quemPedeEhAdminDoSistema: true,
                quemPedeEhAdminDoTime: false,
                alvoEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId),
                quantosAdministradores: quantos);

            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Times");
            }

            var linha = await _context.TimeAdministradores
                .FirstAsync(a => a.TimeId == timeId && a.JogadorId == jogadorId);
            _context.TimeAdministradores.Remove(linha);
            await _context.SaveChangesAsync();

            // Sai da administração, mas continua no time: perder o cargo não é ser expulso.
            TempData["Sucesso"] = "Administrador removido. A pessoa continua no time.";
            return RedirectToAction("Times");
        }
    }
}
