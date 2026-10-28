using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// DESFAZ O QUE O BLOQUEIO CANCELOU ANTES DA HORA (28/10/2026).
//
// Até a correção de `BloqueioDoProfessor.CancelaAulasEm`, a estreia do bloqueio era piso das
// duas datas, e quem tinha vencido havia mais de um mês teve as aulas canceladas no mesmo tique
// em que a agenda fechou (21/10). 🗣️ Felipe: *"crie o botão de restaurar para todos"*.
//
// ⚠️ A JANELA É A TRAVA. Com a conta corrigida, o primeiro cancelamento legítimo possível é
// `estreia + (DiasAteCancelarAsAulas − DiasAteOBloqueio)`. Tudo que o sistema cancelou antes
// disso é o defeito; nada depois é. Por isso o botão pode ser apertado de novo sem ressuscitar
// aula que caiu com razão — e por isso não existe um "restaurar só deste professor".
public static class RestauracaoDoCancelamentoPorBloqueio
{
    public sealed record Resultado(int Restauradas, int PuladasPorConflito, int Professores);

    // [início, fim) dos cancelamentos que foram o defeito. Nulo com o bloqueio dormente: sem
    // estreia o sistema nunca cancelou nada.
    public static (DateTime De, DateTime Ate)? Janela(PlanoProfessorSettings cfg) =>
        cfg.BloqueioAPartirDe is DateTime estreia
            ? (estreia.Date, estreia.Date
                .AddDays(cfg.DiasAteCancelarAsAulas - cfg.DiasAteOBloqueio)
                .AddHours(BloqueioDoProfessor.HoraDoBloqueio))
            : null;

    // As aulas que o botão traz de volta: cancelamento do SISTEMA, dentro da janela, e que
    // ainda vão acontecer. Aula que já passou não volta — ficaria esperando presença de um dia
    // em que ninguém foi à quadra.
    public static async Task<List<Aula>> CandidatasAsync(DbPadelContext context, DateTime agora,
        PlanoProfessorSettings cfg, CancellationToken ct = default)
    {
        if (Janela(cfg) is not { } janela) return new List<Aula>();
        var (de, ate) = janela;

        return await ConsultaDasCandidatas(context, agora, de, ate).ToListAsync(ct);
    }

    // As duas consultas ficam expostas pra TraducaoDaRestauracaoDoBloqueioTests compilá-las
    // contra o Npgsql — o InMemory da suíte não valida SQL.
    public static IQueryable<Aula> ConsultaDasCandidatas(DbPadelContext context, DateTime agora, DateTime de, DateTime ate) =>
        context.Aulas
            .Where(a => a.Status == PoliticaAula.Cancelada
                     && a.CanceladaPor == PoliticaAula.CanceladaPeloSistema
                     && a.CanceladaEm >= de && a.CanceladaEm < ate
                     && a.DataHora >= agora);

    public static IQueryable<Guid> ConsultaDasSeriesSemFim(DbPadelContext context, List<Guid> series, List<int> idsCandidatas) =>
        context.Aulas
            .Where(a => a.RecorrenciaId != null && series.Contains(a.RecorrenciaId.Value)
                     && a.RecorrenciaSemFim && !idsCandidatas.Contains(a.Id))
            .Select(a => a.RecorrenciaId!.Value)
            .Distinct();

