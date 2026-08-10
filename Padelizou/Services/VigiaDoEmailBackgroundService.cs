using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// As regras do vigia, separadas do serviço pra poder conferir em teste sem subir host nenhum.
public static class VigiaDoEmail
{
    public static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan IntervaloEntreChecagens = TimeSpan.FromMinutes(5);

    // Uma falha solta é a rede piscando. Três no mesmo dia é canal quebrado — e foi assim que
    // 09/08 começou, com uma falha virando 130 sem ninguém olhar.
    public const int FalhasParaAvisar = 3;

    public const string TipoDoAlertaDeTeto = "EmailPertoDoTeto";
    public const string TipoDoAlertaDeFalha = "EmailFalhando";

    // Texto puro, e não HTML como nos outros vigias: estes avisos vão pro push e pra caixa de
    // entrada, que mostram texto. Escrever em HTML aqui obrigaria a arrancar as tags depois.
    public static string CorpoDoTeto(int noDia) =>
        $"Já saíram {noDia} e-mails hoje, de um teto de {TetoDoEmail.PorDia} por dia. "
        + "Nada se perdeu ainda, mas passando do teto o provedor recusa e o e-mail some sem "
        + "reenvio — e o primeiro a se perder é recuperação de senha, que sai a qualquer hora. "
        + "Se teve torneio grande sorteando chaves, era esperado; se não teve, vale olhar o log.";

    public static string CorpoDaFalha(int falhas, int enviados) =>
        $"{falhas} e-mails falharam hoje ({enviados} saíram normalmente). "
        + "As causas de sempre, em ordem: cota do dia estourada, chave de API revogada, "
        + "domínio que perdeu a verificação no provedor. "
        + "No log a linha é 'Falha ao enviar e-mail para', e ela diz pra quem cada um ia.";
}

// Vigia do canal de e-mail: conta quanto saiu no dia e chama o Felipe antes de o teto cortar.
//
// ⚠️ POR QUE EXISTE: em 09/08/2026 o disparo de "Novo torneio aberto" (87) estourou a cota do
// Gmail e **130 e-mails morreram calados** no resto do dia, duas recuperações de senha entre
// eles. O `EmailService` engole a falha de propósito — não pode quebrar a inscrição de quem
// clicou —, então o canal caiu inteiro sem um único alarme. É o mesmo silêncio de 03/08 no
// WhatsApp, que gerou o vigia de lá; este é o gêmeo.
//
// ⚠️ E ELE NÃO AVISA POR E-MAIL. Parece detalhe e é o coração da coisa: o alerta de que o e-mail
// está acabando não pode viajar pelo e-mail. Vai pela caixa de entrada de avisos e pelo push —
// os dois canais que não dependem do que está quebrado. O vigia do WhatsApp pode se dar ao luxo
// de mandar e-mail justamente porque o problema dele é o outro canal.
public class VigiaDoEmailBackgroundService : BackgroundService
{
    private readonly VolumeDoEmail _volume;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VigiaDoEmailBackgroundService> _logger;

    public VigiaDoEmailBackgroundService(VolumeDoEmail volume, IServiceScopeFactory scopeFactory,
        ILogger<VigiaDoEmailBackgroundService> logger)
    {
        _volume = volume;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(VigiaDoEmail.EsperaInicial, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;   // app desligando antes da primeira checagem
        }

        using var timer = new PeriodicTimer(VigiaDoEmail.IntervaloEntreChecagens);

        do
        {
            await VerificarAsync(DateTime.Now, stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    // `agora` vem por parâmetro, igual à varredura do lembrete de inscrição: é o que permite
    // conferir em teste um dia cheio sem esperar o relógio nem encher uma cota de verdade.
    public async Task VerificarAsync(DateTime agora, CancellationToken stoppingToken = default)
    {
        try
        {
            var noDia = _volume.NoDia(agora);
            var falhas = _volume.FalhasNoDia(agora);

            // Nada a dizer é o caso normal: não abre escopo, não toca o banco.
            if (!TetoDoEmail.PertoDoTeto(noDia) && falhas < VigiaDoEmail.FalhasParaAvisar) return;

            using var scope = _scopeFactory.CreateScope();

            if (TetoDoEmail.PertoDoTeto(noDia))
            {
                await AvisarAsync(scope, VigiaDoEmail.TipoDoAlertaDeTeto,
                    "O e-mail do site está perto do teto do dia",
                    VigiaDoEmail.CorpoDoTeto(noDia), agora, stoppingToken);
            }

            if (falhas >= VigiaDoEmail.FalhasParaAvisar)
            {
                await AvisarAsync(scope, VigiaDoEmail.TipoDoAlertaDeFalha,
                    "O e-mail do site parou de sair",
                    VigiaDoEmail.CorpoDaFalha(falhas, noDia), agora, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima passada tenta de novo.
            _logger.LogError(ex, "Falha ao vigiar a cota de e-mail.");
        }
    }

    private async Task AvisarAsync(IServiceScope scope, string tipo, string titulo, string corpo,
        DateTime agora, CancellationToken stoppingToken)
    {
        var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();

        // Um por DIA CIVIL, e não uma janela deslizante: o teto que ele vigia é diário, então
        // "já avisei hoje" é a pergunta certa. Sem isto seriam 12 avisos por hora, porque a
        // condição continua verdadeira até a virada da meia-noite.
        var jaAvisouHoje = await context.AlertasSistema
            .AnyAsync(a => a.Tipo == tipo && a.EnviadoEm.Date == agora.Date, stoppingToken);

        if (jaAvisouHoje) return;

        var admins = await context.Jogadores
            .Where(j => j.IsAdminRaiz && j.ExcluidoEm == null)
            .Select(j => j.Id)
            .ToListAsync(stoppingToken);

        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        foreach (var adminId in admins)
        {
            // ⚠️ AppSemEmail obrigatório aqui, não é economia de cota: um aviso sobre o e-mail
            // estar acabando, mandado por e-mail, gasta a cota que sobrou e provavelmente nem
            // chega. Caixa de entrada e push não dependem do canal quebrado.
            await push.EnviarParaJogadorAsync(adminId, titulo, corpo,
                "/Admin/Metricas", AlcanceDoAviso.AppSemEmail);
        }

        // ⚠️ `EnviadoEm` explícito, e não o `DateTime.Now` do valor padrão da propriedade: é
        // por ele que a trava de "já avisei hoje" pergunta, logo acima. Deixando o padrão, o
        // carimbo e a pergunta usam relógios diferentes — em produção coincidem, mas a varredura
        // deixa de ser exercitável num dia que não seja hoje, e foi o teste que mostrou isso.
        context.AlertasSistema.Add(new AlertaSistema { Tipo = tipo, Ano = agora.Year, EnviadoEm = agora });
        await context.SaveChangesAsync(stoppingToken);

        _logger.LogWarning("{Titulo} — avisei {Qtd} admin(s) pela caixa de avisos.", titulo, admins.Count);
    }
}
