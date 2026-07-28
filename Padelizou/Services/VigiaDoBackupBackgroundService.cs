using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Manda e-mail pros administradores quando o backup fora do servidor para de acontecer.
//
// Por que dentro do app e não num script de shell: o e-mail já funciona aqui, testado, com a
// senha do SMTP num lugar só. Um script separado precisaria de uma segunda cópia dessa senha —
// mais um arquivo pra vazar, pra desatualizar e pra esquecer.
//
// O aviso sai UMA vez por semana, não todo dia: um alerta que se repete vira ruído e a pessoa
// aprende a ignorar. `AlertaSistema` guarda o que já foi avisado, mesmo que o app reinicie.
public class VigiaDoBackupBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(6);
    private static readonly TimeSpan IntervaloEntreAvisos = TimeSpan.FromDays(7);

    private const string TipoDoAlerta = "BackupParado";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VigiaDoBackupBackgroundService> _logger;

    public VigiaDoBackupBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<VigiaDoBackupBackgroundService> logger)
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
            // Só o ambiente que faz backup vigia backup. Sem isto, o dev mandaria e-mail de
            // "backup parado" todo dia — ele nunca teve backup, e não precisa ter.
            var caminho = _configuration["Backup:ArquivoDeUltimoSucesso"];
            if (string.IsNullOrWhiteSpace(caminho)) return;

            var agora = DateTime.Now;
            var ultimo = VigiaDoBackup.LerUltimoSucesso(caminho);
            if (!VigiaDoBackup.PrecisaAvisar(ultimo, agora)) return;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // Não repete o aviso antes de uma semana.
            var avisoRecente = await context.AlertasSistema
                .Where(a => a.Tipo == TipoDoAlerta)
                .OrderByDescending(a => a.EnviadoEm)
                .FirstOrDefaultAsync(stoppingToken);

            if (avisoRecente != null && agora - avisoRecente.EnviadoEm < IntervaloEntreAvisos) return;

            var admins = await context.Jogadores
                .Where(j => j.IsAdminRaiz && j.Email != null)
                .ToListAsync(stoppingToken);

            var corpo = $@"
                <p>A cópia do backup pro Google Drive <strong>parou de acontecer</strong>:
                {VigiaDoBackup.DescreverAtraso(ultimo, agora)}.</p>
                <p>O backup local no servidor provavelmente continua rodando — o que falhou é a
                cópia <strong>fora</strong> dele, que é justamente a que serve se o servidor morrer.</p>
                <p>Causas comuns, em ordem: a autorização do Google expirou (a tela de consentimento
                em modo Teste derruba o acesso a cada 7 dias), o disco encheu, ou a pasta perdeu
                permissão de escrita.</p>
                <p>Pra ver o motivo:<br/>
                <code>ssh root@179.197.233.184 ""tail -30 /var/log/padelizou-backup-drive.log""</code></p>";

            foreach (var admin in admins)
            {
                await email.EnviarAsync(admin.Email!, admin.Nome,
                    "Padelizou: o backup fora do servidor parou", corpo);
            }

            context.AlertasSistema.Add(new AlertaSistema { Tipo = TipoDoAlerta, Ano = agora.Year });
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogWarning("Backup parado ({Atraso}) — avisei {Qtd} admin(s).",
                VigiaDoBackup.DescreverAtraso(ultimo, agora), admins.Count);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima tick tenta de novo.
            _logger.LogError(ex, "Falha ao verificar se o backup continua acontecendo.");
        }
    }
}
