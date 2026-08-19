using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers;

// Estoque do bar: entrada de compra, baixa automática pela comanda, perda e contagem.
//
// A premissa, escrita aqui pra não se perder: estoque de bar NÃO é exato. Garrafa quebra,
// funcionário consome, cerveja sai sem passar pela comanda no sábado lotado. O que mantém o
// saldo colado na prateleira não é precisão — é a CONTAGEM semanal. Por isso perda e contagem
// são botões de primeira classe, e não correções escondidas num canto.
//
// Fica de fora, com convicção: ficha técnica de drink (a caipirinha baixando limão, açúcar e
// 80ml de cachaça), validade e lote, múltiplos depósitos. Quem precisa disso precisa de um ERP
// de restaurante, não do Padelizou.
public partial class BarController
{
    [HttpGet]
    public async Task<IActionResult> Estoque(int id)
    {
        if (await BloqueioAsync(id) is { } bloqueio) return bloqueio;

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var produtos = await _context.ProdutosBar
            .Where(p => p.ClubeId == id && p.ControlaEstoque)
            .OrderBy(p => p.Ordem).ThenBy(p => p.Nome)
            .ToListAsync();

        var ids = produtos.Select(p => p.Id).ToList();

        var movimentos = await _context.MovimentosEstoque
            .Where(m => ids.Contains(m.ProdutoBarId))
            .ToListAsync();

        // Uma linha por produto, já com saldo, último custo e margem — a tela responde
        // "quanto tem, quanto custa e quanto sobra" sem o dono abrir três lugares.
        var linhas = produtos.Select(p =>
        {
            var meus = movimentos.Where(m => m.ProdutoBarId == p.Id).ToList();
            var saldo = EstoqueDoBar.Saldo(meus);

            var ultimoCusto = meus
                .Where(m => m.Tipo == MovimentoEstoque.Entrada && m.CustoUnitario != null)
                .OrderByDescending(m => m.CriadoEm)
                .Select(m => m.CustoUnitario)
                .FirstOrDefault();

            return new EstoqueLinhaVM
            {
                Produto = p,
                Saldo = saldo,
                UltimoCusto = ultimoCusto,
                Margem = EstoqueDoBar.Margem(p.Preco, ultimoCusto),
                PrecisaRepor = EstoqueDoBar.PrecisaRepor(saldo, p.EstoqueMinimo),
            };
        }).ToList();

        ViewBag.Clube = clube;
        ViewBag.Linhas = linhas;
        ViewBag.EmConstrucao = _modulo.EmConstrucao;

        // O histórico recente responde "por que tem 8?" — que é a pergunta que aparece quando
        // a prateleira discorda do sistema.
        ViewBag.Movimentos = movimentos
            .OrderByDescending(m => m.CriadoEm)
            .Take(40)
            .Select(m => (Movimento: m, Produto: produtos.First(p => p.Id == m.ProdutoBarId)))
            .ToList();

        // Produtos que ainda não controlam estoque, pra ligar direto daqui.
        ViewBag.SemControle = await _context.ProdutosBar
            .Where(p => p.ClubeId == id && !p.ControlaEstoque && p.Ativo)
            .OrderBy(p => p.Nome)
            .ToListAsync();

        return View();
    }

    // Compra do fornecedor. É o único movimento que carrega custo — e é dele que sai a
    // resposta pra "quanto eu gasto de cerveja por mês" e a margem de cada item.
    [HttpPost]
    public async Task<IActionResult> RegistrarEntrada(int produtoBarId, int quantidade,
        bool porEmbalagem = false, decimal? custoUnitario = null, string? fornecedor = null,
        bool lancarEmContas = false, DateTime? vencimento = null)
    {
        var produto = await _context.ProdutosBar.FindAsync(produtoBarId);
        if (produto == null) return NotFound();
        if (!await PodeUsarAsync(produto.ClubeId)) return Forbid();

        if (EstoqueDoBar.ProblemaComEntrada(quantidade, custoUnitario) is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
        }

        // Compra-se caixa com 24, vende-se lata. Sem a conversão, o dono digita 24 e fica com
        // 24 caixas no sistema — o erro mais comum de todo controle de estoque de bar.
        int unidades = EstoqueDoBar.UnidadesDaCompra(quantidade, produto.UnidadesPorEmbalagem, porEmbalagem);

        _context.MovimentosEstoque.Add(new MovimentoEstoque
        {
            ProdutoBarId = produto.Id,
            Tipo = MovimentoEstoque.Entrada,
            Quantidade = unidades,
            CustoUnitario = custoUnitario,
            Motivo = string.IsNullOrWhiteSpace(fornecedor) ? null : fornecedor.Trim(),
            CriadoPorId = UsuarioId(),
        });

        // A ponte com as contas a pagar: a mesma compra que entra no estoque vira o boleto
        // que vence. Digitar duas vezes é o que faz o dono desistir de uma das duas telas.
        if (lancarEmContas && custoUnitario is > 0)
        {
            _context.LancamentosFinanceiros.Add(new LancamentoFinanceiro
            {
                ClubeId = produto.ClubeId,
                Tipo = LancamentoFinanceiro.Pagar,
                Descricao = $"{produto.Nome} — {unidades} un.",
                Valor = custoUnitario.Value * unidades,
                Vencimento = (vencimento ?? DateTime.Today).Date,
                Contraparte = string.IsNullOrWhiteSpace(fornecedor) ? null : fornecedor.Trim(),
                Categoria = produto.Categoria,
                CriadoPorId = UsuarioId(),
            });
        }

        await _context.SaveChangesAsync();

        TempData["SucessoBar"] = $"Entrada de {unidades} un. de {produto.Nome} registrada.";
        return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
    }

