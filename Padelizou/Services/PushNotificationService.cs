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
    private readonly IWhatsAppService _whatsApp;
    private readonly SiteSettings _site;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(DbPadelContext context, IOptions<VapidSettings> vapidOptions,
        IWhatsAppService whatsApp, IOptions<SiteSettings> siteOptions,
        ILogger<PushNotificationService> logger)
    {
        _context = context;
        _whatsApp = whatsApp;
        _site = siteOptions.Value;
        _logger = logger;
        var settings = vapidOptions.Value;
        _vapidDetails = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);
    }

    public async Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null)
    {
        // O WhatsApp sai ANTES do return de quem não tem push, e de propósito: quem não
        // instalou o app não tem inscrição nenhuma, e é exatamente essa pessoa que o canal
        // novo existe pra alcançar. Fica aqui, num lugar só, em vez de nos ~30 pontos que
        // mandam aviso — assim nenhum aviso novo nasce esquecendo o WhatsApp.
        await EnviarWhatsAppAsync(jogadorId, titulo, corpo, url);
        await EnviarPushAsync(jogadorId, titulo, corpo, url);
    }

    private async Task EnviarPushAsync(int jogadorId, string titulo, string corpo, string? url)
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

    // Falha aqui não pode derrubar o aviso: o push é o canal principal, e uma indisponibilidade
    // do provedor de mensagem não pode fazer o jogador deixar de ser notificado (nem a ação que
    // gerou o aviso estourar na cara de quem clicou).
    private async Task EnviarWhatsAppAsync(int jogadorId, string titulo, string corpo, string? url)
    {
        try
        {
            var destinatario = await _context.Jogadores
                .Where(j => j.Id == jogadorId)
                .Select(j => new { j.Celular, j.NotificarWhatsApp })
                .FirstOrDefaultAsync();

            if (destinatario == null) return;
            if (!AvisoPorWhatsApp.PodeReceber(destinatario.NotificarWhatsApp, destinatario.Celular)) return;

            await _whatsApp.EnviarAsync(destinatario.Celular,
                AvisoPorWhatsApp.Montar(titulo, corpo, url, _site.Url));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao avisar por WhatsApp o jogador {JogadorId}.", jogadorId);
        }
    }

    // Só push, sem WhatsApp — de propósito. O único uso disto é o botão de notificação de
    // TESTE do painel, cujo texto fala do app instalado. Mandar um teste no celular de todo
    // mundo é o tipo de aviso que faz a pessoa desligar o canal (e ainda custa envio).
    // Aviso de verdade pra muita gente passa pelo EnviarParaJogadorAsync, um a um.
    public async Task<int> EnviarParaTodosInscritosAsync(string titulo, string corpo, string? url = null)
    {
        var jogadorIds = await _context.Set<PushSubscriptionJogador>()
            .Select(s => s.JogadorId)
            .Distinct()
            .ToListAsync();

        foreach (var jogadorId in jogadorIds)
            await EnviarPushAsync(jogadorId, titulo, corpo, url);

        return jogadorIds.Count;
    }
}
