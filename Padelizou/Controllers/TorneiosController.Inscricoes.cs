using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // Inscrições: inscrever pelo balcão, pago/não pago, remover dupla e encerrar as inscrições.
    public partial class TorneiosController
    {
        // Inscrição individual (Torneio Americano) — achar-ou-criar Jogador por CPF, mesmo
        // padrão de DuplasController.Create, só que sem parceiro fixo.
        [HttpPost]
        public async Task<IActionResult> InscreverIndividual(int torneioId, int categoriaId, string nome, string cpf,
            string? chaveAcesso = null, string? formaPagamentoEscolhida = null)
        {
            // Mesma limpeza de DuplasController.Create: CPF com máscara estoura a coluna de
            // 11 chars e derruba a página em vez de avisar o jogador.
            cpf = Documentos.SomenteDigitos(cpf);
            if (!Documentos.CpfTemFormatoValido(cpf))
            {
                TempData["Erro"] = "CPF inválido — informe os 11 números, sem pontos ou traço.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // varchar(100) recusa (não corta) o que passa do tamanho: sem isto, nome comprido
            // colado da agenda do celular derrubava a inscrição com erro 500.
            if (LimitesDeTexto.Problema(nome, LimitesDeTexto.NomeDeJogador, "O nome") is { } nomeLongo)
            {
                TempData["Erro"] = nomeLongo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (torneio.Restrito && !string.Equals(chaveAcesso?.Trim(), torneio.ChaveAcesso, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Chave de acesso inválida. Confira com o organizador do torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (jogador == null)
            {
                jogador = new Jogador { Nome = nome, Cpf = cpf };
                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();
            }

            // Uma categoria por jogador, quando o organizador desligou as múltiplas.
            var bloqueioCategorias = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { jogador.Id });
            if (bloqueioCategorias != null)
            {
                TempData["Erro"] = bloqueioCategorias;
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool jaInscrito = await _context.InscricoesAmericanas
                .AnyAsync(i => i.CategoriaId == categoriaId && i.JogadorId == jogador.Id);
            if (!jaInscrito)
            {
                // Torneio pago com recebimento ativado? A inscrição ainda NÃO é criada: o
                // jogador vai pro checkout e ela nasce quando o webhook confirmar o pagamento
                // (PagamentoInscricaoService.EfetivarAsync).
                var recebedor = await _pagamentos.ObterRecebedorTorneioAsync(torneioId);
                // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
            // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
            if (_pagamentos.PodeCobrar(torneio, recebedor) && torneio.PagamentoObrigatorioNaInscricao)
                {
                    var dadosInscricao = new DadosInscricaoTorneio(
                        torneioId, categoriaId, jogador.Id, null, false, false, false);

                    var checkout = await _pagamentos.IniciarCobrancaTorneioAsync(
                        torneio, recebedor!, jogador, "TorneioAmericano", dadosInscricao, formaPagamentoEscolhida);

                    if (checkout != null) return Redirect(checkout);

                    TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                // Vagas: mesma regra da inscrição em dupla (ver DuplasController) — se a
                // categoria ou o torneio já estão cheios, entra na lista de espera.
                bool emListaDeEspera = false;
                if (categoria.LimiteDuplas.HasValue)
                {
                    int naCategoria = await _context.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoriaId && !i.EmListaDeEspera);
                    emListaDeEspera = naCategoria >= categoria.LimiteDuplas.Value;
                }
                if (!emListaDeEspera && torneio.LimiteDuplasTotal.HasValue)
                {
                    int noTorneio = await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == torneioId && !i.EmListaDeEspera);
                    emListaDeEspera = noTorneio >= torneio.LimiteDuplasTotal.Value;
                }

                _context.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoriaId, JogadorId = jogador.Id, EmListaDeEspera = emListaDeEspera });
                await _context.SaveChangesAsync();

                await NotificarSeguidoresDeInscricaoAsync(torneioId, new[] { jogador.Id });

                TempData["Sucesso"] = emListaDeEspera
                    ? "Vagas esgotadas — inscrição entrou na lista de espera. Se alguém desistir, é chamado na ordem de inscrição."
                    : "Inscrição individual confirmada!";
            }
            else
            {
                TempData["Sucesso"] = "Inscrição individual confirmada!";
            }
            return RedirectToAction("Details", new { id = torneioId });
        }

        // A última palavra sobre quem pagou é sempre do organizador: muita inscrição é
        // acertada em dinheiro na quadra ou por Pix direto, e o site não tem como saber.
        // Vale nos dois sentidos — marcar e desmarcar.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPagamentoDupla(int duplaId)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            dupla.Pago = !dupla.Pago;
            dupla.PagoEm = dupla.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = dupla.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPagamentoAmericano(int inscricaoId)
        {
            var inscricao = await _context.InscricoesAmericanas
                .Include(i => i.Categoria).FirstOrDefaultAsync(i => i.Id == inscricaoId);
            if (inscricao == null) return NotFound();

            int torneioId = inscricao.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            inscricao.Pago = !inscricao.Pago;
            inscricao.PagoEm = inscricao.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = inscricao.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // Aba "Gerenciar Torneio": remove um inscrito (só enquanto as inscrições estiverem abertas —
        // depois disso já pode existir Partida referenciando a dupla)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverDupla(int duplaId)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            if (torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "Só é possível remover inscritos enquanto as inscrições estiverem abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool eraConfirmada = !dupla.EmListaDeEspera;
            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            // Abriu vaga: promove quem está há mais tempo na lista de espera desta categoria
            // (a dupla de menor Id, já que a ordem de inscrição segue a ordem de criação).
            if (eraConfirmada)
            {
                var proximaDaFila = await _context.Duplas
                    .Where(d => d.CategoriaId == dupla.CategoriaId && d.EmListaDeEspera)
                    .OrderBy(d => d.Id)
                    .FirstOrDefaultAsync();
                if (proximaDaFila != null)
                {
                    proximaDaFila.EmListaDeEspera = false;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Sucesso"] = "Inscrito removido do torneio.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EncerrarInscricoes(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);

            // Verifica se o torneio existe
            if (torneio == null) return NotFound();

            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            torneio.Status = "Chaves em Sorteio";
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // ---- A taxa dos 5% do torneio "por fora" (Services/TaxaDoTorneioExterno) ----

    }
}
