using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using WebPush;

namespace Padelizou.Services;

public class PushNotificationService : IPushNotificationService
{
    private readonly DbPadelContext _context;
    private readonly VapidDetails _vapidDetails;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(DbPadelContext context, IOptions<VapidSettings> vapidOptions, ILogger<PushNotificationService> logger)
    {
        _context = context;
        _logger = logger;
        var settings = vapidOptions.Value;
        _vapidDetails = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);
    }

    public async Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null)
    {
        var subscriptions = await _context.Set<PushSubscriptionJogador>()
            .Where(s => s.JogadorId == jogadorId)
            .ToListAsync();

        if (subscriptions.Count == 0) return;

        var payload = JsonSerializer.Serialize(new { title = titulo, body = corpo, url = url ?? "/" });
        var client = new WebPushClient();

        foreach (var sub in subscriptions)
        {
            var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            try
            {
                await client.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                _context.Remove(sub); // inscrição expirada/revogada pelo navegador
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar push pro jogador {JogadorId}.", jogadorId);
            }
        }

        await _context.SaveChangesAsync();
    }
}
