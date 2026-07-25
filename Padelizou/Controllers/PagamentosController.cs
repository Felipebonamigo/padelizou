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
    public async Task<IActionResult> Meus()
    {
        var meuId = ObterJogadorIdLogado();

        var recebidos = await _context.Pagamentos
            .Where(p => p.RecebedorId == meuId)
            .Include(p => p.Jogador)
            .OrderByDescending(p => p.CriadoEm)
            .Take(200)
            .ToListAsync();

        ViewBag.Pagos = await _context.Pagamentos
            .Where(p => p.JogadorId == meuId)
            .OrderByDescending(p => p.CriadoEm)
            .Take(200)
            .ToListAsync();

        // O dono do app enxerga o total de comissão de todo mundo; os demais, só o próprio.
        var eu = await _context.Jogadores.FindAsync(meuId);
        ViewBag.EhAdmin = eu?.IsAdminRaiz == true;
        if (ViewBag.EhAdmin)
        {
            ViewBag.ComissaoTotal = await _context.Pagamentos
                .Where(p => p.Status == "Confirmado")
                .SumAsync(p => (decimal?)p.Comissao) ?? 0m;
            ViewBag.ComissaoPendente = await _context.Pagamentos
                .Where(p => p.Status == "Pendente")
                .SumAsync(p => (decimal?)p.Comissao) ?? 0m;
        }

        return View(recebidos);
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
