using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers;

// O bar do clube: cardápio, comandas e o caixa do dia.
//
// EM CONSTRUÇÃO. Enquanto `Bar:Habilitado` estiver desligado, só admin do Padelizou entra —
// as tabelas existem em produção e ninguém vê nada (ver Services/BarSettings).
//
// O que este módulo NÃO faz, e é decisão, não pendência: **nota fiscal**. NFC-e é homologação
// por estado, impressora fiscal e um produto inteiro — existem empresas que só fazem isso. O
// Padelizou cuida do controle interno (o que foi vendido, pra quem, quanto entrou) e o clube
// segue emitindo nota pelo que já usa. Dinheiro e maquininha são REGISTRO aqui, não cobrança.
[Authorize]
public partial class BarController : Controller
{
    private readonly DbPadelContext _context;
    private readonly ModuloDoBar _modulo;
    private readonly ILogger<BarController> _logger;

    public BarController(DbPadelContext context, ModuloDoBar modulo, ILogger<BarController> logger)
    {
        _context = context;
        _modulo = modulo;
        _logger = logger;
    }

    private int? UsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // A regra mora em Services/ModuloDoBar — a mesma que vale pras contas do clube. Duas
    // cópias de uma permissão divergem no dia em que só uma é atualizada.
    private Task<bool> PodeUsarAsync(int clubeId) => _modulo.PodeUsarAsync(clubeId, UsuarioId());

    // O dia a que o movimento pertence. É o dia do caixa ABERTO, não o relógio: comanda
    // aberta 23h50 e paga 00h30 entra no movimento de ontem, que é como o bar conta. Sem
    // caixa aberto, vale hoje.
    private async Task<DateTime> DiaDeReferenciaAsync(int clubeId)
    {
        var caixaAberto = await _context.CaixasDoDia
            .Where(c => c.ClubeId == clubeId && c.FechadoEm == null)
            .OrderByDescending(c => c.Dia)
            .FirstOrDefaultAsync();

        return caixaAberto?.Dia ?? DateTime.Today;
    }

    // ===================== BALCÃO (a tela que fica aberta o dia todo) =====================

