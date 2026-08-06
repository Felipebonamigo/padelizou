namespace Padelizou.Services;

// Tira os avisos da fila e entrega — e-mail, push e (quando o aviso pediu) WhatsApp.
//
// Sem respiro entre um e outro, ao contrário do entregador de WhatsApp: ali o espaçamento
// existe porque a Meta pune rajada, e aqui não há ninguém pra punir. O que importa é que a
// espera não esteja mais no clique do organizador.
public class EntregadorDeAvisosBackgroundService : BackgroundService
{
    private readonly FilaDeAvisos _fila;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EntregadorDeAvisosBackgroundService> _logger;

    public EntregadorDeAvisosBackgroundService(FilaDeAvisos fila, IServiceScopeFactory scopeFactory,
        ILogger<EntregadorDeAvisosBackgroundService> logger)
    {
        _fila = fila;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var aviso in _fila.LerTodosAsync(stoppingToken))
                await EntregarAsync(aviso);
        }
        catch (OperationCanceledException)
        {
            // app desligando — o que sobrou na fila se perde, e tudo bem: aviso é perecível.
        }
    }

    private async Task EntregarAsync(AvisoPendente aviso)
    {
        try
        {
            // Escopo por aviso, e é obrigatório: o PushNotificationService carrega um
            // DbPadelContext, que é scoped. Segurar um só pela vida do serviço acumularia
            // o rastreamento de tudo que passou por ele desde o start.
            using var scope = _scopeFactory.CreateScope();
            var push = scope.ServiceProvider.GetRequiredService<PushNotificationService>();

            await push.EntregarAgoraAsync(aviso);
        }
        catch (Exception ex)
        {
            // Um aviso ruim não pode parar a fila inteira.
            _logger.LogWarning(ex, "Falha ao entregar aviso pro jogador {JogadorId}.", aviso.JogadorId);
        }
    }
}
