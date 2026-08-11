using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Job diário (não a cada 15min, como o LembreteJogoBackgroundService) — checa de hora em hora,
// mas só dispara de fato 1x por dia, na primeira tick depois das 9h, pros clubes que ativaram
// "avisar diariamente" em Marcar Jogo.
public class HorarioVagoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);
    private const int HoraDoDisparo = 9;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HorarioVagoBackgroundService> _logger;
    private readonly MarcarJogoSettings _marcarJogo;
    private DateTime? _ultimaDataDisparada;

    public HorarioVagoBackgroundService(IServiceScopeFactory scopeFactory, ILogger<HorarioVagoBackgroundService> logger,
        Microsoft.Extensions.Options.IOptions<MarcarJogoSettings> marcarJogo)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _marcarJogo = marcarJogo.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        await ProcessarSeForaDaHoraAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessarSeForaDaHoraAsync(stoppingToken);
        }
    }

    private async Task ProcessarSeForaDaHoraAsync(CancellationToken stoppingToken)
    {
        var agora = DateTime.Now;
        if (agora.Hour < HoraDoDisparo || _ultimaDataDisparada == agora.Date) return;

        // Módulo escondido, aviso calado: este push manda o jogador pra uma tela que responde
        // 404 pra ele (ver Services/MarcarJogoSettings). Aviso que leva a lugar nenhum queima a
        // permissão de notificação — a pessoa desliga tudo e não volta.
        if (!_marcarJogo.Habilitado) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var horarioMarcacaoService = scope.ServiceProvider.GetRequiredService<IHorarioMarcacaoService>();

            var clubes = await context.Clubes
                .Where(c => c.MarcacaoHorariosAtiva && c.NotificarHorariosDiariamente)
                .ToListAsync(stoppingToken);

            foreach (var clube in clubes)
            {
                var slotsHoje = await horarioMarcacaoService.ObterSlotsDisponiveisAsync(clube.Id, DateTime.Today);
                if (slotsHoje.Count > 0)
                {
                    await horarioMarcacaoService.NotificarHorarioVagoAsync(clube.Id);
                }
            }

            _ultimaDataDisparada = agora.Date;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar aviso diário de horário vago.");
        }
    }
}