    [HttpGet]
    public async Task<IActionResult> Index(int id)
    {
        if (!await PodeUsarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var dia = await DiaDeReferenciaAsync(id);

        var comandas = await _context.Comandas
            .Include(c => c.Itens)
            .Where(c => c.ClubeId == id && c.DiaReferencia == dia)
            .OrderBy(c => c.Status == Models.Comanda.Aberta ? 0 : 1)
            .ThenByDescending(c => c.Numero)
            .ToListAsync();

        var caixa = await _context.CaixasDoDia
            .FirstOrDefaultAsync(c => c.ClubeId == id && c.Dia == dia);

        // Quem está jogando agora e ainda não tem comanda: é daqui que sai o "abrir comanda
        // num toque", sem digitar nome nenhum. A janela é generosa (o pessoal chega antes e
        // fica depois) porque errar pra mais só oferece um botão a mais na tela.
        var agora = DateTime.Now;
        var comMarcacao = comandas.Where(c => c.MarcacaoJogoId != null)
            .Select(c => c.MarcacaoJogoId!.Value).ToHashSet();

        var reservasDeHoje = await _context.MarcacoesJogo
            .Include(m => m.Jogador)
            .Include(m => m.QuadraClube)
            .Where(m => m.ClubeId == id
                        && !m.EhBloqueio
                        && m.CanceladaEm == null
                        && m.DataHora >= agora.AddHours(-3)
                        && m.DataHora <= agora.AddHours(3))
            .OrderBy(m => m.DataHora)
            .ToListAsync();

        ViewBag.Clube = clube;
        ViewBag.Dia = dia;
        ViewBag.Caixa = caixa;
        ViewBag.Comandas = comandas;
        ViewBag.ReservasSemComanda = reservasDeHoje.Where(m => !comMarcacao.Contains(m.Id)).ToList();
        ViewBag.EmConstrucao = _modulo.EmConstrucao;

        // O caixa fecha com o que entrou EM DINHEIRO — cartão e Pix não passam pela gaveta.
        ViewBag.VendasEmDinheiro = comandas
            .Where(c => c.Status == Models.Comanda.Fechada && c.FormaPagamento == BarDoClube.Dinheiro)
            .Sum(c => c.Total);
        ViewBag.VendasNoDia = comandas.Where(c => c.Status == Models.Comanda.Fechada).Sum(c => c.Total);
        ViewBag.EmAberto = comandas.Where(c => c.Status == Models.Comanda.Aberta).Sum(c => c.Total);

        return View();
    }

    // ===================== COMANDA =====================

    [HttpPost]
    public async Task<IActionResult> AbrirComanda(int clubeId, string? nomeCliente, int? marcacaoJogoId)
    {
        if (!await PodeUsarAsync(clubeId)) return Forbid();

        var dia = await DiaDeReferenciaAsync(clubeId);

        int? jogadorId = null;
        string? celular = null;

        // Veio de uma reserva: nome, celular e o vínculo saem do que o sistema já sabe. É a
        // diferença entre este bar e um PDV qualquer — aqui o cliente do dia já tem ficha.
        if (marcacaoJogoId is int reservaId)
        {
            var reserva = await _context.MarcacoesJogo
                .Include(m => m.Jogador)
                .FirstOrDefaultAsync(m => m.Id == reservaId && m.ClubeId == clubeId);

            if (reserva != null)
            {
                // Reserva de balcão não tem jogador cadastrado, só o nome escrito na hora.
                jogadorId = reserva.JogadorId;
                nomeCliente = string.IsNullOrWhiteSpace(reserva.NomeClienteBalcao)
                    ? reserva.Jogador?.ComoChamar
                    : reserva.NomeClienteBalcao;
                celular = reserva.CelularClienteBalcao ?? reserva.Jogador?.Celular;
            }
        }

        if (string.IsNullOrWhiteSpace(nomeCliente))
        {
            TempData["ErroBar"] = "Escreva o nome do cliente — é por ele que a comanda é achada no balcão.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        var comanda = new Comanda
        {
            ClubeId = clubeId,
            DiaReferencia = dia,
            NomeCliente = nomeCliente.Trim(),
            JogadorId = jogadorId,
            MarcacaoJogoId = marcacaoJogoId,
            Celular = celular,
            AbertaPorId = UsuarioId(),
        };

        // Dois tablets no balcão abrindo comanda no mesmo segundo pegam o mesmo número. O
        // índice único transforma isso em erro (em vez de duas "comanda 7" no mesmo turno) e
        // aqui a gente simplesmente tenta o próximo.
        for (int tentativa = 0; tentativa < 5; tentativa++)
        {
            var usados = await _context.Comandas
                .Where(c => c.ClubeId == clubeId && c.DiaReferencia == dia)
                .Select(c => c.Numero)
                .ToListAsync();

            comanda.Numero = BarDoClube.ProximoNumero(usados);

            try
            {
                _context.Comandas.Add(comanda);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Comanda), new { id = comanda.Id });
            }
            catch (DbUpdateException)
            {
                // Alguém chegou primeiro com este número: solta a entidade e recalcula.
                _context.Entry(comanda).State = EntityState.Detached;
            }
        }

        _logger.LogWarning("Não foi possível numerar comanda do clube {ClubeId} em {Dia}.", clubeId, dia);
        TempData["ErroBar"] = "Não deu pra abrir a comanda agora. Tente de novo.";
        return RedirectToAction(nameof(Index), new { id = clubeId });
    }

