using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using padelizou.Models;
using Padelizou.Models;
using System.Text.Json;

namespace Padelizou.Services;

// O que fica guardado no Pagamento pra montar a inscrição quando o dinheiro entrar.
//
// Jogador2Id nulo tem DOIS significados, distinguidos por SemParceiro:
//   SemParceiro = false -> inscrição individual de Torneio Americano
//   SemParceiro = true  -> dupla aberta, o jogador define o parceiro depois
// O campo tem default false de propósito: pagamento antigo, serializado antes dele
// existir, continua sendo lido como americano (que era o único caso).
public record DadosInscricaoTorneio(
    int TorneioId,
    int CategoriaId,
    int Jogador1Id,
    int? Jogador2Id,
    bool ImpedimentoSextaNoite,
    bool ImpedimentoSabadoManha,
    bool ImpedimentoSabadoTarde,
    bool SemParceiro = false);

public record DadosInscricaoAula(int JogoAulaId, int JogadorId);

// Cobrança da taxa de 5% do torneio "por fora" — o pagamento que destrava o sorteio das
// chaves (ver Services/TaxaDoTorneioExterno).
public record DadosTaxaExterno(int TorneioId);

// Mensalidade do professor assinante (ver Services/PlanoDoProfessor).
public record DadosAssinaturaProfessor(int ProfessorId);

public record DadosMarcacaoJogo(int ClubeId, int QuadraClubeId, int JogadorId, DateTime DataHora, int DuracaoMinutos);

// Vários horários numa cobrança só (jogador selecionou 2h seguidas, ou duas quadras pro
// grupo grande). Tipo próprio ("JogoVarios") em vez de esticar o DadosMarcacaoJogo:
// cobranças pendentes antigas continuam desserializando no formato que nasceram.
public record SlotReservado(int QuadraClubeId, DateTime DataHora, int DuracaoMinutos);
public record DadosMarcacaoJogoVarios(int ClubeId, int JogadorId, List<SlotReservado> Slots);

public interface IPagamentoInscricaoService
{
    Task<Jogador?> ObterRecebedorTorneioAsync(int torneioId);
    bool PodeCobrar(Torneio torneio, Jogador? recebedor);
    bool PodeCobrarAula(JogoAula jogo, Jogador? professor);

    // A mesma pergunta de PodeCobrar, mas sem exigir que o torneio/aula já exista — é o que
    // a tela de criação precisa saber. OQueFaltaParaReceber devolve null quando está tudo
    // certo, e o texto pra mostrar quando não está.
    bool PodeReceberOnline(Jogador? dono);
    string? OQueFaltaParaReceber(Jogador? dono);

    Task<string?> IniciarCobrancaTorneioAsync(Torneio torneio, Jogador recebedor, Jogador pagador,
        string tipo, DadosInscricaoTorneio dados, string? formaEscolhida = null);
    Task<string?> IniciarCobrancaAulaAsync(JogoAula jogo, Jogador professor, Jogador pagador,
        DadosInscricaoAula dados, string? formaEscolhida = null);

    // O avesso do EfetivarAsync: o dinheiro voltou, a inscrição volta junto.
    Task<bool> DesfazerAsync(Pagamento pagamento);

    // A taxa dos 5% do torneio "por fora": quem paga é o ORGANIZADOR, e o valor inteiro fica
    // com o Padelizou (sem split). Devolve a URL da fatura, ou null se o gateway falhou.
    Task<string?> IniciarCobrancaTaxaExternoAsync(Torneio torneio, Jogador organizador, decimal valor);

    // Mensalidade do professor assinante — também fica inteira com o Padelizou.
    Task<string?> IniciarCobrancaAssinaturaAsync(Jogador professor);

    // As mesmas duas cobranças acima, mas por Pix DIRETO na conta do Padelizou, sem gateway
    // (ver Services/PixDireto). Devolvem o Pagamento — a URL é a nossa tela do QR, não uma
    // fatura de terceiro. Null = já existe cobrança de gateway pendente, ou o Pix não está
    // configurado no painel.
    Task<Pagamento?> IniciarPixDiretoAssinaturaAsync(Jogador professor);
    Task<Pagamento?> IniciarPixDiretoTaxaExternoAsync(Torneio torneio, Jogador organizador, decimal valor);

    // Quem recebe a quadra é o dono do clube. Null = clube sem dono definido, então não há
    // pra quem repassar e a marcação segue gratuita.
    Task<Jogador?> ObterDonoDoClubeAsync(int clubeId);
    bool PodeCobrarQuadra(decimal? preco, Jogador? dono);
    Task<string?> IniciarCobrancaQuadraAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal preco, DadosMarcacaoJogo dados);
    Task<string?> IniciarCobrancaQuadrasAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal precoTotal, DadosMarcacaoJogoVarios dados);

    // Quanto a tela deve anunciar: o valor que o jogador realmente vai pagar e a taxa embutida.
    // Null quando não há cobrança online — aí o preço do torneio/aula já é o valor final.
    (decimal Total, decimal Taxa)? CalcularExibicao(decimal preco, string tipoOperacao,
        Jogador? recebedor, string? modoComissao = null, decimal? percentual = null);

    Task EfetivarAsync(Pagamento pagamento);
}

