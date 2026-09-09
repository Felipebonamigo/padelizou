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
            //
            // ⚠️ Do lado da CASA, quem entra é só o raiz (18/08/2026): era o crachá `IsAdmin`,
            // que inclui o administrador nomeado. Pelo crachá ainda, e não pelo banco, porque a
            // pergunta é a mesma de sempre — quem sou eu — e `PodeVerDinheiro(User)` é a régua
            // única (Services/PoderesNoSistema).
            bool ehAdmin = PoderesNoSistema.PodeVerDinheiro(User);
            if (!ehAdmin && !await PodeVerDinheiroAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (!TaxaDoTorneioExterno.SeAplica(torneio)) return RedirectToAction("Details", new { id });

            int pessoas = await PessoasInscritasAsync(id);
            ViewBag.Pessoas = pessoas;
            ViewBag.Percentual = _taxas.ComissaoPercentualExterno;
            ViewBag.Valor = TaxaDoTorneioExterno.Valor(pessoas, torneio.PrecoInscricao, _taxas.ComissaoPercentualExterno);
            ViewBag.EhAdmin = ehAdmin;
            ViewBag.CobrancaPendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
                p.Tipo == "TaxaTorneio" && p.TorneioId == id && p.Status == "Pendente" && p.InvoiceUrl != null);

            // A cobrança Pix aberta, se houver — o botão vira "ver o Pix" em vez de gerar outra.
            ViewBag.PixPendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
                p.Tipo == PixDireto.TipoTaxaTorneio && p.TorneioId == id
                && p.MetodoPagamento == PixDireto.Metodo
                && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao));

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

            // Pix direto primeiro: os 5% caem inteiros na conta do Padelizou, sem gateway no
            // meio. O caminho da fatura só fica pra quando a chave Pix não está configurada.
            var pix = await _pagamentos.IniciarPixDiretoTaxaExternoAsync(torneio, organizador, valor);
            if (pix != null) return RedirectToAction("Pix", "Pagamentos", new { id = pix.Id });

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
            // Só o raiz (18/08/2026): registrar a negociação é abrir mão da nossa taxa, e
            // quem não enxerga o valor devido não tem como decidir isso.
            if (!PoderesNoSistema.PodeVerDinheiro(User)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.TaxaExternoNegociadaEm = DateTime.Now;
            torneio.TaxaExternoNegociadaObs = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Negociação registrada — as chaves estão liberadas.";
            return RedirectToAction("TaxaPlataforma", new { id });
        }

        // O FIADO (08/09/2026, pedido do Felipe): o organizador sorteia agora e paga a taxa
        // depois. A trava do "por fora" deixa de ser bloqueio e vira DÍVIDA REGISTRADA.
        //
        // ⚠️ Isto NÃO é o RegistrarNegociacaoTaxa logo acima, e a diferença é de quem assina.
        // Lá é o Padelizou abrindo mão da cobrança, e só o raiz pode. Aqui é o organizador
        // dizendo "eu pago", e por isso ele mesmo aperta — a conta continua dele, e o torneio
        // aparece devendo no /Admin/Financeiro até alguém dar baixa.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AdiarTaxaExterno(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Mesma régua do GerarChaves: a taxa é conta de quem ficou com o dinheiro das
            // inscrições, então quem não organiza não assina dívida em nome dele.
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // Torneio sem taxa a cobrar não tem o que adiar — e deixar carimbar assim mesmo
            // encheria a lista de cobrança de linha que não deve nada.
            if (!TaxaDoTorneioExterno.SeAplica(torneio))
            {
                TempData["Erro"] = "Este torneio não tem taxa do Padelizou a pagar.";
                return RedirectToAction("Details", new { id });
            }

            // ⚠️ IDEMPOTENTE, e a data é a do PRIMEIRO fiado. Botão de painel é clicado duas
            // vezes; reescrever a data zeraria a idade da dívida, que é justamente o que a
            // lista de cobrança mostra.
            if (torneio.TaxaExternoAdiadaEm == null)
            {
                torneio.TaxaExternoAdiadaEm = DateTime.Now;
                await _context.SaveChangesAsync();

                await AvisarAdminsDoFiadoAsync(torneio);
            }

            TempData["Sucesso"] = "Taxa adiada — as chaves estão liberadas. O acerto com o Padelizou fica pendente.";
            return RedirectToAction("Details", "Torneios", new { id }, fragment: "admin");
        }

        // Sem este aviso a dívida só existe pra quem abrir o financeiro — e ninguém abre o
        // financeiro por causa de um torneio que não sabe que aconteceu.
        private async Task AvisarAdminsDoFiadoAsync(Torneio torneio)
        {
            var admins = await _context.Jogadores
                .Where(j => (j.IsAdminGeral || j.IsAdminRaiz) && j.ExcluidoEm == null)
                .Select(j => j.Id)
                .ToListAsync();

            if (admins.Count == 0) return;

            await AvisarAsync(admins, "Taxa do Padelizou ficou pendente",
                $"O organizador de {torneio.Nome} sorteou as chaves e vai pagar a taxa depois. "
                + "O torneio está na lista de cobrança do financeiro.", torneio.Id);
        }

        // A BAIXA DO FIADO, quando o sorteio é desfeito (ver TorneiosController.Chaves).
        //
        // ⚠️ Existe pelo mesmo motivo do aviso acima, e é o par dele: quem recebeu o push da
        // dívida nascendo precisa saber que ela morreu, senão continua cobrando um torneio que
        // não deve mais — e a linha some da lista do financeiro sem ninguém saber por quê.
        private async Task AvisarAdminsDoFiadoDesfeitoAsync(Torneio torneio)
        {
            var admins = await _context.Jogadores
                .Where(j => (j.IsAdminGeral || j.IsAdminRaiz) && j.ExcluidoEm == null)
                .Select(j => j.Id)
                .ToListAsync();

            if (admins.Count == 0) return;

            await AvisarAsync(admins, "Taxa do Padelizou não está mais pendente",
                $"O organizador de {torneio.Nome} desfez o sorteio, então a taxa que ele ia pagar "
                + "depois saiu da lista de cobrança. Ela volta a ser cobrada quando ele sortear de novo.",
                torneio.Id);
        }
    }
}
