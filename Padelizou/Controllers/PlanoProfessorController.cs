using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers;

// O plano do professor: 15 dias de teste com condições de assinante e, no fim, a escolha —
// Assinante (mensalidade + taxa menor por aula) ou Avulso (sem mensalidade, taxa cheia).
// Regras em Services/PlanoDoProfessor; os números em PlanoProfessorSettings.
[Authorize]
public class PlanoProfessorController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IPagamentoInscricaoService _pagamentos;
    private readonly PlanoProfessorSettings _cfg;

    public PlanoProfessorController(DbPadelContext context, IPagamentoInscricaoService pagamentos,
        IOptions<PlanoProfessorSettings> cfg)
    {
        _context = context;
        _pagamentos = pagamentos;
        _cfg = cfg.Value;
    }

    private int MeuId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<Jogador?> ProfessorLogadoAsync()
    {
        var eu = await _context.Jogadores.FindAsync(MeuId());
        return eu is { IsProfessor: true } ? eu : null;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var eu = await ProfessorLogadoAsync();
        if (eu == null) return Forbid();

        // O relógio dos 15 dias começa quando o professor VÊ o plano pela primeira vez —
        // não no cadastro, senão o teste corre antes de ele saber que existe.
        if (eu.TesteProfessorInicio == null)
        {
            eu.TesteProfessorInicio = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        ViewBag.Cfg = _cfg;
        ViewBag.Situacao = PlanoDoProfessor.SituacaoDe(eu, DateTime.Now, _cfg);
        ViewBag.FimDoTeste = PlanoDoProfessor.FimDoTeste(eu, _cfg);
        ViewBag.CobrancaPendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == "AssinaturaProfessor" && p.JogadorId == eu.Id
            && p.Status == "Pendente" && p.InvoiceUrl != null);

        // A cobrança Pix aberta, se houver — o botão vira "ver o Pix" em vez de gerar outra.
        ViewBag.PixPendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == PixDireto.TipoAssinatura && p.JogadorId == eu.Id
            && p.MetodoPagamento == PixDireto.Metodo
            && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao));

        return View(eu);
    }

    [HttpPost]
    public async Task<IActionResult> Escolher(string plano)
    {
        if (plano != PlanoDoProfessor.Assinante && plano != PlanoDoProfessor.Avulso) return BadRequest();

        var eu = await ProfessorLogadoAsync();
        if (eu == null) return Forbid();

        eu.PlanoProfessor = plano;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = plano == PlanoDoProfessor.Assinante
            ? "Plano Assinante escolhido! Gere a mensalidade quando quiser — a taxa menor vale enquanto ela estiver em dia."
            : "Plano Avulso escolhido: sem mensalidade, taxa cheia por aula. Dá pra mudar de ideia quando quiser.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> PagarMensalidade()
    {
        var eu = await ProfessorLogadoAsync();
        if (eu == null) return Forbid();

        if (eu.PlanoProfessor != PlanoDoProfessor.Assinante)
        {
            TempData["Erro"] = "Escolha o plano Assinante primeiro.";
            return RedirectToAction("Index");
        }

        // Pix direto primeiro: cai na nossa conta sem taxa de gateway. Só quando a chave não
        // está configurada é que a mensalidade vai pro caminho antigo, com fatura do gateway.
        var pix = await _pagamentos.IniciarPixDiretoAssinaturaAsync(eu);
        if (pix != null) return RedirectToAction("Pix", "Pagamentos", new { id = pix.Id });

        var url = await _pagamentos.IniciarCobrancaAssinaturaAsync(eu);
        if (url == null)
        {
            TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente de novo em instantes.";
            return RedirectToAction("Index");
        }

        return Redirect(url);
    }
}