    public static async Task<Resultado> RestaurarAsync(DbPadelContext context, IPushNotificationService push,
        DateTime agora, PlanoProfessorSettings cfg, ILogger? logger = null, CancellationToken ct = default)
    {
        var candidatas = await CandidatasAsync(context, agora, cfg, ct);
        if (candidatas.Count == 0) return new Resultado(0, 0, 0);

        var idsProfessores = candidatas.Select(a => a.ProfessorId).Distinct().ToList();

        // O que está de pé HOJE na agenda desses professores: o que ele encaixou depois de ver
        // a agenda vazia não pode ganhar uma aula restaurada por cima.
        var ativas = await context.Aulas
            .Where(a => idsProfessores.Contains(a.ProfessorId)
                     && a.DataHora >= agora.AddDays(-1)
                     && (a.Status == PoliticaAula.Pendente || a.Status == PoliticaAula.Confirmada))
            .Select(a => new { a.ProfessorId, a.DataHora, a.DuracaoMinutos, a.TurmaId })
            .ToListAsync(ct);

        // As séries sem fim. O cancelamento desligou `RecorrenciaSemFim` só nas aulas futuras; as
        // já dadas ficaram ligadas, e "Encerrar repetição" desliga a série INTEIRA. Então: alguma
        // irmã fora do cancelamento ainda ligada = a série era sem fim e ninguém a encerrou.
        var series = candidatas.Where(a => a.RecorrenciaId != null).Select(a => a.RecorrenciaId!.Value).Distinct().ToList();
        var idsCandidatas = candidatas.Select(a => a.Id).ToList();
        var seriesSemFim = (await ConsultaDasSeriesSemFim(context, series, idsCandidatas).ToListAsync(ct))
            .ToHashSet();

        var restauradas = new List<Aula>();
        var puladas = 0;

        foreach (var aula in candidatas)
        {
            var ocupado = ativas.Any(o => o.ProfessorId == aula.ProfessorId
                && (aula.TurmaId == null || o.TurmaId != aula.TurmaId)
                && DuracaoDaAula.Conflita(aula.DataHora, aula.DuracaoMinutos, o.DataHora, o.DuracaoMinutos));
            if (ocupado)
            {
                puladas++;
                continue;
            }

            // ⚠️ CONFIRMADA, e não o status de antes: o cancelamento não guardou se era Pendente.
            // Aula criada pelo professor (a fixa, a quase totalidade) nasce Confirmada; Pendente
            // é só o pedido de aluno ainda não respondido. Voltar tudo Pendente obrigaria o
            // professor a reaceitar a agenda inteira, aula por aula.
            aula.Status = PoliticaAula.Confirmada;
            aula.CanceladaPor = null;
            aula.CanceladaEm = null;
            aula.RecorrenciaSemFim = aula.RecorrenciaId is Guid serie && seriesSemFim.Contains(serie);
            restauradas.Add(aula);
        }

        if (restauradas.Count > 0) await context.SaveChangesAsync(ct);

        var professores = await context.Jogadores
            .Where(j => idsProfessores.Contains(j.Id))
            .Select(j => new { j.Id, j.Nome })
            .ToDictionaryAsync(j => j.Id, j => j.Nome, ct);

        foreach (var doProfessor in restauradas.GroupBy(a => a.ProfessorId))
        {
            var nome = professores.TryGetValue(doProfessor.Key, out var n) ? n : "seu professor";
            await AvisarAsync(push, doProfessor.Key, nome, doProfessor.ToList(), logger);
        }

        return new Resultado(restauradas.Count, puladas, restauradas.Select(a => a.ProfessorId).Distinct().Count());
    }

    // ⚠️ O aviso é acessório: falhar o push não desfaz o que já foi gravado. E é UM POR PESSOA,
    // como no cancelamento — quem recebeu "suas aulas foram canceladas" precisa saber que
    // voltaram, senão a aula restaurada vira falta.
    private static async Task AvisarAsync(IPushNotificationService push, int professorId, string professor,
        List<Aula> aulas, ILogger? logger)
    {
        foreach (var grupo in aulas.Where(a => a.AlunoId != null).GroupBy(a => a.AlunoId!.Value))
        {
            var quantas = grupo.Count();
            var quais = quantas == 1 ? "Sua aula" : $"Suas {quantas} aulas";
            await Tentar(logger, () => push.EnviarParaJogadorAsync(grupo.Key,
                "Suas aulas voltaram",
                $"{quais} com {professor} {(quantas == 1 ? "foi restaurada" : "foram restauradas")}. "
                + "O cancelamento foi um erro do Padelizou — está tudo de pé como antes.",
                "/Aulas/MinhasAulas"));
        }

        await Tentar(logger, () => push.EnviarParaJogadorAsync(professorId,
            "Suas aulas voltaram",
            $"{aulas.Count} aula(s) futuras que o Padelizou cancelou por engano voltaram pra sua agenda, "
            + "e seus alunos foram avisados. Desculpe o transtorno.",
            "/Aulas/MinhaAgenda"));
    }

    private static async Task Tentar(ILogger? logger, Func<Task> enviar)
    {
        try { await enviar(); }
        catch (Exception ex) { logger?.LogWarning(ex, "Falha ao avisar a restauração de aulas."); }
    }
}
