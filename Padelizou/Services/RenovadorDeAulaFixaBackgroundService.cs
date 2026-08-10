using Padelizou.Models;

namespace Padelizou.Services;

// Mantém as aulas fixas SEM PRAZO sempre com o horizonte cheio (ver RenovacaoDaAulaFixa).
//
// Tick de 6 horas, e não de minutos: o que este job faz é criar aula da semana que vem — não
// existe urgência nenhuma, e rodar de hora em hora só gastaria banco à toa. Também roda na
// subida do app, pra um deploy no meio da semana já repor o que faltou.
public class RenovadorDeAulaFixaBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RenovadorDeAulaFixaBackgroundService> _logger;

    public RenovadorDeAulaFixaBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<RenovadorDeAulaFixaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        await ProcessarAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessarAsync(stoppingToken);
        }
    }

    private async Task ProcessarAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();

            var criadas = await RenovacaoDaAulaFixa.RenovarAsync(context, DateTime.Now, stoppingToken);

            if (criadas > 0)
            {
                _logger.LogInformation("Aulas fixas sem prazo renovadas: {Criadas} aula(s) criada(s).", criadas);
            }
        }
        catch (Exception ex)
        {
            // Nunca derruba o host: aula fixa que não renovou hoje renova no próximo tick.
            _logger.LogError(ex, "Falha ao renovar as aulas fixas sem prazo.");
        }
    }
}
