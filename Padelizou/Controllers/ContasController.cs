using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers;

// Contas a pagar e a receber do clube.
//
// EM CONSTRUÇÃO junto com o bar: mesma trava, mesma chave `Bar:Habilitado` (ver
// Services/ModuloDoBar). Enquanto estiver desligada, só admin do Padelizou entra.
//
// O que entra aqui é o que o sistema NÃO tem como saber: conta de luz, boleto da
// distribuidora, cachê do DJ, cliente que levou fiado. O que já passou pelo Padelizou —
// comanda fechada e reserva paga — aparece na tela como "o que o sistema viu", em coluna
// separada, e nunca somado com isto. Somar contaria a mesma receita duas vezes.
[Authorize]
public class ContasController : Controller
{
    private readonly DbPadelContext _context;
    private readonly ModuloDoBar _modulo;

    public ContasController(DbPadelContext context, ModuloDoBar modulo)
    {
        _context = context;
        _modulo = modulo;
    }

    private int? UsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private Task<bool> PodeUsarAsync(int clubeId) => _modulo.PodeUsarAsync(clubeId, UsuarioId());

    // `filtro`: "aberto" (padrão), "pagar", "receber", "quitado" ou "todos".
    [HttpGet]
    public async Task<IActionResult> Index(int id, string? filtro, int? mes, int? ano)
    {
        if (!await PodeUsarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var hoje = DateTime.Today;
        var referencia = new DateTime(ano ?? hoje.Year, mes ?? hoje.Month, 1);
        var fimDoMes = referencia.AddMonths(1);

        var todos = await _context.LancamentosFinanceiros
            .Where(l => l.ClubeId == id)
            .ToListAsync();

        // O RESUMO olha tudo que está em aberto, não só o mês: conta vencida de três meses
        // atrás continua sendo dívida, e sumir com ela do painel esconde exatamente o que o
        // dono precisa ver.
        var resumo = FinanceiroDoClube.Resumir(todos, hoje);

        var lista = (filtro ?? "aberto") switch
        {
            "pagar" => todos.Where(l => l.EhPagar && !l.EstaQuitado),
            "receber" => todos.Where(l => !l.EhPagar && !l.EstaQuitado),
            "quitado" => todos.Where(l => l.EstaQuitado && l.Vencimento >= referencia && l.Vencimento < fimDoMes),
            "todos" => todos.Where(l => l.Vencimento >= referencia && l.Vencimento < fimDoMes),
            _ => todos.Where(l => !l.EstaQuitado),
        };

        // Vencido primeiro, e dentro disso o mais antigo — é a ordem em que se paga.
        ViewBag.Lancamentos = lista
            .OrderBy(l => l.EstaQuitado ? 1 : 0)
            .ThenBy(l => l.Vencimento)
            .ToList();

        // O que o sistema JÁ VIU no mês: bar e quadra. Fica ao lado, nunca somado com os
        // lançamentos manuais — o dinheiro da comanda fechada já entrou, e transformá-lo em
        // "a receber" contaria a mesma receita duas vezes.
        var comandasDoMes = await _context.Comandas
            .Include(c => c.Itens)
            .Where(c => c.ClubeId == id
                        && c.Status == Comanda.Fechada
                        && c.DiaReferencia >= referencia && c.DiaReferencia < fimDoMes)
            .ToListAsync();

        ViewBag.ReceitaDoBar = comandasDoMes.Sum(c => c.Total);
        ViewBag.ComandasNoMes = comandasDoMes.Count;

        ViewBag.ReceitaDeQuadra = await _context.MarcacoesJogo
            .Where(m => m.ClubeId == id && !m.EhBloqueio && m.CanceladaEm == null
                        && m.PagoEm != null
                        && m.PagoEm >= referencia && m.PagoEm < fimDoMes)
            .CountAsync();

        ViewBag.Clube = clube;
        ViewBag.Resumo = resumo;
        ViewBag.Filtro = filtro ?? "aberto";
        ViewBag.Referencia = referencia;
        ViewBag.Hoje = hoje;
        ViewBag.EmConstrucao = _modulo.EmConstrucao;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Salvar(int clubeId, string? tipo, string? descricao, decimal valor,
        DateTime vencimento, string? contraparte, string? categoria, string? observacao, int repetirMeses = 1)
    {
        if (!await PodeUsarAsync(clubeId)) return Forbid();

        if (FinanceiroDoClube.ProblemaCom(descricao, valor, tipo, repetirMeses) is { } problema)
        {
            TempData["ErroConta"] = problema;
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        // Conta que se repete nasce com TODAS as parcelas gravadas, não como uma regra
        // "repete todo dia 10". Conta que se repete muda — o aluguel reajusta, o contrato
        // acaba, um mês é pago adiantado — e com as linhas gravadas mexer numa parcela é
        // mexer numa linha. Com regra calculada, seria preciso inventar exceções.
        var datas = FinanceiroDoClube.Vencimentos(vencimento, repetirMeses);
        var grupo = repetirMeses > 1 ? Guid.NewGuid() : (Guid?)null;

        foreach (var data in datas)
        {
            _context.LancamentosFinanceiros.Add(new LancamentoFinanceiro
            {
                ClubeId = clubeId,
                Tipo = tipo!,
                Descricao = descricao!.Trim(),
                Valor = valor,
                Vencimento = data,
                Contraparte = string.IsNullOrWhiteSpace(contraparte) ? null : contraparte.Trim(),
                Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim(),
                Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
                GrupoRecorrencia = grupo,
                CriadoPorId = UsuarioId(),
            });
        }

        await _context.SaveChangesAsync();

        TempData["SucessoConta"] = datas.Count == 1
            ? "Conta lançada."
            : $"{datas.Count} parcelas lançadas, de {datas.First():MM/yyyy} a {datas.Last():MM/yyyy}.";

        return RedirectToAction(nameof(Index), new { id = clubeId });
    }

    // Quitar é um interruptor: quem marca errado desmarca no mesmo botão, sem precisar
    // apagar e relançar a conta.
    [HttpPost]
    public async Task<IActionResult> AlternarQuitado(int id)
    {
        var lancamento = await _context.LancamentosFinanceiros.FindAsync(id);
        if (lancamento == null) return NotFound();
        if (!await PodeUsarAsync(lancamento.ClubeId)) return Forbid();

        if (lancamento.EstaQuitado)
        {
            lancamento.QuitadoEm = null;
            lancamento.QuitadoPorId = null;
        }
        else
        {
            lancamento.QuitadoEm = DateTime.Now;
            lancamento.QuitadoPorId = UsuarioId();
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = lancamento.ClubeId });
    }

    // Aqui apagar É a operação certa, ao contrário do produto do cardápio: conta lançada
    // errada não tem histórico pra preservar — ela nunca existiu no mundo. `apagarSerie`
    // limpa a recorrência inteira, senão seriam 12 cliques pra desfazer um engano.
    [HttpPost]
    public async Task<IActionResult> Apagar(int id, bool apagarSerie = false)
    {
        var lancamento = await _context.LancamentosFinanceiros.FindAsync(id);
        if (lancamento == null) return NotFound();
        if (!await PodeUsarAsync(lancamento.ClubeId)) return Forbid();

        int clubeId = lancamento.ClubeId;

        if (apagarSerie && lancamento.GrupoRecorrencia is Guid grupo)
        {
            // Só as que ainda não foram quitadas: apagar parcela paga apagaria o registro de
            // um dinheiro que de fato saiu.
            var serie = await _context.LancamentosFinanceiros
                .Where(l => l.ClubeId == clubeId && l.GrupoRecorrencia == grupo && l.QuitadoEm == null)
                .ToListAsync();

            _context.LancamentosFinanceiros.RemoveRange(serie);
            TempData["SucessoConta"] = $"{serie.Count} parcela(s) em aberto apagada(s).";
        }
        else
        {
            _context.LancamentosFinanceiros.Remove(lancamento);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = clubeId });
    }
}
