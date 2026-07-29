using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Vigia do dia de jogo: manda push pros jogadores cuja partida agendada não começou no
// horário. A regra de quem avisar mora em QuadraAtrasada (pura, testável); aqui é só o
// relógio e o correio.
public class QuadraAtrasadaBackgroundService : BackgroundService
{
    // 5 minutos: com a tolerância de 15, o aviso sai entre 15 e 20 minutos de atraso.
    // Tick menor não melhora nada — ninguém cronometra atraso de torneio em segundos.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuadraAtrasadaBackgroundService> _logger;

    public QuadraAtrasadaBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<QuadraAtrasadaBackgroundService> logger)
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

        await VerificarAsync(stoppingToken);

        using var timer = new PeriodicTimer(IntervaloTick);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await VerificarAsync(stoppingToken);
        }
    }

    private async Task VerificarAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var agora = DateTime.Now;

            // Filtro barato no banco primeiro: partidas agendadas na janela de atraso, sem
            // aviso ainda. Quase todo tick isso volta vazio e o resto nem roda.
            var piso = agora - QuadraAtrasada.Teto;
            var limite = agora - QuadraAtrasada.Tolerancia;
            var torneiosComCandidata = await context.Partidas
                .Where(p => p.TorneioId != null && p.Status == "Agendada"
                    && p.AvisoAtrasoEnviadoEm == null && p.AvisoProximoEnviadoEm == null
                    && p.HorarioPrevisto != null && p.HorarioPrevisto >= piso && p.HorarioPrevisto <= limite)
                .Select(p => p.TorneioId!.Value)
                .Distinct()
                .ToListAsync(stoppingToken);

            foreach (var torneioId in torneiosComCandidata)
            {
                // A decisão precisa do torneio inteiro (pra saber se o dia começou), então
                // carrega tudo dele — são dezenas de linhas, não milhares.
                var partidas = await context.Partidas
                    .Include(p => p.Dupla1)
                    .Include(p => p.Dupla2)
                    .Where(p => p.TorneioId == torneioId)
                    .ToListAsync(stoppingToken);

                var atrasadas = QuadraAtrasada.Atrasadas(partidas, agora);
                if (atrasadas.Count == 0) continue;

                foreach (var partida in atrasadas)
                {
                    foreach (var jogadorId in AvisosDoDiaDeJogo.JogadoresDa(partida))
                    {
                        await push.EnviarParaJogadorAsync(jogadorId,
                            "Sua quadra está atrasada",
                            QuadraAtrasada.Corpo(partida, agora),
                            $"/Torneios/Jogos/{torneioId}");
                    }
                    partida.AvisoAtrasoEnviadoEm = agora;
                }

                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation(
                    "Aviso de quadra atrasada: {Qtd} partida(s) do torneio {TorneioId}.",
                    atrasadas.Count, torneioId);
            }
        }
        catch (Exception ex)
        {
            // Na próxima tick tenta de novo; push é acessório e não pode derrubar o host.
            _logger.LogError(ex, "Falha ao verificar quadras atrasadas.");
        }
    }
}
