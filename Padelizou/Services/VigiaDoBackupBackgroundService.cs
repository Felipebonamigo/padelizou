using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Avisa os administradores quando o backup fora do servidor para de acontecer.
//
// Por que dentro do app e não num script de shell: o aviso já funciona aqui, testado, com as
// credenciais num lugar só. Um script separado precisaria de uma segunda cópia delas — mais um
// arquivo pra vazar, pra desatualizar e pra esquecer.
//
// ⚠️ O aviso vai pela FILA (push + e-mail), e não por SMTP direto: em 07/08/2026 a cota diária
// do Gmail estourou, e um alerta que depende de um único canal fora do ar é igual a não ter
// alerta. Pelo mesmo motivo o estado também aparece SEMPRE no painel de métricas — lá ele não
// depende de entrega nenhuma, é só abrir a tela.
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

    // Espera curta antes da primeira checagem: dá tempo do app terminar de subir, e faz com que
    // um restart valide o vigia em minutos em vez de horas. Vigia que só se manifesta 6h depois
    // de subir é vigia que ninguém consegue testar — e no que não se testa, não se confia.
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(EsperaInicial, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;   // app desligando antes da primeira checagem
        }

        await VerificarAsync(stoppingToken);

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
            var avisos = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            // Não repete o aviso antes de uma semana.
            var avisoRecente = await context.AlertasSistema
                .Where(a => a.Tipo == TipoDoAlerta)
                .OrderByDescending(a => a.EnviadoEm)
                .FirstOrDefaultAsync(stoppingToken);

            if (avisoRecente != null && agora - avisoRecente.EnviadoEm < IntervaloEntreAvisos) return;

            // Sem exigir e-mail cadastrado: a fila resolve o canal de cada um, e quem só tem o
            // app instalado recebe por push. Filtrar por e-mail aqui deixaria de fora justamente
            // quem seria alcançado quando o SMTP é o que está fora do ar.
            var admins = await context.Jogadores
                .Where(j => j.IsAdminRaiz && j.ExcluidoEm == null)
                .Select(j => j.Id)
                .ToListAsync(stoppingToken);

            var corpo = $"A cópia do backup pro Google Drive parou de acontecer: "
                      + $"{VigiaDoBackup.DescreverAtraso(ultimo, agora)}. "
                      + "O backup DENTRO do servidor provavelmente continua rodando — o que falhou "
                      + "é a cópia FORA dele, que é a que serve se o servidor morrer. "
                      + "A causa mais comum é a autorização do Google ter expirado. "
                      + "Conserto: rclone config reconnect padelizou-drive:";

            foreach (var adminId in admins)
            {
                await avisos.EnviarParaJogadorAsync(adminId,
                    "O backup fora do servidor parou", corpo, "/Admin/Metricas");
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
