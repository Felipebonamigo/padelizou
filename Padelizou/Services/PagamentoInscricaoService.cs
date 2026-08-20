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
    bool ImpedimentoQuintaNoite,
    bool ImpedimentoSextaNoite,
    bool ImpedimentoSabadoManha,
    bool ImpedimentoSabadoTarde,
    bool SemParceiro = false);

// Pagamento de uma inscrição QUE JÁ EXISTE — o "pagar agora" do torneio que garante a vaga
// primeiro e cobra depois (`PagamentoObrigatorioNaInscricao == false`).
//
// É um tipo separado de DadosInscricaoTorneio porque o efeito na confirmação é o OPOSTO: lá
// a inscrição NASCE quando o dinheiro entra; aqui ela já está na lista e só precisa ser
// marcada como paga. Reusar o mesmo tipo criaria uma dupla duplicada a cada pagamento.
//
// Um dos dois ids vem preenchido: `DuplaId` no Oficial e no Americano de Duplas,
// `InscricaoAmericanaId` no Americano individual — cada formato grava numa tabela.
public record DadosPagamentoDeInscricao(
    int TorneioId,
    int? DuplaId,
    int? InscricaoAmericanaId);

public record DadosInscricaoAula(int JogoAulaId, int JogadorId);

// Cobrança da taxa de 5% do torneio "por fora" — o pagamento que destrava o sorteio das
// chaves (ver Services/TaxaDoTorneioExterno).
public record DadosTaxaExterno(int TorneioId);

// Mensalidade (ou anuidade) do professor assinante — ver Services/PlanoDoProfessor.
//
// ⚠️ `Ciclo` é opcional porque as cobranças criadas ANTES do plano anual existir têm só o
// ProfessorId gravado no banco. Sem o default, elas parariam de desserializar e a confirmação
// de quem tem cobrança em aberto morreria calada; com ele, valem um mês — que é o que foram.
public record DadosAssinaturaProfessor(int ProfessorId, string? Ciclo = null);