// Liga o gateway ao domínio: cria a cobrança na hora da inscrição e materializa a inscrição
// quando o webhook confirma o pagamento.
public class PagamentoInscricaoService : IPagamentoInscricaoService
{
    private readonly DbPadelContext _context;
    private readonly IAsaasService _asaas;
    private readonly AsaasSettings _settings;
    private readonly TaxasExibicao _taxas;
    private readonly PlanoProfessorSettings _plano;
    private readonly ILogger<PagamentoInscricaoService> _logger;
    private readonly IPushNotificationService _push;

    public PagamentoInscricaoService(DbPadelContext context, IAsaasService asaas,
        IOptions<AsaasSettings> settings, ILogger<PagamentoInscricaoService> logger,
        IPushNotificationService push, IOptions<TaxasExibicao> taxas,
        IOptions<PlanoProfessorSettings> plano)
    {
        _context = context;
        _asaas = asaas;
        _settings = settings.Value;
        _taxas = taxas.Value;
        _plano = plano.Value;
        _logger = logger;
        _push = push;
    }

    // Quem recebe é quem criou o torneio. Se por algum motivo não houver "Criador" gravado,
    // cai pro primeiro organizador em vez de deixar o torneio sem recebedor.
    public async Task<Jogador?> ObterRecebedorTorneioAsync(int torneioId)
    {
        var organizadores = await _context.TorneioOrganizadores
            .Where(o => o.TorneioId == torneioId)
            .Include(o => o.Jogador)
            .ToListAsync();

        return organizadores.FirstOrDefault(o => o.NivelAcesso == "Criador")?.Jogador
            ?? organizadores.FirstOrDefault()?.Jogador;
    }

    // Torneio marcado como "por fora" nunca gera cobrança no site, mesmo com tudo
    // configurado — a escolha do organizador vem antes da capacidade técnica.
    public bool PodeCobrar(Torneio torneio, Jogador? recebedor) =>
        torneio.CobraPeloSite && EstaApto(recebedor) && torneio.PrecoInscricao > 0;

    public bool PodeCobrarAula(JogoAula jogo, Jogador? professor) =>
        EstaApto(professor) && jogo.Preco > 0;

    public async Task<Jogador?> ObterDonoDoClubeAsync(int clubeId)
    {
        var clube = await _context.Clubes.Include(c => c.Dono).FirstOrDefaultAsync(c => c.Id == clubeId);
        return clube?.Dono;
    }

    public bool PodeCobrarQuadra(decimal? preco, Jogador? dono) =>
        EstaApto(dono) && preco > 0;

    public (decimal Total, decimal Taxa)? CalcularExibicao(decimal preco, string tipoOperacao,
        Jogador? recebedor, string? modoComissao = null, decimal? percentual = null)
    {
        if (!EstaApto(recebedor) || preco <= 0) return null;

        // Mesmo cálculo usado pra gerar a cobrança, pra tela e checkout nunca divergirem.
        // O modo vem do torneio quando informado; o do cadastro do recebedor é só o padrão.
        var rateio = _asaas.CalcularRateio(preco, tipoOperacao, modoComissao ?? recebedor!.ModoComissao, percentual);
        return (rateio.ValorTotal, rateio.ValorTotal - preco);
    }

    private bool EstaApto(Jogador? recebedor) =>
        _asaas.Configurado && ContaDeRecebimento.Conectada(recebedor);

    // As duas perguntas acima ficam disponíveis SEM um torneio/aula pronto, porque a hora
    // de avisar é antes: quem está criando o torneio precisa saber que "pelo site" não vai
    // funcionar pra ele enquanto ainda dá pra escolher outra coisa.
    public bool PodeReceberOnline(Jogador? dono) => EstaApto(dono);

    public string? OQueFaltaParaReceber(Jogador? dono) =>
        ContaDeRecebimento.OQueFalta(dono, _asaas.Configurado);

    // O preço do torneio é POR PESSOA: a inscrição de uma dupla cobra as duas de uma vez,
    // de quem está se inscrevendo. Americano é individual, então cobra uma.
    // `formaEscolhida` é a forma que o JOGADOR declarou no nosso checkout (Pix / cartão /
    // boleto). Ela decide as duas coisas juntas — o que a cobrança trava e a taxa que fica —
    // porque o rateio é fixado quando a cobrança nasce. Ver Services/CobrancaDoTorneio.
    public Task<string?> IniciarCobrancaTorneioAsync(Torneio torneio, Jogador recebedor,
        Jogador pagador, string tipo, DadosInscricaoTorneio dados, string? formaEscolhida = null)
    {
        var cobranca = CobrancaDoTorneio.Montar(torneio.FormaPagamento, formaEscolhida, _taxas);

        return CriarCobrancaAsync(
            recebedor, pagador,
            torneio.ValorCobrado(
                inscricaoDeDupla: tipo == "TorneioDupla",
                impedimentos: ContarImpedimentos(dados)),
            "Torneio", tipo,
            $"Inscrição — {torneio.Nome}", dados,
            torneioId: torneio.Id, jogoAulaId: null, modoComissao: torneio.ModoComissao,
            percentual: cobranca.Percentual,
            billingType: cobranca.BillingType);
    }

