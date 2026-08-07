using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace padelizou.Controllers
{
    // O acerto do Ranking Americano: R$ 5 por pessoa inscrita, cobrado do organizador que
    // pediu pro Americano dele valer ponto.
    //
    // Mesmo desenho do acerto com o Ranking RS e da taxa do "por fora": o sistema CALCULA e
    // MOSTRA, o dinheiro entra por fora, e a confirmação é manual, pelo extrato. Não passa
    // gateway — é acerto direto, e inventar cobrança automática aqui seria criar um caminho de
    // dinheiro novo pra um valor de dezenas de reais.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> RankingAmericano()
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var torneios = await _context.Torneios
                .Where(t => t.PontuaNoRankingAmericano)
                .OrderByDescending(t => t.DataInicio)
                .Select(t => new { t.Id, t.Nome, t.DataInicio, t.Status, t.RankingAmericanoPagoEm })
                .ToListAsync();

            var ids = torneios.Select(t => t.Id).ToList();

            // A contagem mora em PessoasDoAmericano porque o RANKING faz a mesma pergunta pra
            // decidir o peso do ponto — e contagens separadas divergiriam um dia, cobrando por
            // um tamanho e pontuando por outro.
            //
            // ⚠️ Aqui ela contava só `InscricoesAmericanas`, que é onde o Americano INDIVIDUAL
            // grava. O de duplas grava em `Dupla` (herdou o caminho do Padrão), então ele
            // aparecia nesta tela com 0 pessoas e R$ 0,00 — "não pontua e não se cobra", sem
            // erro nenhum na tela.
            var pessoasPorTorneio = await PessoasDoAmericano.PorTorneioAsync(_context, ids);

            var linhas = torneios.Select(t =>
            {
                int pessoas = pessoasPorTorneio.TryGetValue(t.Id, out var q) ? q : 0;
                return new LinhaDoAcertoAmericano(
                t.Id, t.Nome, t.DataInicio, t.Status, pessoas,
                PontosDoAmericano.CustoParaPontuar(pessoas),
                t.RankingAmericanoPagoEm,
                // Abaixo do piso não pontua e não se cobra — a tela precisa dizer isso, senão
                // o número zerado parece defeito.
                PontosDoAmericano.PontuaNesteTamanho(pessoas),
                // Com inscrição aberta o número ainda se mexe; fechar o valor agora seria
                // cobrar por uma lista que ainda vai mudar.
                t.Status == "Inscrições Abertas");
            }).ToList();

            ViewBag.Linhas = linhas;
            ViewBag.PrecoPorPessoa = PontosDoAmericano.PrecoPorPessoa;
            ViewBag.MinimoParaPontuar = PontosDoAmericano.MinimoParaPontuar;
            return View();
        }

        public record LinhaDoAcertoAmericano(
            int TorneioId, string Nome, DateTime? DataInicio, string Status, int Pessoas,
            decimal Valor, DateTime? PagoEm, bool AlcancaOPiso, bool InscricoesAbertas);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarAcertoAmericanoPago(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null || !torneio.PontuaNoRankingAmericano)
            {
                TempData["Erro"] = "Esse torneio não pediu pra valer ponto no Ranking Americano.";
                return RedirectToAction(nameof(RankingAmericano));
            }

            torneio.RankingAmericanoPagoEm = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"\"{torneio.Nome}\" marcado como pago — os pontos entram no "
                + "Ranking Americano assim que o torneio for finalizado.";
            return RedirectToAction(nameof(RankingAmericano));
        }

        // Desfazer, porque marcar pago é um clique e errar de linha é fácil. Sem isto, o
        // conserto seria ir no banco à mão.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesfazerAcertoAmericano(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return RedirectToAction(nameof(RankingAmericano));

            torneio.RankingAmericanoPagoEm = null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"\"{torneio.Nome}\" voltou pra lista de acertos pendentes — "
                + "os pontos dele saem do Ranking Americano.";
            return RedirectToAction(nameof(RankingAmericano));
        }
    }
}
