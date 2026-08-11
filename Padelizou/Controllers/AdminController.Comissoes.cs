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
    }
}
