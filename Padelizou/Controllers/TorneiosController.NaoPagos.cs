using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers
{
    // A DECISÃO SOBRE QUEM NÃO PAGOU, no fechamento das inscrições.
    //
    // ⚠️ A caixinha "quem não pagar perde a vaga" prometia exclusão automática e o sistema nunca
    // a cumpriu — ela vivia só no modelo e na tela de criação. O conserto NÃO foi passar a
    // excluir sozinho: aqui quem decide é o organizador, olhando a lista, no botão. Ver
    // Services/DecisaoSobreQuemNaoPagou.
    public partial class TorneiosController
    {
        // A lista de quem está devendo, com as duas saídas.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> NaoPagos(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            ViewBag.PodeRemover = DecisaoSobreQuemNaoPagou.PodeRemoverAgora(torneio);
            ViewBag.PorQueNaoPodeRemover = DecisaoSobreQuemNaoPagou.PorQueNaoPodeRemover;

            return View(await MontarNaoPagosAsync(torneio));
        }

        // "Vou acertar pessoalmente": ninguém sai.
        //
        // A resposta dele DESLIGA a caixinha, e isso não é atalho — é o que ela significa. Ele
        // acabou de dizer que não quer que a falta de pagamento tire ninguém; deixá-la ligada
        // faria a mesma pergunta voltar no próximo torneio copiado deste.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManterNaoPagos(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            torneio.ExcluirSeNaoPagar = false;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Combinado: ninguém sai por falta de pagamento. "
                + "Você pode marcar quem pagou pela aba Gerenciar, a qualquer momento.";
            return RedirectToAction("Details", new { id });
        }

        // "Remover quem não pagou": libera as vagas.
        //
        // ⚠️ AÇÃO DESTRUTIVA, e por isso ela pede a lista de volta: o formulário devolve os ids
        // que o organizador VIU na tela, e só esses saem. Sem isso, alguém que pagou entre a
        // abertura da página e o clique — Pix cai em segundos — sairia junto, e a tela dele
        // nunca teria mostrado esse nome.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverNaoPagos(int id, int[] duplaIds, int[] americanaIds)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            if (!DecisaoSobreQuemNaoPagou.PodeRemoverAgora(torneio))
            {
                TempData["Erro"] = DecisaoSobreQuemNaoPagou.PorQueNaoPodeRemover;
                return RedirectToAction("Details", new { id });
            }

            var motivo = $"Sua inscrição em {torneio.Nome} foi removida porque não foi paga até o "
                       + "prazo. Se você já tinha acertado, fale com o organizador.";

            // ⚠️ A condição de "não pago" é conferida DE NOVO aqui, contra o banco. A lista da
            // tela diz o que ELE quis remover; quem manda sobre o pagamento é o banco no instante
            // do clique. Confiar só no formulário deixaria a remoção a um POST montado à mão de
            // distância de tirar gente paga.
            var duplas = await _context.Duplas
                .Where(d => duplaIds.Contains(d.Id) && d.Categoria.TorneioId == id
                         && !d.Pago && !d.EmListaDeEspera && d.NomeTime == null)
                .ToListAsync();

            foreach (var dupla in duplas)
                await TirarDuplaDoTorneioAsync(dupla, torneio, motivo);

            var americanas = await _context.InscricoesAmericanas
                .Where(i => americanaIds.Contains(i.Id) && i.Categoria.TorneioId == id
                         && !i.Pago && !i.EmListaDeEspera)
                .ToListAsync();

            if (americanas.Count > 0)
            {
                var avisados = americanas.Select(i => i.JogadorId).ToList();
                _context.InscricoesAmericanas.RemoveRange(americanas);
                await _context.SaveChangesAsync();
                await AvisarAsync(avisados, "Você saiu do torneio", motivo, id);
            }

            int total = duplas.Count + americanas.Count;

            // A pergunta foi respondida; a caixinha não precisa voltar a perguntar neste torneio.
            torneio.PerguntaDeNaoPagosEm ??= DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = total == 0
                ? "Nenhuma inscrição foi removida — nesse meio-tempo os pagamentos foram acertados."
                : total == 1
                    ? "1 inscrição removida e a vaga liberada."
                    : $"{total} inscrições removidas e as vagas liberadas.";

            return RedirectToAction("Details", new { id });
        }

        private async Task<NaoPagosVM> MontarNaoPagosAsync(Torneio torneio)
        {
            // ⚠️ No Americano individual a tabela Dupla guarda o PAR DA RODADA, não a inscrição —
            // varrê-la ali ofereceria pra remoção uma partida jogada. Mesma régua do lembrete.
            var duplas = LembreteDeInscricaoNaoPaga.AInscricaoEhDupla(torneio)
                ? await _context.Duplas
                    .Include(d => d.Jogador1).Include(d => d.Jogador2).Include(d => d.Categoria)
                    .Where(d => d.Categoria.TorneioId == torneio.Id
                             && !d.Pago && !d.EmListaDeEspera && d.NomeTime == null)
                    .OrderBy(d => d.Categoria.Nome).ThenBy(d => d.Id)
                    .ToListAsync()
                : new List<Dupla>();

            var americanas = await _context.InscricoesAmericanas
                .Include(i => i.Jogador).Include(i => i.Categoria)
                .Where(i => i.Categoria.TorneioId == torneio.Id && !i.Pago && !i.EmListaDeEspera)
                .OrderBy(i => i.Categoria.Nome).ThenBy(i => i.Id)
                .ToListAsync();

            return new NaoPagosVM
            {
                Torneio = torneio,
                Duplas = duplas.Select(d => new NaoPagosVM.Linha
                {
                    Id = d.Id,
                    Nome = d.NomeDeExibicao,
                    Categoria = d.Categoria.Nome,
                    Valor = PrecoDaInscricao.DaDupla(torneio, d),
                }).ToList(),
                Americanas = americanas.Select(i => new NaoPagosVM.Linha
                {
                    Id = i.Id,
                    Nome = i.Jogador.NomeNaTela,
                    Categoria = i.Categoria.Nome,
                    Valor = PrecoDaInscricao.DaInscricaoAmericana(torneio, i),
                }).ToList(),
            };
        }
    }
}
