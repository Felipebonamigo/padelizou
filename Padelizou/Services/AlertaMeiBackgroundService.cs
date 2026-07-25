using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Vigia o faturamento anual da plataforma (soma das comissões confirmadas) contra o teto
// do MEI. Ao cruzar 70% e 90%, manda UM e-mail pros administradores raiz — o registro em
// AlertaSistema garante que cada aviso sai uma vez só por ano.
public class AlertaMeiBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertaMeiBackgroundService> _logger;

    public AlertaMeiBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<AlertaMeiBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var ano = DateTime.Now.Year;
            var inicioAno = new DateTime(ano, 1, 1);
            var teto = _configuration.GetValue<decimal?>("Mei:TetoAnual") ?? 81000m;

            var comissaoAno = await context.Pagamentos
                .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= inicioAno)
                .SumAsync(p => (decimal?)p.Comissao, stoppingToken) ?? 0;

            var percentual = teto > 0 ? comissaoAno / teto * 100 : 0;

            // Do maior pro menor: se já passou de 90%, o aviso de 90 é o que importa.
            var (tipo, limite) = percentual >= 90 ? ("MEI90", 90) : percentual >= 70 ? ("MEI70", 70) : (null, 0);
            if (tipo == null) return;

            var jaAvisou = await context.AlertasSistema
                .AnyAsync(a => a.Tipo == tipo && a.Ano == ano, stoppingToken);
            if (jaAvisou) return;

            var admins = await context.Jogadores
                .Where(j => j.IsAdminRaiz && j.Email != null)
                .ToListAsync(stoppingToken);

            var corpo = $@"
                <p>O faturamento da plataforma em {ano} chegou a <strong>{comissaoAno:C}</strong> —
                <strong>{percentual:F0}%</strong> do teto anual do MEI ({teto:C}).</p>
                {(limite >= 90
                    ? "<p><strong>Acima de 90%:</strong> hora de planejar a migração de MEI pra ME antes de estourar o limite.</p>"
                    : "<p>Acima de 70% — vale acompanhar de perto o ritmo dos próximos meses.</p>")}
                <p>Detalhes no painel: <a href='https://admin.padelizou.com.br/Admin/Metricas'>Métricas de uso</a>.</p>";

            foreach (var admin in admins)
            {
                await email.EnviarAsync(admin.Email!, admin.Nome,
                    $"Padelizou: faturamento chegou a {limite}% do teto do MEI", corpo);
            }

            context.AlertasSistema.Add(new AlertaSistema { Tipo = tipo, Ano = ano });
            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Alerta {Tipo} de {Ano} enviado pra {Qtd} admin(s).", tipo, ano, admins.Count);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima tick tenta de novo.
            _logger.LogError(ex, "Falha ao verificar o alerta de teto do MEI.");
        }
    }
}
