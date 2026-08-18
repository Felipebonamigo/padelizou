using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // A tela /Admin/Comissoes: quanto cada parceiro comercial tem a receber.
    //
    // O elo já existia — o lead ganho aponta pro Jogador que virou cliente (/Admin/Leads) — e o
    // dinheiro também: `Pagamento.Comissao` é gravado quando o pagamento confirma. Faltava a
    // conta, e é ela que transforma o programa numa promessa cumprível.
    //
    // ⚠️ ESTA TELA TEM DOIS PÚBLICOS COM DIREITOS DIFERENTES, e é aí que mora o risco:
    //   - o Felipe (e o assistente) veem TODOS os parceiros;
    //   - o parceiro comercial vê SÓ AS LINHAS DELE.
    //
    // A segunda regra não é cosmética. Os parceiros disputam os mesmos contatos: quem o outro
    // trouxe, quanto rendeu e quanto vai receber é informação de concorrente. Por isso o id do
    // parceiro **é imposto pela sessão** quando quem olha não é da casa — a query string é
    // ignorada, não validada. Validar deixaria a porta existir; impor faz a porta não existir.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> Comissoes(int? parceiroId)
        {
            var quem = await QuemEstaLogadoAsync();
            if (!PoderesNoSistema.PodeVerComissoesDeParceiro(quem))
                return RedirectToAction("Perfil", "Auth");

            var soVeAsProprias = PoderesNoSistema.SoVeAsPropriasComissoes(quem);

            // ⚠️ A LINHA QUE SEGURA TUDO. Quem não é da casa não escolhe o filtro: ele É o
            // filtro. Um `parceiroId ?? quem.Id` aqui pareceria igual e deixaria `?parceiroId=3`
            // ler a carteira do colega.
            var filtro = soVeAsProprias ? quem!.Id : parceiroId;

            var leads = await _context.LeadsComerciais
                .Include(l => l.Parceiro)
                .Include(l => l.Cliente)
                .Where(l => l.Status == LeadsComerciais.Ganho && l.ClienteId != null)
                .Where(l => filtro == null || l.ParceiroId == filtro)
                .ToListAsync();

            // Um golpe só no banco pra todos os clientes: uma consulta por linha seria uma ida
            // por cliente indicado, e essa lista só cresce.
            var clienteIds = leads.Select(l => l.ClienteId!.Value).Distinct().ToList();

            // O cliente aparece como RECEBEDOR (inscrição, aula, quadra) ou como PAGADOR
            // (mensalidade e taxa do externo, onde o valor inteiro é nosso). Buscar só por um
            // dos dois perderia metade das frentes em silêncio — ver ComissaoDoParceiro.
            var pagamentos = await _context.Pagamentos
                .Where(p => p.Status == ComissaoDoParceiro.StatusQueConta && p.ConfirmadoEm != null)
                .Where(p => (p.RecebedorId != null && clienteIds.Contains(p.RecebedorId.Value))
                         || (p.RecebedorId == null && clienteIds.Contains(p.JogadorId)))
                .ToListAsync();

            var porCliente = pagamentos
                .GroupBy(ComissaoDoParceiro.ClienteDoPagamento)
                .ToDictionary(g => g.Key, g => g.ToList());

            var agora = DateTime.Now;
            var linhas = leads.Select(lead =>
            {
                var doCliente = porCliente.GetValueOrDefault(lead.ClienteId!.Value, new List<Pagamento>());
                var conta = ComissaoDoParceiro.Calcular(lead.Tipo, doCliente);

                return new ComissaoDeClienteVM
                {
                    Lead = lead,
                    Conta = conta,
                    DoMesCorrente = conta.Parcelas
                        .Where(p => ComissaoDoParceiro.EhDoMesCorrente(p, agora))
                        .Sum(p => p.Valor),
                };
            })
            .OrderByDescending(l => l.Conta.Total)
            .ToList();

            // ── O bloco de dinheiro, por parceiro ─────────────────────────────────────────
            // Os repasses saem da MESMA regra de filtro das linhas: um parceiro não pode ver
            // nem o que o outro recebeu.
            var parceiroIds = leads.Select(l => l.ParceiroId).Distinct().ToList();
            var repasses = await _context.RepassesAoParceiro
                .Where(r => parceiroIds.Contains(r.ParceiroId))
                .OrderByDescending(r => r.PagoEm)
                .ToListAsync();

            ViewBag.Acertos = linhas
                .GroupBy(l => l.Lead.Parceiro)
                .Select(g =>
                {
                    var doParceiro = repasses.Where(r => r.ParceiroId == g.Key.Id).ToList();
                    return new AcertoDoParceiroVM
                    {
                        Parceiro = g.Key,
                        Clientes = g.Count(),
                        Repasses = doParceiro,
                        Acerto = new ComissaoDoParceiro.Acerto(
                            TotalGanho: g.Sum(l => l.Conta.Total),
                            JaRepassado: doParceiro.Sum(r => r.Valor),
                            DoMesCorrente: g.Sum(l => l.DoMesCorrente)),
                    };
                })
                .OrderByDescending(a => a.Acerto.PagavelAgora)
                .ToList();

            ViewBag.Agora = agora;
            ViewBag.SoVeAsProprias = soVeAsProprias;
            ViewBag.NomeDeQuemOlha = quem!.Nome;
            ViewBag.Filtro = filtro;

            // A lista de parceiros pro seletor só existe pra quem vê todo mundo. Montá-la
            // sempre entregaria os nomes dos outros parceiros no HTML de quem não pode vê-los.
            ViewBag.Parceiros = soVeAsProprias
                ? new List<Jogador>()
                : await _context.Jogadores
                    .Where(j => j.IsParceiroComercial || _context.LeadsComerciais.Any(l => l.ParceiroId == j.Id))
                    .OrderBy(j => j.Nome)
                    .ToListAsync();

            return View(linhas);
        }

        // ⚠️ ISTO NÃO PAGA NINGUÉM. O Pix quem faz é o Felipe, no banco dele; esta ação só
        // REGISTRA que ele fez, pra que "quanto eu já te paguei?" tenha resposta sem abrir o
        // extrato. O texto da tela diz isso com todas as letras, de propósito.
        //
        // 🔒 Quem pode gravar: só o RAIZ (18/08/2026). Era `ObterJogadorAdminAsync`, e mudou
        // junto com a tela: se o administrador nomeado não enxerga mais o saldo de parceiro
        // nenhum, deixá-lo registrando repasse seria dar a caneta a quem não vê o papel.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarRepasse(int parceiroId, decimal valor,
            DateTime? pagoEm, string? observacao)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var parceiro = await _context.Jogadores.FindAsync(parceiroId);
            if (parceiro == null) return NotFound();

            if (valor <= 0)
            {
                TempData["Erro"] = "O valor do repasse precisa ser maior que zero.";
                return RedirectToAction("Comissoes", new { parceiroId });
            }

            // Data no futuro é quase sempre erro de digitação (ano trocado), e uma data futura
            // some do fechamento do mês sem ninguém entender por quê.
            var quando = pagoEm ?? DateTime.Now;
            if (quando.Date > DateTime.Now.Date)
            {
                TempData["Erro"] = "A data do repasse não pode ser no futuro.";
                return RedirectToAction("Comissoes", new { parceiroId });
            }

            observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            if (observacao != null && observacao.Length > LeadsComerciais.TamanhoMaximoObservacao)
                observacao = observacao[..LeadsComerciais.TamanhoMaximoObservacao];

            _context.RepassesAoParceiro.Add(new RepasseAoParceiro
            {
                ParceiroId = parceiro.Id,
                Valor = valor,
                PagoEm = quando,
                RegistradoEm = DateTime.Now,
                RegistradoPorId = admin.Id,
                Observacao = observacao,
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Repasse de R$ {valor:N2} para {parceiro.ComoChamar} registrado "
                + $"em {quando:dd/MM/yyyy}. O saldo dele caiu na mesma hora.";
            return RedirectToAction("Comissoes", new { parceiroId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirRepasse(int repasseId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var repasse = await _context.RepassesAoParceiro
                .Include(r => r.Parceiro)
                .FirstOrDefaultAsync(r => r.Id == repasseId);
            if (repasse == null) return NotFound();

            // Apagar é pra lançamento errado — o dinheiro que saiu do banco não volta por aqui.
            // O saldo do parceiro SOBE de novo, e é isso que o aviso da tela diz.
            var parceiroId = repasse.ParceiroId;
            var valor = repasse.Valor;
            _context.RepassesAoParceiro.Remove(repasse);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Lançamento de R$ {valor:N2} apagado. O saldo voltou a contar esse valor.";
            return RedirectToAction("Comissoes", new { parceiroId });
        }
    }
}
