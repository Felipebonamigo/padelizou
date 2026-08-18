using System.Threading.Channels;

namespace Padelizou.Services;

// `Tag` e `ApenasPush` nasceram pro PLACAR AO VIVO (16/08/2026): o mesmo jogo manda vários
// avisos seguidos (um por game), e um aviso "normal" entraria na Caixa de Avisos e mandaria
// e-mail a cada um — um jogo de 9 games viraria 9 linhas na tela de Notificações e 9 e-mails.
// `Tag` faz o navegador SUBSTITUIR a notificação anterior do mesmo jogo em vez de empilhar;
// `ApenasPush` pula caixa de entrada, e-mail e WhatsApp — é acompanhamento em tempo real, não
// recado que precise ficar guardado. Ver Services/AvisoDePlacarAoVivo.
public record AvisoPendente(int JogadorId, string Titulo, string Corpo, string? Url, AlcanceDoAviso Alcance,
    string? Tag = null, bool ApenasPush = false);

// A fila de saída dos avisos. Fica entre quem GERA o aviso e quem ENTREGA.
//
// ⚠️ Existe por causa do Interno de 05/08/2026, e o sintoma não parecia de notificação:
// *"muito lento pra finalizar um jogo"*. Finalizar dispara aviso pros 4 jogadores e pros
// seguidores deles, e cada aviso abre uma conexão SMTP com o Gmail e uma chamada de push por
// aparelho — tudo DENTRO da requisição, com o organizador de pé na quadra olhando o botão
// girar. Numa semifinal com gente acompanhando, são dezenas de idas à rede antes de a tela
// voltar.
//
// E o atraso não ficava só no relógio: com a tela travada o organizador tocava de novo, e daí
// saía o "o mesmo jogo subiu duas vezes" que ele relatou. Um botão lento vira um botão
// clicado duas vezes.
//
// Agora a ação grava o resultado e devolve a tela na hora; o aviso sai logo atrás, por fora.
//
// Em memória de propósito, mesma escolha da fila de WhatsApp: aviso é perecível — "seu jogo é
// o próximo" não vale nada meia hora depois —, então persistir a fila num banco compraria
// complexidade pra entregar coisa velha. Se o app reiniciar com fila cheia, o que estava
// dentro se perde, e tudo bem.
public class FilaDeAvisos
{
    // Teto alto porque aqui a rajada é NORMAL: uma rodada inteira terminando junto são
    // dezenas de avisos legítimos em segundos, e o gargalo (SMTP) escoa rápido. O limite
    // existe pro caso de o provedor cair — melhor perder o excedente do que a memória do
    // servidor no meio do torneio.
    public const int TamanhoMaximo = 2000;

    private readonly Channel<AvisoPendente> _canal =
        Channel.CreateBounded<AvisoPendente>(new BoundedChannelOptions(TamanhoMaximo)
        {
            // `Wait` + `TryWrite` (nunca `WriteAsync`): fila cheia devolve FALSE na hora, sem
            // bloquear quem está finalizando o jogo. Os modos `Drop*` seriam a escolha óbvia
            // pelo nome e estão ERRADOS: com eles o `TryWrite` devolve `true` mesmo tendo
            // jogado o aviso fora, e o descarte ficaria invisível no log.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    private readonly ILogger<FilaDeAvisos> _logger;

    public FilaDeAvisos(ILogger<FilaDeAvisos> logger) => _logger = logger;

    public int Descartados { get; private set; }

    public bool Enfileirar(AvisoPendente aviso)
    {
        if (_canal.Writer.TryWrite(aviso)) return true;

        Descartados++;
        _logger.LogWarning("Fila de avisos cheia ({Teto}) — aviso descartado. Total descartado: {Total}.",
            TamanhoMaximo, Descartados);
        return false;
    }

    public IAsyncEnumerable<AvisoPendente> LerTodosAsync(CancellationToken ct) =>
        _canal.Reader.ReadAllAsync(ct);

    // Tira um sem esperar. É o que o teste usa pra conferir o que foi enfileirado, e o que
    // permite contar a fila sem um serviço de fundo rodando.
    public bool TentarLer(out AvisoPendente? aviso)
    {
        var tem = _canal.Reader.TryRead(out var lido);
        aviso = lido;
        return tem;
    }

    public int Pendentes => _canal.Reader.Count;
}
