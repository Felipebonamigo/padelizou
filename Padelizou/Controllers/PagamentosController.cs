using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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

    // ── Pix direto: a tela de quem paga ───────────────────────────────────────────────────
    // A "fatura" do Pix direto é nossa, não do gateway: o QR, o copia e cola e o botão de
    // "já fiz o Pix". Regras e textos em Services/PixDireto.

    [HttpGet]
    public async Task<IActionResult> Pix(int id)
    {
        var pagamento = await _context.Pagamentos.FindAsync(id);

        // A fatura de OUTRA pessoa é dinheiro dela: pela casa, só o raiz (18/08/2026). Era o
        // crachá `IsAdmin`, que inclui o administrador nomeado. O dono da cobrança continua
        // entrando pelo próprio id, como sempre.
        bool ehAdmin = PoderesNoSistema.PodeVerDinheiro(User);
        var problema = PixDireto.ProblemaParaVer(pagamento, ObterJogadorIdLogado(), ehAdmin);
        if (problema != null) return NotFound();

        var dados = await ChavePixDoPadelizou.LerAsync(_context);
        if (dados == null)
        {
            // A cobrança nasceu com a chave configurada; se sumiu no meio do caminho, o
            // pagador não tem o que fazer — o problema é nosso e o texto diz isso.
            TempData["Erro"] = "O recebimento por Pix está indisponível agora. Fale com a gente.";
            return RedirectToAction("Meus");
        }

        ViewBag.CopiaECola = PixCopiaECola.Montar(
            dados.Chave, dados.Nome, dados.Cidade, pagamento!.Valor, PixDireto.TxId(pagamento.Id));
        ViewBag.Situacao = PixDireto.Situacao(pagamento);
        return View(pagamento);
    }

    // O PNG do QR, desenhado na hora a partir do MESMO copia e cola da tela. Rota própria em
    // vez de data-URI pra página não carregar uma string gigante — e o navegador pode cachear.
    [HttpGet]
    public async Task<IActionResult> PixQr(int id)
    {
        var pagamento = await _context.Pagamentos.FindAsync(id);

        bool ehAdmin = PoderesNoSistema.PodeVerDinheiro(User);
        if (PixDireto.ProblemaParaVer(pagamento, ObterJogadorIdLogado(), ehAdmin) != null) return NotFound();

        var dados = await ChavePixDoPadelizou.LerAsync(_context);
        if (dados == null) return NotFound();

        var payload = PixCopiaECola.Montar(
            dados.Chave, dados.Nome, dados.Cidade, pagamento!.Valor, PixDireto.TxId(pagamento.Id));

        using var gerador = new QRCoder.QRCodeGenerator();
        // ECC M é o padrão dos QR de Pix: aguenta tela riscada sem inflar o código.
        using var qr = gerador.CreateQrCode(payload, QRCoder.QRCodeGenerator.ECCLevel.M);
        var png = new QRCoder.PngByteQRCode(qr).GetGraphic(pixelsPerModule: 10);

        return File(png, "image/png");
    }

    // "Já fiz o Pix": muda o status pra fila de conferência do admin. Não confirma nada —
    // quem confirma é quem olha o extrato do banco.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclararPix(int id)
    {
        var pagamento = await _context.Pagamentos.FindAsync(id);

        var problema = PixDireto.ProblemaParaDeclarar(pagamento, ObterJogadorIdLogado());
        if (problema != null)
        {
            TempData["Erro"] = problema;
            return RedirectToAction("Pix", new { id });
        }

        pagamento!.Status = PixDireto.AguardandoConfirmacao;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Aviso recebido! Assim que o Pix aparecer no extrato, a gente confirma por aqui.";
        return RedirectToAction("Pix", new { id });
    }

    // ── A fatura do gateway, mas dentro de casa ───────────────────────────────────────────
    // O Pix vem do meio de pagamento; a TELA é nossa. O porquê está em Services/LinkDoPagamento.

    [HttpGet]
    public async Task<IActionResult> Fatura(int id)
    {
        var pagamento = await _context.Pagamentos.FindAsync(id);
        if (pagamento == null) return NotFound();

        // Mesma régua do Comprovante: quem pagou, quem recebe e o admin raiz.
        var meuId = ObterJogadorIdLogado();
        var eu = await _context.Jogadores.FindAsync(meuId);
        if (pagamento.JogadorId != meuId && pagamento.RecebedorId != meuId && eu?.IsAdminRaiz != true)
            return Forbid();

        // Pix direto nunca passou pelo gateway: ele tem tela própria, com o QR da nossa chave
        // e o botão de "já fiz o Pix" (a confirmação lá é manual).
        if (pagamento.MetodoPagamento == PixDireto.Metodo)
            return RedirectToAction(nameof(Pix), new { id });

        if (string.IsNullOrWhiteSpace(pagamento.AsaasPaymentId))
            return RedirectToAction(nameof(Comprovante), new { id });

        // Cobrança já resolvida não precisa de QR nenhum — e pedir Pix de algo pago só gasta
        // uma chamada pra receber um código que ninguém deve usar.
        if (pagamento.Status == "Pendente")
        {
            var pix = await _asaas.ObterPixAsync(pagamento.AsaasPaymentId);
            if (pix == null)
            {
                // Sem Pix é cobrança de cartão: quem sabe cobrar isso é o gateway, no ambiente
                // dele — o número do cartão não passa (e não deve passar) por aqui.
                if (!string.IsNullOrWhiteSpace(pagamento.InvoiceUrl)) return Redirect(pagamento.InvoiceUrl);

                TempData["Erro"] = "Não conseguimos abrir esta cobrança agora. Tente de novo em instantes.";
                return RedirectToAction(nameof(Meus));
            }

            ViewBag.CopiaECola = pix.CopiaECola;
            // O desenho é nosso, a partir do código que veio — ver Services/QrDoPix.
            ViewBag.QrBase64 = QrDoPix.Base64(pix.CopiaECola);
        }

        ViewBag.Origem = await DescricaoDaOrigemAsync(pagamento);
        return View(pagamento);
    }

    // O relógio da tela de pagamento: o webhook confirma sozinho, e sem isto a pessoa fica
    // olhando um QR já pago sem saber que deu certo. Só lê o banco — nada de bater no gateway
    // a cada poucos segundos.
    [HttpGet]
    public async Task<IActionResult> Situacao(int id)
    {
        var pagamento = await _context.Pagamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pagamento == null) return NotFound();

        var meuId = ObterJogadorIdLogado();
        if (pagamento.JogadorId != meuId && pagamento.RecebedorId != meuId) return Forbid();

        return Json(new { status = pagamento.Status, confirmado = pagamento.Status == "Confirmado" });
    }

    // "Inscrição — Copa X", "Aula", "Aluguel de quadra": o que a pessoa está pagando, escrito
    // do jeito que ela reconhece. Mesma régua do Comprovante.
    private async Task<string> DescricaoDaOrigemAsync(Pagamento pagamento)
    {
        if (pagamento.TorneioId != null)
            return (await _context.Torneios.FindAsync(pagamento.TorneioId.Value))?.Nome ?? "Torneio";

        return pagamento.Tipo switch
        {
            "Aula" => "Aula",
            "Jogo" or "JogoVarios" => "Aluguel de quadra",
            PixDireto.TipoAssinatura => "Plano Assinante",
            PixDireto.TipoTaxaTorneio => "Taxa do Padelizou",
            _ => "Pagamento",
        };
    }

    // Tela onde quem organiza torneio ou dá aula liga o recebimento pelo app, informa a wallet
    // do Asaas e escolhe quem paga a comissão.
    [HttpGet]
    public async Task<IActionResult> Configurar()
    {
        var jogador = await _context.Jogadores.FindAsync(ObterJogadorIdLogado());
        if (jogador == null) return NotFound();

        ViewBag.ComissoesPorTipo = _settings.ComissaoPercentualPorTipo;
        ViewBag.ComissaoMinima = _settings.ComissaoMinima;
        ViewBag.MinimasPorTipo = _settings.ComissaoMinimaPorTipo;
        ViewBag.ModoPadrao = _settings.ModoComissaoPadrao;

        // Abrir a conta aqui dentro só é oferecido se o gateway estiver de pé e o cadastro
        // dele tiver o necessário — prometer o caminho fácil e recusar depois do formulário
        // preenchido é pior do que já mostrar o que falta.
        ViewBag.PodeAbrirConta = _asaas.Configurado;
        ViewBag.FaltaNoPerfil = AberturaDeConta.FaltaNoPerfil(jogador);

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
        // Devolução PARCIAL não vira status "Estornado" (a cobrança segue paga, só que menor),
        // então sem somar o ValorEstornado o card diria "R$ 0,00 devolvido" logo depois de
        // devolvermos dinheiro de verdade.
        vm.Estornado = recebidos.Where(p => p.Status == "Estornado").Sum(p => p.ValorRepasse)
            + recebidos.Sum(p => p.ValorEstornado);
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

    // Extrato de recebimentos em CSV — pro contador. Mesmo recorte da tela Meus:
    // o que entrou (ou está pra entrar) pra mim como organizador/professor.
    [HttpGet]
    public async Task<IActionResult> ExportarCsv(string? periodo)
    {
        var meuId = ObterJogadorIdLogado();
        var per = (periodo ?? "").Trim().ToLower() switch { "mes" => "mes", "ano" => "ano", _ => "sempre" };

        var agora = DateTime.Now;
        DateTime? de = per switch
        {
            "mes" => new DateTime(agora.Year, agora.Month, 1),
            "ano" => new DateTime(agora.Year, 1, 1),
            _ => null
        };

        var recebidos = await _context.Pagamentos
            .Where(p => p.RecebedorId == meuId)
            .Include(p => p.Jogador)
            .OrderBy(p => p.ConfirmadoEm ?? p.CriadoEm)
            .ToListAsync();
        if (de != null) recebidos = recebidos.Where(p => (p.ConfirmadoEm ?? p.CriadoEm) >= de.Value).ToList();

        var idsTorneio = recebidos.Where(p => p.TorneioId != null).Select(p => p.TorneioId!.Value).Distinct().ToList();
        var nomesTorneio = idsTorneio.Count == 0
            ? new Dictionary<int, string>()
            : await _context.Torneios.Where(t => idsTorneio.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nome);

        // Ponto e vírgula + vírgula decimal: é o que o Excel brasileiro abre certo de primeira.
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Data;Status;Origem;Pagador;Valor pago;Meu repasse;Comissao plataforma");
        foreach (var p in recebidos)
        {
            var origem = p.TorneioId != null
                ? nomesTorneio.GetValueOrDefault(p.TorneioId.Value, "Torneio")
                : p.Tipo == "Aula" ? "Aula" : "Aluguel de quadra";
            var data = (p.ConfirmadoEm ?? p.CriadoEm).ToString("dd/MM/yyyy");
            static string Campo(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            sb.AppendLine(string.Join(";",
                data, p.Status, Campo(origem), Campo(p.Jogador?.Nome ?? "-"),
                p.Valor.ToString("F2").Replace('.', ','),
                p.ValorRepasse.ToString("F2").Replace('.', ','),
                p.Comissao.ToString("F2").Replace('.', ',')));
        }

        // BOM pro Excel reconhecer UTF-8 (sem ele, acentos viram lixo).
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"extrato-padelizou-{per}-{agora:yyyyMMdd}.csv");
    }

    // Comprovante de um pagamento — visível pra quem pagou, quem recebeu e o admin raiz.
    [HttpGet]
    public async Task<IActionResult> Comprovante(int id)
    {
        var meuId = ObterJogadorIdLogado();
        var pagamento = await _context.Pagamentos
            .Include(p => p.Jogador)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (pagamento == null) return NotFound();

        var eu = await _context.Jogadores.FindAsync(meuId);
        var podeVer = pagamento.JogadorId == meuId || pagamento.RecebedorId == meuId || eu?.IsAdminRaiz == true;
        if (!podeVer) return Forbid();

        ViewBag.Recebedor = pagamento.RecebedorId == null
            ? null
            : await _context.Jogadores.FindAsync(pagamento.RecebedorId.Value);
        ViewBag.Origem = pagamento.TorneioId != null
            ? (await _context.Torneios.FindAsync(pagamento.TorneioId.Value))?.Nome ?? "Torneio"
            : pagamento.Tipo == "Aula" ? "Aula" : "Aluguel de quadra";

        return View(pagamento);
    }

    // Estorna (ou cancela, se ainda não foi paga) uma cobrança dos meus torneios/aulas.
    //
    // `valor` em branco devolve TUDO — e devolver tudo desfaz a inscrição junto. Com valor, o
    // jogador recebe só aquela parte e a inscrição continua de pé (ver Services/EstornoParcial).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Estornar(int id, decimal? valor)
    {
        var meuId = ObterJogadorIdLogado();
        var pagamento = await _context.Pagamentos.FindAsync(id);
        if (pagamento == null) return Forbid();

        // Só o dono do torneio/aula mexe no dinheiro dele — sem esta checagem qualquer usuário
        // logado poderia estornar a cobrança de outra pessoa mandando o id na mão.
        //
        // O admin RAIZ também passa: quando quem cobrou errado foi o Padelizou, quem conserta
        // é a plataforma. A conta do gateway é dele de qualquer forma, então recusar aqui só
        // empurraria o conserto pro painel do Asaas — onde o nosso banco não fica sabendo.
        var eu = await _context.Jogadores.FindAsync(meuId);
        if (pagamento.RecebedorId != meuId && eu?.IsAdminRaiz != true) return Forbid();

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

        // Devolver exatamente o que resta é o estorno de sempre: cai no caminho total, que
        // desfaz a inscrição. Só é "parcial" o que deixa dinheiro na cobrança.
        bool parcial = valor is { } pedido && jaFoiPaga && !EstornoParcial.EhTotal(pagamento, pedido);

        if (parcial)
        {
            var problema = EstornoParcial.ProblemaParaEstornar(pagamento, valor!.Value);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction(nameof(Meus));
            }
        }

        // No parcial o gateway precisa saber quanto sai da fatia do ORGANIZADOR: sozinho, ele
        // tira tudo da comissão e recusa quando não cabe (ver DevolucaoParcial).
        var devolucao = parcial
            ? new DevolucaoParcial(valor!.Value, EstornoParcial.ParteDoRepasse(pagamento, valor.Value))
            : null;

        if (!await _asaas.EstornarAsync(pagamento.AsaasPaymentId, jaFoiPaga, devolucao))
        {
            TempData["Erro"] = "O gateway recusou o estorno. Tente novamente em instantes.";
            return RedirectToAction(nameof(Meus));
        }

        if (parcial)
        {
            // A cobrança segue CONFIRMADA: o que aconteceu foi uma devolução em cima dela, não
            // um cancelamento. Marcar "Estornado" aqui faria a inscrição sumir na hora em que
            // o webhook de estorno total chegasse — e ela não deve sumir.
            var devolvido = valor!.Value;
            EstornoParcial.Aplicar(pagamento, devolvido);
            await _context.SaveChangesAsync();

            await _inscricoes.AjustarValorDaInscricaoAsync(pagamento);

            _logger.LogInformation("Pagamento {Id}: devolvidos {Valor} ao jogador; sobrou {Sobra} "
                + "(repasse {Repasse} / comissão {Comissao}).",
                pagamento.Id, devolvido, pagamento.Valor, pagamento.ValorRepasse, pagamento.Comissao);

            TempData["Sucesso"] = $"Devolvidos {devolvido:C} — a inscrição continua de pé, "
                + $"agora valendo {pagamento.Valor:C}.";
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

    // Abre a conta de recebimento SEM o organizador sair do Padelizou. O caminho antigo (ir
    // no site do meio de pagamento, achar o código, voltar e colar) continua existindo pra
    // quem já tem conta — mas deixou de ser o único.
    //
    // Nada do endereço é gravado: ele atravessa daqui pro gateway e acaba na resposta.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AbrirConta(decimal? faturamentoMensal, DateTime? dataNascimento,
        string? cep, string? endereco, string? numero, string? bairro, string? modoComissao)
    {
        var jogador = await _context.Jogadores.FindAsync(ObterJogadorIdLogado());
        if (jogador == null) return NotFound();

        if (ContaDeRecebimento.Conectada(jogador))
        {
            TempData["Erro"] = "Sua conta de recebimento já está conectada.";
            return RedirectToAction(nameof(Configurar));
        }

        if (!_asaas.Configurado)
        {
            TempData["Erro"] = "O pagamento pelo app está fora do ar no momento.";
            return RedirectToAction(nameof(Configurar));
        }

        if (AberturaDeConta.FaltaNoPerfil(jogador) is { } faltaPerfil)
        {
            TempData["Erro"] = faltaPerfil + " Ajuste em Editar Perfil e volte aqui.";
            return RedirectToAction(nameof(Configurar));
        }

        if (AberturaDeConta.ProblemaNoFormulario(
                faturamentoMensal, dataNascimento, cep, endereco, numero, bairro) is { } problema)
        {
            TempData["Erro"] = problema;
            return RedirectToAction(nameof(Configurar));
        }

        var (sucesso, falha) = await _asaas.CriarSubcontaAsync(AberturaDeConta.Montar(
            jogador, faturamentoMensal!.Value, dataNascimento!.Value, cep!, endereco!, numero!, bairro!));

        if (sucesso == null)
        {
            // O caminho manual é oferecido em QUALQUER recusa, não só nas que a gente sabe
            // interpretar: colar o código funciona sempre. O motivo mais provável de cair
            // aqui e não ser "já tem conta" é o teto de 10 subcontas do Período de Avaliação
            // do Asaas — e nesse caso deixar o organizador sem saída seria o pior desfecho.
            TempData["Erro"] = falha?.JaTemConta == true
                ? $"{falha.Motivo} Use a conta que você já tem: cole o código dela aqui embaixo."
                : $"{falha?.Motivo ?? "Não foi possível criar a conta agora."} " +
                  "Se você já tem conta no meio de pagamento, dá pra colar o código dela aqui embaixo.";
            return RedirectToAction(nameof(Configurar));
        }

        jogador.AsaasWalletId = sucesso.WalletId;
        jogador.ReceberPagamentoOnline = true;
        jogador.ModoComissao = modoComissao is "Somada" or "Descontada" ? modoComissao : null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Conta de recebimento criada para o jogador {JogadorId}.", jogador.Id);

        TempData["Sucesso"] = AberturaDeConta.DepoisDeCriar;
        return RedirectToAction(nameof(Configurar));
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

                // O dinheiro voltou, a inscrição volta junto: sem isto a dupla continuava
                // inscrita e marcada como paga, ocupando vaga de quem estava na fila. Antes
                // era serviço manual (ESTORNO.md) e, enquanto ninguém fazia, o torneio tinha
                // uma vaga tomada por quem já tinha recebido de volta.
                await _inscricoes.DesfazerAsync(pagamento);
                break;

            case "PAYMENT_PARTIALLY_REFUNDED":
                // Devolução de PARTE do dinheiro. Nada de DesfazerAsync aqui: a inscrição
                // continua valendo, só passou a custar menos. Confundir este evento com o
                // total apagaria a inscrição de quem só recebeu um troco de volta.
                await RegistrarDevolucaoParcialAsync(pagamento, pagamentoJson);
                break;

            case "PAYMENT_DELETED":
            case "PAYMENT_OVERDUE":
                pagamento.Status = "Cancelado";
                await _context.SaveChangesAsync();
                break;
        }

        return Ok();
    }

    // O quanto o gateway diz que já voltou pro jogador, reconciliado com o que temos gravado.
    //
    // Reconciliar em vez de "subtrair o que chegou agora" é o que torna isto idempotente: o
    // Asaas reenvia o mesmo evento até receber 200, e o payload traz a lista INTEIRA de
    // estornos da cobrança. Descontar por evento tiraria o mesmo dinheiro duas vezes.
    //
    // Também cobre o estorno feito direto no painel do Asaas, fora do Padelizou — que é como
    // ele era feito antes desta tela existir.
    private async Task RegistrarDevolucaoParcialAsync(Pagamento pagamento, JsonElement pagamentoJson)
    {
        if (!pagamentoJson.TryGetProperty("refunds", out var refunds) || refunds.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Devolução parcial do pagamento {Id} chegou sem a lista de estornos — "
                + "o valor precisa ser conferido à mão.", pagamento.Id);
            return;
        }

        decimal totalDevolvido = 0m;
        foreach (var refund in refunds.EnumerateArray())
        {
            // Estorno cancelado é dinheiro que NÃO voltou.
            var status = refund.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (string.Equals(status, "CANCELLED", StringComparison.OrdinalIgnoreCase)) continue;

            if (refund.TryGetProperty("value", out var v) && v.TryGetDecimal(out var quanto))
                totalDevolvido += quanto;
        }

        var falta = totalDevolvido - pagamento.ValorEstornado;
        if (falta <= 0) return;   // já registrado (o estorno saiu da nossa tela, ou o evento repetiu)

        EstornoParcial.Aplicar(pagamento, falta);
        await _context.SaveChangesAsync();

        await _inscricoes.AjustarValorDaInscricaoAsync(pagamento);

        _logger.LogInformation("Pagamento {Id}: devolução parcial de {Falta} registrada pelo webhook "
            + "(total devolvido {Total}).", pagamento.Id, falta, totalDevolvido);
    }

    private bool TokenValido()
    {
        // Sem token configurado o endpoint fica fechado de propósito — melhor recusar do que
        // aceitar chamada anônima como confirmação de pagamento.
        if (string.IsNullOrWhiteSpace(_settings.WebhookToken)) return false;

        if (!Request.Headers.TryGetValue("asaas-access-token", out var recebido)) return false;

        // Comparação em tempo fixo, a mesma de ConviteDeParceiro.TokenConfere: o == do .NET
        // para no primeiro caractere diferente, e o tempo de resposta entrega quantos
        // caracteres iniciais já estavam certos — dá pra adivinhar o token um caractere por
        // vez, com chamadas repetidas. E este é o endpoint que CONFIRMA PAGAMENTO: quem
        // descobre o token marca inscrição como paga sem pagar.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(recebido.ToString().Trim()),
            Encoding.UTF8.GetBytes(_settings.WebhookToken.Trim()));
    }
}