    // Cada impedimento marcado tira uma janela da grade do organizador — quando ele cobra
    // por isso, é este número que multiplica a taxa.
    private static int ContarImpedimentos(DadosInscricaoTorneio dados) =>
        (dados.ImpedimentoSextaNoite ? 1 : 0)
        + (dados.ImpedimentoSabadoManha ? 1 : 0)
        + (dados.ImpedimentoSabadoTarde ? 1 : 0);

    public async Task<string?> IniciarCobrancaTaxaExternoAsync(Torneio torneio, Jogador organizador, decimal valor)
    {
        if (!_asaas.Configurado || valor <= 0) return null;

        // Dois cliques não podem virar duas cobranças: se já existe uma pendente pra este
        // torneio, é ela que o organizador tem que pagar.
        var pendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == "TaxaTorneio" && p.TorneioId == torneio.Id
            && p.Status == "Pendente" && p.InvoiceUrl != null);
        if (pendente != null) return pendente.InvoiceUrl;

        var clienteId = await _asaas.ObterOuCriarClienteAsync(
            organizador.Nome, organizador.Cpf, organizador.Email, organizador.Celular);
        if (clienteId == null) return null;

        var pagamento = new Pagamento
        {
            Tipo = "TaxaTorneio",
            TorneioId = torneio.Id,
            JogadorId = organizador.Id,
            RecebedorId = null,          // o valor inteiro é do Padelizou — não há repasse
            Valor = valor,
            ValorRepasse = 0m,
            Comissao = valor,
            AsaasCustomerId = clienteId,
            DadosInscricao = JsonSerializer.Serialize(new DadosTaxaExterno(torneio.Id)),
            // Sem ExpiraEm de propósito: aqui não há vaga sendo segurada. A cobrança espera
            // o quanto precisar — e as chaves continuam travadas até ela ser paga.
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            new RateioComissao(valor, 0m, valor),
            $"Taxa do Padelizou — {torneio.Nome}",
            pagamento.Id.ToString(),
            DateTime.Today.AddDays(3),
            walletIdRecebedor: null,
            billingType: "UNDEFINED");

        if (cobranca == null)
        {
            _context.Pagamentos.Remove(pagamento);
            await _context.SaveChangesAsync();
            return null;
        }

        pagamento.AsaasPaymentId = cobranca.PaymentId;
        pagamento.InvoiceUrl = cobranca.InvoiceUrl;
        await _context.SaveChangesAsync();

