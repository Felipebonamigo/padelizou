namespace Padelizou.Services;

// O relógio da varredura do Padelímetro. A regra mora em Services/VarreduraDoPadelimetro (testável
// com o relógio na mão); aqui é só o tique e o aviso.
//
// ⚠️ A ESPERA INICIAL É CURTA PELO MESMO MOTIVO DA VarreduraDaChaveBackgroundService: o que isto
// conserta é a aplicação morrer junto com o processo — um RESTART. A primeira passada logo depois
// de subir é a que pega o estrago do deploy anterior.
//
// 📣 QUANDO A VARREDURA APLICA ALGUMA COISA, O ADMIN FICA SABENDO: cada partida da lista é uma
// entrega que se perdeu — um defeito, mesmo consertado. Vai pelo RegistroDeErros (o /Admin/Erros
// e o aviso ao admin raiz, com a janela de silêncio de lá), porque log do servidor ninguém lê.
public class VarreduraDoPadelimetroBackgroundService : BackgroundService
{
    // 5 minutos: o número parado não trava torneio nenhum (ao contrário da chave), mas aparece no
    // perfil e no ranking. Junto com a espera mínima da varredura (10 min), o jogo perdido volta
    // em no máximo ~15 min.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VarreduraDoPadelimetroBackgroundService> _logger;

    public VarreduraDoPadelimetroBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<VarreduraDoPadelimetroBackgroundService> logger)
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
            var aplicadas = await escopo.ServiceProvider.GetRequiredService<VarreduraDoPadelimetro>()
                .PassarAsync(DateTime.Now, ct);
            if (aplicadas.Count == 0) return;

            // Depois da passada, e não no meio: o RegistroDeErros limpa o ChangeTracker do contexto.
            await escopo.ServiceProvider.GetRequiredService<RegistroDeErros>().RegistrarAsync(
                new InvalidOperationException(
                    $"Padelímetro: {aplicadas.Count} partida(s) tinham ficado fora do número (a aplicação do "
                    + $"encerramento se perdeu) e foram aplicadas pela varredura: {string.Join(", ", aplicadas)}."),
                "varredura/padelimetro", "VARRE", null);
        }
        catch (OperationCanceledException)
        {
            // desligando
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Varredura do Padelímetro falhou nesta passada");
        }
    }
}
