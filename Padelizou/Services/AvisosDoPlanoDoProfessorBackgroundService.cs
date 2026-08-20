using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;

namespace Padelizou.Services;

// Avisa o professor quando ele está prestes a perder (ou já perdeu) a taxa menor por aula —
// tanto pelo fim do teste quanto pelo vencimento da assinatura. A regra de QUEM e QUANDO mora
// em Services/AvisosDoPlanoDoProfessor; aqui só se varre o banco e se enfileira o aviso.
//
// Irmão do LembreteInscricaoNaoPagaBackgroundService, e de propósito: mesmo formato de tick,
// mesma hora civilizada, mesma coluna decidindo se já avisou. O que muda é de onde parte a
// pergunta — lá é a inscrição de alguém, aqui é o que sustenta a taxa menor do professor.
public class AvisosDoPlanoDoProfessorBackgroundService : BackgroundService
{
    // De hora em hora, como o irmão: os marcos são em DIAS e quem decide se já avisou é a
    // coluna. O tick curto só garante pegar a primeira hora civilizada do dia sem depender de
    // o processo ter subido às 9h em ponto.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AvisosDoPlanoDoProfessorBackgroundService> _logger;

    public AvisosDoPlanoDoProfessorBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<AvisosDoPlanoDoProfessorBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        // Uma varredura já na subida: publicar de manhã não pode custar a janela do dia.
        await UmaVarreduraAsync(stoppingToken);

        using var timer = new PeriodicTimer(IntervaloTick);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await UmaVarreduraAsync(stoppingToken);
        }
    }

    private async Task UmaVarreduraAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            var cfg = scope.ServiceProvider.GetRequiredService<IOptions<PlanoProfessorSettings>>().Value;

            var enviados = await VarrerAsync(context, push, cfg, DateTime.Now, stoppingToken);

            if (enviados > 0)
                _logger.LogInformation("{Total} aviso(s) do plano do professor enfileirado(s).", enviados);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima hora tenta de novo.
            _logger.LogError(ex, "Falha ao avisar sobre o plano dos professores.");
        }
    }

    // A varredura, separada e com o `agora` POR PARÂMETRO: é o que permite exercitá-la inteira
    // num teste, inclusive nos dias que ainda não chegaram. Devolve quantos avisos saíram.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        PlanoProfessorSettings cfg, DateTime agora, CancellationToken stoppingToken = default)
    {
        // Mesma régua de "não acorde ninguém às 3h" do lembrete de inscrição. É a MESMA regra,
        // então é a mesma cópia — um segundo `HoraCivilizada` seria a duplicata que um dia
        // discorda da primeira.
        if (!LembreteDeInscricaoNaoPaga.HoraCivilizada(agora)) return 0;

        // O filtro que o banco sabe fazer: professor que JÁ ENTROU no plano de algum jeito —
        // abriu a tela (e o relógio do teste começou) ou tem mensalidade paga alguma vez.
        //
        // ⚠️ As TRÊS pontas do OU são necessárias. Sem `TesteProfessorInicio` some quem está no
        // teste, que é metade do serviço; sem `AssinaturaProfessorPagaAte` some quem foi pago
        // à MÃO pelo admin sem nunca ter aberto a tela — o RegistrarPagamento grava o "pago
        // até" e não encosta no relógio do teste; e sem `CortesiaProfessorAte` (20/08/2026) some
        // quem ganhou cortesia sem nunca ter aberto o /PlanoProfessor: ele não receberia o aviso
        // de que a cortesia acabou e a taxa dele subiu — nem no dia, nem nunca.
        //
        // O resto da régua (qual mundo, estágio, janela, o que já foi enviado) roda em memória,
        // sobre um punhado de linhas — são os professores, não a base inteira.
        var professores = await context.Jogadores
            .Where(j => j.IsProfessor
                     && (j.TesteProfessorInicio != null
                      || j.AssinaturaProfessorPagaAte != null
                      || j.CortesiaProfessorAte != null))
            .ToListAsync(stoppingToken);

        int avisados = 0;

        foreach (var professor in professores)
        {
            var estagio = AvisosDoPlanoDoProfessor.EstagioDevido(professor, agora, cfg);
            if (estagio == null) continue;

            await push.EnviarParaJogadorAsync(professor.Id,
                AvisosDoPlanoDoProfessor.Titulo(estagio.Value),
                AvisosDoPlanoDoProfessor.Frase(estagio.Value, professor, agora, cfg),
                "/PlanoProfessor");

            professor.UltimoLembreteDeAssinatura = estagio;
            avisados++;
        }

        // O estágio é gravado DEPOIS de enfileirar, no mesmo tick: a fila de avisos é em
        // memória e a entrega acontece fora daqui. Se o processo cair no meio, o pior caso é
        // um aviso repetido — melhor que o silêncio, que é o defeito que isto vem corrigir.
        if (avisados > 0) await context.SaveChangesAsync(stoppingToken);

        return avisados;
    }
}
