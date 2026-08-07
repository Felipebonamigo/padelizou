using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Vigia do canal de WhatsApp: confere de 5 em 5 minutos se o chip continua pareado, religa
// sozinho quando dá, e só chama o Felipe quando o conserto exige gente.
//
// Por que existe: em 03/08/2026 o canal caiu às 18h36 e ficou fora 17 horas. ~200 avisos
// falharam, um a um, cada um escrevendo no log — e ninguém soube. A queda não foi o problema
// (um restart resolveu, sem QR); o problema foi o SILÊNCIO. E o canal não é um extra: a
// grande maioria das contas só é alcançável por ele.
//
// O estado da última checagem fica em memória (`UltimoEstado`) porque o painel mostra o selo:
// perguntar pra Evolution a cada carregamento do /Admin colocaria uma chamada de rede no
// caminho de uma tela.
public class VigiaDoWhatsAppBackgroundService : BackgroundService
{
    private const string TipoDoAlerta = "WhatsAppFora";

    // Alerta separado do de canal fora, e por isso tipo próprio: são problemas opostos (o canal
    // está de pé, mandando DEMAIS) e cada um tem a sua janela de silêncio.
    private const string TipoDoAlertaDeTeto = "WhatsAppPertoDoTeto";

    // O que a última checagem viu. `Desligado` é o começo honesto: antes da primeira passada
    // não sabemos nada, e no dev nunca vamos saber outra coisa.
    public static EstadoDoCanal UltimoEstado { get; private set; } = EstadoDoCanal.Desligado;
    public static DateTime? UltimaChecagem { get; private set; }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VigiaDoWhatsAppBackgroundService> _logger;

    public VigiaDoWhatsAppBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<VigiaDoWhatsAppBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(VigiaDoWhatsApp.EsperaInicial, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;   // app desligando antes da primeira checagem
        }

        await VerificarAsync(stoppingToken);

        using var timer = new PeriodicTimer(VigiaDoWhatsApp.IntervaloEntreChecagens);

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
            var whats = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();

            var estado = await whats.ConsultarEstadoAsync();
            UltimoEstado = estado;
            UltimaChecagem = DateTime.Now;

            if (estado == EstadoDoCanal.Desligado) return;   // dev/localhost: não é falha

            // Perto do teto o Felipe é avisado ANTES de perder aviso. Sem isto ele só
            // descobriria pelo contador de barradas do painel — ou seja, depois de a mensagem
            // já ter sido descartada. Vale só com o canal de pé: canal fora já tem alarme
            // próprio, e dois e-mails sobre o mesmo sábado ruim viram ruído.
            if (estado == EstadoDoCanal.Conectado)
                await AvisarSePertoDoTetoAsync(scope, stoppingToken);

            var tentouReligar = false;

            if (VigiaDoWhatsApp.Decidir(estado, tentouReligar) == AcaoDoVigia.TentarReligar)
            {
                _logger.LogWarning("Canal de WhatsApp fora ({Estado}) — tentando religar sozinho.", estado);

                tentouReligar = true;
                await whats.ReligarAsync();

                // O socket não sobe na hora: sem esta espera, a checagem seguinte pegaria o
                // canal ainda em pé de subida e o Felipe receberia e-mail de um problema que
                // se resolveu dez segundos depois.
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

                estado = await whats.ConsultarEstadoAsync();
                UltimoEstado = estado;
                UltimaChecagem = DateTime.Now;

                if (estado == EstadoDoCanal.Conectado)
                {
                    _logger.LogWarning("Canal de WhatsApp religado sozinho — nenhum aviso perdido daqui pra frente.");
                    return;
                }
            }

            if (VigiaDoWhatsApp.Decidir(estado, tentouReligar) != AcaoDoVigia.ChamarOFelipe) return;

