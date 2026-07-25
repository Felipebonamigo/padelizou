using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Cobrança pendente que passou do prazo vira "Expirado". Sem isso o extrato do organizador
// mostraria "aguardando pagamento" pra sempre, somando dinheiro que nunca vai entrar.
// A inscrição em si não é afetada: ela só nasce quando o pagamento confirma, então não há
// vaga presa a liberar aqui.
public class PagamentoExpiradoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PagamentoExpiradoBackgroundService> _logger;

    public PagamentoExpiradoBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<PagamentoExpiradoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ExpirarPendentesAsync(stoppingToken);
        }
    }

    private async Task ExpirarPendentesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();

            var agora = DateTime.Now;
            var vencidos = await context.Pagamentos
                .Where(p => p.Status == "Pendente" && p.ExpiraEm != null && p.ExpiraEm < agora)
                .ToListAsync(stoppingToken);

            if (vencidos.Count == 0) return;

            foreach (var pagamento in vencidos)
            {
                pagamento.Status = "Expirado";
            }

            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("{Total} cobrança(s) pendente(s) marcada(s) como expirada(s).", vencidos.Count);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima tick tenta de novo.
            _logger.LogError(ex, "Falha ao expirar pagamentos pendentes.");
        }
    }
}
