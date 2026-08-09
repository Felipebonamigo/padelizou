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
    private readonly FilaDeWhatsApp _fila;
    private readonly FilaDeAvisos _filaDeAvisos;
    private readonly IEmailService _email;
    private readonly SiteSettings _site;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(DbPadelContext context, IOptions<VapidSettings> vapidOptions,
        IWhatsAppService whatsApp, FilaDeWhatsApp fila, FilaDeAvisos filaDeAvisos, IEmailService email,
        IOptions<SiteSettings> siteOptions, ILogger<PushNotificationService> logger)
    {
        _context = context;
        _whatsApp = whatsApp;
        _fila = fila;
        _filaDeAvisos = filaDeAvisos;
        _email = email;
        _site = siteOptions.Value;
        _logger = logger;
        var settings = vapidOptions.Value;
        _vapidDetails = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);
    }

    // ENFILEIRA, NÃO ENTREGA. A entrega sai logo atrás, no
    // EntregadorDeAvisosBackgroundService — ver Services/FilaDeAvisos pro porquê.
    //
    // Em resumo: cada aviso abre uma conexão SMTP e uma chamada de push por aparelho, e isso
    // rodava DENTRO da ação que gerou o aviso. Finalizar um jogo avisa 4 jogadores mais os
    // seguidores deles, então o organizador ficava olhando o botão girar no meio da quadra —
    // e tocava de novo, o que fazia o mesmo jogo subir duas vezes.
    //
    // ⚠️ É `Task.CompletedTask` de propósito: nada aqui espera rede. Se um dia esta função
    // voltar a fazer I/O, o defeito volta junto.
    public Task EnviarParaJogadorAsync(int jogadorId, string titulo, string corpo, string? url = null,
        AlcanceDoAviso alcance = AlcanceDoAviso.SoApp)
    {
        _filaDeAvisos.Enfileirar(new AvisoPendente(jogadorId, titulo, corpo, url, alcance));
        return Task.CompletedTask;
    }

    // A entrega de verdade. Público porque quem chama é o serviço de fundo; ninguém mais
    // deveria chamar isto direto — de dentro de uma requisição, use EnviarParaJogadorAsync.
    public async Task EntregarAgoraAsync(AvisoPendente aviso)
    {
        // E-mail e push saem ANTES do return de quem não tem push, e de propósito: quem não
        // instalou o app não tem inscrição nenhuma, e é exatamente essa pessoa que os outros
        // canais existem pra alcançar. Ficam aqui, num lugar só, em vez de nos ~30 pontos que
        // mandam aviso — assim nenhum aviso novo nasce esquecendo um canal.
        //
        // O WhatsApp é a exceção: só vai quando o aviso PEDIU (ver AlcanceDoAviso). Ele tem
        // um custo que os outros não têm — a Meta restringe o número — e por isso é o único
        // canal onde o silêncio é o padrão.
        if (aviso.Alcance == AlcanceDoAviso.AppEWhatsApp)
            await EnviarWhatsAppAsync(aviso.JogadorId, aviso.Titulo, aviso.Corpo, aviso.Url);

        // A CAIXA DE ENTRADA vem PRIMEIRO, e é o único canal que não pode falhar em silêncio.
        // Push depende de aparelho registrado (4 em 128), e-mail depende de cota do provedor,
        // WhatsApp depende de chip pareado — os três já falharam num dia só. A tela de
        // Notificações é o canal que não depende de entrega nenhuma: é só abrir o app.
        await GuardarNaCaixaDeEntradaAsync(aviso);

        // O e-mail é o único canal que um aviso pode dispensar (ver AlcanceDoAviso.AppSemEmail):
        // bilhete social não vale uma entrada na caixa de e-mail de ninguém.
        if (aviso.Alcance != AlcanceDoAviso.AppSemEmail)
            await EnviarEmailAsync(aviso.JogadorId, aviso.Titulo, aviso.Corpo, aviso.Url);

        await EnviarPushAsync(aviso.JogadorId, aviso.Titulo, aviso.Corpo, aviso.Url);
    }

    // Guarda o aviso na tela de Notificações do jogador.
    //
    // ⚠️ Falha aqui NÃO pode derrubar os outros canais — mesma regra do e-mail e do WhatsApp.
    // Um erro de banco não pode impedir o push de sair; o aviso na mão da pessoa vale mais
    // que a linha no histórico.
    private async Task GuardarNaCaixaDeEntradaAsync(AvisoPendente aviso)
    {
        try
        {
            _context.AvisosDoJogador.Add(new AvisoDoJogador
            {
                JogadorId = aviso.JogadorId,
                Titulo = aviso.Titulo,
                Corpo = aviso.Corpo,
                Url = aviso.Url,
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Não consegui guardar o aviso do jogador {JogadorId} na caixa de entrada.",
                aviso.JogadorId);
        }
    }

    // Falha aqui não pode derrubar o aviso, mesmo motivo do WhatsApp: SMTP fora do ar não
    // pode fazer a inscrição estourar na cara de quem clicou.
    private async Task EnviarEmailAsync(int jogadorId, string titulo, string corpo, string? url)
    {
        try
        {
            var destinatario = await _context.Jogadores
                .Where(j => j.Id == jogadorId)
                .Select(j => new { j.Nome, j.Email, j.NotificarEmail })
                .FirstOrDefaultAsync();

            if (destinatario == null) return;
            if (!AvisoPorEmail.PodeReceber(destinatario.NotificarEmail, destinatario.Email)) return;

            await _email.EnviarAsync(destinatario.Email!, destinatario.Nome, titulo,
                AvisoPorEmail.MontarHtml(titulo, corpo, url, _site.Url));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao avisar por e-mail o jogador {JogadorId}.", jogadorId);
        }
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

    // Enfileira, não envia. Quem entrega é o EntregadorDeWhatsAppBackgroundService, uma
    // mensagem de cada vez com respiro entre elas — a rajada é o que a Meta pune, não o total.
    //
    // Falha aqui não pode derrubar o aviso: o push é o canal principal, e um problema no canal
    // de mensagem não pode fazer a ação que gerou o aviso estourar na cara de quem clicou.
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

            _fila.Enfileirar(destinatario.Celular!,
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
