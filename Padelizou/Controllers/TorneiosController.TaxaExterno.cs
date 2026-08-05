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
    // A taxa dos 5% do torneio "por fora": área de pagamento, negociação e a trava das chaves (Services/TaxaDoTorneioExterno).
    public partial class TorneiosController
    {
        // Pessoas inscritas no torneio inteiro, pela régua do serviço (dupla completa = 2,
        // sem parceiro = 1, lista de espera fora). Busca as listas planas e delega — a regra
        // mora num lugar só.
        private async Task<int> PessoasInscritasAsync(int torneioId)
        {
            // Chave direta fora da base: as duplas dela são as MESMAS pessoas já contadas na
            // categoria em que se inscreveram. Contar de novo cobraria do organizador duas
            // vezes pela mesma gente. O filtro é na query (não numa propriedade calculada)
            // porque aqui a Categoria não vem carregada — e um `false` silencioso viraria
            // dinheiro cobrado a mais.
            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId && !d.Categoria.ChaveDireta).ToListAsync();
            var americanas = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == torneioId).ToListAsync();
            return TaxaDoTorneioExterno.PessoasInscritas(duplas, americanas);
        }

        // A trava em si: o sorteio para aqui se a taxa do Externo não foi paga nem negociada.
        // Torneio que fechou sem ninguém inscrito não tem o que taxar — não trava.
        private async Task<bool> TaxaExternoImpedeChavesAsync(Torneio torneio)
        {
            if (TaxaDoTorneioExterno.ChavesLiberadas(torneio)) return false;
            return await PessoasInscritasAsync(torneio.Id) > 0;
        }

        // A área de pagamento do organizador: a conta na frente (inscritos × preço × 5%),
        // o botão de pagar e o caminho da negociação.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> TaxaPlataforma(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Esta tela é dinheiro do começo ao fim — o valor devido e o botão de pagar. Quem
            // só ajuda a organizar não entra: a conta é de quem recebeu as inscrições.
            bool ehAdmin = User.FindFirstValue("IsAdmin") == "true";
            if (!ehAdmin && !await PodeVerDinheiroAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (!TaxaDoTorneioExterno.SeAplica(torneio)) return RedirectToAction("Details", new { id });

            int pessoas = await PessoasInscritasAsync(id);
            ViewBag.Pessoas = pessoas;
            ViewBag.Percentual = _taxas.ComissaoPercentualExterno;
            ViewBag.Valor = TaxaDoTorneioExterno.Valor(pessoas, torneio.PrecoInscricao, _taxas.ComissaoPercentualExterno);
            ViewBag.EhAdmin = ehAdmin;
            ViewBag.CobrancaPendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
                p.Tipo == "TaxaTorneio" && p.TorneioId == id && p.Status == "Pendente" && p.InvoiceUrl != null);

            return View(torneio);
        }

        // Gera (ou reaproveita) a cobrança da taxa e manda o organizador pra fatura.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PagarTaxaPlataforma(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Gerar cobrança em nome de outra pessoa não é "ajudar na mesa": quem paga a taxa
            // é quem ficou com o dinheiro das inscrições.
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await PodeVerDinheiroAsync(id, meuId)) return Forbid();

            if (!TaxaDoTorneioExterno.SeAplica(torneio) || TaxaDoTorneioExterno.ChavesLiberadas(torneio))
                return RedirectToAction("Details", new { id });

            // O valor final só existe com a lista fechada: pagar com inscrição aberta
            // cobraria um número que ainda vai mudar.
            if (torneio.Status == "Inscrições Abertas")
            {
                TempData["Erro"] = "Encerre as inscrições primeiro — o valor da taxa é calculado com a lista fechada.";
                return RedirectToAction("TaxaPlataforma", new { id });
            }

            int pessoas = await PessoasInscritasAsync(id);
            var valor = TaxaDoTorneioExterno.Valor(pessoas, torneio.PrecoInscricao, _taxas.ComissaoPercentualExterno);
            if (valor <= 0)
            {
                TempData["Erro"] = "Não há inscrições pra taxar — as chaves já estão liberadas.";
                return RedirectToAction("Details", new { id });
            }

            var organizador = await _context.Jogadores.FindAsync(meuId);
            if (organizador == null) return NotFound();

            var url = await _pagamentos.IniciarCobrancaTaxaExternoAsync(torneio, organizador, valor);
            if (url == null)
            {
                TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente de novo em instantes — ou fale com a gente pra negociar.";
                return RedirectToAction("TaxaPlataforma", new { id });
            }

            return Redirect(url);
        }

        // O outro caminho da condição: "mediante pagamento OU NEGOCIAÇÃO com o sistema".
        // Só um admin do Padelizou registra — é o Padelizou abrindo mão da cobrança
        // automática, não o organizador se liberando sozinho.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> RegistrarNegociacaoTaxa(int id, string? observacao)
        {
            if (User.FindFirstValue("IsAdmin") != "true") return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.TaxaExternoNegociadaEm = DateTime.Now;
            torneio.TaxaExternoNegociadaObs = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Negociação registrada — as chaves estão liberadas.";
            return RedirectToAction("TaxaPlataforma", new { id });
        }

    }
}