    // Quebrou, venceu, funcionário consumiu, saiu de cortesia sem passar pela comanda. Sem um
    // jeito FÁCIL de registrar isso, o saldo descola da prateleira em duas semanas e o dono
    // para de confiar no número.
    [HttpPost]
    public async Task<IActionResult> RegistrarPerda(int produtoBarId, int quantidade, string? motivo)
    {
        var produto = await _context.ProdutosBar.FindAsync(produtoBarId);
        if (produto == null) return NotFound();
        if (!await PodeUsarAsync(produto.ClubeId)) return Forbid();

        if (EstoqueDoBar.ProblemaComPerda(quantidade) is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
        }

        // Perda sem motivo é um buraco sem explicação — e é justamente o registro que o dono
        // vai ler quando quiser saber por que sumiram seis cervejas na sexta.
        if (string.IsNullOrWhiteSpace(motivo))
        {
            TempData["ErroBar"] = "Escreva o motivo da perda (quebrou, venceu, cortesia...).";
            return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
        }

        _context.MovimentosEstoque.Add(new MovimentoEstoque
        {
            ProdutoBarId = produto.Id,
            Tipo = MovimentoEstoque.Perda,
            Quantidade = -quantidade,
            Motivo = motivo.Trim(),
            CriadoPorId = UsuarioId(),
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
    }

    // A contagem: conte a prateleira e digite o que TEM. O sistema acerta a diferença.
    // É isto que mantém o estoque honesto para sempre — dez minutos por semana valem mais que
    // qualquer precisão prometida por um sistema.
    [HttpPost]
    public async Task<IActionResult> RegistrarContagem(int produtoBarId, int contado)
    {
        var produto = await _context.ProdutosBar.FindAsync(produtoBarId);
        if (produto == null) return NotFound();
        if (!await PodeUsarAsync(produto.ClubeId)) return Forbid();

        if (EstoqueDoBar.ProblemaComContagem(contado) is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
        }

        var movimentos = await _context.MovimentosEstoque
            .Where(m => m.ProdutoBarId == produto.Id)
            .ToListAsync();

        int saldo = EstoqueDoBar.Saldo(movimentos);
        int ajuste = EstoqueDoBar.AjusteDaContagem(saldo, contado);

        if (ajuste == 0)
        {
            // Bateu: não grava nada. Uma linha de "ajuste de 0" só encheria o histórico de
            // registro que não conta nada.
            TempData["SucessoBar"] = $"{produto.Nome}: contagem bateu com o sistema ({contado} un.).";
            return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
        }

        _context.MovimentosEstoque.Add(new MovimentoEstoque
        {
            ProdutoBarId = produto.Id,
            Tipo = MovimentoEstoque.Contagem,
            Quantidade = ajuste,
            Motivo = $"Sistema dizia {saldo}, contado {contado}",
            CriadoPorId = UsuarioId(),
        });

        await _context.SaveChangesAsync();

        TempData["SucessoBar"] = ajuste > 0
            ? $"{produto.Nome}: contagem achou {ajuste} un. a mais que o sistema."
            : $"{produto.Nome}: contagem achou {Math.Abs(ajuste)} un. a menos que o sistema.";

        return RedirectToAction(nameof(Estoque), new { id = produto.ClubeId });
    }
}