// A conta do MÊS de um aluno (ver Models/FaturaDoAluno). Só o id: tudo o que a cobrança
// precisa dizer — valor, quem paga, competência — já está congelado na própria fatura.
public record DadosFaturaDeAula(int FaturaId);

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

    // "Pagar agora" de uma inscrição que JÁ existe e está como não paga — o par que faltava
    // pro torneio que garante a vaga primeiro e cobra depois. Devolve a URL da fatura.
    Task<string?> IniciarCobrancaDeInscricaoAsync(Torneio torneio, Jogador recebedor, Jogador pagador,
        bool inscricaoDeDupla, int impedimentos, DadosPagamentoDeInscricao dados,
        string? formaEscolhida = null);
    Task<string?> IniciarCobrancaAulaAsync(JogoAula jogo, Jogador professor, Jogador pagador,
        DadosInscricaoAula dados, string? formaEscolhida = null);

    // O avesso do EfetivarAsync: o dinheiro voltou, a inscrição volta junto.
    Task<bool> DesfazerAsync(Pagamento pagamento);

    // Devolvemos só PARTE do dinheiro: a inscrição fica, mas passa a valer menos. Sincroniza
    // o ValorInscricao dela com o valor que sobrou no Pagamento (ver Services/EstornoParcial).
    Task<bool> AjustarValorDaInscricaoAsync(Pagamento pagamento);

    // A taxa dos 5% do torneio "por fora": quem paga é o ORGANIZADOR, e o valor inteiro fica
    // com o Padelizou (sem split). Devolve a URL da fatura, ou null se o gateway falhou.
    Task<string?> IniciarCobrancaTaxaExternoAsync(Torneio torneio, Jogador organizador, decimal valor);

    // Mensalidade (ou anuidade) do professor assinante — também fica inteira com o Padelizou.
    // `ciclo` vem de PlanoDoProfessor.CicloMensal/CicloAnual; null é mensal.
    Task<string?> IniciarCobrancaAssinaturaAsync(Jogador professor, string? ciclo = null);

    // A conta do mês de um aluno, cobrada pelo app com split pro professor. Devolve o link
    // de pagamento, ou null quando não deu (sem CPF do pagador, gateway fora, professor sem
    // conta de recebimento).
    Task<string?> IniciarCobrancaDaFaturaAsync(FaturaDoAluno fatura, Jogador professor);

    // As mesmas duas cobranças acima, mas por Pix DIRETO na conta do Padelizou, sem gateway
    // (ver Services/PixDireto). Devolvem o Pagamento — a URL é a nossa tela do QR, não uma
    // fatura de terceiro. Null = já existe cobrança de gateway pendente, ou o Pix não está
    // configurado no painel.
    Task<Pagamento?> IniciarPixDiretoAssinaturaAsync(Jogador professor, string? ciclo = null);
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
    public async Task<string?> IniciarCobrancaTorneioAsync(Torneio torneio, Jogador recebedor,
        Jogador pagador, string tipo, DadosInscricaoTorneio dados, string? formaEscolhida = null)
    {
        var cobranca = CobrancaDoTorneio.Montar(torneio, formaEscolhida, _taxas);

        // ⚠️ DOIS CLIQUES, UMA FATURA — e aqui não é hipótese: aconteceu em produção em
        // 08/08/2026, no NATA PADEL TOUR. O Lucas gerou TRÊS cobranças de R$ 125 (duas no
        // mesmo minuto, 20:20, e a terceira às 20:23, que foi a que ele pagou) e sobraram duas
        // pendentes órfãs no nome dele — no painel financeiro, na conta do organizador e no
        // e-mail de cobrança do meio de pagamento.
        //
        // O checkout demora (fala com o gateway), e tocar de novo é o que qualquer um faz.
        if (await CobrancaPendenteIgualAsync(tipo, dados) is { } jaAberta)
            return jaAberta;

        return await CriarCobrancaAsync(
            recebedor, pagador,
            await ValorDaInscricaoAsync(
                torneio,
                inscricaoDeDupla: tipo == "TorneioDupla",
                jogadoresConhecidos: new[] { dados.Jogador1Id, dados.Jogador2Id },
                impedimentos: ContarImpedimentos(dados)),
            "Torneio", tipo,
            $"Inscrição — {torneio.Nome}", dados,
            torneioId: torneio.Id, jogoAulaId: null, modoComissao: torneio.ModoComissao,
            percentual: cobranca.Percentual,
            billingType: cobranca.BillingType,
            expiraEm: PrazoParaPagar.Ate(torneio));
    }

    // O "pagar agora" de quem JÁ está inscrito. Mesma conta e mesma taxa do checkout de
    // inscrição — o que muda é só o efeito lá na frente: em vez de CRIAR a inscrição, o
    // webhook vai marcar a que já existe como paga (ver EfetivarPagamentoDeInscricaoAsync).
    //
    // ⚠️ O tipo gravado é "TorneioPagarDepois", e ele é a chave que separa os dois efeitos:
    // se caísse no `default` do EfetivarAsync, o pagamento criaria uma dupla NOVA e a pessoa
    // ficaria inscrita duas vezes.
    public async Task<string?> IniciarCobrancaDeInscricaoAsync(Torneio torneio, Jogador recebedor,
        Jogador pagador, bool inscricaoDeDupla, int impedimentos, DadosPagamentoDeInscricao dados,
        string? formaEscolhida = null)
    {
        var cobranca = CobrancaDoTorneio.Montar(torneio, formaEscolhida, _taxas);

        // ⚠️ DOIS CLIQUES NÃO PODEM VIRAR DUAS FATURAS (Felipe, 08/08/2026: "cuide para não
        // cobrar 2 vezes a mesma pessoa, para que não fique pagamentos pendentes").
        //
        // Sem isto, cada toque no "pagar agora" criava uma cobrança nova: a pessoa pagaria
        // uma e as outras ficariam penduradas como pendentes — na conta do organizador, no
        // painel financeiro e no e-mail de cobrança do meio de pagamento. E o botão é
        // justamente do tipo que se toca de novo quando a página demora.
        //
        // A mesma regra já existia pra taxa do "por fora", assinatura e Pix direto; a
        // inscrição era a que faltava.
        if (await CobrancaPendenteDaInscricaoAsync(dados) is { } jaAberta)
            return jaAberta;

        return await CriarCobrancaAsync(
            recebedor, pagador,
            // ⚠️ Aqui o valor NÃO se recalcula: quem já está inscrito tem o preço gravado na
            // própria inscrição, e é ele que se cobra. Recalcular faria o "pagar agora" pedir
            // um valor diferente do que a pessoa viu quando entrou — bastaria o organizador
            // ter mexido no preço no meio do caminho.
            await ValorJaCombinadoAsync(torneio, inscricaoDeDupla, impedimentos, dados),
            "Torneio", "TorneioPagarDepois",
            $"Inscrição — {torneio.Nome}", dados,
            torneioId: torneio.Id, jogoAulaId: null, modoComissao: torneio.ModoComissao,
            percentual: cobranca.Percentual,
            billingType: cobranca.BillingType,
            // O link de pagamento é justamente o que a pessoa volta pra usar depois: matá-lo
            // em 60 minutos transformaria "pago depois" em "não pagou".
            expiraEm: PrazoParaPagar.Ate(torneio));
    }

    // Já existe cobrança aberta pra EXATAMENTE esta inscrição (que ainda não nasceu)?
    //
    // ⚠️ A comparação é da INTENÇÃO INTEIRA — categoria, os dois jogadores e o "sem parceiro"
    // —, e não só de quem está pagando. Aqui a dupla só existe DENTRO do JSON da cobrança: é
    // ele que o webhook lê pra criar a inscrição. Reaproveitar uma fatura de intenção
    // diferente inscreveria a pessoa com o PARCEIRO ERRADO — ela começou com um, desistiu,
    // recomeçou com outro, e pagaria a primeira sem perceber.
    //
    // Por isso, se qualquer campo mudou, nasce cobrança nova: errar pra cá deixa uma pendente
    // a mais (que o organizador vê e ignora); errar pro outro lado inscreve gente errada.
    private async Task<string?> CobrancaPendenteIgualAsync(string tipo, DadosInscricaoTorneio dados)
    {
        var candidatas = await _context.Pagamentos
            .Where(p => p.Tipo == tipo
                     && p.TorneioId == dados.TorneioId
                     && p.JogadorId == dados.Jogador1Id
                     && p.Status == "Pendente"
                     && p.InvoiceUrl != null)
            .ToListAsync();

        foreach (var pagamento in candidatas)
        {
            var alvo = Desserializar<DadosInscricaoTorneio>(pagamento);
            if (alvo == null) continue;

            if (alvo.CategoriaId == dados.CategoriaId
                && alvo.Jogador1Id == dados.Jogador1Id
                && alvo.Jogador2Id == dados.Jogador2Id
                && alvo.SemParceiro == dados.SemParceiro)
            {
                return LinkDoPagamento.Para(pagamento);
            }
        }

        return null;
    }

    // Já existe uma cobrança aberta pra ESTA inscrição? Devolve a fatura dela.
    //
    // ⚠️ A comparação é pela INSCRIÇÃO (dupla ou inscrição de americano), e não por
    // jogador+torneio: com múltiplas categorias ligadas, a mesma pessoa tem duas inscrições no
    // mesmo torneio, e casar por jogador devolveria a fatura da OUTRA — ela pagaria a
    // categoria errada e as duas ficariam erradas.
    //
    // Por isso o filtro fino é feito em memória, sobre o JSON: são pouquíssimas linhas (as
    // pendentes daquela pessoa naquele torneio), e o `DadosInscricao` não é consultável em SQL.
    private async Task<string?> CobrancaPendenteDaInscricaoAsync(DadosPagamentoDeInscricao dados)
    {
        var candidatas = await _context.Pagamentos
            .Where(p => p.Tipo == "TorneioPagarDepois"
                     && p.TorneioId == dados.TorneioId
                     && p.Status == "Pendente"
                     && p.InvoiceUrl != null)
            .ToListAsync();

        foreach (var pagamento in candidatas)
        {
            var alvo = Desserializar<DadosPagamentoDeInscricao>(pagamento);
            if (alvo == null) continue;

            if (alvo.DuplaId == dados.DuplaId
                && alvo.InscricaoAmericanaId == dados.InscricaoAmericanaId)
            {
                return LinkDoPagamento.Para(pagamento);
            }
        }

        return null;
    }

    // O valor de uma inscrição QUE JÁ EXISTE: o que ficou gravado nela quando nasceu.
    //
    // Nulo = inscrição anterior à coluna `ValorInscricao`; aí vale o cálculo de sempre (preço
    // do torneio × pessoas), que naquele tempo era exatamente o que tinha sido cobrado.
    private async Task<decimal> ValorJaCombinadoAsync(
        Torneio torneio, bool inscricaoDeDupla, int impedimentos, DadosPagamentoDeInscricao dados)
    {
        decimal? gravado = dados.DuplaId is int duplaId
            ? (await _context.Duplas.FindAsync(duplaId))?.ValorInscricao
            : dados.InscricaoAmericanaId is int inscricaoId
                ? (await _context.InscricoesAmericanas.FindAsync(inscricaoId))?.ValorInscricao
                : null;

        return gravado ?? torneio.ValorCobrado(inscricaoDeDupla, impedimentos);
    }

    // O valor da inscrição, PESSOA A PESSOA — quem já está no torneio paga o preço da segunda
    // inscrição (ver Services/PrecoDaInscricao).
    //
    // ⚠️ O número de PESSOAS continua vindo do formato (dupla = 2), e não de quantos ids eu
    // conheço: a dupla "procurando parceiro" segue custando por duas, como sempre custou.
    // Fazer o parceiro ausente sumir da conta seria mudar o PREÇO de todo torneio de dupla de
    // carona numa mudança que era só sobre desconto — e ninguém pediu isso.
    //
    // O parceiro ainda desconhecido paga o preço CHEIO: não dá pra saber se ele repete, e
    // errar pra menos aqui tira dinheiro do organizador sem ele ter escolhido.
    private async Task<decimal> ValorDaInscricaoAsync(
        Torneio torneio, bool inscricaoDeDupla, IEnumerable<int?> jogadoresConhecidos, int impedimentos)
    {
        // ⚠️ QUANTAS PESSOAS a inscrição cobra sai de QUEM ESTÁ NELA, não do formato dela
        // (Felipe, 08/08/2026). Antes o teto virava piso: dupla cobrava 2 mesmo com o segundo
        // nome em branco, então quem entrava "procurando parceiro" pagava por DOIS — sozinho,
        // e sem saber quem seria o outro.
        //
        // A régua já existia do outro lado: TaxaDoTorneioExterno.PessoasInscritas conta dupla
        // sem parceiro como 1 ("cobrar por alguém que ainda não foi definido seria cobrar por
        // fantasma"). Eram as duas contas discordando sobre a mesma inscrição — o site cobrava
        // 2 e a base da taxa contava 1.
        //
        // `inscricaoDeDupla` continua valendo como TETO: numa dupla entram no máximo dois.
        var ids = jogadoresConhecidos
            .Where(id => id.HasValue).Select(id => id!.Value)
            .Take(inscricaoDeDupla ? 2 : 1)
            .ToList();

        var jaEstao = await QuemJaEstaNoTorneio.DentreAsync(_context, torneio.Id, ids);
        var repetindo = ids.Select(id => jaEstao.Contains(id)).ToList();

        // Ninguém conhecido não é inscrição de graça: cobra uma pessoa, pelo preço cheio.
        if (repetindo.Count == 0) repetindo.Add(false);

        return PrecoDaInscricao.Total(torneio, repetindo, impedimentos);
    }

    // Cada impedimento marcado tira uma janela da grade do organizador — quando ele cobra
    // por isso, é este número que multiplica a taxa.
    private static int ContarImpedimentos(DadosInscricaoTorneio dados) =>
        (dados.ImpedimentoQuintaNoite ? 1 : 0)
        + (dados.ImpedimentoSextaNoite ? 1 : 0)
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
        if (pendente != null) return LinkDoPagamento.Para(pendente);

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

        // A fatura do gateway fica GRAVADA (é o plano B da tela e o marcador de "existe
        // cobrança lá"), mas quem vai pagar vai pra NOSSA — ver Services/LinkDoPagamento.
        return LinkDoPagamento.Para(pagamento);
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

    // A mensalidade (ou a anuidade) do assinante. Sem split (é receita do Padelizou) e sem
    // ExpiraEm — quem decide quando pagar é o professor; enquanto não paga, a taxa por aula
    // volta pra cheia sozinha (PlanoDoProfessor).
    public async Task<string?> IniciarCobrancaAssinaturaAsync(Jogador professor, string? ciclo = null)
    {
        if (!_asaas.Configurado) return null;

        // Uma cobrança de assinatura aberta por vez, qualquer que seja o ciclo pedido: duas
        // faturas vivas ao mesmo tempo é como o professor paga duas vezes sem perceber. Quem
        // dá o recado de "você já tem uma em aberto" é o controller, que sabe o valor dela.
        var pendente = await _context.Pagamentos.FirstOrDefaultAsync(p =>
            p.Tipo == "AssinaturaProfessor" && p.JogadorId == professor.Id
            && p.Status == "Pendente" && p.InvoiceUrl != null);
        if (pendente != null) return LinkDoPagamento.Para(pendente);

        var clienteId = await _asaas.ObterOuCriarClienteAsync(
            professor.Nome, professor.Cpf, professor.Email, professor.Celular);
        if (clienteId == null) return null;

        var cicloEscolhido = PlanoDoProfessor.CicloValido(ciclo);
        var valor = PlanoDoProfessor.ValorDo(cicloEscolhido, _plano);
        var pagamento = new Pagamento
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = professor.Id,
            RecebedorId = null,
            Valor = valor,
            ValorRepasse = 0m,
            Comissao = valor,
            AsaasCustomerId = clienteId,
            DadosInscricao = JsonSerializer.Serialize(
                new DadosAssinaturaProfessor(professor.Id, cicloEscolhido)),
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            new RateioComissao(valor, 0m, valor),
            cicloEscolhido == PlanoDoProfessor.CicloAnual
                ? "Padelizou Professor — plano anual (12 meses)"
                : "Padelizou Professor — mensalidade",
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

        // A fatura do gateway fica GRAVADA (é o plano B da tela e o marcador de "existe
        // cobrança lá"), mas quem vai pagar vai pra NOSSA — ver Services/LinkDoPagamento.
        return LinkDoPagamento.Para(pagamento);
    }

    // ── Pix direto, sem gateway ───────────────────────────────────────────────────────────
    // Só mensalidade e taxa do externo (100% receita nossa — ver Services/PixDireto). Não há
    // cliente Asaas, não há fatura: o Pagamento nasce com o método "PixDireto" e a tela do QR
    // é nossa. A confirmação vem do admin, não de webhook.

    public Task<Pagamento?> IniciarPixDiretoAssinaturaAsync(Jogador professor, string? ciclo = null)
    {
        var cicloEscolhido = PlanoDoProfessor.CicloValido(ciclo);
        return IniciarPixDiretoAsync(
            PixDireto.TipoAssinatura, professor,
            PlanoDoProfessor.ValorDo(cicloEscolhido, _plano),
            new DadosAssinaturaProfessor(professor.Id, cicloEscolhido),
            torneioId: null);
    }

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
        decimal? percentual = null, string billingType = "UNDEFINED",
        // Até quando esta cobrança vale. Nulo = a regra de sempre (os minutos do gateway), que
        // é o certo pra quadra, aula e mensalidade: ali o valor fica reservado e não pode
        // ficar pendurado. Quem manda outra data é o torneio que aceita pagar depois —
        // ver Services/PrazoParaPagar.
        DateTime? expiraEm = null)
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
            ExpiraEm = expiraEm ?? DateTime.Now.AddMinutes(_settings.MinutosParaPagar),
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            rateio,
            descricao,
            pagamento.Id.ToString(),
            // Boleto precisa de mais que "amanhã": só compensar já leva 1 dia útil. Ver
            // VencimentoDaCobranca — Pix e cartão seguem em 1 dia, como sempre foram.
            //
            // ⚠️ E o `expiraEm` vai JUNTO: é o que faz o vencimento lá no gateway acompanhar o
            // prazo que a tela prometeu. Sem ele, "Pague até 21/09" convivia com uma fatura que
            // vencia amanhã — os dois relógios corriam separados, e o curto vencia.
            VencimentoDaCobranca.Para(billingType, DateTime.Today, expiraEm),
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

        // A fatura do gateway fica GRAVADA (é o plano B da tela e o marcador de "existe
        // cobrança lá"), mas quem vai pagar vai pra NOSSA — ver Services/LinkDoPagamento.
        return LinkDoPagamento.Para(pagamento);
    }

    // A conta do MÊS de um aluno, cobrada pelo app: a cobrança nasce na nossa conta e o split
    // manda a fatia do professor direto pra carteira dele, como em todo o resto do sistema.
    //
    // ⚠️ QUEM PAGA PODE NÃO TER CONTA NO PADELIZOU — é o caso normal aqui: a mãe que paga a
    // aula do filho não usa o app. `Pagamento.JogadorId` exige um Jogador, então o pagador
    // entra como PRÉ-CADASTRO por CPF, exatamente como o inscrito que o organizador coloca no
    // torneio (ver TorneiosController.Inscricoes). Ela assume a conta depois, entrando com o
    // mesmo CPF — e aí passa a ver o histórico do que pagou.
    public async Task<string?> IniciarCobrancaDaFaturaAsync(FaturaDoAluno fatura, Jogador professor)
    {
        if (!_asaas.Configurado) return null;
        if (!EstaApto(professor)) return null;
        if (fatura.Valor <= 0) return null;

        // Sem CPF do pagador o gateway recusa o cliente e a cobrança morre lá dentro. Parar
        // aqui deixa o professor com a conta em aberto pra acertar por fora, que é o que ele
        // já faz hoje — em vez de um erro sem explicação.
        if (!Documentos.CpfEhValido(fatura.PagadorCpf)) return null;

        // Uma cobrança viva por fatura: emitir de novo geraria duas faturas no gateway pro
        // mesmo mês, e o aluno pagaria a que achasse primeiro.
        if (fatura.PagamentoId is int jaTem)
        {
            var existente = await _context.Pagamentos.FindAsync(jaTem);
            if (existente != null && existente.Status == "Pendente" && existente.InvoiceUrl != null)
            {
                return LinkDoPagamento.Para(existente);
            }
        }

        var pagador = await ObterOuCriarPagadorAsync(fatura);
        if (pagador == null) return null;

        // A taxa é a do plano do professor, igual à da aula avulsa: assinante em dia paga a
        // menor, avulso paga a cheia. A forma fica aberta porque quem escolhe é quem paga.
        var cobrancaDoPlano = PlanoDoProfessor.CobrancaDaAula(professor, null, DateTime.Now, _plano);
        var rateio = _asaas.CalcularRateio(fatura.Valor, "Aula", professor.ModoComissao,
            cobrancaDoPlano.Percentual);

        var clienteId = await _asaas.ObterOuCriarClienteAsync(
            fatura.PagadorNome, fatura.PagadorCpf!, pagador.Email, fatura.PagadorCelular);
        if (clienteId == null) return null;

        var competencia = new DateTime(fatura.Ano, fatura.Mes, 1);

        var pagamento = new Pagamento
        {
            Tipo = "FaturaDeAula",
            JogadorId = pagador.Id,
            RecebedorId = professor.Id,
            Valor = rateio.ValorTotal,
            ValorRepasse = rateio.ValorRepasse,
            Comissao = rateio.Comissao,
            AsaasCustomerId = clienteId,
            DadosInscricao = JsonSerializer.Serialize(new DadosFaturaDeAula(fatura.Id)),
            // Sem ExpiraEm de propósito: aqui não há vaga reservada esperando: a conta do mês
            // vale até ser paga, e uma cobrança que expira em 60 minutos viraria trabalho novo
            // pro professor toda vez que o pagador demorasse.
            Status = "Pendente"
        };
        _context.Pagamentos.Add(pagamento);
        await _context.SaveChangesAsync();

        var cobranca = await _asaas.CriarCobrancaAsync(
            clienteId,
            rateio,
            $"Aulas de {competencia:MM/yyyy} — {professor.Nome}",
            pagamento.Id.ToString(),
            // O vencimento é o que o professor combinou no fechamento, não "amanhã": a conta
            // do mês nasce com data pra pagar, e mudá-la aqui quebraria o combinado.
            fatura.Vencimento,
            professor.AsaasWalletId,
            "UNDEFINED");

        if (cobranca == null)
        {
            _context.Pagamentos.Remove(pagamento);
            await _context.SaveChangesAsync();
            return null;
        }

        pagamento.AsaasPaymentId = cobranca.PaymentId;
        pagamento.InvoiceUrl = cobranca.InvoiceUrl;
        fatura.PagamentoId = pagamento.Id;
        await _context.SaveChangesAsync();

        return LinkDoPagamento.Para(pagamento);
    }

    // O pagador como conta: a dele se já existir com aquele CPF, ou um pré-cadastro novo.
    // Achar-ou-criar por CPF é o mesmo caminho da inscrição em torneio — e é ele que impede
    // duas contas da mesma pessoa quando o pai paga por dois filhos de professores diferentes.
    private async Task<Jogador?> ObterOuCriarPagadorAsync(FaturaDoAluno fatura)
    {
        var cpf = Documentos.SomenteDigitosOuNulo(fatura.PagadorCpf);
        if (cpf == null) return null;

        var existente = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
        if (existente != null) return existente;

        // Só nome e CPF: inventar e-mail ou celular aqui criaria conflito de unicidade quando
        // a pessoa for criar a conta dela de verdade.
        var novo = new Jogador { Nome = fatura.PagadorNome, Cpf = cpf };
        _context.Jogadores.Add(novo);
        await _context.SaveChangesAsync();
        return novo;
    }

    // O dinheiro da conta do mês entrou.
    private async Task EfetivarFaturaDeAulaAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosFaturaDeAula>(pagamento);
        if (dados == null) return;

        var fatura = await _context.FaturasDeAlunos.FindAsync(dados.FaturaId);
        if (fatura == null)
        {
            // A conta sumiu entre a cobrança e a confirmação (professor cancelou). O dinheiro
            // entrou e não há conta pra quitar: fica no log pra devolução à mão, porque
            // recriar uma conta cancelada seria pior.
            _logger.LogError("Pagamento {Id} confirmado, mas a fatura {FaturaId} não existe mais — "
                + "devolver à mão (ver ESTORNO.md).", pagamento.Id, dados.FaturaId);
            return;
        }

        fatura.Status = FechamentoDoMes.Paga;
        fatura.PagaEm = DateTime.Now;

        // O carimbo do ReferenciaId é o que torna isto idempotente: o Asaas reenvia o mesmo
        // evento até receber 200, e o EfetivarAsync sai fora quando ele já está preenchido.
        pagamento.ReferenciaId = fatura.Id;
        await _context.SaveChangesAsync();
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

        if (pagamento.Tipo is "Aula" or "Jogo" or "JogoVarios" or "TaxaTorneio" or "AssinaturaProfessor"
            or "FaturaDeAula")
        {
            _logger.LogWarning(
                "Estorno do pagamento {Id} (tipo {Tipo}) precisa ser desfeito à mão — o automático "
                + "cobre inscrição em torneio.", pagamento.Id, pagamento.Tipo);
            return false;
        }

        // ⚠️ "Pagar depois" desfaz de um jeito DIFERENTE, e sair daqui é obrigatório: no
        // caminho de baixo o dinheiro de volta APAGA a inscrição, porque lá ela só nasceu por
        // causa do pagamento. Aqui a vaga existia antes e continua existindo — o que volta
        // atrás é só o "pago".
        //
        // Não é detalhe: o JSON deste tipo é outro (DadosPagamentoDeInscricao), então a
        // desserialização abaixo devolveria um objeto zerado e o código removeria a linha de
        // OUTRA pessoa, achada por um id que veio da tabela errada.
        if (pagamento.Tipo == "TorneioPagarDepois")
        {
            return await DesfazerPagamentoDeInscricaoAsync(pagamento);
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

    // Estorno do "pagar agora": a inscrição FICA, só deixa de estar paga. Quem garantiu a
    // vaga não a perde por ter tomado o dinheiro de volta — ele acerta por fora com o
    // organizador, que é exatamente o combinado desse tipo de torneio.
    private async Task<bool> DesfazerPagamentoDeInscricaoAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosPagamentoDeInscricao>(pagamento);
        if (dados == null) return false;

        if (dados.DuplaId is int duplaId)
        {
            var dupla = await _context.Duplas.FindAsync(duplaId);
            if (dupla == null) return false;

            dupla.Pago = false;
            dupla.PagoEm = null;
        }
        else if (dados.InscricaoAmericanaId is int inscricaoId)
        {
            var inscricao = await _context.InscricoesAmericanas.FindAsync(inscricaoId);
            if (inscricao == null) return false;

            inscricao.Pago = false;
            inscricao.PagoEm = null;
        }
        else
        {
            return false;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Pagamento {Id} estornado — inscrição segue de pé, marcada como "
            + "não paga.", pagamento.Id);
        return true;
    }

    // Devolvemos parte do dinheiro: a inscrição CONTINUA de pé e paga, mas passou a custar
    // menos. Sem isto o `ValorInscricao` gravado nela seguiria dizendo o valor antigo — e
    // é justamente esse número que os somatórios de dinheiro do torneio leem (ver
    // Dupla.ValorInscricao), então o organizador veria uma receita que não existe mais.
    //
    // Espelha a bifurcação do DesfazerAsync de propósito: cada formato grava numa tabela, e
    // escolher a errada aqui reescreveria o valor da inscrição de OUTRA pessoa.
    public async Task<bool> AjustarValorDaInscricaoAsync(Pagamento pagamento)
    {
        if (pagamento.ReferenciaId == null) return false;

        // Aula, quadra, taxa e mensalidade não têm "valor da inscrição" pra corrigir: o
        // Pagamento já é o registro do quanto valeram.
        if (pagamento.Tipo is not ("TorneioDupla" or "TorneioAmericano" or "TorneioPagarDepois"))
            return false;

        if (pagamento.Tipo == "TorneioPagarDepois")
        {
            var dadosDoPagamento = Desserializar<DadosPagamentoDeInscricao>(pagamento);
            if (dadosDoPagamento == null) return false;

            if (dadosDoPagamento.DuplaId is int duplaJaExistente)
            {
                var dupla = await _context.Duplas.FindAsync(duplaJaExistente);
                if (dupla == null) return false;
                dupla.ValorInscricao = pagamento.Valor;
            }
            else if (dadosDoPagamento.InscricaoAmericanaId is int inscricaoJaExistente)
            {
                var inscricao = await _context.InscricoesAmericanas.FindAsync(inscricaoJaExistente);
                if (inscricao == null) return false;
                inscricao.ValorInscricao = pagamento.Valor;
            }
            else
            {
                return false;
            }
        }
        else
        {
            var dados = Desserializar<DadosInscricaoTorneio>(pagamento);
            if (dados == null) return false;

            if (dados.Jogador2Id.HasValue || dados.SemParceiro)
            {
                var dupla = await _context.Duplas.FindAsync(pagamento.ReferenciaId.Value);
                if (dupla == null) return false;
                dupla.ValorInscricao = pagamento.Valor;
            }
            else
            {
                var inscricao = await _context.InscricoesAmericanas.FindAsync(pagamento.ReferenciaId.Value);
                if (inscricao == null) return false;
                inscricao.ValorInscricao = pagamento.Valor;
            }
        }

        await _context.SaveChangesAsync();
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
            case "FaturaDeAula":
                await EfetivarFaturaDeAulaAsync(pagamento);
                break;
            // ⚠️ TEM que vir ANTES do default: no default o pagamento CRIA a inscrição, e aqui
            // ela já existe — cair lá deixaria a pessoa inscrita duas vezes, ocupando duas
            // vagas e aparecendo duas vezes na chave.
            case "TorneioPagarDepois":
                await EfetivarPagamentoDeInscricaoAsync(pagamento);
                break;
            default:
                await EfetivarTorneioAsync(pagamento);
                break;
        }
    }

    // O dinheiro entrou pra uma inscrição que JÁ estava na lista: ela só passa a ser paga.
    //
    // O carimbo do ReferenciaId no fim é o que torna isto idempotente — o Asaas reenvia o
    // mesmo evento até receber 200, e o EfetivarAsync sai fora quando ele já está preenchido.
    private async Task EfetivarPagamentoDeInscricaoAsync(Pagamento pagamento)
    {
        var dados = Desserializar<DadosPagamentoDeInscricao>(pagamento);
        if (dados == null) return;

        if (dados.DuplaId is int duplaId)
        {
            var dupla = await _context.Duplas.FindAsync(duplaId);
            if (dupla == null)
            {
                // A inscrição sumiu entre a cobrança e a confirmação (desistência). O dinheiro
                // entrou e não há vaga: fica no log pra devolução à mão, porque inscrever de
                // volta alguém que desistiu seria pior.
                _logger.LogError("Pagamento {Id} confirmado, mas a dupla {DuplaId} não existe mais — "
                    + "devolver à mão (ver ESTORNO.md).", pagamento.Id, duplaId);
                return;
            }

            dupla.Pago = true;
            dupla.PagoEm = DateTime.Now;
            pagamento.ReferenciaId = dupla.Id;
        }
        else if (dados.InscricaoAmericanaId is int inscricaoId)
        {
            var inscricao = await _context.InscricoesAmericanas.FindAsync(inscricaoId);
            if (inscricao == null)
            {
                _logger.LogError("Pagamento {Id} confirmado, mas a inscrição {InscricaoId} não existe "
                    + "mais — devolver à mão (ver ESTORNO.md).", pagamento.Id, inscricaoId);
                return;
            }

            inscricao.Pago = true;
            inscricao.PagoEm = DateTime.Now;
            pagamento.ReferenciaId = inscricao.Id;
        }
        else
        {
            _logger.LogError("Pagamento {Id} do tipo TorneioPagarDepois não diz qual inscrição "
                + "pagar.", pagamento.Id);
            return;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Pagamento {Id} confirmado — inscrição marcada como paga.", pagamento.Id);
    }

    // O dinheiro da assinatura entrou: estende a partir de onde ela estiver — 1 mês no ciclo
    // mensal, 12 no anual.
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

        var ehAnual = dados.Ciclo == PlanoDoProfessor.CicloAnual;
        professor.AssinaturaProfessorPagaAte =
            PlanoDoProfessor.NovaDataPagaAte(professor.AssinaturaProfessorPagaAte, DateTime.Now, dados.Ciclo);

        // ⚠️ QUEM PAGOU A ASSINATURA É ASSINANTE — e esta linha faltava (achado em 20/08/2026).
        // `SituacaoDe` começa perguntando `PlanoProfessor == "Assinante"`: sem ela, quem paga sem
        // nunca ter aberto o /PlanoProfessor fica com "pago até" no futuro e plano NULO, cai no
        // ramo final como "teste vencido sem escolha" e CONTINUA PAGANDO 10% POR AULA depois de
        // ter pago a mensalidade — e os avisos dele também calam. O caminho que chega aqui nesse
        // estado é o registro manual do admin (AdminController.RegistrarPagamento), que grava a
        // data e não encosta no plano; pela tela do professor ele já escolheu antes de pagar.
        professor.PlanoProfessor = PlanoDoProfessor.Assinante;

        // ⚠️ Zera os avisos de vencimento: a vigência é outra, então o ciclo de lembretes
        // recomeça. Sem esta linha o professor seria avisado UMA VEZ NA VIDA — no vencimento
        // seguinte o estágio gravado já seria maior que o devido e nada mais sairia.
        professor.UltimoLembreteDeAssinatura = null;

        pagamento.ReferenciaId = professor.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — assinatura ({Ciclo}) do professor {ProfessorId} "
            + "paga até {PagaAte}.",
            pagamento.Id, dados.Ciclo ?? PlanoDoProfessor.CicloMensal, professor.Id,
            professor.AssinaturaProfessorPagaAte);

        try
        {
            await _push.EnviarParaJogadorAsync(professor.Id,
                ehAnual ? "Plano anual confirmado!" : "Mensalidade recebida!",
                $"Seu plano Assinante está em dia até {professor.AssinaturaProfessorPagaAte:dd/MM/yyyy}. Boas aulas!",
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
                ImpedimentoQuintaNoite = dados.ImpedimentoQuintaNoite,
                ImpedimentoSextaNoite = dados.ImpedimentoSextaNoite,
                ImpedimentoSabadoManha = dados.ImpedimentoSabadoManha,
                ImpedimentoSabadoTarde = dados.ImpedimentoSabadoTarde,
                EmListaDeEspera = emListaDeEspera,
                // Chegou aqui porque o dinheiro entrou: a inscrição já nasce quitada.
                Pago = true,
                PagoEm = DateTime.Now,
                // ⚠️ O que foi REALMENTE cobrado, e não o preço do torneio: com desconto de
                // segunda inscrição os dois divergem, e é este número que os somatórios
                // (relatório, devolução, base da taxa do externo) precisam ler.
                ValorInscricao = pagamento.Valor,
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
                ValorInscricao = pagamento.Valor,
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
                    // Fora do WhatsApp por decisão do Felipe (09/08/2026): a pessoa ACABOU de
                    // se inscrever e está olhando a tela que já diz isso. Mensagem no celular
                    // repetindo o que ela viu meio segundo antes não informa nada — e é o
                    // aviso de maior volume que existe, um por inscrição.
                    // "Abriu vaga" continua no WhatsApp: aquele é notícia, este é eco.
                    $"/Torneios/Details/{torneio.Id}");
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