    [HttpGet]
    public async Task<IActionResult> Comanda(int id)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Itens)
            .Include(c => c.MarcacaoJogo).ThenInclude(m => m!.QuadraClube)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comanda == null) return NotFound();
        if (!await PodeUsarAsync(comanda.ClubeId)) return Forbid();

        ViewBag.Clube = await _context.Clubes.FindAsync(comanda.ClubeId);
        ViewBag.Produtos = await _context.ProdutosBar
            .Where(p => p.ClubeId == comanda.ClubeId && p.Ativo)
            .OrderBy(p => p.Ordem).ThenBy(p => p.Nome)
            .ToListAsync();
        ViewBag.Formas = BarDoClube.FormasDePagamento;

        return View(comanda);
    }

    [HttpPost]
    public async Task<IActionResult> LancarItem(int comandaId, int? produtoBarId,
        string? descricaoLivre, decimal? precoLivre, int quantidade = 1)
    {
        var comanda = await _context.Comandas.FindAsync(comandaId);
        if (comanda == null) return NotFound();
        if (!await PodeUsarAsync(comanda.ClubeId)) return Forbid();

        if (!comanda.EstaAberta)
        {
            TempData["ErroBar"] = "Essa comanda já foi fechada. Abra outra pro cliente.";
            return RedirectToAction(nameof(Comanda), new { id = comandaId });
        }

        if (BarDoClube.ProblemaComQuantidade(quantidade) is { } problemaQtd)
        {
            TempData["ErroBar"] = problemaQtd;
            return RedirectToAction(nameof(Comanda), new { id = comandaId });
        }

        string descricao;
        decimal preco;
        ProdutoBar? produtoVendido = null;

        if (produtoBarId is int produtoId)
        {
            var produto = await _context.ProdutosBar
                .FirstOrDefaultAsync(p => p.Id == produtoId && p.ClubeId == comanda.ClubeId);

            if (produto == null)
            {
                TempData["ErroBar"] = "Produto não encontrado neste clube.";
                return RedirectToAction(nameof(Comanda), new { id = comandaId });
            }

            // COPIA nome e preço. Aumentar a cerveja hoje à noite não pode mexer no que foi
            // vendido esta tarde nem nas comandas fechadas do mês.
            descricao = produto.Nome;
            preco = produto.Preco;
            produtoVendido = produto;
        }
        else
        {
            // Item fora do cardápio, pra não travar a venda por falta de cadastro.
            if (string.IsNullOrWhiteSpace(descricaoLivre) || precoLivre == null)
            {
                TempData["ErroBar"] = "Escolha um produto, ou escreva a descrição e o valor.";
                return RedirectToAction(nameof(Comanda), new { id = comandaId });
            }

            if (BarDoClube.ProblemaComProduto(descricaoLivre, precoLivre.Value) is { } problemaLivre)
            {
                TempData["ErroBar"] = problemaLivre;
                return RedirectToAction(nameof(Comanda), new { id = comandaId });
            }

            descricao = descricaoLivre.Trim();
            preco = precoLivre.Value;
        }

        var item = new ItemComanda
        {
            ComandaId = comandaId,
            ProdutoBarId = produtoBarId,
            Descricao = descricao,
            PrecoUnitario = preco,
            Quantidade = quantidade,
            LancadoPorId = UsuarioId(),
        };

        _context.ItensComanda.Add(item);
        await _context.SaveChangesAsync();

        // A baixa do estoque pega carona no lançamento: o operador não faz NADA a mais, que é
        // a única forma de estoque sobreviver a um sábado cheio.
        //
        // E não impede a venda quando o saldo está zerado. A prateleira manda, não o sistema:
        // recusar uma cerveja que está na mão do cliente porque o número não bate seria o
        // jeito mais rápido de o bar desligar o controle de estoque pra sempre. Saldo negativo
        // aparece na tela como aviso — é sinal de entrada não registrada, não de venda ilegal.
        if (produtoVendido is { ControlaEstoque: true })
        {
            _context.MovimentosEstoque.Add(new MovimentoEstoque
            {
                ProdutoBarId = produtoVendido.Id,
                Tipo = MovimentoEstoque.Venda,
                Quantidade = -quantidade,
                ItemComandaId = item.Id,
                CriadoPorId = UsuarioId(),
            });

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Comanda), new { id = comandaId });
    }

    [HttpPost]
    public async Task<IActionResult> CancelarItem(int itemId, string? motivo)
    {
        var item = await _context.ItensComanda
            .Include(i => i.Comanda)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null) return NotFound();
        if (!await PodeUsarAsync(item.Comanda.ClubeId)) return Forbid();

        if (!item.Comanda.EstaAberta)
        {
            TempData["ErroBar"] = "A comanda já foi fechada — não dá pra mexer nos itens.";
            return RedirectToAction(nameof(Comanda), new { id = item.ComandaId });
        }

        // Não apaga a linha: marca. Item que some da comanda é a forma mais comum de furto
        // num bar, e o histórico é o que permite ao dono auditar depois.
        if (item.CanceladoEm == null)
        {
            item.CanceladoEm = DateTime.Now;
            item.CanceladoPorId = UsuarioId();
            item.MotivoCancelamento = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();

            // A cerveja volta pra geladeira sozinha. Sem isto, cancelar item furaria o
            // estoque em silêncio e a diferença só apareceria na contagem da semana seguinte,
            // quando ninguém mais lembra do que aconteceu.
            var baixa = await _context.MovimentosEstoque
                .FirstOrDefaultAsync(m => m.ItemComandaId == item.Id && m.Tipo == MovimentoEstoque.Venda);

            if (baixa != null)
            {
                _context.MovimentosEstoque.Add(new MovimentoEstoque
                {
                    ProdutoBarId = baixa.ProdutoBarId,
                    Tipo = MovimentoEstoque.Estorno,
                    Quantidade = -baixa.Quantidade,   // a baixa é negativa; o estorno devolve
                    ItemComandaId = item.Id,
                    Motivo = item.MotivoCancelamento,
                    CriadoPorId = UsuarioId(),
                });
            }

            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Comanda), new { id = item.ComandaId });
    }

    [HttpPost]
    public async Task<IActionResult> FecharComanda(int comandaId, string? formaPagamento,
        decimal desconto = 0, string? observacao = null)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        if (comanda == null) return NotFound();
        if (!await PodeUsarAsync(comanda.ClubeId)) return Forbid();

        if (!comanda.EstaAberta)
        {
            TempData["ErroBar"] = "Essa comanda já estava fechada.";
            return RedirectToAction(nameof(Index), new { id = comanda.ClubeId });
        }

        if (BarDoClube.ProblemaParaFechar(comanda.Subtotal, desconto, formaPagamento) is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Comanda), new { id = comandaId });
        }

        comanda.Desconto = desconto;
        comanda.FormaPagamento = formaPagamento;
        comanda.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
        comanda.Status = Models.Comanda.Fechada;
        comanda.FechadaEm = DateTime.Now;
        comanda.FechadaPorId = UsuarioId();

        await _context.SaveChangesAsync();

        TempData["SucessoBar"] = $"Comanda {comanda.Numero} ({comanda.NomeCliente}) fechada em {comanda.Total:C}.";
        return RedirectToAction(nameof(Index), new { id = comanda.ClubeId });
    }

    [HttpPost]
    public async Task<IActionResult> CancelarComanda(int comandaId, string? motivo)
    {
        var comanda = await _context.Comandas
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == comandaId);

        if (comanda == null) return NotFound();
        if (!await PodeUsarAsync(comanda.ClubeId)) return Forbid();

        if (!comanda.EstaAberta)
        {
            TempData["ErroBar"] = "Comanda já fechada não pode ser cancelada — o dinheiro já entrou.";
            return RedirectToAction(nameof(Index), new { id = comanda.ClubeId });
        }

        // Cancelar comanda COM consumo é anular uma venda. Exigir o motivo é o que separa
        // "abriu sem querer" de "sumiram quatro cervejas", e é a única pista que o dono terá.
        if (comanda.Subtotal > 0 && string.IsNullOrWhiteSpace(motivo))
        {
            TempData["ErroBar"] = "Essa comanda tem consumo lançado. Escreva o motivo do cancelamento.";
            return RedirectToAction(nameof(Comanda), new { id = comandaId });
        }

        comanda.Status = Models.Comanda.Cancelada;
        comanda.FechadaEm = DateTime.Now;
        comanda.FechadaPorId = UsuarioId();
        comanda.MotivoCancelamento = motivo?.Trim();

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = comanda.ClubeId });
    }

    // ===================== CAIXA DO DIA =====================

    [HttpPost]
    public async Task<IActionResult> AbrirCaixa(int clubeId, decimal valorAbertura = 0)
    {
        if (!await PodeUsarAsync(clubeId)) return Forbid();

        var hoje = DateTime.Today;

        var jaExiste = await _context.CaixasDoDia
            .FirstOrDefaultAsync(c => c.ClubeId == clubeId && c.Dia == hoje);

        if (jaExiste != null)
        {
            TempData["ErroBar"] = jaExiste.EstaAberto
                ? "O caixa de hoje já está aberto."
                : "O caixa de hoje já foi fechado. Reabrir apagaria a conferência — fale comigo se precisar.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        _context.CaixasDoDia.Add(new CaixaDoDia
        {
            ClubeId = clubeId,
            Dia = hoje,
            ValorAbertura = valorAbertura,
            AbertoPorId = UsuarioId(),
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { id = clubeId });
    }

    // `decimal?` de propósito, não `decimal`. Com o tipo não-nulável, campo em branco chega
    // como ZERO e o caixa fecha acusando um rombo do tamanho do dia inteiro — e fechar caixa
    // não tem volta. O `required` do formulário protege o clique normal, mas não protege
    // submit por script, sessão expirada nem página velha em cache, e a ação é destrutiva
    // demais pra depender só do navegador. (Achado testando o fluxo de ponta a ponta.)
    [HttpPost]
    public async Task<IActionResult> FecharCaixa(int clubeId, decimal? valorContado, string? observacao)
    {
        if (!await PodeUsarAsync(clubeId)) return Forbid();

        if (valorContado == null)
        {
            TempData["ErroBar"] = "Digite quanto tem na gaveta pra fechar o caixa.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        if (valorContado < 0)
        {
            TempData["ErroBar"] = "O valor contado não pode ser negativo.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        var dia = await DiaDeReferenciaAsync(clubeId);
        var caixa = await _context.CaixasDoDia
            .FirstOrDefaultAsync(c => c.ClubeId == clubeId && c.Dia == dia && c.FechadoEm == null);

        if (caixa == null)
        {
            TempData["ErroBar"] = "Não há caixa aberto pra fechar.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        // Fechar o caixa com comanda aberta é o erro clássico: aquele consumo não entra na
        // conferência e o dia fecha "faltando" dinheiro que ninguém deve.
        var abertas = await _context.Comandas
            .CountAsync(c => c.ClubeId == clubeId && c.DiaReferencia == dia && c.Status == Models.Comanda.Aberta);

        if (abertas > 0)
        {
            TempData["ErroBar"] = abertas == 1
                ? "Ainda há 1 comanda aberta. Feche ou cancele antes de fechar o caixa."
                : $"Ainda há {abertas} comandas abertas. Feche ou cancele antes de fechar o caixa.";
            return RedirectToAction(nameof(Index), new { id = clubeId });
        }

        caixa.ValorContado = valorContado.Value;
        caixa.FechadoEm = DateTime.Now;
        caixa.FechadoPorId = UsuarioId();
        caixa.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();

        await _context.SaveChangesAsync();

        var vendasDinheiro = await _context.Comandas
            .Where(c => c.ClubeId == clubeId && c.DiaReferencia == dia
                        && c.Status == Models.Comanda.Fechada && c.FormaPagamento == BarDoClube.Dinheiro)
            .Include(c => c.Itens)
            .ToListAsync();

        var (esperado, diferenca) = BarDoClube.ConferenciaDoCaixa(
            caixa.ValorAbertura, vendasDinheiro.Sum(c => c.Total), valorContado.Value);

        TempData["SucessoBar"] = diferenca == 0
            ? $"Caixa fechado certinho: {esperado:C}."
            : diferenca > 0
                ? $"Caixa fechado. Esperado {esperado:C}, contado {valorContado.Value:C} — sobrou {diferenca:C}."
                : $"Caixa fechado. Esperado {esperado:C}, contado {valorContado.Value:C} — faltou {Math.Abs(diferenca):C}.";

        return RedirectToAction(nameof(Index), new { id = clubeId });
    }

    // ===================== CARDÁPIO =====================

    [HttpGet]
    public async Task<IActionResult> Produtos(int id)
    {
        if (!await PodeUsarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        ViewBag.Clube = clube;
        ViewBag.EmConstrucao = _modulo.EmConstrucao;

        return View(await _context.ProdutosBar
            .Where(p => p.ClubeId == id)
            .OrderBy(p => p.Ativo ? 0 : 1).ThenBy(p => p.Ordem).ThenBy(p => p.Nome)
            .ToListAsync());
    }

    [HttpPost]
    // Os campos de estoque são NULÁVEIS de propósito: quando não vêm no formulário, o valor
    // atual é preservado. Com `bool controlaEstoque = false`, editar o preço de um produto
    // pela tela de cardápio desligaria o controle de estoque em silêncio — e o dono só
    // descobriria na contagem seguinte, sem nenhuma pista do que aconteceu.
    public async Task<IActionResult> SalvarProduto(int clubeId, int? id, string? nome, decimal preco,
        string? categoria, int ordem = 0,
        bool? controlaEstoque = null, int? unidadesPorEmbalagem = null, int? estoqueMinimo = null)
    {
        if (!await PodeUsarAsync(clubeId)) return Forbid();

        if (BarDoClube.ProblemaComProduto(nome, preco) is { } problema)
        {
            TempData["ErroBar"] = problema;
            return RedirectToAction(nameof(Produtos), new { id = clubeId });
        }

        var produto = id == null
            ? null
            : await _context.ProdutosBar.FirstOrDefaultAsync(p => p.Id == id && p.ClubeId == clubeId);

        if (produto == null)
        {
            produto = new ProdutoBar { ClubeId = clubeId };
            _context.ProdutosBar.Add(produto);
        }

        produto.Nome = nome!.Trim();
        produto.Preco = preco;
        produto.Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim();
        produto.Ordem = ordem;
        if (controlaEstoque != null) produto.ControlaEstoque = controlaEstoque.Value;
        if (unidadesPorEmbalagem != null) produto.UnidadesPorEmbalagem = Math.Max(1, unidadesPorEmbalagem.Value);
        if (estoqueMinimo != null) produto.EstoqueMinimo = Math.Max(0, estoqueMinimo.Value);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Produtos), new { id = clubeId });
    }

    // Inativar, nunca apagar: o produto que saiu de linha continua sendo apontado pelas
    // comandas antigas, e apagá-lo levaria junto o histórico de venda dele.
    [HttpPost]
    public async Task<IActionResult> AlternarProduto(int id)
    {
        var produto = await _context.ProdutosBar.FindAsync(id);
        if (produto == null) return NotFound();
        if (!await PodeUsarAsync(produto.ClubeId)) return Forbid();

        produto.Ativo = !produto.Ativo;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Produtos), new { id = produto.ClubeId });
    }
}
