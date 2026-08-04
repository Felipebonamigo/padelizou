using System.Threading.Channels;

namespace Padelizou.Services;

public record MensagemDeWhatsApp(string Celular, string Texto);

// O ritmo do envio. Puro e testável porque é ELE que decide se o número parece gente ou robô.
//
// Em 03/08/2026 o sistema despejou dezenas de mensagens em minutos (24 cadastros numa hora,
// cada um gerando avisos) e a Meta restringiu o número. Não foi o total do dia que pesou —
// foi a rajada.
public static class RitmoDoWhatsApp
{
    // Entre uma mensagem e a próxima. O intervalo é SORTEADO na faixa, não fixo: cadência
    // exata de relógio é assinatura de robô, e é barato não ter essa assinatura.
    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(7);
    public static readonly TimeSpan IntervaloMaximo = TimeSpan.FromSeconds(16);

    // Teto da fila. Sem ele, uma noite de torneio com o provedor fora do ar encheria a memória
    // do servidor. Aviso é perecível: melhor perder o excedente do que derrubar o site.
    public const int TamanhoMaximo = 500;

    public static TimeSpan ProximoIntervalo(Random sorteio)
    {
        var minimo = (int)IntervaloMinimo.TotalMilliseconds;
        var maximo = (int)IntervaloMaximo.TotalMilliseconds;
        return TimeSpan.FromMilliseconds(sorteio.Next(minimo, maximo + 1));
    }

    // Quanto tempo pra escoar N mensagens, na média. Serve pra conferir que a fila não vira
    // um atraso absurdo num dia cheio.
    public static TimeSpan TempoParaEscoar(int quantidade)
    {
        if (quantidade <= 0) return TimeSpan.Zero;
        var medio = (IntervaloMinimo + IntervaloMaximo) / 2;
        return medio * quantidade;
    }
}

// A fila de saída do WhatsApp. Fica entre quem gera o aviso e quem entrega: o aviso é
// enfileirado na hora (sem segurar a ação do jogador) e sai no ritmo do entregador.
//
// Em memória de propósito. Aviso é perecível — "seu jogo é o próximo" não vale nada meia hora
// depois — então persistir a fila num banco compraria complexidade pra entregar coisa velha.
// Se o app reiniciar com fila cheia, o que estava dentro se perde, e tudo bem.
public class FilaDeWhatsApp
{
    private readonly Channel<MensagemDeWhatsApp> _canal =
        Channel.CreateBounded<MensagemDeWhatsApp>(new BoundedChannelOptions(RitmoDoWhatsApp.TamanhoMaximo)
        {
            // `Wait` combinado com `TryWrite` (nunca `WriteAsync`) é o que dá o comportamento
            // que queremos: fila cheia devolve FALSE na hora, sem bloquear quem está gerando o
            // aviso — uma ação do jogador nunca pode travar esperando espaço numa fila de
            // notificação.
            //
            // Os modos `Drop*` seriam a escolha óbvia pelo nome e estão ERRADOS aqui: com
            // eles o `TryWrite` devolve `true` mesmo tendo jogado a mensagem fora, e o
            // descarte ficaria invisível pra quem lê o log.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    private readonly ILogger<FilaDeWhatsApp> _logger;

    public FilaDeWhatsApp(ILogger<FilaDeWhatsApp> logger) => _logger = logger;

    public int Descartadas { get; private set; }

    public bool Enfileirar(string celular, string texto)
    {
        if (_canal.Writer.TryWrite(new MensagemDeWhatsApp(celular, texto))) return true;

        Descartadas++;
        _logger.LogWarning("Fila de WhatsApp cheia ({Teto}) — mensagem descartada. Total descartado: {Total}.",
            RitmoDoWhatsApp.TamanhoMaximo, Descartadas);
        return false;
    }

    public IAsyncEnumerable<MensagemDeWhatsApp> LerTodasAsync(CancellationToken ct) =>
        _canal.Reader.ReadAllAsync(ct);

    // Tira uma sem esperar. É o que o teste usa pra conferir o que foi enfileirado, e o que
    // permite contar a fila sem um serviço de fundo rodando.
    public bool TentarLer(out MensagemDeWhatsApp? mensagem)
    {
        var tem = _canal.Reader.TryRead(out var lida);
        mensagem = lida;
        return tem;
    }

    public int Pendentes => _canal.Reader.Count;
}
