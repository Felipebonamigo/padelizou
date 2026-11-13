namespace Padelizou.Services;

// O relógio do aviso de horário mudado. A espera mora em AvisosDeHorarioPendentes e o texto em
// AvisoDeHorarioMudado (os dois testáveis com o relógio na mão); aqui é só o tique.
//
// 30 segundos: a espera é de 3 minutos, e um tique de 5 (o da QuadraAtrasada) atrasaria o aviso
// até mais que a própria espera. O tique quase sempre não acha nada — e aí nem abre o banco.
public class AvisoDeHorarioMudadoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AvisosDeHorarioPendentes _pendentes;
    private readonly ILogger<AvisoDeHorarioMudadoBackgroundService> _logger;

    public AvisoDeHorarioMudadoBackgroundService(IServiceScopeFactory scopeFactory,
        AvisosDeHorarioPendentes pendentes, ILogger<AvisoDeHorarioMudadoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _pendentes = pendentes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PassarAsync();
        }
    }

    private async Task PassarAsync()
    {
        var vencidos = _pendentes.Tirar(DateTime.Now);
        if (vencidos.Count == 0) return;

        try
        {
            using var escopo = _scopeFactory.CreateScope();
            var avisados = await escopo.ServiceProvider.GetRequiredService<AvisoDeHorarioMudado>().EnviarAsync(vencidos);
            _logger.LogInformation("Horário mudado: {Avisados} pessoa(s) avisada(s) por {Jogos} jogo(s) remarcado(s).",
                avisados, vencidos.Count);
        }
        catch (Exception ex)
        {
            // Já saiu da espera: um aviso perdido é o preço de não mandar o mesmo duas vezes, e a
            // tela continua com a hora certa.
            _logger.LogError(ex, "Aviso de horário mudado falhou ({Jogos} jogo(s)).", vencidos.Count);
        }
    }
}
