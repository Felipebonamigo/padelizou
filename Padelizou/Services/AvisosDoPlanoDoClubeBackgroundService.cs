using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;

namespace Padelizou.Services;

// Avisa o clube antes de o bar fechar por falta de pagamento. A regra de QUEM e QUANDO mora
// em Services/AvisosDoPlanoDoClube; aqui só se varre o banco e se enfileira o aviso.
//
// Irmão do AvisosDoPlanoDoProfessorBackgroundService, e de propósito: mesmo tick, mesma hora
// civilizada, mesma coluna decidindo se já avisou. O que muda é quem recebe — lá é uma pessoa,
// aqui é um clube, e clube não tem push. Quem recebe é o DONO (ver a busca lá embaixo).
public class AvisosDoPlanoDoClubeBackgroundService : BackgroundService
{
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AvisosDoPlanoDoClubeBackgroundService> _logger;

    public AvisosDoPlanoDoClubeBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<AvisosDoPlanoDoClubeBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

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
            var cfg = scope.ServiceProvider.GetRequiredService<IOptions<PlanoClubeSettings>>().Value;

            var enviados = await VarrerAsync(context, push, cfg, DateTime.Now, stoppingToken);

            if (enviados > 0)
                _logger.LogInformation("{Total} aviso(s) do plano do clube enfileirado(s).", enviados);
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar o host — na próxima hora tenta de novo.
            _logger.LogError(ex, "Falha ao avisar sobre o plano dos clubes.");
        }
    }

    // A varredura, separada e com o `agora` POR PARÂMETRO: é o que permite exercitá-la inteira
    // num teste, inclusive nos dias que ainda não chegaram. Devolve quantos avisos saíram.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        PlanoClubeSettings cfg, DateTime agora, CancellationToken stoppingToken = default)
    {
        // Mesma régua de "não acorde ninguém às 3h" do lembrete de inscrição. É a MESMA regra,
        // então é a mesma cópia — um segundo `HoraCivilizada` seria a duplicata que um dia
        // discorda da primeira.
        if (!LembreteDeInscricaoNaoPaga.HoraCivilizada(agora)) return 0;

        // Clube que JÁ ENTROU no plano de algum jeito: alguém abriu a tela (e o relógio do
        // teste começou) ou existe assinatura paga alguma vez.
        //
        // ⚠️ As duas pontas do OU são necessárias. Sem `TesteDoClubeInicio` some quem está no
        // teste, que é metade do serviço; sem `AssinaturaClubePagaAte` some o clube que foi
        // pago À MÃO pelo admin sem nunca ter aberto a tela — o registro manual grava o "pago
        // até" e não encosta no relógio do teste.
        var clubes = await context.Clubes
            .Where(c => c.DonoId != null
                     && (c.TesteDoClubeInicio != null || c.AssinaturaClubePagaAte != null))
            .ToListAsync(stoppingToken);

        int avisados = 0;

        foreach (var clube in clubes)
        {
            var estagio = AvisosDoPlanoDoClube.EstagioDevido(clube, agora, cfg);
            if (estagio == null) continue;

            // ⚠️ O aviso vai pro DONO, e só pra ele. Mandar pra todos os administradores do
            // clube faria três pessoas receberem "o bar vai fechar" pra uma conta que nenhuma
            // delas paga — e quem decide pagar é uma só.
            await push.EnviarParaJogadorAsync(clube.DonoId!.Value,
                AvisosDoPlanoDoClube.Titulo(estagio.Value),
                AvisosDoPlanoDoClube.Frase(estagio.Value, clube, agora, cfg),
                $"/PlanoClube/Index/{clube.Id}");

            clube.UltimoLembreteDoPlano = estagio;
            avisados++;
        }

        // O estágio é gravado DEPOIS de enfileirar, no mesmo tick: a fila de avisos é em
        // memória e a entrega acontece fora daqui. Se o processo cair no meio, o pior caso é
        // um aviso repetido — melhor que o silêncio, que é o defeito que isto vem corrigir.
        if (avisados > 0) await context.SaveChangesAsync(stoppingToken);

        return avisados;
    }
}
