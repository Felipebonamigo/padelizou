using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Controllers;

// O relatório do bar: o que vendeu, como foi pago e o que dá mais dinheiro.
//
// Existe porque o caixa do dia responde "bateu?" e nada mais. A pergunta que decide compra e
// preço é outra — "o que sai mais?", "quanto entrou este mês?", "vale a pena continuar
// vendendo isso?" — e ela só aparece quando se olha um período inteiro.
public partial class BarController
{
    [HttpGet]
    public async Task<IActionResult> Relatorio(int id, DateTime? de, DateTime? ate)
    {
        if (await BloqueioAsync(id) is { } bloqueio) return bloqueio;

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        // Padrão: o mês corrente. É o período em que o dono pensa — luz, fornecedor e aluguel
        // são todos mensais.
        var hoje = DateTime.Today;
        var inicio = (de ?? new DateTime(hoje.Year, hoje.Month, 1)).Date;
        var fim = (ate ?? hoje).Date;

        // Datas trocadas é engano de digitação, não pedido: inverter em silêncio devolve o que
        // a pessoa quis ver, em vez de uma tela vazia sem explicação.
        if (fim < inicio) (inicio, fim) = (fim, inicio);

        var comandas = await _context.Comandas
            .Include(c => c.Itens)
            .Where(c => c.ClubeId == id
                        && c.Status == Models.Comanda.Fechada
                        && c.DiaReferencia >= inicio && c.DiaReferencia <= fim)
            .ToListAsync();

        var vm = new RelatorioBarVM
        {
            De = inicio,
            Ate = fim,
            Comandas = comandas.Count,
            Vendido = comandas.Sum(c => c.Total),
            Descontos = comandas.Sum(c => c.Desconto),
        };

        // Por forma de pagamento: diz quanto passou pela gaveta e quanto foi maquininha.
        vm.PorForma = comandas
            .GroupBy(c => c.FormaPagamento ?? "—")
            .Select(g => (Forma: g.Key, Valor: g.Sum(c => c.Total), Quantidade: g.Count()))
            .OrderByDescending(x => x.Valor)
            .ToList();

        // Os campeões de venda. Agrupa pela DESCRIÇÃO gravada no item, não pelo produto: item
        // avulso ("2 fichas de estacionamento") não tem produto, e ignorá-lo esconderia venda
        // de verdade do relatório.
        vm.MaisVendidos = comandas
            .SelectMany(c => c.Itens)
            .Where(i => i.Vale)
            .GroupBy(i => i.Descricao)
            .Select(g => (Produto: g.Key, Unidades: g.Sum(i => i.Quantidade), Valor: g.Sum(i => i.Total)))
            .OrderByDescending(x => x.Valor)
            .Take(15)
            .ToList();

        // Movimento por dia, pra enxergar em que dia da semana o bar vende.
        vm.PorDia = comandas
            .GroupBy(c => c.DiaReferencia)
            .Select(g => (Dia: g.Key, Valor: g.Sum(c => c.Total), Comandas: g.Count()))
            .OrderBy(x => x.Dia)
            .ToList();

        // O que foi cancelado no período. Fica no relatório de propósito: é o número que o
        // dono precisa ver crescer pra desconfiar de alguma coisa.
        var itensCancelados = comandas.SelectMany(c => c.Itens).Where(i => !i.Vale).ToList();
        vm.ItensCancelados = itensCancelados.Count;
        vm.ValorCancelado = itensCancelados.Sum(i => i.Total);

        // Perdas de estoque no mesmo período, pela mesma razão.
        var produtoIds = await _context.ProdutosBar
            .Where(p => p.ClubeId == id)
            .Select(p => p.Id)
            .ToListAsync();

        vm.UnidadesPerdidas = await _context.MovimentosEstoque
            .Where(m => produtoIds.Contains(m.ProdutoBarId)
                        && m.Tipo == MovimentoEstoque.Perda
                        && m.CriadoEm >= inicio && m.CriadoEm < fim.AddDays(1))
            .SumAsync(m => -m.Quantidade);

        ViewBag.Clube = clube;
        ViewBag.EmConstrucao = _modulo.EmConstrucao;

        return View(vm);
    }
}
