namespace Padelizou.Services;

// Tira as mensagens da fila e entrega uma de cada vez, com respiro entre elas.
//
// Existe por causa de 03/08/2026: o sistema despejou os avisos de 24 cadastros em minutos e a
// Meta restringiu o número. O total do dia não era absurdo — a RAJADA era. Este serviço troca
// a rajada por um gotejar, ao custo de alguns minutos de atraso que ninguém percebe.
public class EntregadorDeWhatsAppBackgroundService : BackgroundService
{
    private readonly FilaDeWhatsApp _fila;
    private readonly VolumeDoWhatsApp _volume;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EntregadorDeWhatsAppBackgroundService> _logger;
    private readonly Random _sorteio = new();

    public EntregadorDeWhatsAppBackgroundService(FilaDeWhatsApp fila, VolumeDoWhatsApp volume,
        IServiceScopeFactory scopeFactory, ILogger<EntregadorDeWhatsAppBackgroundService> logger)
    {
        _fila = fila;
        _volume = volume;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var mensagem in _fila.LerTodasAsync(stoppingToken))
            {
                // O TETO vem antes da entrega, e barrar DESCARTA (não adia). Adiar encheria a
                // fila de aviso velho — "seu jogo é o próximo" entregue duas horas depois é
                // pior que não entregue —, e o excedente voltaria em rajada assim que a janela
                // abrisse, que é exatamente o que o teto existe pra impedir. Ver VolumeDoWhatsApp.
                if (!_volume.RegistrarSePuder(DateTime.Now)) continue;

                await EntregarAsync(mensagem, stoppingToken);

                // O respiro vem DEPOIS de entregar, não antes: a primeira mensagem de uma
                // rajada sai na hora, e só o que vem atrás é espaçado. Sem isso, um aviso
                // solitário e urgente esperaria à toa.
                await Task.Delay(RitmoDoWhatsApp.ProximoIntervalo(_sorteio), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // app desligando — o que sobrou na fila se perde, e tudo bem: aviso é perecível.
        }
    }

    private async Task EntregarAsync(MensagemDeWhatsApp mensagem, CancellationToken stoppingToken)
    {
        try
        {
            // Escopo por mensagem: o IWhatsAppService vive com o HttpClient tipado, e segurar
            // um escopo aberto pela vida inteira do serviço seguraria a conexão junto.
            using var scope = _scopeFactory.CreateScope();
            var whats = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            await whats.EnviarAsync(mensagem.Celular, mensagem.Texto);
        }
        catch (Exception ex)
        {
            // Uma mensagem ruim não pode parar a fila inteira.
            _logger.LogWarning(ex, "Falha ao entregar mensagem da fila de WhatsApp.");
        }
    }
}