        return cobranca.InvoiceUrl;
    }

    // A taxa da aula depende do plano do professor: assinante em dia (ou em teste) paga a
    // taxa menor DA FORMA que o aluno declarou; avulso paga a taxa cheia com a forma aberta.
    public Task<string?> IniciarCobrancaAulaAsync(JogoAula jogo, Jogador professor,
        Jogador pagador, DadosInscricaoAula dados, string? formaEscolhida = null)
    {
        var cobranca = PlanoDoProfessor.CobrancaDaAula(professor, formaEscolhida, DateTime.Now, _plano);

        return CriarCobrancaAsync(
            professor, pagador, jogo.Preco ?? 0m, "Aula", "Aula",
            $"Aula {jogo.DataHora:dd/MM HH:mm} — {professor.Nome}", dados,
            torneioId: null, jogoAulaId: jogo.Id, modoComissao: null,
            percentual: cobranca.Percentual,
            billingType: cobranca.BillingType);
    }

    // A mensalidade do assinante. Sem split (é receita do Padelizou) e sem ExpiraEm — quem
    // decide quando pagar é o professor; enquanto não paga, a taxa por aula volta pra cheia
    // sozinha (PlanoDoProfessor).
    public async Task<string?> IniciarCobrancaAssinaturaAsync(Jogador professor)
    {
        if (!_asaas.Configurado) return null;

        var pendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == "AssinaturaProfessor" && p.JogadorId == professor.Id
            && p.Status == "Pendente" && p.InvoiceUrl != null);
        if (pendente != null) return pendente.InvoiceUrl;

        var clienteId = await _asaas.ObterOuCriarClienteAsync(
            professor.Nome, professor.Cpf, professor.Email, professor.Celular);
        if (clienteId == null) return null;

        var valor = _plano.MensalidadeAssinante;
        var pagamento = new Pagamento
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = professor.Id,
            RecebedorId = null,
            Valor = valor,
            ValorRepasse = 0m,
            Comissao = valor,
            AsaasCustomerId = clienteId,
            DadosInscricao = JsonSerializer.Serialize(new DadosAssinaturaProfessor(professor.Id)),
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            new RateioComissao(valor, 0m, valor),
            $"Padelizou Professor — mensalidade",
            pagamento.Id.ToString(),
            DateTime.Today.AddDays(3),
            walletIdRecebedor: null,
            billingType: "UNDEFINED");

        if (cobranca == null)
        {
            _context.Pagamentos.Remove(pagamento);
            await _context.SaveChangesAsync();
            return null;
        }

        pagamento.AsaasPaymentId = cobranca.PaymentId;
        pagamento.InvoiceUrl = cobranca.InvoiceUrl;
        await _context.SaveChangesAsync();

        return cobranca.InvoiceUrl;
    }

    // ── Pix direto, sem gateway ───────────────────────────────────────────────────────────
    // Só mensalidade e taxa do externo (100% receita nossa — ver Services/PixDireto). Não há
    // cliente Asaas, não há fatura: o Pagamento nasce com o método "PixDireto" e a tela do QR
    // é nossa. A confirmação vem do admin, não de webhook.

    public Task<Pagamento?> IniciarPixDiretoAssinaturaAsync(Jogador professor) =>
        IniciarPixDiretoAsync(
            PixDireto.TipoAssinatura, professor,
            _plano.MensalidadeAssinante,
            new DadosAssinaturaProfessor(professor.Id),
            torneioId: null);

    public Task<Pagamento?> IniciarPixDiretoTaxaExternoAsync(Torneio torneio, Jogador organizador, decimal valor) =>
        IniciarPixDiretoAsync(
            PixDireto.TipoTaxaTorneio, organizador,
            valor,
            new DadosTaxaExterno(torneio.Id),
            torneioId: torneio.Id);

    private async Task<Pagamento?> IniciarPixDiretoAsync(
        string tipo, Jogador pagador, decimal valor, object dados, int? torneioId)
    {
        if (valor <= 0) return null;

        // Sem chave configurada não existe pra onde apontar o QR — quem chama cai no gateway.
        if (await ChavePixDoPadelizou.LerAsync(_context) == null) return null;

        // Dois cliques, uma cobrança — mesma regra do gateway. A pendente reaproveitada pode
        // estar em qualquer um dos dois estados abertos do Pix.
        var pendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == tipo && p.JogadorId == pagador.Id
            && (torneioId == null || p.TorneioId == torneioId)
            && p.MetodoPagamento == PixDireto.Metodo
            && (p.Status == "Pendente" || p.Status == PixDireto.AguardandoConfirmacao));
        if (pendente != null) return pendente;

        var pagamento = new Pagamento
        {
            Tipo = tipo,
            TorneioId = torneioId,
            JogadorId = pagador.Id,
            RecebedorId = null,          // o valor inteiro é do Padelizou — não há repasse
            Valor = valor,
            ValorRepasse = 0m,
            Comissao = valor,
            MetodoPagamento = PixDireto.Metodo,
            DadosInscricao = JsonSerializer.Serialize(dados),
            // Sem ExpiraEm, como nas versões de gateway destes tipos: nenhuma vaga está sendo
            // segurada, a cobrança espera o quanto precisar.
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        return pagamento;
    }

    public Task<string?> IniciarCobrancaQuadraAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal preco, DadosMarcacaoJogo dados) =>
        CriarCobrancaAsync(
            dono, pagador, preco, "Jogo", "Jogo",
            $"Quadra {dados.DataHora:dd/MM HH:mm} — {clube.Nome}", dados,
            torneioId: null, jogoAulaId: null);

    public Task<string?> IniciarCobrancaQuadrasAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal precoTotal, DadosMarcacaoJogoVarios dados) =>
        CriarCobrancaAsync(
            // A comissão é a mesma régua do jogo avulso ("Jogo") — só o tipo de PAGAMENTO
            // muda, pra efetivação saber que o JSON carrega vários slots.
            dono, pagador, precoTotal, "Jogo", "JogoVarios",
            $"Quadra ({dados.Slots.Count} horários) {dados.Slots[0].DataHora:dd/MM} — {clube.Nome}", dados,
            torneioId: null, jogoAulaId: null);

    private async Task<string?> CriarCobrancaAsync(Jogador recebedor, Jogador pagador, decimal preco,
        string tipoOperacao, string tipoPagamento, string descricao, object dados,
        int? torneioId, int? jogoAulaId, string? modoComissao = null,
        decimal? percentual = null, string billingType = "UNDEFINED")
    {
        var rateio = _asaas.CalcularRateio(preco, tipoOperacao, modoComissao ?? recebedor.ModoComissao, percentual);

        var clienteId = await _asaas.ObterOuCriarClienteAsync(
            pagador.Nome, pagador.Cpf, pagador.Email, pagador.Celular);
        if (clienteId == null) return null;

        var pagamento = new Pagamento
        {
            Tipo = tipoPagamento,
            TorneioId = torneioId,
            JogoAulaId = jogoAulaId,
            JogadorId = pagador.Id,
            RecebedorId = recebedor.Id,
            Valor = rateio.ValorTotal,
            ValorRepasse = rateio.ValorRepasse,
            Comissao = rateio.Comissao,
            AsaasCustomerId = clienteId,
            DadosInscricao = JsonSerializer.Serialize(dados),
            ExpiraEm = DateTime.Now.AddMinutes(_settings.MinutosParaPagar),
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            rateio,
            descricao,
            pagamento.Id.ToString(),
            DateTime.Today.AddDays(1),
            recebedor.AsaasWalletId,
            billingType);

        if (cobranca == null)
        {
            // Sem cobrança no gateway o registro pendente só sujaria o histórico.
            _context.Pagamentos.Remove(pagamento);
            await _context.SaveChangesAsync();
            return null;
        }

        pagamento.AsaasPaymentId = cobranca.PaymentId;
        pagamento.InvoiceUrl = cobranca.InvoiceUrl;
        await _context.SaveChangesAsync();

        return cobranca.InvoiceUrl;
    }

    // Chamado pelo webhook: o dinheiro entrou, então agora a inscrição existe de fato.
    // O AVESSO do EfetivarAsync: o dinheiro voltou, então a inscrição precisa voltar também.
    //
    // Antes o estorno mexia só no dinheiro: a dupla continuava inscrita, continuava marcada
    // como paga e a lista de espera não andava. Alguém tinha que arrumar tudo isso na mão
    // (o roteiro está no ESTORNO.md) — e enquanto não arrumasse, o torneio tinha uma vaga
    // ocupada por quem já tinha recebido o dinheiro de volta.
    //
    // É idempotente por construção: o Asaas reenvia o mesmo evento até receber 200, e a
    // segunda passada não acha mais a inscrição pra remover.
    //
    // Cobre a INSCRIÇÃO EM TORNEIO, que é o caso do estorno de verdade. Os outros tipos
    // (aula, quadra, taxa, mensalidade) são registrados no log em vez de desfeitos no chute:
    // desfazer errado destrói dado, e cada um desses tem regra própria.
    public async Task<bool> DesfazerAsync(Pagamento pagamento)
    {
        // Sem ReferenciaId o pagamento nunca virou inscrição — não há o que desfazer.
        if (pagamento.ReferenciaId == null) return false;

        if (pagamento.Tipo is "Aula" or "Jogo" or "JogoVarios" or "TaxaTorneio" or "AssinaturaProfessor")
        {
            _logger.LogWarning(
                "Estorno do pagamento {Id} (tipo {Tipo}) precisa ser desfeito à mão — o automático "
                + "cobre inscrição em torneio.", pagamento.Id, pagamento.Tipo);
            return false;
        }

        var dados = Desserializar<DadosInscricaoTorneio>(pagamento);
        if (dados == null) return false;

        var torneio = await _context.Torneios.FindAsync(dados.TorneioId);

        // Mesma bifurcação do EfetivarTorneioAsync, pra desfazer exatamente o que ele criou.
        if (dados.Jogador2Id.HasValue || dados.SemParceiro)
        {
            var dupla = await _context.Duplas.FindAsync(pagamento.ReferenciaId.Value);
            if (dupla == null) return false;

            bool eraConfirmada = !dupla.EmListaDeEspera;
            var afetados = new[] { dupla.Jogador1Id, dupla.Jogador2Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();

            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            await AvisarDoEstornoAsync(afetados, torneio);
            if (eraConfirmada) await ChamarDaListaDeEsperaAsync(dupla.CategoriaId, torneio);
        }
        else
        {
            var inscricao = await _context.InscricoesAmericanas.FindAsync(pagamento.ReferenciaId.Value);
            if (inscricao == null) return false;

            bool eraConfirmada = !inscricao.EmListaDeEspera;
            var categoriaId = inscricao.CategoriaId;

            _context.InscricoesAmericanas.Remove(inscricao);
            await _context.SaveChangesAsync();

            await AvisarDoEstornoAsync(new[] { inscricao.JogadorId }, torneio);
            if (eraConfirmada) await ChamarDaListaDeEsperaAsync(categoriaId, torneio);
        }

        _logger.LogInformation("Pagamento {Id} estornado — inscrição desfeita.", pagamento.Id);
        return true;
    }

    private async Task AvisarDoEstornoAsync(IEnumerable<int> jogadorIds, Torneio? torneio)
    {
        // Quem levou o dinheiro de volta precisa saber que a vaga foi junto. Sem isso a pessoa
        // apareceria no clube no dia do jogo achando que ainda estava inscrita.
        foreach (var id in jogadorIds)
        {
            try
            {
                await _push.EnviarParaJogadorAsync(id, "Inscrição cancelada",
                    $"O pagamento de {torneio?.Nome ?? "um torneio"} foi estornado e a inscrição foi desfeita. "
                    + "Se foi engano, dá pra se inscrever de novo enquanto as inscrições estiverem abertas.",
                    torneio != null ? $"/Torneios/Details/{torneio.Id}" : null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar o jogador {JogadorId} do estorno.", id);
            }
        }
    }

    // Abriu vaga: a primeira da fila entra (menor Id = quem chegou antes) e é avisada.
    private async Task ChamarDaListaDeEsperaAsync(int categoriaId, Torneio? torneio)
    {
        var proxima = await _context.Duplas
            .Where(d => d.CategoriaId == categoriaId && d.EmListaDeEspera)
            .OrderBy(d => d.Id)
            .FirstOrDefaultAsync();

        if (proxima == null) return;

        proxima.EmListaDeEspera = false;
        await _context.SaveChangesAsync();

        foreach (var id in new[] { proxima.Jogador1Id, proxima.Jogador2Id }.Where(i => i != null))
        {
            try
            {
                await _push.EnviarParaJogadorAsync(id!.Value, "Abriu vaga — vocês estão dentro!",
                    $"Alguém saiu de {torneio?.Nome ?? "um torneio"} e vocês saíram da lista de espera. Boa sorte!",
                    // Virou jogo de verdade pra quem estava esperando, e tem prazo: quem não
                    // ficar sabendo a tempo perde a vaga que acabou de ganhar.
                    torneio != null ? $"/Torneios/Details/{torneio.Id}" : null, AlcanceDoAviso.AppEWhatsApp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar promoção do jogador {JogadorId}.", id);
            }
        }
    }

    public async Task EfetivarAsync(Pagamento pagamento)
    {
        // O Asaas reenvia o mesmo evento até receber 200 — inscrever duas vezes seria pior
        // do que não inscrever.
        if (pagamento.ReferenciaId.HasValue) return;
        if (string.IsNullOrWhiteSpace(pagamento.DadosInscricao)) return;

        switch (pagamento.Tipo)
        {
            case "Aula":
                await EfetivarAulaAsync(pagamento);
                break;
            case "Jogo":
                await EfetivarMarcacaoAsync(pagamento);
                break;
            case "JogoVarios":
                await EfetivarMarcacoesAsync(pagamento);
                break;
            case "TaxaTorneio":
                await EfetivarTaxaExternoAsync(pagamento);
                break;
            case "AssinaturaProfessor":
                await EfetivarAssinaturaAsync(pagamento);
                break;
            default:
                await EfetivarTorneioAsync(pagamento);
                break;
        }
    }

    // A mensalidade entrou: estende a assinatura em 1 mês a partir de onde ela estiver.
    private async Task EfetivarAssinaturaAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosAssinaturaProfessor>(pagamento);
        if (dados == null) return;

        var professor = await _context.Jogadores.FindAsync(dados.ProfessorId);
        if (professor == null)
        {
            _logger.LogWarning("Pagamento {Id} (assinatura) confirmado, mas o professor sumiu.", pagamento.Id);
            return;
        }

        professor.AssinaturaProfessorPagaAte =
            PlanoDoProfessor.NovaDataPagaAte(professor.AssinaturaProfessorPagaAte, DateTime.Now);
        pagamento.ReferenciaId = professor.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — assinatura do professor {ProfessorId} paga até {PagaAte}.",
            pagamento.Id, professor.Id, professor.AssinaturaProfessorPagaAte);

        try
        {
            await _push.EnviarParaJogadorAsync(professor.Id,
                "Mensalidade recebida!",
                $"Seu plano Assinante está em dia até {professor.AssinaturaProfessorPagaAte:dd/MM}. Boas aulas!",
                "/PlanoProfessor");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao notificar assinatura do professor {ProfessorId}.", professor.Id);
        }
    }

    // A taxa dos 5% entrou: destrava o sorteio das chaves do torneio "por fora".
    private async Task EfetivarTaxaExternoAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosTaxaExterno>(pagamento);
        if (dados == null) return;

        var torneio = await _context.Torneios.FindAsync(dados.TorneioId);
        if (torneio == null)
        {
            _logger.LogWarning("Pagamento {Id} (taxa do externo) confirmado, mas o torneio sumiu.", pagamento.Id);
            return;
        }

        torneio.TaxaExternoPagaEm ??= DateTime.Now;
        pagamento.ReferenciaId = torneio.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — taxa do externo do torneio {TorneioId} quitada, chaves liberadas.",
            pagamento.Id, torneio.Id);

        try
        {
            await _push.EnviarParaJogadorAsync(pagamento.JogadorId,
                "Chaves liberadas!",
                $"{torneio.Nome}: a taxa do Padelizou foi recebida. Pode sortear os grupos.",
                $"/Torneios/Details/{torneio.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao notificar liberação das chaves do torneio {TorneioId}.", torneio.Id);
        }
    }

    private async Task EfetivarMarcacaoAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosMarcacaoJogo>(pagamento);
        if (dados == null) return;

        // O horário não fica reservado enquanto a cobrança está pendente, então alguém pode ter
        // marcado no meio do caminho. Aí não dá pra criar a marcação — o dinheiro entrou e
        // precisa ser devolvido, então deixa registrado pro dono estornar pela tela.
        bool jaMarcado = await _context.MarcacoesJogo.AnyAsync(m =>
            m.ClubeId == dados.ClubeId && m.QuadraClubeId == dados.QuadraClubeId &&
            m.DataHora == dados.DataHora && m.Status == "Confirmada");

        if (jaMarcado)
        {
            _logger.LogWarning("Pagamento {Id} confirmado, mas a quadra {Quadra} às {Hora} já estava marcada — precisa de estorno.",
                pagamento.Id, dados.QuadraClubeId, dados.DataHora);
            pagamento.Status = "AguardandoEstorno";
            await _context.SaveChangesAsync();
            return;
        }

        var marcacao = new MarcacaoJogo
        {
            ClubeId = dados.ClubeId,
            QuadraClubeId = dados.QuadraClubeId,
            JogadorId = dados.JogadorId,
            DataHora = dados.DataHora,
            DuracaoMinutos = dados.DuracaoMinutos,
            Status = "Confirmada",
            // Nasceu do webhook = o dinheiro já entrou; é o selo "pago" da agenda do dono.
            PagoEm = DateTime.Now,
        };
        _context.MarcacoesJogo.Add(marcacao);
        await _context.SaveChangesAsync();

        pagamento.ReferenciaId = marcacao.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — marcação de quadra {Ref} criada.",
            pagamento.Id, marcacao.Id);
    }

    private async Task EfetivarMarcacoesAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosMarcacaoJogoVarios>(pagamento);
        if (dados == null || dados.Slots.Count == 0) return;

        // Tudo-ou-nada, igual à recusa da tela: o jogador pagou pelo CONJUNTO (2h seguidas,
        // duas quadras pro grupo). Criar só metade entregaria outra coisa que não a comprada
        // — se qualquer horário foi tomado enquanto a cobrança estava pendente, nada é
        // criado e o pagamento fica pro estorno, como no caso de um horário só.
        foreach (var slot in dados.Slots)
        {
            bool jaMarcado = await _context.MarcacoesJogo.AnyAsync(m =>
                m.ClubeId == dados.ClubeId && m.QuadraClubeId == slot.QuadraClubeId &&
                m.DataHora == slot.DataHora && m.Status == "Confirmada");
            if (jaMarcado)
            {
                _logger.LogWarning("Pagamento {Id} confirmado, mas a quadra {Quadra} às {Hora} já estava marcada — precisa de estorno (nenhum dos {N} horários foi criado).",
                    pagamento.Id, slot.QuadraClubeId, slot.DataHora, dados.Slots.Count);
                pagamento.Status = "AguardandoEstorno";
                await _context.SaveChangesAsync();
                return;
            }
        }

        MarcacaoJogo? primeira = null;
        foreach (var slot in dados.Slots)
        {
            var marcacao = new MarcacaoJogo
            {
                ClubeId = dados.ClubeId,
                QuadraClubeId = slot.QuadraClubeId,
                JogadorId = dados.JogadorId,
                DataHora = slot.DataHora,
                DuracaoMinutos = slot.DuracaoMinutos,
                Status = "Confirmada",
                PagoEm = DateTime.Now,
            };
            _context.MarcacoesJogo.Add(marcacao);
            primeira ??= marcacao;
        }
        await _context.SaveChangesAsync();

        pagamento.ReferenciaId = primeira!.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — {N} marcações de quadra criadas.",
            pagamento.Id, dados.Slots.Count);
    }

    private async Task EfetivarTorneioAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosInscricaoTorneio>(pagamento);
        if (dados == null) return;

        var categoria = await _context.Categorias.FindAsync(dados.CategoriaId);
        var torneio = await _context.Torneios.FindAsync(dados.TorneioId);
        if (categoria == null || torneio == null)
        {
            _logger.LogWarning("Pagamento {Id} confirmado, mas categoria/torneio sumiu.", pagamento.Id);
            return;
        }

        // A vaga não fica reservada enquanto o pagamento está pendente, então quem pagou por
        // último pode achar a categoria cheia — nesse caso entra na lista de espera, mesma
        // regra de quem se inscreve num torneio lotado.
        // O último parâmetro diz se a vaga é de dupla (ocupa 1 vaga de dupla) ou de
        // americano — a dupla sem parceiro conta como dupla, ela só está incompleta.
        bool emListaDeEspera = await CategoriaOuTorneioEstaCheioAsync(
            categoria, torneio, dados.Jogador2Id.HasValue || dados.SemParceiro);

        if (dados.Jogador2Id.HasValue || dados.SemParceiro)
        {
            var dupla = new Dupla
            {
                CategoriaId = dados.CategoriaId,
                Jogador1Id = dados.Jogador1Id,
                Jogador2Id = dados.Jogador2Id,
                ImpedimentoSextaNoite = dados.ImpedimentoSextaNoite,
                ImpedimentoSabadoManha = dados.ImpedimentoSabadoManha,
                ImpedimentoSabadoTarde = dados.ImpedimentoSabadoTarde,
                EmListaDeEspera = emListaDeEspera,
                // Chegou aqui porque o dinheiro entrou: a inscrição já nasce quitada.
                Pago = true,
                PagoEm = DateTime.Now,
            };
            _context.Duplas.Add(dupla);
            await _context.SaveChangesAsync();
            pagamento.ReferenciaId = dupla.Id;
        }
        else
        {
            var inscricao = new InscricaoAmericana
            {
                CategoriaId = dados.CategoriaId,
                JogadorId = dados.Jogador1Id,
                EmListaDeEspera = emListaDeEspera,
                Pago = true,
                PagoEm = DateTime.Now,
            };
            _context.InscricoesAmericanas.Add(inscricao);
            await _context.SaveChangesAsync();
            pagamento.ReferenciaId = inscricao.Id;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Pagamento {Id} efetivado — inscrição de torneio {Ref} criada.",
            pagamento.Id, pagamento.ReferenciaId);

        // A inscrição paga só existe quando o dinheiro entra — este é o momento em que o
        // jogador precisa saber que está dentro (ou na lista de espera).
        var inscritos = dados.Jogador2Id.HasValue
            ? new[] { dados.Jogador1Id, dados.Jogador2Id.Value }
            : new[] { dados.Jogador1Id };

        foreach (var jogadorId in inscritos)
        {
            try
            {
                await _push.EnviarParaJogadorAsync(jogadorId,
                    emListaDeEspera ? "Você entrou na lista de espera" : "Inscrição confirmada!",
                    emListaDeEspera
                        ? $"{torneio.Nome} · {categoria.Nome} estava lotado. Se alguém desistir, vocês são chamados."
                        : $"{torneio.Nome} · {categoria.Nome}. Boa sorte!",
                    // Confirmação de algo que a pessoa acabou de fazer e pagar: é o recibo.
                    $"/Torneios/Details/{torneio.Id}", AlcanceDoAviso.AppEWhatsApp);
            }
            catch (Exception ex)
            {
                // Push é acessório — a inscrição já está paga e criada, não pode falhar por isso.
                _logger.LogWarning(ex, "Falha ao notificar inscrição do jogador {JogadorId}.", jogadorId);
            }
        }
    }

    private async Task EfetivarAulaAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosInscricaoAula>(pagamento);
        if (dados == null) return;

        var jogo = await _context.JogosAula.FindAsync(dados.JogoAulaId);
        if (jogo == null)
        {
            _logger.LogWarning("Pagamento {Id} confirmado, mas o jogo aula sumiu.", pagamento.Id);
            return;
        }

        // Alguém pode ter se inscrito de graça (ou pago antes) enquanto este pagamento estava
        // pendente; se já existe inscrição, não duplica.
        bool jaInscrito = await _context.InscricoesJogoAula
            .AnyAsync(i => i.JogoAulaId == dados.JogoAulaId && i.JogadorId == dados.JogadorId);

        if (!jaInscrito)
        {
            bool cheio = false;
            if (jogo.LimiteVagas.HasValue)
            {
                int confirmados = await _context.InscricoesJogoAula
                    .CountAsync(i => i.JogoAulaId == dados.JogoAulaId && !i.EmListaDeEspera);
                cheio = confirmados >= jogo.LimiteVagas.Value;
            }

            _context.InscricoesJogoAula.Add(new InscricaoJogoAula
            {
                JogoAulaId = dados.JogoAulaId,
                JogadorId = dados.JogadorId,
                EmListaDeEspera = cheio
            });
        }

        // InscricaoJogoAula não tem Id próprio (chave composta), então guardamos o id do jogo
        // — o que interessa aqui é marcar que este pagamento já foi efetivado.
        pagamento.ReferenciaId = dados.JogoAulaId;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — inscrição na aula {Jogo}.",
            pagamento.Id, dados.JogoAulaId);
    }

    private T? Desserializar<T>(Pagamento pagamento) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(pagamento.DadosInscricao!);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "DadosInscricao inválidos no pagamento {Id}.", pagamento.Id);
            return null;
        }
    }

    // Mesma regra de lotação usada na inscrição gratuita (DuplasController), aplicada agora
    // no momento em que o pagamento confirma.
    private async Task<bool> CategoriaOuTorneioEstaCheioAsync(Categoria categoria, Torneio torneio, bool ehDupla)
    {
        if (categoria.LimiteDuplas.HasValue)
        {
            int naCategoria = ehDupla
                ? await _context.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && !d.EmListaDeEspera)
                : await _context.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoria.Id && !i.EmListaDeEspera);
            if (naCategoria >= categoria.LimiteDuplas.Value) return true;
        }

        if (torneio.LimiteDuplasTotal.HasValue)
        {
            int noTorneio = ehDupla
                ? await _context.Duplas.CountAsync(d => d.Categoria.TorneioId == torneio.Id && !d.EmListaDeEspera)
                : await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == torneio.Id && !i.EmListaDeEspera);
            if (noTorneio >= torneio.LimiteDuplasTotal.Value) return true;
        }

        return false;
    }
}
