using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// As regras da fila de notas — puras, sem banco e sem provedor.
//
// Ficam aqui porque são exatamente as que não podem estar erradas: quando uma venda vira
// nota, quantas vezes vale tentar, e quando parar de tentar e chamar gente.
public static class FilaDeNotas
{
    // ⚠️ TRÊS TENTATIVAS, E O MOTIVO NÃO É TÉCNICO — É FINANCEIRO. O provedor cobra por
    // REQUISIÇÃO, não por nota autorizada: cada rejeição também consome crédito. Um produto
    // com NCM errado num clube movimentado tentaria pra sempre e queimaria crédito em laço,
    // e ninguém descobriria antes da fatura. Passadas três, a nota vira problema de gente.
    public const int MaximoDeTentativas = 3;

    // Espera entre tentativas, crescendo. A SEFAZ cai por minutos, não por milissegundos —
    // repetir na hora só gasta crédito no mesmo apagão.
    public static TimeSpan EsperaAntesDeRepetir(int tentativas) => tentativas switch
    {
        <= 1 => TimeSpan.FromMinutes(2),
        2 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromHours(1),
    };

    // Esta nota pode ser mandada agora?
    public static bool PodeEnviar(NotaFiscal nota, DateTime agora)
    {
        if (nota.Status is not (NotaFiscal.Pendente or NotaFiscal.Rejeitada)) return false;
        if (nota.Tentativas >= MaximoDeTentativas) return false;

        // Primeira tentativa vai na hora; as seguintes respeitam a espera.
        if (nota.EnviadaEm is not DateTime ultima) return true;

        return agora >= ultima + EsperaAntesDeRepetir(nota.Tentativas);
    }

    // O que fazer com a resposta do provedor.
    //
    // ⚠️ A distinção entre recusa definitiva e temporária é onde mora o dinheiro: erro de
    // cadastro não melhora tentando de novo, e cada nova tentativa é mais um crédito pago
    // pra receber o mesmo "não".
    public static string StatusDepoisDaResposta(RespostaDaEmissao resposta, int tentativasFeitas)
    {
        if (resposta.Aceita) return NotaFiscal.Enviada;
        if (!resposta.ValeTentarDeNovo) return NotaFiscal.Manual;

        return tentativasFeitas >= MaximoDeTentativas ? NotaFiscal.Manual : NotaFiscal.Rejeitada;
    }

    // Uma comanda vira nota? Só a que fechou de verdade e movimentou dinheiro.
    //
    // Cortesia fica de fora de propósito: valor zero não é venda, e nota de R$ 0 é rejeição
    // certa na SEFAZ — pagaríamos crédito pra ser recusados.
    public static bool DeveEmitirPorComanda(Comanda comanda) =>
        comanda.Status == Comanda.Fechada
        && comanda.Total > 0
        && comanda.FormaPagamento != BarDoClube.Cortesia;

    // A frase da tela, por status. Fica junto da regra pra fila e tela nunca contarem
    // histórias diferentes sobre a mesma nota.
    public static string Situacao(NotaFiscal nota) => nota.Status switch
    {
        NotaFiscal.Pendente => "Na fila para emitir.",
        NotaFiscal.Enviada => "Enviada — aguardando a SEFAZ.",
        NotaFiscal.Autorizada => $"Autorizada{(nota.Numero != null ? $" (nº {nota.Numero})" : "")}.",
        NotaFiscal.Rejeitada => $"Recusada: {nota.Mensagem ?? "sem detalhe"}. Vai tentar de novo.",
        NotaFiscal.Cancelada => "Cancelada.",
        NotaFiscal.Manual => $"Precisa de você: {nota.Mensagem ?? "não foi possível emitir sozinho"}.",
        _ => nota.Status,
    };
}

// Coloca a venda na fila — e é o ÚNICO lugar do sistema que cria nota.
//
// Existe como serviço (e não como código solto no fechamento da comanda) por uma razão que
// vale dinheiro: **idempotência**. O webhook do provedor reenvia, o balcão clica duas vezes,
// a página é recarregada — e nada disso pode virar duas notas da mesma venda. Nota duplicada
// não é bug de tela: é problema fiscal no CNPJ do cliente.
public class NotasDoClube
{
    private readonly DbPadelContext _context;
    private readonly ILogger<NotasDoClube> _logger;

    public NotasDoClube(DbPadelContext context, ILogger<NotasDoClube> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Enfileira a NFC-e de uma comanda fechada. Devolve a nota (nova ou a que já existia).
    //
    // ⚠️ NUNCA LANÇA. Quem chama está no fechamento da comanda, com a pessoa no balcão
    // esperando — e a regra número um do módulo é que a venda não trava por causa da nota.
    // Falhar aqui vira log, não erro na tela.
    public async Task<NotaFiscal?> EnfileirarDaComandaAsync(Comanda comanda)
    {
        try
        {
            if (!FilaDeNotas.DeveEmitirPorComanda(comanda)) return null;

            // A trava contra duplicidade. Uma comanda tem no máximo uma nota viva; a
            // cancelada não conta, porque depois de cancelar pode-se emitir de novo.
            var existente = await _context.NotasFiscais
                .FirstOrDefaultAsync(n => n.ComandaId == comanda.Id && n.Status != NotaFiscal.Cancelada);
            if (existente != null) return existente;

            var nota = new NotaFiscal
            {
                ClubeId = comanda.ClubeId,
                Tipo = NotaFiscal.Nfce,
                ComandaId = comanda.Id,
                Valor = comanda.Total,
                // Copiado agora: a comanda pode ser corrigida depois, e a nota tem que dizer
                // o que foi emitido, não o que o cadastro virou.
                CpfConsumidor = comanda.CpfConsumidor,
                Status = NotaFiscal.Pendente,
            };

            _context.NotasFiscais.Add(nota);
            await _context.SaveChangesAsync();

            return nota;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enfileirar a nota da comanda {ComandaId} — a venda "
                + "seguiu normalmente.", comanda.Id);
            return null;
        }
    }

    // O que a tela do clube mostra: o que precisa de gente olhando.
    public Task<List<NotaFiscal>> PendenciasAsync(int clubeId) =>
        _context.NotasFiscais
            .Where(n => n.ClubeId == clubeId
                        && (n.Status == NotaFiscal.Rejeitada || n.Status == NotaFiscal.Manual))
            .OrderByDescending(n => n.CriadaEm)
            .ToListAsync();
}
