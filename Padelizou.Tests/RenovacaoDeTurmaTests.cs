using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Turma de N alunos é N séries independentes — uma RecorrenciaId por aluno — que
// compartilham um TurmaId ESTÁVEL (ver Models/Aula.TurmaId): nasce uma vez na criação e
// viaja com a série pra sempre, copiado adiante a cada renovação semanal
// (RenovacaoDaAulaFixa.Copiar). É o TurmaId que deixa o renovador RECONHECER que os colegas
// de turma no mesmo horário não são um conflito de agenda — sem isso, um aluno que cancela
// uma aula sozinho (ficando um passo atrás dos colegas) faria o renovador enxergar a aula do
// colega, no mesmo horário, como "já tem outra coisa marcada ali" e pular a semana à toa.
public class RenovacaoDeTurmaTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Marcio", Login = "marcio", Cpf = "55500000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Aula(Jogador professor, LocalAula local, DateTime quando, Guid serie, string nome, Guid? turmaId) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 90,
        Preco = 60,
        Status = PoliticaAula.Confirmada,
        NomeAlunoAvulso = nome,
        QuantidadeAlunos = 3,
        RecorrenciaId = serie,
        RecorrenciaSemFim = true,
        TurmaId = turmaId,
    };

    [Fact]
    public async Task Renovar_uma_turma_carrega_o_mesmo_TurmaId_pra_frente()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        var turma = Guid.NewGuid();

        // As 3 séries independentes de uma mesma turma, todas com o MESMO TurmaId (nasceram
        // juntas na criação) e sincronizadas na mesma última data.
        ctx.Aulas.AddRange(
            Aula(professor, local, proxima, Guid.NewGuid(), "Medina", turma),
            Aula(professor, local, proxima, Guid.NewGuid(), "Coello", turma),
            Aula(professor, local, proxima, Guid.NewGuid(), "Lima", turma));
        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        var semanaSeguinte = await ctx.Aulas
            .Where(a => a.DataHora == proxima.AddDays(7))
            .ToListAsync();

        Assert.Equal(3, semanaSeguinte.Count);
        // O MESMO TurmaId — não um sorteado de novo pra semana nova.
        Assert.All(semanaSeguinte, a => Assert.Equal(turma, a.TurmaId));

        // Cada aluno mantém a própria série — 3 RecorrenciaId distintas continuam existindo.
        Assert.Equal(3, (await ctx.Aulas.Select(a => a.RecorrenciaId).Distinct().ToListAsync()).Count);
    }

    [Fact]
    public async Task Serie_sozinha_sem_turma_nunca_ganha_TurmaId()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        ctx.Aulas.Add(Aula(professor, local, DateTime.Today.AddDays(3).AddHours(9), serie, "Leonardo", turmaId: null));
        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.Null(a.TurmaId));
    }

    [Fact]
    public async Task Um_aluno_que_cancelou_uma_semana_nao_trava_a_propria_renovacao_por_causa_do_colega_de_turma()
    {
        // O cenário real que expôs o bug: Medina cancelou UMA aula da série (a linha muda de
        // status e sai da contagem), então a série dela fica um passo atrás da de Coello —
        // que ainda tem a aula daquela mesma semana, no MESMO horário. Sem a exclusão por
        // TurmaId, o renovador de Medina veria a aula de Coello, no mesmo horário, como
        // ocupação de outra coisa — e pularia a semana à toa.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        var turma = Guid.NewGuid();

        // Medina: só a semana de hoje (a de "proxima+7" foi cancelada — não está aqui).
        ctx.Aulas.Add(Aula(professor, local, proxima, Guid.NewGuid(), "Medina", turma));

        // Coello: continua com as duas, "proxima" e "proxima+7", no mesmo horário de Medina.
        ctx.Aulas.AddRange(
            Aula(professor, local, proxima, Guid.NewGuid(), "Coello", turma),
            Aula(professor, local, proxima.AddDays(7), Guid.NewGuid(), "Coello", turma));

        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        // Medina precisava criar a aula de "proxima+7" pra alcançar Coello de novo — e
        // conseguiu, apesar de Coello já ter uma aula marcada exatamente nesse horário.
        var deMedinaNaSemanaSeguinte = await ctx.Aulas
            .Where(a => a.NomeAlunoAvulso == "Medina" && a.DataHora == proxima.AddDays(7))
            .ToListAsync();

        Assert.Single(deMedinaNaSemanaSeguinte);
        Assert.Equal(turma, deMedinaNaSemanaSeguinte[0].TurmaId);
    }

    [Fact]
    public async Task Duas_turmas_diferentes_no_mesmo_horario_continuam_conflitando_de_verdade()
    {
        // A exclusão é só pros COLEGAS de turma (mesmo TurmaId). Duas turmas diferentes que
        // by acidente caem no mesmo horário do mesmo professor continuam sendo um conflito
        // de verdade — a trava não pode virar "grupo nunca conflita com nada".
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var proxima = DateTime.Today.AddDays(3).AddHours(9);

        // Turma A: só "proxima" (precisa renovar).
        ctx.Aulas.Add(Aula(professor, local, proxima, Guid.NewGuid(), "Medina", Guid.NewGuid()));

        // Turma B (TurmaId diferente): já ocupa "proxima+7" no mesmo horário/professor.
        ctx.Aulas.Add(Aula(professor, local, proxima.AddDays(7), Guid.NewGuid(), "Outro aluno", Guid.NewGuid()));

        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        // A turma A pulou a semana ocupada pela turma B — não criou aula de Medina em cima
        // da aula do "Outro aluno".
        Assert.Equal(0, await ctx.Aulas.CountAsync(a => a.DataHora == proxima.AddDays(7) && a.NomeAlunoAvulso == "Medina"));
        Assert.Equal(1, await ctx.Aulas.CountAsync(a => a.DataHora == proxima.AddDays(7) && a.NomeAlunoAvulso == "Outro aluno"));
        // Mas a série de Medina segue viva: pulou uma semana, não morreu.
        Assert.True(await ctx.Aulas.CountAsync(a => a.NomeAlunoAvulso == "Medina") > 1);
    }
}
