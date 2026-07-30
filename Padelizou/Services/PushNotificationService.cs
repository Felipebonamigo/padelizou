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

    // Devolve em QUANTOS aparelhos a entrega deu certo. O envio normal ignora o número (é
    // "tenta e segue a vida"), mas o teste do painel precisa dele: dizer "enviado" só porque
    // existe aparelho cadastrado seria mentir justamente na tela feita pra conferir a verdade.
    private async Task<int> EnviarPushAsync(int jogadorId, string titulo, string corpo, string? url)
    {
        var subscriptions = await _context.Set<PushSubscriptionJogador>()
            .Where(s => s.JogadorId == jogadorId)
            .ToListAsync();

        if (subscriptions.Count == 0) return 0;

        var payload = JsonSerializer.Serialize(new { title = titulo, body = corpo, url = url ?? "/" });
        var client = new WebPushClient();
        var entregues = 0;

        foreach (var sub in subscriptions)
        {
            var pushSubscription = new PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
            try
            {
                await client.SendNotificationAsync(pushSubscription, payload, _vapidDetails);
                entregues++;
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
        return entregues;
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

    public async Task<ResultadoTesteNotificacao> EnviarTesteAsync(int jogadorId, bool porPush,
        bool porWhatsApp, string titulo, string corpo, string? url = null)
    {
        var jogador = await _context.Jogadores
            .Where(j => j.Id == jogadorId)
            .Select(j => new { j.Celular, j.NotificarWhatsApp })
            .FirstOrDefaultAsync();

        var push = ResultadoDoCanal.NaoPedido;
        var aparelhos = 0;

        if (porPush)
        {
            var cadastrados = await _context.Set<PushSubscriptionJogador>()
                .CountAsync(s => s.JogadorId == jogadorId);

            if (cadastrados == 0)
            {
                push = ResultadoDoCanal.SemAppInstalado;
            }
            else
            {
                // Conta ENTREGAS, não aparelhos cadastrados: inscrição que o navegador já
                // revogou continua na tabela até a primeira tentativa depois disso.
                aparelhos = await EnviarPushAsync(jogadorId, titulo, corpo, url);
                push = aparelhos > 0 ? ResultadoDoCanal.Enviado : ResultadoDoCanal.FalhouNoEnvio;
            }
        }

        var whats = ResultadoDoCanal.NaoPedido;
        string? numero = null;

        if (porWhatsApp)
        {
            // A ordem das recusas é a ordem em que o admin consegue AGIR: o que ele muda no
            // servidor vem antes do que depende do jogador.
            if (!_whatsApp.Configurado)
                whats = ResultadoDoCanal.CanalDesligadoNesteAmbiente;
            else if (jogador == null || !jogador.NotificarWhatsApp)
                whats = ResultadoDoCanal.PreferenciaDesmarcada;
            else if (!AvisoPorWhatsApp.PodeReceber(true, jogador.Celular))
                whats = ResultadoDoCanal.SemNumeroNoCadastro;
            else
            {
                var texto = AvisoPorWhatsApp.Montar(titulo, corpo, url, _site.Url);
                whats = await _whatsApp.EnviarAsync(jogador.Celular, texto)
                    ? ResultadoDoCanal.Enviado
                    : ResultadoDoCanal.FalhouNoEnvio;
                numero = WhatsAppLinkHelper.Formatar(jogador.Celular);
            }
        }

        return new ResultadoTesteNotificacao
        {
            Push = push,
            Aparelhos = aparelhos,
            WhatsApp = whats,
            Numero = whats == ResultadoDoCanal.Enviado ? numero : null,
        };
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
