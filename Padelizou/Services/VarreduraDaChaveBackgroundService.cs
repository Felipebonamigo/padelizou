namespace Padelizou.Services;

// O relógio da varredura da chave. A regra mora em Services/VarreduraDaChave (pura e testável);
// aqui é só o tique.
//
// ⚠️ A ESPERA INICIAL É CURTA DE PROPÓSITO. O modo de falha que isto conserta é a chamada do
// robô morrer junto com o processo — ou seja, um RESTART. A primeira passada logo depois de
// subir é a que pega o estrago do deploy anterior, que é exatamente o que aconteceu no ER em
// 12/09/2026.
public class VarreduraDaChaveBackgroundService : BackgroundService
{
    // 3 minutos: o dano é um torneio esperando uma chave que não vem, e num sábado de jogos
    // ninguém aguenta muito mais que isso. Tique menor não ajuda — a varredura só tem o que
    // fazer quando alguém acabou de finalizar um jogo.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(45);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VarreduraDaChaveBackgroundService> _logger;

    public VarreduraDaChaveBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<VarreduraDaChaveBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(EsperaInicial, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await PassarAsync(stoppingToken);

        using var timer = new PeriodicTimer(IntervaloTick);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PassarAsync(stoppingToken);
        }
    }

    private async Task PassarAsync(CancellationToken ct)
    {
        try
        {
            using var escopo = _scopeFactory.CreateScope();
            var varredura = escopo.ServiceProvider.GetRequiredService<VarreduraDaChave>();
            await varredura.PassarAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // desligando
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Varredura da chave falhou nesta passada");
        }
    }
}
