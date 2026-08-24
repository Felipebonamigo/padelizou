using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
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
        // são todos mensais. A régua mora em PeriodoDoRelatorio, compartilhada com o CSV.
        var (inicio, fim) = PeriodoDoRelatorio(de, ate);

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

    // ===================== O PACOTE DO CONTADOR =====================

    // As vendas do bar em CSV, pro contador do clube lançar.
    //
    // Nasceu de uma constatação simples: o relatório da tela responde as perguntas do DONO
    // ("o que sai mais?", "quanto entrou?"), e o contador precisa de outra coisa — a lista
    // crua, linha por linha, pra conferir contra o extrato e lançar no sistema dele. Até
    // aqui a única saída era ele olhar a tela e digitar.
    //
    // ⚠️ Isto NÃO é documento fiscal e a tela diz isso: é o registro interno do que foi
    // vendido. A nota é outra coisa, e ela depende do plano Fiscal (ver FISCAL.md).
    //
    // Dois recortes, porque são duas perguntas diferentes:
    //   "vendas" (padrão) → uma linha por comanda. É o que bate com o caixa e com o extrato
    //                       da maquininha: data, forma de pagamento, desconto, total.
    //   "itens"           → uma linha por item vendido, com NCM. É o detalhe que o contador
    //                       pede quando precisa separar por produto — e é o mesmo recorte que
    //                       a nota vai usar um dia.
    [HttpGet]
    public async Task<IActionResult> ExportarCsv(int id, DateTime? de, DateTime? ate, string? tipo)
    {
        if (await BloqueioAsync(id) is { } bloqueio) return bloqueio;

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var (inicio, fim) = PeriodoDoRelatorio(de, ate);

        var comandas = await _context.Comandas
            .Include(c => c.Itens)
            .Where(c => c.ClubeId == id
                        && c.Status == Models.Comanda.Fechada
                        && c.DiaReferencia >= inicio && c.DiaReferencia <= fim)
            .OrderBy(c => c.DiaReferencia).ThenBy(c => c.Numero)
            .ToListAsync();

        var porItem = tipo == "itens";
        var sb = new System.Text.StringBuilder();

        if (porItem)
        {
            // O NCM vem do produto de HOJE, e o nome do ITEM (congelado na venda). Não é
            // contradição: o nome tem que dizer o que foi vendido naquele dia, e o NCM é
            // classificação do produto, que não muda com a venda. Item avulso não tem
            // produto e sai sem NCM — é exatamente o que o contador precisa enxergar.
            var ncmPorProduto = await _context.ProdutosBar
                .Where(p => p.ClubeId == id && p.Ncm != null)
                .ToDictionaryAsync(p => p.Id, p => p.Ncm!);

            sb.AppendLine("Data;Comanda;Produto;NCM;Quantidade;Preco unitario;Total");

            foreach (var c in comandas)
            {
                foreach (var i in c.Itens.Where(i => i.Vale).OrderBy(i => i.LancadoEm))
                {
                    var ncm = i.ProdutoBarId is int pid ? ncmPorProduto.GetValueOrDefault(pid, "") : "";
                    sb.AppendLine(string.Join(";",
                        ArquivoCsv.Data(c.DiaReferencia),
                        c.Numero,
                        ArquivoCsv.Campo(i.Descricao),
                        ArquivoCsv.Campo(ncm),
                        i.Quantidade,
                        ArquivoCsv.Dinheiro(i.PrecoUnitario),
                        ArquivoCsv.Dinheiro(i.Total)));
                }
            }
        }
        else
        {
            sb.AppendLine("Data;Comanda;Cliente;CPF na nota;Forma de pagamento;Itens;Subtotal;Desconto;Total");

            foreach (var c in comandas)
            {
                sb.AppendLine(string.Join(";",
                    ArquivoCsv.Data(c.DiaReferencia),
                    c.Numero,
                    ArquivoCsv.Campo(c.NomeCliente),
                    // O CPF sai só quando o cliente pediu nota — é dado pessoal, e exportar
                    // o de quem não pediu seria espalhar documento que ninguém entregou.
                    ArquivoCsv.Campo(Documentos.CpfEhValido(c.CpfConsumidor) ? c.CpfConsumidor! : ""),
                    ArquivoCsv.Campo(c.FormaPagamento ?? "—"),
                    c.Itens.Count(i => i.Vale),
                    ArquivoCsv.Dinheiro(c.Subtotal),
                    ArquivoCsv.Dinheiro(c.Desconto),
                    ArquivoCsv.Dinheiro(c.Total)));
            }
        }

        var nome = $"bar-{(porItem ? "itens" : "vendas")}-{inicio:yyyyMMdd}-a-{fim:yyyyMMdd}.csv";
        return File(ArquivoCsv.Bytes(sb), "text/csv", nome);
    }

    // O período do relatório, com o mesmo padrão e a mesma correção de datas trocadas da
    // tela. Fica num lugar só porque tela e CSV TÊM que mostrar o mesmo recorte — dois
    // períodos diferentes pro mesmo mês é como o contador perde a confiança no arquivo.
    private static (DateTime Inicio, DateTime Fim) PeriodoDoRelatorio(DateTime? de, DateTime? ate)
    {
        var hoje = DateTime.Today;
        var inicio = (de ?? new DateTime(hoje.Year, hoje.Month, 1)).Date;
        var fim = (ate ?? hoje).Date;

        // Datas trocadas é engano de digitação, não pedido: inverter em silêncio devolve o
        // que a pessoa quis ver, em vez de uma tela vazia sem explicação.
        return fim < inicio ? (fim, inicio) : (inicio, fim);
    }
}
