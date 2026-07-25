using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Não existia nenhum job de background no projeto — este é o primeiro. Roda periodicamente e manda,
// via Z-API, um lembrete de WhatsApp pros mensalistas que ainda não confirmaram presença no jogo
// fixo da semana, quando faltam menos de 24h e o organizador ativou a opção em Configuracoes.
public class LembreteJogoBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LembreteJogoBackgroundService> _logger;

    public LembreteJogoBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LembreteJogoBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloTick);

        // Roda uma vez logo na subida do app, sem esperar o primeiro tick do timer.
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
            var sessaoGrupoService = scope.ServiceProvider.GetRequiredService<ISessaoGrupoService>();
            var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
            var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            var grupos = await context.GruposPrivados
                .Include(g => g.Clube)
                .Where(g => g.EnviarLembrete24h && g.DiaSemanaFixo != null && g.HorarioFixo != null)
                .ToListAsync(stoppingToken);

            foreach (var grupo in grupos)
            {
                await ProcessarGrupoAsync(grupo, context, sessaoGrupoService, whatsAppService, pushService, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar lembretes de 24h.");
        }
    }

    private async Task ProcessarGrupoAsync(
        GrupoPrivado grupo, DbPadelContext context, ISessaoGrupoService sessaoGrupoService,
        IWhatsAppService whatsAppService, IPushNotificationService pushService, CancellationToken stoppingToken)
    {
        var sessao = await sessaoGrupoService.ObterOuCriarSessaoAsync(grupo, null);

        var agora = DateTime.Now;
        if (agora < sessao.DataHora.AddHours(-24) || agora >= sessao.DataHora)
        {
            return; // fora da janela de 24h (cedo demais ou o jogo já começou/passou)
        }

        var pendentes = sessao.Confirmacoes
            .Where(c => !c.Avulso && c.LembreteEnviadoEm == null
                     && c.Jogador.AceitaConvitesJogo && !string.IsNullOrEmpty(c.Jogador.Celular))
            .ToList();

        foreach (var confirmacao in pendentes)
        {
            var mensagem = $"Fala {confirmacao.Jogador.Nome}! Confirma se você vai no nosso jogo fixo " +
                           $"em {(grupo.Clube?.Nome ?? "nosso clube")} dia {sessao.DataHora:dd/MM 'às' HH:mm}? " +
                           $"Entra no app pra confirmar sua presença.";

            var enviado = await whatsAppService.EnviarAsync(confirmacao.Jogador.Celular, mensagem);
            if (enviado)
            {
                confirmacao.LembreteEnviadoEm = agora;
            }

            await pushService.EnviarParaJogadorAsync(
                confirmacao.JogadorId,
                "Jogo em 24h!",
                $"Confirma presença no jogo fixo dia {sessao.DataHora:dd/MM 'às' HH:mm}.",
                "/Agenda");
        }

        if (pendentes.Any())
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
