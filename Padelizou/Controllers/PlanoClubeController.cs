using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers;

// O plano do clube: Rede (grátis), Gestão e Fiscal. Regras em Services/PlanoDoClube; os
// preços em PlanoClubeSettings.
//
// É a tela pra onde vai quem tentou abrir o bar sem assinatura — e é por isso que ela não
// pode parecer um erro. Quem chega aqui é um cliente em potencial que acabou de mostrar
// interesse clicando no produto; a tela existe pra vender, não pra explicar um 403.
[Authorize]
public class PlanoClubeController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IPagamentoInscricaoService _pagamentos;
    private readonly PlanoClubeSettings _cfg;

    public PlanoClubeController(DbPadelContext context, IPagamentoInscricaoService pagamentos,
        IOptions<PlanoClubeSettings> cfg)
    {
        _context = context;
        _pagamentos = pagamentos;
        _cfg = cfg.Value;
    }

    private int MeuId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Quem pode mexer no plano é quem manda no clube — dono, administrador do clube ou admin
    // do Padelizou. Note que aqui NÃO se pergunta pelo plano: seria pedir assinatura pra
    // chegar na tela de assinar.
    private async Task<bool> MandaNoClubeAsync(int clubeId)
    {
        var eu = MeuId();
        if (eu == 0) return false;

        if (await _context.Jogadores.AnyAsync(j => j.Id == eu && (j.IsAdminGeral || j.IsAdminRaiz)))
            return true;

        return await _context.Clubes.AnyAsync(c => c.Id == clubeId && c.DonoId == eu)
            || await _context.ClubeAdministradores.AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == eu);
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        if (!await MandaNoClubeAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        // O relógio dos 15 dias começa quando alguém do clube VÊ o plano pela primeira vez —
        // não na criação do clube. Mesma razão do professor: senão o teste corre antes de o
        // dono saber que existe, e ele descobre o produto no dia em que já acabou.
        if (clube.TesteDoClubeInicio == null)
        {
            clube.TesteDoClubeInicio = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        ViewBag.Cfg = _cfg;
        ViewBag.Situacao = PlanoDoClube.SituacaoDe(clube, DateTime.Now, _cfg);
        ViewBag.FimDoTeste = PlanoDoClube.FimDoTeste(clube, _cfg);
        ViewBag.CobrancaAberta = await CobrancaAbertaAsync(clube.Id);

        return View(clube);
    }

    // A cobrança aberta é a DO CLUBE, não a de quem está olhando. Sem isso, o dono geraria uma
    // e o administrador geraria outra — duas faturas do mesmo mês pro mesmo bar.
    private async Task<Pagamento?> CobrancaAbertaAsync(int clubeId)
    {
        var marca = $"\"ClubeId\":{clubeId},";

        return await _context.Pagamentos
            .Where(p => p.Tipo == PixDireto.TipoAssinaturaClube
                && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao)
                && p.DadosInscricao != null && p.DadosInscricao.Contains(marca))
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();
    }

    // Escolher o plano NÃO cobra nada — só diz o que o clube quer. A cobrança é o passo
    // seguinte, e são dois botões porque escolher e pagar são decisões diferentes: o dono
    // costuma escolher no celular e pagar depois, no computador, com o financeiro do lado.
    [HttpPost]
    public async Task<IActionResult> Escolher(int clubeId, string plano)
    {
        if (!await MandaNoClubeAsync(clubeId)) return Forbid();
        if (!PlanoDoClube.PlanoValido(plano)) return BadRequest();

        var clube = await _context.Clubes.FindAsync(clubeId);
        if (clube == null) return NotFound();

        clube.PlanoDoClube = plano;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = $"{PlanoDoClube.RotuloDoPlano(plano)} escolhido. "
            + "Gere a cobrança quando quiser — o teste segue valendo até acabar.";
        return RedirectToAction(nameof(Index), new { id = clubeId });
    }

    [HttpPost]
    public async Task<IActionResult> Pagar(int clubeId, string? ciclo)
    {
        if (!await MandaNoClubeAsync(clubeId)) return Forbid();

        var clube = await _context.Clubes.FindAsync(clubeId);
        if (clube == null) return NotFound();

        if (!PlanoDoClube.PlanoValido(clube.PlanoDoClube))
        {
            TempData["Erro"] = "Escolha um plano primeiro.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        var eu = await _context.Jogadores.FindAsync(MeuId());
        if (eu == null) return Forbid();

        var cicloEscolhido = CicloDeAssinatura.Valido(ciclo);
        var valorPedido = PlanoDoClube.ValorDo(clube.PlanoDoClube, cicloEscolhido, _cfg);

        // Uma cobrança aberta por vez. A tela já esconde os botões quando existe uma, mas a
        // trava mora aqui: sem ela, a página aberta em outra aba (ou o botão de voltar) geraria
        // a segunda, e pagar as duas é o erro que o clube não desfaz sozinho.
        var aberta = await CobrancaAbertaAsync(clubeId);
        if (aberta != null)
        {
            if (aberta.Valor != valorPedido)
            {
                TempData["Erro"] = $"Este clube já tem uma cobrança de {aberta.Valor:C} em aberto. "
                    + "Pague ou espere ela vencer antes de trocar de plano ou de ciclo.";
            }

            return aberta.MetodoPagamento == PixDireto.Metodo
                ? RedirectToAction("Pix", "Pagamentos", new { id = aberta.Id })
                : Redirect(LinkDoPagamento.Para(aberta));
        }

        // Pix direto primeiro: cai na nossa conta sem taxa de gateway. Só quando a chave não
        // está configurada é que a cobrança vai pro caminho da fatura.
        var pix = await _pagamentos.IniciarPixDiretoAssinaturaClubeAsync(
            clube, eu, clube.PlanoDoClube!, cicloEscolhido);
        if (pix != null) return RedirectToAction("Pix", "Pagamentos", new { id = pix.Id });

        var url = await _pagamentos.IniciarCobrancaAssinaturaClubeAsync(
            clube, eu, clube.PlanoDoClube!, cicloEscolhido);

        if (url == null)
        {
            TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente de novo em instantes.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        return Redirect(url);
    }
}
