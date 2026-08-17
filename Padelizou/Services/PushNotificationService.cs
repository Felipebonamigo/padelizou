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
    private readonly PorteiroDaSaida _porteiro;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(DbPadelContext context, IOptions<VapidSettings> vapidOptions,
        IWhatsAppService whatsApp, FilaDeWhatsApp fila, FilaDeAvisos filaDeAvisos, IEmailService email,
        IOptions<SiteSettings> siteOptions, PorteiroDaSaida porteiro, ILogger<PushNotificationService> logger)
    {
        _context = context;
        _whatsApp = whatsApp;
        _fila = fila;
        _filaDeAvisos = filaDeAvisos;
        _email = email;
        _site = siteOptions.Value;
        _porteiro = porteiro;
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
        // PLACAR AO VIVO sai por um caminho À PARTE, mais curto: só o push, com a TAG que
        // substitui o aviso anterior do mesmo jogo. Sem este desvio, um jogo de 9 games viraria
        // 9 linhas na Caixa de Avisos e 9 e-mails — a régua dos outros canais (caixa sempre,
        // e-mail por padrão) foi pensada pra recado, não pra um placar que se atualiza sozinho.
        if (aviso.ApenasPush)
        {
            await EnviarPushAsync(aviso.JogadorId, aviso.Titulo, aviso.Corpo, aviso.Url, aviso.Tag);
            return;
        }

        // E-mail e push saem ANTES do return de quem não tem push, e de propósito: quem não
        // instalou o app não tem inscrição nenhuma, e é exatamente essa pessoa que os outros
        // canais existem pra alcançar. Ficam aqui, num lugar só, em vez de nos ~30 pontos que
        // mandam aviso — assim nenhum aviso novo nasce esquecendo um canal.
        //
        // O WhatsApp é a exceção: só vai quando o aviso PEDIU (ver AlcanceDoAviso). Ele tem
        // um custo que os outros não têm — a Meta restringe o número — e por isso é o único
        // canal onde o silêncio é o padrão.
        if (aviso.Alcance.VaiNoWhatsApp())
            await EnviarWhatsAppAsync(aviso.JogadorId, aviso.Titulo, aviso.Corpo, aviso.Url);

        // A CAIXA DE ENTRADA vem PRIMEIRO, e é o único canal que não pode falhar em silêncio.
        // Push depende de aparelho registrado (4 em 128), e-mail depende de cota do provedor,
        // WhatsApp depende de chip pareado — os três já falharam num dia só. A tela de
        // Notificações é o canal que não depende de entrega nenhuma: é só abrir o app.
        await GuardarNaCaixaDeEntradaAsync(aviso);

        // O e-mail é o único canal que um aviso pode dispensar (ver AlcanceDoAviso.AppSemEmail):
        // bilhete social não vale uma entrada na caixa de e-mail de ninguém.
        if (aviso.Alcance.VaiNoEmail())
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
    //
    // `tag` só existe pro placar ao vivo — o sw.js usa ela pra SUBSTITUIR a notificação
    // anterior do mesmo jogo (mesma tag) em vez de empilhar, e pra tocar o aparelho em
    // silêncio nas atualizações (ver wwwroot/sw.js). Nula, o comportamento de sempre.
    private async Task<int> EnviarPushAsync(int jogadorId, string titulo, string corpo, string? url,
        string? tag = null)
    {
        // O push é o único canal que não sabe pra QUEM está mandando: a inscrição é de um
        // aparelho. Então aqui a pergunta custa uma consulta — e ela só é feita quando o
        // ambiente restringe. Em produção a lista é vazia, o `Restringindo` é false e este
        // trecho inteiro não chega a tocar no banco.
        if (_porteiro.Restringindo && !await PodeSairParaOJogadorAsync(jogadorId))
        {
            _logger.LogInformation("Saída restrita: push pro jogador {JogadorId} NÃO foi enviado.", jogadorId);
            return 0;
        }

        var subscriptions = await _context.Set<PushSubscriptionJogador>()
            .Where(s => s.JogadorId == jogadorId)
            .ToListAsync();

        if (subscriptions.Count == 0) return 0;

        var payload = JsonSerializer.Serialize(new { title = titulo, body = corpo, url = url ?? "/", tag });
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

    // O dono do aparelho, pro porteiro. Qualquer um dos dois contatos serve: a lista do
    // ambiente é escrita com o que a pessoa tem à mão — às vezes o e-mail, às vezes o número.
    private async Task<bool> PodeSairParaOJogadorAsync(int jogadorId)
    {
        var dono = await _context.Jogadores
            .Where(j => j.Id == jogadorId)
            .Select(j => new { j.Email, j.Celular })
            .FirstOrDefaultAsync();

        return dono != null && _porteiro.PodeSairParaAlgum(dono.Email, dono.Celular);
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
            .Select(j => new { j.Email, j.Celular, j.NotificarWhatsApp })
            .FirstOrDefaultAsync();

        // O porteiro do ambiente é perguntado UMA vez, aqui, e vale pros dois canais: sem
        // isto a tela diria "o provedor recusou" pra uma mensagem que nem chegou a ser
        // tentada — e o admin iria caçar um problema de rede que não existe.
        var barradoPelaSaida = !_porteiro.PodeSairParaAlgum(jogador?.Email, jogador?.Celular);

        var push = ResultadoDoCanal.NaoPedido;
        var aparelhos = 0;

        if (porPush)
        {
            var cadastrados = await _context.Set<PushSubscriptionJogador>()
                .CountAsync(s => s.JogadorId == jogadorId);

            if (barradoPelaSaida)
            {
                push = ResultadoDoCanal.SaidaRestritaNesteAmbiente;
            }
            else if (cadastrados == 0)
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
            // servidor vem antes do que depende do jogador. A lista de saída é a mais de
            // servidor de todas — é uma linha do systemd —, então vem primeiro.
            if (barradoPelaSaida)
                whats = ResultadoDoCanal.SaidaRestritaNesteAmbiente;
            else if (!_whatsApp.Configurado)
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

    // Enfileira, não envia — mesmo motivo do EnviarParaJogadorAsync: quem marca um game não
    // pode ficar esperando N chamadas de push por seguidor antes da tela voltar. `ApenasPush`
    // é o que muda o caminho na entrega (ver EntregarAgoraAsync).
    public Task EnviarPlacarAoVivoAsync(int jogadorId, string titulo, string corpo, string url, string tag)
    {
        _filaDeAvisos.Enfileirar(new AvisoPendente(jogadorId, titulo, corpo, url, AlcanceDoAviso.SoApp,
            Tag: tag, ApenasPush: true));
        return Task.CompletedTask;
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
