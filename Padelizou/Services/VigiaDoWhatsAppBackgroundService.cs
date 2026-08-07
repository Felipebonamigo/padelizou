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

            // O relógio do aquecimento começa quando o canal CONECTA de verdade, não quando
            // alguém mexe no systemd: entre religar a configuração e alguém ler o QR podem
            // passar dias, e nesses dias o número não enviou nada — não esquentou nada.
            // Grava uma vez só (ver VolumeDoWhatsApp.MarcarConectadoAsync).
            if (estado == EstadoDoCanal.Conectado)
            {
                var volume = scope.ServiceProvider.GetRequiredService<VolumeDoWhatsApp>();
                var contexto = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
                await volume.MarcarConectadoAsync(contexto, DateTime.Now);
            }

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
