using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Cobrança pendente que passou do prazo vira "Expirado". Sem isso o extrato do organizador
// mostraria "aguardando pagamento" pra sempre, somando dinheiro que nunca vai entrar.
// A inscrição em si não é afetada: ela só nasce quando o pagamento confirma, então não há
// vaga presa a liberar aqui.
// Também manda o lembrete de "sua cobrança está pra vencer" (push + e-mail, uma vez só)
// quando faltam menos de 6 horas pro prazo — mesmo varredor, mesma tabela.
public class PagamentoExpiradoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AntecedenciaLembrete = TimeSpan.FromHours(6);

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
            await LembrarPendentesAsync(stoppingToken);
        }
    }

    private async Task LembrarPendentesAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var agora = DateTime.Now;
            var limite = agora.Add(AntecedenciaLembrete);
            var paraLembrar = await context.Pagamentos
                .Where(p => p.Status == "Pendente"
                    && p.LembreteEnviadoEm == null
                    && p.ExpiraEm != null && p.ExpiraEm > agora && p.ExpiraEm <= limite)
                .ToListAsync(stoppingToken);

            if (paraLembrar.Count == 0) return;

            foreach (var pagamento in paraLembrar)
            {
                var horas = Math.Max(1, (int)Math.Round((pagamento.ExpiraEm!.Value - agora).TotalHours));
                var texto = $"Sua inscrição de {pagamento.Valor:C} expira em ~{horas}h. Pague pra garantir a vaga!";

                // ⚠️ SÓ ENFILEIRAR — o e-mail sai DAQUI TAMBÉM. Este método mandava e-mail por
                // fora, direto pelo IEmailService, e o resultado eram DOIS e-mails do mesmo
                // aviso: a fila entrega caixa de entrada + e-mail + push num lugar só
                // (PushNotificationService.EntregarAgoraAsync). O envio inline sobrou de quando
                // o funil ainda não existia. Mesmo motivo pelo qual DuplasController não tem
                // IEmailService.
                await push.EnviarParaJogadorAsync(pagamento.JogadorId,
                    // Tem hora pra vencer e custa a vaga. Aviso que chega tarde aqui é o mesmo
                    // que não ter chegado.
                    // Caminho relativo: os avisos por e-mail e WhatsApp já sabem colar o
                    // domínio na frente (AvisoPorEmail.LinkAbsoluto).
                    "Pagamento pendente", texto, LinkDoPagamento.Para(pagamento), AlcanceDoAviso.AppEWhatsApp);

                pagamento.LembreteEnviadoEm = agora;
            }

            await context.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("{Total} lembrete(s) de cobrança enviado(s).", paraLembrar.Count);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima tick tenta de novo.
            _logger.LogError(ex, "Falha ao enviar lembretes de cobrança.");
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
