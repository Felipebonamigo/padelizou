using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// ITEM 4 DO BLOQUEIO: as aulas caem um mês depois do vencimento. 🗣️ Felipe: *"fica marcado até
// 1 mes depois do vencimento... caso acabe esse 1 mes de prazo, cancele todas as aulas e avise
// os alunos e o professor"*.
//
// ⚠️ ESTE É O ÚNICO SERVIÇO DO BLOCO QUE APAGA AGENDA, e por isso ele é o mais conservador:
// não roda com o bloqueio dormente, não toca no passado, e não faz nada por quem voltou a
// pagar. A data vem de `BloqueioDoProfessor.CancelaAulasEm` — a MESMA conta do bloqueio —,
// porque a escada de avisos passou vinte dias prometendo exatamente esse dia ao professor.
//
// ⚠️ E O ALUNO É O INOCENTE DA HISTÓRIA: ele não escolheu o plano de ninguém. O aviso a ele é
// parte do cancelamento, não um extra — o pior desfecho possível é alguém viajar pra quadra por
// uma aula que o sistema já tinha derrubado.
public class CancelamentoPorBloqueioBackgroundService : BackgroundService
{
    // De hora em hora, como os irmãos: o marco é em DIAS e quem decide se já foi feito é o
    // próprio status das aulas.
    private static readonly TimeSpan IntervaloTick = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CancelamentoPorBloqueioBackgroundService> _logger;

    public CancelamentoPorBloqueioBackgroundService(IServiceScopeFactory scopeFactory,
        ILogger<CancelamentoPorBloqueioBackgroundService> logger)
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
            await UmaVarreduraAsync(stoppingToken);
    }

    private async Task UmaVarreduraAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbPadelContext>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
            var cfg = scope.ServiceProvider.GetRequiredService<IOptions<PlanoProfessorSettings>>().Value;

            var canceladas = await VarrerAsync(context, push, cfg, DateTime.Now, stoppingToken);

            if (canceladas > 0)
                _logger.LogInformation("{Total} aula(s) canceladas por plano vencido.", canceladas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao cancelar as aulas de professor com plano vencido.");
        }
    }

    // A varredura, com o `agora` por parâmetro pra poder ser exercitada inteira. Devolve quantas
    // aulas caíram.
    public static async Task<int> VarrerAsync(DbPadelContext context, IPushNotificationService push,
        PlanoProfessorSettings cfg, DateTime agora, CancellationToken stoppingToken = default)
    {
        // ⚠️ A PRIMEIRA TRAVA, E A MAIS IMPORTANTE: com o interruptor desligado este serviço não
        // encosta em nada. É o que faz o bloco inteiro subir em produção sem risco.
        if (cfg.BloqueioAPartirDe == null) return 0;

        // Mesma régua de "não acorde ninguém às 3h" dos outros: o cancelamento dispara avisos,
        // e avisar de madrugada é pior do que avisar uma hora depois.
        if (!LembreteDeInscricaoNaoPaga.HoraCivilizada(agora)) return 0;

        var candidatos = await context.Jogadores
            .Where(j => j.IsProfessor && j.ExcluidoEm == null
                     && (j.TesteProfessorInicio != null
                      || j.AssinaturaProfessorPagaAte != null
                      || j.CortesiaProfessorAte != null))
            .ToListAsync(stoppingToken);

        var total = 0;

        foreach (var professor in candidatos)
        {
            if (!BloqueioDoProfessor.EstaBloqueado(professor, agora, cfg)) continue;
            if (BloqueioDoProfessor.CancelaAulasEm(professor, cfg) is not DateTime cancela) continue;
            if (agora < cancela) continue;

            // Só o que AINDA VAI ACONTECER e ainda está de pé. Aula dada é dinheiro a receber e
            // ficha do aluno; cancelar pra trás apagaria as duas coisas.
            var aulas = await context.Aulas
                .Where(a => a.ProfessorId == professor.Id
                         && a.DataHora >= agora
                         && (a.Status == PoliticaAula.Pendente || a.Status == PoliticaAula.Confirmada))
                .ToListAsync(stoppingToken);

            if (aulas.Count == 0) continue;

            foreach (var aula in aulas)
            {
                aula.Status = PoliticaAula.Cancelada;
                aula.CanceladaPor = PoliticaAula.CanceladaPeloSistema;
                aula.CanceladaEm = agora;

                // ⚠️ A repetição TAMBÉM morre. Sem isto o renovador voltaria a encher a agenda
                // no instante em que o professor pagasse — e o desenho é o contrário: ele
                // remarca o que quiser, sabendo o que está remarcando.
                aula.RecorrenciaSemFim = false;
            }

            await context.SaveChangesAsync(stoppingToken);
            total += aulas.Count;

            await AvisarAsync(push, professor, aulas, cancela);
        }

        return total;
    }

    private static async Task AvisarAsync(IPushNotificationService push, Jogador professor,
        List<Aula> aulas, DateTime cancela)
    {
        // ⚠️ UM AVISO POR PESSOA, e não por aula: um aluno com três aulas é uma pessoa, e três
        // avisos iguais no mesmo minuto é como se perde a permissão de notificação de alguém.
        // `AlunoId` nulo é o aluno avulso (só nome, sem conta) — ele não tem pra onde receber, e
        // é o professor quem vai avisá-lo por fora.
        foreach (var grupo in aulas.Where(a => a.AlunoId != null).GroupBy(a => a.AlunoId!.Value))
        {
            var quantas = grupo.Count();
            var quais = quantas == 1 ? "Sua aula" : $"Suas {quantas} aulas";

            await push.EnviarParaJogadorAsync(grupo.Key,
                "Suas aulas foram canceladas",
                $"{quais} com {professor.Nome} {(quantas == 1 ? "foi cancelada" : "foram canceladas")} "
                + "porque o plano dele no Padelizou venceu. Você não será cobrado por elas — "
                + "fale com ele pra remarcar.",
                "/Aulas/MinhasAulas");
        }

        await push.EnviarParaJogadorAsync(professor.Id,
            "Suas aulas foram canceladas",
            $"O prazo de {cancela:dd/MM} acabou e suas {aulas.Count} aula(s) futuras foram canceladas. "
            + "Seus alunos foram avisados. Assine pra reabrir a agenda — a remarcação é na mão, "
            + "inclusive as aulas fixas.",
            "/PlanoProfessor");
    }
}
