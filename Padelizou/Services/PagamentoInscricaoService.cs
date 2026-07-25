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

public record DadosMarcacaoJogo(int ClubeId, int QuadraClubeId, int JogadorId, DateTime DataHora, int DuracaoMinutos);

public interface IPagamentoInscricaoService
{
    Task<Jogador?> ObterRecebedorTorneioAsync(int torneioId);
    bool PodeCobrar(Torneio torneio, Jogador? recebedor);
    bool PodeCobrarAula(JogoAula jogo, Jogador? professor);

    Task<string?> IniciarCobrancaTorneioAsync(Torneio torneio, Jogador recebedor, Jogador pagador,
        string tipo, DadosInscricaoTorneio dados);
    Task<string?> IniciarCobrancaAulaAsync(JogoAula jogo, Jogador professor, Jogador pagador,
        DadosInscricaoAula dados);

    // Quem recebe a quadra é o dono do clube. Null = clube sem dono definido, então não há
    // pra quem repassar e a marcação segue gratuita.
    Task<Jogador?> ObterDonoDoClubeAsync(int clubeId);
    bool PodeCobrarQuadra(decimal? preco, Jogador? dono);
    Task<string?> IniciarCobrancaQuadraAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal preco, DadosMarcacaoJogo dados);

    // Quanto a tela deve anunciar: o valor que o jogador realmente vai pagar e a taxa embutida.
    // Null quando não há cobrança online — aí o preço do torneio/aula já é o valor final.
    (decimal Total, decimal Taxa)? CalcularExibicao(decimal preco, string tipoOperacao, Jogador? recebedor);

    Task EfetivarAsync(Pagamento pagamento);
}

// Liga o gateway ao domínio: cria a cobrança na hora da inscrição e materializa a inscrição
// quando o webhook confirma o pagamento.
public class PagamentoInscricaoService : IPagamentoInscricaoService
{
    private readonly DbPadelContext _context;
    private readonly IAsaasService _asaas;
    private readonly AsaasSettings _settings;
    private readonly ILogger<PagamentoInscricaoService> _logger;
    private readonly IPushNotificationService _push;

    public PagamentoInscricaoService(DbPadelContext context, IAsaasService asaas,
        IOptions<AsaasSettings> settings, ILogger<PagamentoInscricaoService> logger,
        IPushNotificationService push)
    {
        _context = context;
        _asaas = asaas;
        _settings = settings.Value;
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

    public bool PodeCobrar(Torneio torneio, Jogador? recebedor) =>
        EstaApto(recebedor) && torneio.PrecoInscricao > 0;

    public bool PodeCobrarAula(JogoAula jogo, Jogador? professor) =>
        EstaApto(professor) && jogo.Preco > 0;

    public async Task<Jogador?> ObterDonoDoClubeAsync(int clubeId)
    {
        var clube = await _context.Clubes.Include(c => c.Dono).FirstOrDefaultAsync(c => c.Id == clubeId);
        return clube?.Dono;
    }

    public bool PodeCobrarQuadra(decimal? preco, Jogador? dono) =>
        EstaApto(dono) && preco > 0;

    public (decimal Total, decimal Taxa)? CalcularExibicao(decimal preco, string tipoOperacao, Jogador? recebedor)
    {
        if (!EstaApto(recebedor) || preco <= 0) return null;

        // Mesmo cálculo usado pra gerar a cobrança, pra tela e checkout nunca divergirem.
        var rateio = _asaas.CalcularRateio(preco, tipoOperacao, recebedor!.ModoComissao);
        return (rateio.ValorTotal, rateio.ValorTotal - preco);
    }

    private bool EstaApto(Jogador? recebedor) =>
        _asaas.Configurado
        && recebedor is { ReceberPagamentoOnline: true }
        && !string.IsNullOrWhiteSpace(recebedor.AsaasWalletId);

    public Task<string?> IniciarCobrancaTorneioAsync(Torneio torneio, Jogador recebedor,
        Jogador pagador, string tipo, DadosInscricaoTorneio dados) =>
        CriarCobrancaAsync(
            recebedor, pagador, torneio.PrecoInscricao, "Torneio", tipo,
            $"Inscrição — {torneio.Nome}", dados,
            torneioId: torneio.Id, jogoAulaId: null);

    public Task<string?> IniciarCobrancaAulaAsync(JogoAula jogo, Jogador professor,
        Jogador pagador, DadosInscricaoAula dados) =>
        CriarCobrancaAsync(
            professor, pagador, jogo.Preco ?? 0m, "Aula", "Aula",
            $"Aula {jogo.DataHora:dd/MM HH:mm} — {professor.Nome}", dados,
            torneioId: null, jogoAulaId: jogo.Id);

    public Task<string?> IniciarCobrancaQuadraAsync(Clube clube, Jogador dono, Jogador pagador,
        decimal preco, DadosMarcacaoJogo dados) =>
        CriarCobrancaAsync(
            dono, pagador, preco, "Jogo", "Jogo",
            $"Quadra {dados.DataHora:dd/MM HH:mm} — {clube.Nome}", dados,
            torneioId: null, jogoAulaId: null);

    private async Task<string?> CriarCobrancaAsync(Jogador recebedor, Jogador pagador, decimal preco,
        string tipoOperacao, string tipoPagamento, string descricao, object dados,
        int? torneioId, int? jogoAulaId)
    {
        var rateio = _asaas.CalcularRateio(preco, tipoOperacao, recebedor.ModoComissao);

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
            recebedor.AsaasWalletId);

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
            default:
                await EfetivarTorneioAsync(pagamento);
                break;
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
            Status = "Confirmada"
        };
        _context.MarcacoesJogo.Add(marcacao);
        await _context.SaveChangesAsync();

        pagamento.ReferenciaId = marcacao.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Pagamento {Id} efetivado — marcação de quadra {Ref} criada.",
            pagamento.Id, marcacao.Id);
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
                EmListaDeEspera = emListaDeEspera
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
                EmListaDeEspera = emListaDeEspera
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
