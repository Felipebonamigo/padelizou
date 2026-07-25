using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Padelizou.Controllers;

// Tela de configuração de recebimento + webhook do Asaas. O controller inteiro exige login;
// só o Webhook abre exceção, porque quem chama é o Asaas de fora.
[Authorize]
public class PagamentosController : Controller
{
    private readonly DbPadelContext _context;
    private readonly AsaasSettings _settings;
    private readonly IPagamentoInscricaoService _inscricoes;
    private readonly IAsaasService _asaas;
    private readonly ILogger<PagamentosController> _logger;

    public PagamentosController(DbPadelContext context, IOptions<AsaasSettings> settings,
        IPagamentoInscricaoService inscricoes, IAsaasService asaas, ILogger<PagamentosController> logger)
    {
        _context = context;
        _settings = settings.Value;
        _inscricoes = inscricoes;
        _asaas = asaas;
        _logger = logger;
    }

    private int ObterJogadorIdLogado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Tela onde quem organiza torneio ou dá aula liga o recebimento pelo app, informa a wallet
    // do Asaas e escolhe quem paga a comissão.
    [HttpGet]
    public async Task<IActionResult> Configurar()
    {
        var jogador = await _context.Jogadores.FindAsync(ObterJogadorIdLogado());
        if (jogador == null) return NotFound();

        ViewBag.ComissoesPorTipo = _settings.ComissaoPercentualPorTipo;
        ViewBag.ComissaoMinima = _settings.ComissaoMinima;
        ViewBag.ModoPadrao = _settings.ModoComissaoPadrao;
        return View(jogador);
    }

    // Extrato: o que entrou pra mim como organizador/professor e o que eu paguei como jogador.
    [HttpGet]
    public async Task<IActionResult> Meus(string? periodo)
    {
        var meuId = ObterJogadorIdLogado();
        var vm = new ViewModels.ExtratoFinanceiroVM
        {
            Periodo = (periodo ?? "").Trim().ToLower() switch { "mes" => "mes", "ano" => "ano", _ => "sempre" }
        };

        // Corte do período. Vale a data em que o dinheiro entrou de fato (ConfirmadoEm);
        // pra cobrança ainda pendente, a data em que ela foi criada.
        var agora = DateTime.Now;
        DateTime? de = vm.Periodo switch
        {
            "mes" => new DateTime(agora.Year, agora.Month, 1),
            "ano" => new DateTime(agora.Year, 1, 1),
            _ => null
        };
        static DateTime DataEfetiva(Pagamento p) => p.ConfirmadoEm ?? p.CriadoEm;

        var recebidos = await _context.Pagamentos
            .Where(p => p.RecebedorId == meuId)
            .Include(p => p.Jogador)
            .OrderByDescending(p => p.CriadoEm)
            .Take(500)
            .ToListAsync();

        if (de != null) recebidos = recebidos.Where(p => DataEfetiva(p) >= de.Value).ToList();

        vm.Movimentos = recebidos;
        vm.Recebido = recebidos.Where(p => p.Status == "Confirmado").Sum(p => p.ValorRepasse);
        vm.AReceber = recebidos.Where(p => p.Status == "Pendente").Sum(p => p.ValorRepasse);
        vm.Estornado = recebidos.Where(p => p.Status == "Estornado").Sum(p => p.ValorRepasse);
        vm.TaxaPaga = recebidos.Where(p => p.Status == "Confirmado").Sum(p => p.Comissao);
        vm.QtdRecebimentos = recebidos.Count(p => p.Status == "Confirmado");
        vm.Pendentes = recebidos.Where(p => p.Status == "Pendente").OrderBy(p => p.ExpiraEm ?? p.CriadoEm).ToList();

        // "De onde veio o dinheiro": cada torneio vira uma linha (o organizador quer ver
        // torneio a torneio); aulas e quadras entram agregadas por tipo.
        var idsTorneio = recebidos.Where(p => p.TorneioId != null).Select(p => p.TorneioId!.Value).Distinct().ToList();
        var nomesTorneio = idsTorneio.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Torneios.Where(t => idsTorneio.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nome);

        vm.PorOrigem = recebidos
            .Where(p => p.Status is "Confirmado" or "Pendente")
            .GroupBy(p => p.TorneioId != null
                ? ("Torneio", nomesTorneio.GetValueOrDefault(p.TorneioId!.Value, "Torneio"))
                : p.Tipo == "Aula" ? ("Aula", "Aulas") : ("Quadra", "Aluguel de quadra"))
            .Select(g => new ViewModels.OrigemFinanceiraVM
            {
                Tipo = g.Key.Item1,
                Nome = g.Key.Item2,
                Icone = g.Key.Item1 switch { "Torneio" => "bi-trophy-fill", "Aula" => "bi-mortarboard-fill", _ => "bi-calendar2-check-fill" },
                Recebido = g.Where(p => p.Status == "Confirmado").Sum(p => p.ValorRepasse),
                Pendente = g.Where(p => p.Status == "Pendente").Sum(p => p.ValorRepasse),
                Qtd = g.Count(p => p.Status == "Confirmado"),
            })
            .OrderByDescending(o => o.Recebido).ThenByDescending(o => o.Pendente)
            .ToList();

        var compras = await _context.Pagamentos
            .Where(p => p.JogadorId == meuId)
            .OrderByDescending(p => p.CriadoEm)
            .Take(200)
            .ToListAsync();
        vm.MinhasCompras = de == null ? compras : compras.Where(p => DataEfetiva(p) >= de.Value).ToList();

        // O dono do app enxerga o total de comissão de todo mundo; os demais, só o próprio.
        var eu = await _context.Jogadores.FindAsync(meuId);
        vm.EhAdmin = eu?.IsAdminRaiz == true;
        if (vm.EhAdmin)
        {
            var todos = await _context.Pagamentos
                .Where(p => p.Status == "Confirmado" || p.Status == "Pendente")
                .Select(p => new { p.Status, p.Comissao, p.CriadoEm, p.ConfirmadoEm })
                .ToListAsync();
            if (de != null) todos = todos.Where(p => (p.ConfirmadoEm ?? p.CriadoEm) >= de.Value).ToList();

            vm.ComissaoPlataforma = todos.Where(p => p.Status == "Confirmado").Sum(p => p.Comissao);
            vm.ComissaoPlataformaPendente = todos.Where(p => p.Status == "Pendente").Sum(p => p.Comissao);
        }

        return View(vm);
    }