            await AvisarAdministradoresAsync(scope, estado, tentouReligar, stoppingToken);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima passada tenta de novo.
            _logger.LogError(ex, "Falha ao vigiar o canal de WhatsApp.");
        }
    }

    // O canal está de pé e mandando perto do limite. Não é falha — é aviso de que o próximo
    // torneio do mesmo dia pode começar a perder mensagem.
    private async Task AvisarSePertoDoTetoAsync(IServiceScope scope, CancellationToken stoppingToken)
    {
        var volume = scope.ServiceProvider.GetRequiredService<VolumeDoWhatsApp>();
        var agora = DateTime.Now;

        if (!volume.PertoDoTeto(agora)) return;

        var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();

        var avisoRecente = await context.AlertasSistema
            .Where(a => a.Tipo == TipoDoAlertaDeTeto)
            .OrderByDescending(a => a.EnviadoEm)
            .FirstOrDefaultAsync(stoppingToken);

        if (avisoRecente != null && agora - avisoRecente.EnviadoEm < VigiaDoWhatsApp.IntervaloEntreAvisos)
            return;

        var naHora = volume.NaUltimaHora(agora);
        var noDia = volume.NoDia(agora);

        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var admins = await context.Jogadores
            .Where(j => j.IsAdminRaiz && j.Email != null)
            .ToListAsync(stoppingToken);

        var corpo = $@"
            <p>O WhatsApp do Padelizou está perto do teto: <strong>{naHora} de {TetoDoWhatsApp.PorHora}
            na última hora</strong> e <strong>{noDia} de {TetoDoWhatsApp.PorDia} hoje</strong>.</p>
            <p>Nada foi perdido ainda. Passando do teto, a mensagem é <strong>descartada e não
            reenviada</strong> — e num dia de torneio o que se perde é justamente o
            ""seu jogo é o próximo"".</p>
            <p>Se hoje é dia de torneio grande e isso era esperado, ignore. Se não era, vale olhar
            o log: volume assim sem torneio no ar costuma ser aviso saindo em laço.</p>";

        foreach (var admin in admins)
            await email.EnviarAsync(admin.Email!, admin.Nome, "Padelizou: o WhatsApp está perto do teto", corpo);

        context.AlertasSistema.Add(new AlertaSistema { Tipo = TipoDoAlertaDeTeto, Ano = agora.Year });
        await context.SaveChangesAsync(stoppingToken);

        _logger.LogWarning("WhatsApp perto do teto ({NaHora}/{TetoHora} na hora, {NoDia}/{TetoDia} "
            + "no dia) — avisei {Qtd} admin(s).",
            naHora, TetoDoWhatsApp.PorHora, noDia, TetoDoWhatsApp.PorDia, admins.Count);
    }

    private async Task AvisarAdministradoresAsync(IServiceScope scope, EstadoDoCanal estado,
        bool tentouReligar, CancellationToken stoppingToken)
    {
        var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var agora = DateTime.Now;

        // Não repete o aviso antes da janela. O canal fica fora até alguém agir, e a checagem
        // é de 5 em 5 minutos — sem esta trava seriam 12 e-mails por hora.
        var avisoRecente = await context.AlertasSistema
            .Where(a => a.Tipo == TipoDoAlerta)
            .OrderByDescending(a => a.EnviadoEm)
            .FirstOrDefaultAsync(stoppingToken);

        if (avisoRecente != null && agora - avisoRecente.EnviadoEm < VigiaDoWhatsApp.IntervaloEntreAvisos)
            return;

        var admins = await context.Jogadores
            .Where(j => j.IsAdminRaiz && j.Email != null)
            .ToListAsync(stoppingToken);

        var corpo = VigiaDoWhatsApp.CorpoDoEmail(estado, tentouReligar);

        foreach (var admin in admins)
        {
            await email.EnviarAsync(admin.Email!, admin.Nome,
                "Padelizou: o WhatsApp parou de enviar", corpo);
        }

        context.AlertasSistema.Add(new AlertaSistema { Tipo = TipoDoAlerta, Ano = agora.Year });
        await context.SaveChangesAsync(stoppingToken);

        _logger.LogError("Canal de WhatsApp fora ({Estado}) e não voltou sozinho — avisei {Qtd} admin(s).",
            estado, admins.Count);
    }
}
