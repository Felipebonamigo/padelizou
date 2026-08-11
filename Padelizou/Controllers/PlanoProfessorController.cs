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
    private readonly AsaasSettings _asaas;

    public PlanoProfessorController(DbPadelContext context, IPagamentoInscricaoService pagamentos,
        IOptions<PlanoProfessorSettings> cfg, IOptions<AsaasSettings> asaas)
    {
        _context = context;
        _pagamentos = pagamentos;
        _cfg = cfg.Value;
        _asaas = asaas.Value;
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
        // A conta do card "Na prática" — quem paga a taxa depende do ModoComissao dele, e o
        // padrão do gateway vale pra quem nunca escolheu (ver ExemploDoPlanoNaTela).
        ViewBag.Exemplo = ExemploDoPlanoNaTela.Montar(eu.ModoComissao, _asaas.ModoComissaoPadrao, _cfg);

        ViewBag.CobrancaPendente = await FaturaAbertaAsync(eu.Id);

        // A cobrança Pix aberta, se houver — o botão vira "ver o Pix" em vez de gerar outra.
        ViewBag.PixPendente = await PixAbertoAsync(eu.Id);

        return View(eu);
    }

    // As duas cobranças de assinatura que podem estar abertas. Ficam aqui, e não repetidas na
    // Index e no PagarMensalidade, porque as duas telas TÊM que enxergar a mesma cobrança:
    // uma achar aberto o que a outra não acha é como nasceriam dois códigos de pagamento.
    private Task<Pagamento?> FaturaAbertaAsync(int professorId) =>
        _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == "AssinaturaProfessor" && p.JogadorId == professorId
            && p.Status == "Pendente" && p.InvoiceUrl != null);

    private Task<Pagamento?> PixAbertoAsync(int professorId) =>
        _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == PixDireto.TipoAssinatura && p.JogadorId == professorId
            && p.MetodoPagamento == PixDireto.Metodo
            && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao));

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

    // Gera a cobrança da assinatura no ciclo escolhido — mensal ou anual (12 meses).
    // ⚠️ O nome da rota ficou de quando só existia mensalidade. Vale pros dois ciclos; renomear
    // só faria a tela em cache de quem está com a página aberta postar num 404.
    [HttpPost]
    public async Task<IActionResult> PagarMensalidade(string? ciclo)
    {
        var eu = await ProfessorLogadoAsync();
        if (eu == null) return Forbid();

        if (eu.PlanoProfessor != PlanoDoProfessor.Assinante)
        {
            TempData["Erro"] = "Escolha o plano Assinante primeiro.";
            return RedirectToAction("Index");
        }

        var cicloEscolhido = PlanoDoProfessor.CicloValido(ciclo);
        var valorPedido = PlanoDoProfessor.ValorDo(cicloEscolhido, _cfg);

        // Uma cobrança de assinatura aberta por vez. A tela já esconde os botões quando existe
        // uma, mas a trava mora aqui: sem ela, a página aberta em outra aba (ou o botão de
        // voltar) geraria a segunda, e pagar as duas é o erro que o professor não desfaz
        // sozinho. Quando a aberta é do MESMO valor, isto não é erro nenhum — é o segundo
        // clique de sempre, e ele volta pra cobrança que já existe.
        var pixAberto = await PixAbertoAsync(eu.Id);
        var faturaAberta = pixAberto == null ? await FaturaAbertaAsync(eu.Id) : null;
        var aberta = pixAberto ?? faturaAberta;

        if (aberta != null)
        {
            if (aberta.Valor != valorPedido)
            {
                TempData["Erro"] = $"Você já tem uma cobrança de {aberta.Valor:C} em aberto. "
                    + "Pague ou espere ela vencer antes de trocar de ciclo.";
            }

            return pixAberto != null
                ? RedirectToAction("Pix", "Pagamentos", new { id = pixAberto.Id })
                : Redirect(LinkDoPagamento.Para(faturaAberta!));
        }

        // Pix direto primeiro: cai na nossa conta sem taxa de gateway. Só quando a chave não
        // está configurada é que a cobrança vai pro caminho antigo, com fatura do gateway.
        var pix = await _pagamentos.IniciarPixDiretoAssinaturaAsync(eu, cicloEscolhido);
        if (pix != null) return RedirectToAction("Pix", "Pagamentos", new { id = pix.Id });

        var url = await _pagamentos.IniciarCobrancaAssinaturaAsync(eu, cicloEscolhido);
        if (url == null)
        {
            TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente de novo em instantes.";
            return RedirectToAction("Index");
        }

        return Redirect(url);
    }
}