    // Estorna (ou cancela, se ainda não foi paga) uma cobrança dos meus torneios/aulas.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Estornar(int id)
    {
        var meuId = ObterJogadorIdLogado();
        var pagamento = await _context.Pagamentos.FindAsync(id);

        // Só o dono do torneio/aula mexe no dinheiro dele — sem esta checagem qualquer usuário
        // logado poderia estornar a cobrança de outra pessoa mandando o id na mão.
        if (pagamento == null || pagamento.RecebedorId != meuId) return Forbid();

        if (pagamento.Status is not ("Confirmado" or "Pendente"))
        {
            TempData["Erro"] = "Esta cobrança não pode ser estornada.";
            return RedirectToAction(nameof(Meus));
        }

        if (string.IsNullOrWhiteSpace(pagamento.AsaasPaymentId))
        {
            TempData["Erro"] = "Cobrança sem identificação no gateway.";
            return RedirectToAction(nameof(Meus));
        }

        bool jaFoiPaga = pagamento.Status == "Confirmado";
        if (!await _asaas.EstornarAsync(pagamento.AsaasPaymentId, jaFoiPaga))
        {
            TempData["Erro"] = "O gateway recusou o estorno. Tente novamente em instantes.";
            return RedirectToAction(nameof(Meus));
        }

        // O webhook (PAYMENT_REFUNDED/PAYMENT_DELETED) também atualiza o status, mas gravar aqui
        // dá retorno imediato na tela em vez de esperar a notificação chegar.
        pagamento.Status = jaFoiPaga ? "Estornado" : "Cancelado";
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = jaFoiPaga
            ? "Estorno solicitado — o valor volta pro jogador pelo Asaas."
            : "Cobrança cancelada.";
        return RedirectToAction(nameof(Meus));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configurar(bool receberPagamentoOnline, string? asaasWalletId, string? modoComissao)
    {
        var jogador = await _context.Jogadores.FindAsync(ObterJogadorIdLogado());
        if (jogador == null) return NotFound();

        asaasWalletId = asaasWalletId?.Trim();

        // Ligar o recebimento sem wallet faria a cobrança inteira cair no Padelizou — melhor
        // barrar aqui do que o organizador descobrir depois que o repasse não saiu.
        if (receberPagamentoOnline && string.IsNullOrWhiteSpace(asaasWalletId))
        {
            TempData["Erro"] = "Informe o Wallet ID da sua conta Asaas para ativar o recebimento.";
            return RedirectToAction(nameof(Configurar));
        }

        jogador.ReceberPagamentoOnline = receberPagamentoOnline;
        jogador.AsaasWalletId = string.IsNullOrWhiteSpace(asaasWalletId) ? null : asaasWalletId;
        jogador.ModoComissao = modoComissao is "Somada" or "Descontada" ? modoComissao : null;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = receberPagamentoOnline
            ? "Pronto! As inscrições dos seus torneios e aulas já podem ser pagas pelo Padelizou."
            : "Recebimento pelo Padelizou desativado — as cobranças seguem por fora.";
        return RedirectToAction(nameof(Configurar));
    }

    // Único ponto público do controller: quem chama é o Asaas, sem cookie e sem antiforgery.
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        if (!TokenValido())
        {
            _logger.LogWarning("Webhook do Asaas recusado: token ausente ou inválido.");
            return Unauthorized();
        }

        using var corpo = await JsonDocument.ParseAsync(Request.Body);
        var raiz = corpo.RootElement;

        var evento = raiz.TryGetProperty("event", out var eventoJson) ? eventoJson.GetString() : null;

        // Eventos que não são de cobrança (assinatura, transferência...) não interessam aqui.
        if (!raiz.TryGetProperty("payment", out var pagamentoJson)) return Ok();

        var asaasId = pagamentoJson.TryGetProperty("id", out var idJson) ? idJson.GetString() : null;
        if (string.IsNullOrEmpty(asaasId)) return Ok();

        var pagamento = await _context.Pagamentos.FirstOrDefaultAsync(p => p.AsaasPaymentId == asaasId);
        if (pagamento == null)
        {
            // Responde 200 mesmo assim: se a cobrança não é nossa, reenviar não vai adiantar.
            _logger.LogWarning("Webhook {Evento} para cobrança desconhecida {AsaasId}.", evento, asaasId);
            return Ok();
        }

        switch (evento)
        {
            case "PAYMENT_CONFIRMED":
            case "PAYMENT_RECEIVED":
                // Precisa ser idempotente: o Asaas reenvia até receber 200 e dispara os dois
                // eventos para a mesma cobrança.
                if (pagamento.Status != "Confirmado")
                {
                    pagamento.Status = "Confirmado";
                    pagamento.ConfirmadoEm = DateTime.Now;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Pagamento {Id} confirmado por {Evento}.", pagamento.Id, evento);
                }

                // Só agora a inscrição passa a existir de fato. Também é idempotente, porque
                // CONFIRMED e RECEIVED chegam os dois para a mesma cobrança.
                await _inscricoes.EfetivarAsync(pagamento);
                break;

            case "PAYMENT_REFUNDED":
                pagamento.Status = "Estornado";
                await _context.SaveChangesAsync();
                break;

            case "PAYMENT_DELETED":
            case "PAYMENT_OVERDUE":
                pagamento.Status = "Cancelado";
                await _context.SaveChangesAsync();
                break;
        }

        return Ok();
    }

    private bool TokenValido()
    {
        // Sem token configurado o endpoint fica fechado de propósito — melhor recusar do que
        // aceitar chamada anônima como confirmação de pagamento.
        if (string.IsNullOrWhiteSpace(_settings.WebhookToken)) return false;

        return Request.Headers.TryGetValue("asaas-access-token", out var recebido)
            && recebido.ToString() == _settings.WebhookToken;
    }
}
