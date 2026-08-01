using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // Categoria de TIMES: criar a categoria, cadastrar e remover os times.
    //
    // Aqui não existe inscrição: quem monta a lista é o ORGANIZADOR, time por time, pelo
    // nome — com vínculo opcional ao cadastro de Times (traz o escudo). Cada time vira uma
    // Dupla com NomeTime preenchido e o motor do torneio faz o resto (ver Models/Dupla).
    public partial class TorneiosController
    {
        // Times podem ser mexidos até o sorteio sair: depois disso eles estão em grupos,
        // com jogos marcados — tirar um aí é outra operação, com outras consequências.
        private static bool AntesDoSorteio(Torneio torneio) =>
            torneio.Status == "Inscrições Abertas" || torneio.Status == "Chaves em Sorteio";

        // A tela do organizador: as categorias de times do torneio, cada uma com seus times.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Times(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias.Where(c => c.DeTimes))
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Time)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // O datalist do cadastro de Times: escolher um nome que já existe vincula o
            // escudo — mas digitar um nome novo é igualmente válido.
            ViewBag.TimesCadastrados = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.PodeMexer = AntesDoSorteio(torneio);

            return View(torneio);
        }

        // Cria a categoria de times num torneio já existente (na criação do torneio há o
        // caminho equivalente). A estrutura é validada AQUI, quando ajustar é de graça —
        // não na hora do sorteio, com os times já avisados.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AdicionarCategoriaDeTimes(
            int torneioId, string nome, int quantidadeTimes, int quantidadeGrupos, int classificadosPorGrupo)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (torneio.Formato == "Americano")
            {
                TempData["Erro"] = "Categoria de times só existe no formato padrão (grupos + mata-mata).";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (!AntesDoSorteio(torneio))
            {
                TempData["Erro"] = "O torneio já está em andamento — não dá mais pra criar categoria de times nele.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (CategoriaDeTimes.ProblemaNaConfiguracao(quantidadeTimes, quantidadeGrupos, classificadosPorGrupo) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Details", new { id = torneioId });
            }

            nome = string.IsNullOrWhiteSpace(nome) ? "Times" : nome.Trim();

            _context.Categorias.Add(new Categoria
            {
                TorneioId = torneioId,
                Nome = nome,
                Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                DeTimes = true,
                QuantidadeTimes = quantidadeTimes,
                QuantidadeGrupos = quantidadeGrupos,
                ClassificadosPorGrupo = classificadosPorGrupo,
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Categoria \"{nome}\" criada: {quantidadeTimes} times em {quantidadeGrupos} grupo(s), " +
                                  $"classificam {classificadosPorGrupo} por grupo. Agora cadastre os times.";
            return RedirectToAction("Times", new { id = torneioId });
        }

        // Mudar a estrutura depois de criada: "achei que davam 8 times, vieram 6". A conta é
        // revalidada aqui pelo mesmo lugar da criação — e ainda contra os times que JÁ estão
        // cadastrados, porque prometer 6 vagas com 8 times dentro deixaria dois de fora sem
        // ninguém saber quais.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EditarCategoriaDeTimes(
            int torneioId, int categoriaId, string nome, int quantidadeTimes, int quantidadeGrupos, int classificadosPorGrupo)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var categoria = await _context.Categorias.FindAsync(categoriaId);

            if (torneio == null || categoria == null || categoria.TorneioId != torneioId || !categoria.DeTimes)
                return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (!AntesDoSorteio(torneio))
            {
                TempData["Erro"] = "As chaves já saíram — a estrutura da categoria não muda mais.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            if (CategoriaDeTimes.ProblemaNaConfiguracao(quantidadeTimes, quantidadeGrupos, classificadosPorGrupo) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Times", new { id = torneioId });
            }

            int jaCadastrados = await _context.Duplas.CountAsync(d => d.CategoriaId == categoriaId && d.NomeTime != null);
            if (quantidadeTimes < jaCadastrados)
            {
                TempData["Erro"] = $"Já são {jaCadastrados} times cadastrados — remova algum antes de baixar pra {quantidadeTimes}.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            if (!string.IsNullOrWhiteSpace(nome)) categoria.Nome = nome.Trim();
            categoria.QuantidadeTimes = quantidadeTimes;
            categoria.QuantidadeGrupos = quantidadeGrupos;
            categoria.ClassificadosPorGrupo = classificadosPorGrupo;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"\"{categoria.Nome}\" agora é {quantidadeTimes} times em {quantidadeGrupos} grupo(s), " +
                                  $"classificam {classificadosPorGrupo} por grupo.";
            return RedirectToAction("Times", new { id = torneioId });
        }

        // Tirar a categoria inteira. Só antes do sorteio e só sem time dentro — apagar uma
        // categoria com times levaria junto a lista que o organizador montou na mão.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoverCategoriaDeTimes(int torneioId, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var categoria = await _context.Categorias.FindAsync(categoriaId);

            if (torneio == null || categoria == null || categoria.TorneioId != torneioId || !categoria.DeTimes)
                return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (!AntesDoSorteio(torneio))
            {
                TempData["Erro"] = "As chaves já saíram — a categoria não pode mais ser removida.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            int comTimes = await _context.Duplas.CountAsync(d => d.CategoriaId == categoriaId && d.NomeTime != null);
            if (comTimes > 0)
            {
                TempData["Erro"] = $"Essa categoria tem {comTimes} time(s) dentro. Remova os times primeiro.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            var nome = categoria.Nome;
            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Categoria \"{nome}\" removida.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AdicionarTime(int torneioId, int categoriaId, string nome)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            var meuId = ObterJogadorIdLogado() ?? 0;

            if (torneio == null || categoria == null || categoria.TorneioId != torneioId || !categoria.DeTimes)
                return NotFound();
            if (!await EhOrganizadorAsync(torneioId, meuId)) return Forbid();

            if (!AntesDoSorteio(torneio))
            {
                TempData["Erro"] = "As chaves já saíram — os times estão sorteados em grupos.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            nome = (nome ?? "").Trim();
            if (nome.Length < 2)
            {
                TempData["Erro"] = "Diga o nome do time.";
                return RedirectToAction("Times", new { id = torneioId });
            }
            if (LimitesDeTexto.Problema(nome, 100, "O nome do time") is { } nomeLongo)
            {
                TempData["Erro"] = nomeLongo;
                return RedirectToAction("Times", new { id = torneioId });
            }

            bool repetido = await _context.Duplas.AnyAsync(d =>
                d.CategoriaId == categoriaId && d.NomeTime != null && d.NomeTime.ToLower() == nome.ToLower());
            if (repetido)
            {
                TempData["Erro"] = $"O time \"{nome}\" já está nessa categoria.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            // Nome que bate com o cadastro de Times ganha o vínculo (e o escudo) de graça.
            var timeCadastrado = await _context.Times.FirstOrDefaultAsync(t => t.Nome.ToLower() == nome.ToLower());

            // O time vira uma Dupla com NomeTime. Jogador1Id leva o organizador porque a
            // coluna é NOT NULL — mas nenhuma regra de jogador enxerga esta linha (as
            // guardas estão em ForaDoSorteio, EstatisticasService e AvisosDoDiaDeJogo).
            _context.Duplas.Add(new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = meuId,
                NomeTime = nome,
                TimeId = timeCadastrado?.Id,
                Pago = true,   // time não paga inscrição pelo sistema; sem isto viraria "pendência"
            });
            await _context.SaveChangesAsync();

            int cadastrados = await _context.Duplas.CountAsync(d => d.CategoriaId == categoriaId && d.NomeTime != null);
            TempData["Sucesso"] = $"Time \"{nome}\" cadastrado ({cadastrados} de {categoria.QuantidadeTimes ?? cadastrados}).";
            return RedirectToAction("Times", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RemoverTime(int torneioId, int duplaId)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var time = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId && d.NomeTime != null && d.Categoria.TorneioId == torneioId);
            if (time == null) return NotFound();

            // Duas cercas: o status do torneio E a existência de jogos — cinto e suspensório,
            // porque apagar uma dupla com partida derrubaria a chave inteira na cascata.
            bool temJogo = await _context.Partidas.AnyAsync(p => p.Dupla1Id == duplaId || p.Dupla2Id == duplaId);
            if (!AntesDoSorteio(torneio) || temJogo)
            {
                TempData["Erro"] = "Esse time já está no sorteio — não dá mais pra removê-lo por aqui.";
                return RedirectToAction("Times", new { id = torneioId });
            }

            _context.Duplas.Remove(time);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Time \"{time.NomeTime}\" removido.";
            return RedirectToAction("Times", new { id = torneioId });
        }
    }
}
