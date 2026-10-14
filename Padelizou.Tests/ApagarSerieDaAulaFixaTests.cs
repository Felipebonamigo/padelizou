using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "Apagar esta e as próximas": o professor tira uma aula fixa da agenda numa ação só, em vez de
// combinar "Encerrar a repetição" (no Editar) com "Apagar" aula por aula (pedido de 14/10/2026,
// o Gabriel não achou o botão do encerrar).
//
// A régua: só as aulas DE PÉ (Pendente/Confirmada) daquela data pra frente. Realizada e
// Cancelada são fato registrado; as anteriores à clicada o professor não pediu. Aula futura já
// PAGA fica — apagar não devolve dinheiro — e a repetição para mesmo assim.
public class ApagarSerieDaAulaFixaTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Gabriel", Login = "gabriel", Cpf = "55500000011", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Shark", PrecoPadrao = 100 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Aula(Jogador professor, LocalAula local, DateTime quando, Guid serie,
        string status = PoliticaAula.Confirmada, bool semFim = true) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 90,
        Preco = 100,
        Status = status,
        NomeAlunoAvulso = "Giovanni",
        RecorrenciaId = serie,
        RecorrenciaSemFim = semFim,
    };

    private static DateTime Semana(int n) => DateTime.Today.AddDays(3).AddHours(7).AddDays(7 * n);

    [Fact]
    public async Task Apaga_a_clicada_e_as_futuras_e_deixa_as_anteriores()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var s = Enumerable.Range(0, 5).Select(n => Aula(professor, local, Semana(n), serie)).ToList();
        ctx.Aulas.AddRange(s);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(s[2].Id);

        var restam = await ctx.Aulas.OrderBy(a => a.DataHora).ToListAsync();
        Assert.Equal(new[] { s[0].Id, s[1].Id }, restam.Select(a => a.Id));
        // As que ficam não podem ser repostas pelo robô: a série foi encerrada.
        Assert.All(restam, a => Assert.False(a.RecorrenciaSemFim));
    }

    [Fact]
    public async Task Nao_apaga_aula_realizada_nem_cancelada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var clicada = Aula(professor, local, Semana(0), serie);
        var realizada = Aula(professor, local, Semana(1), serie, PoliticaAula.Realizada);
        var cancelada = Aula(professor, local, Semana(2), serie, PoliticaAula.Cancelada);
        var pendente = Aula(professor, local, Semana(3), serie, PoliticaAula.Pendente);
        ctx.Aulas.AddRange(clicada, realizada, cancelada, pendente);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(clicada.Id);

        var restam = (await ctx.Aulas.ToListAsync()).Select(a => a.Id).ToHashSet();
        Assert.Equal(new HashSet<int> { realizada.Id, cancelada.Id }, restam);
    }

    [Fact]
    public async Task Aula_futura_ja_paga_fica_e_a_repeticao_para_mesmo_assim()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var clicada = Aula(professor, local, Semana(0), serie);
        var paga = Aula(professor, local, Semana(1), serie);
        paga.PagaEm = DateTime.Now;
        var outra = Aula(professor, local, Semana(2), serie);
        ctx.Aulas.AddRange(clicada, paga, outra);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.ExcluirAulaEProximas(clicada.Id);

        var restam = await ctx.Aulas.ToListAsync();
        var sobrou = Assert.Single(restam);
        Assert.Equal(paga.Id, sobrou.Id);
        // Se continuasse "sem fim", o renovador repovoaria a série a partir da paga.
        Assert.False(sobrou.RecorrenciaSemFim);
        Assert.Equal(0, await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now, new PlanoProfessorSettings()));
        // E o professor precisa saber que ficou uma pra trás.
        Assert.Contains("1", controller.TempData["Sucesso"]?.ToString());
        Assert.Contains("paga", controller.TempData["Sucesso"]?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Nao_mexe_em_outra_serie_do_mesmo_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var a = Aula(professor, local, Semana(0), Guid.NewGuid());
        var b = Aula(professor, local, Semana(1), Guid.NewGuid());
        ctx.Aulas.AddRange(a, b);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(a.Id);

        var restou = Assert.Single(await ctx.Aulas.ToListAsync());
        Assert.Equal(b.Id, restou.Id);
        Assert.True(restou.RecorrenciaSemFim);
    }

    [Fact]
    public async Task Professor_nao_apaga_serie_de_outro_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "55500000012", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var serie = Guid.NewGuid();
        var aula = Aula(professor, local, Semana(0), serie);
        ctx.Aulas.AddRange(aula, Aula(professor, local, Semana(1), serie));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, intruso.Id).ExcluirAulaEProximas(aula.Id);

        Assert.Equal(2, await ctx.Aulas.CountAsync());
        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.True(a.RecorrenciaSemFim));
    }

    [Fact]
    public async Task Serie_com_prazo_tambem_apaga_as_proximas()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var s = Enumerable.Range(0, 4).Select(n => Aula(professor, local, Semana(n), serie, semFim: false)).ToList();
        ctx.Aulas.AddRange(s);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(s[1].Id);

        Assert.Equal(new[] { s[0].Id }, (await ctx.Aulas.ToListAsync()).Select(a => a.Id));
    }

    [Fact]
    public async Task Tira_do_Google_cada_evento_apagado()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var s = Enumerable.Range(0, 3).Select(n => Aula(professor, local, Semana(n), serie)).ToList();
        for (var i = 0; i < s.Count; i++) s[i].GoogleEventId = $"evt-{i}";
        ctx.Aulas.AddRange(s);
        await ctx.SaveChangesAsync();

        var google = Substitute.For<IGoogleCalendarService>();
        await TestInfra.NovoAulasController(ctx, professor.Id, google).ExcluirAulaEProximas(s[1].Id);

        await google.Received(1).RemoverEventoAsync(professor.Id, "evt-1");
        await google.Received(1).RemoverEventoAsync(professor.Id, "evt-2");
        await google.DidNotReceive().RemoverEventoAsync(professor.Id, "evt-0");
    }

    // Turma: o card da agenda é UM pra turma inteira, e cada aluno tem a própria série.
    // "Apagar esta e as próximas" ali tem que levar os colegas — senão sobram duas
    // das três linhas da turma na agenda, e o renovador continuaria repondo as delas.
    [Fact]
    public async Task Na_turma_leva_os_colegas_e_as_series_deles()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var turma = Guid.NewGuid();
        var serieA = Guid.NewGuid();
        var serieB = Guid.NewGuid();
        var linhas = new List<Aula>();
        for (var n = 0; n < 3; n++)
        {
            var a = Aula(professor, local, Semana(n), serieA); a.TurmaId = turma; a.NomeAlunoAvulso = "A";
            var b = Aula(professor, local, Semana(n), serieB); b.TurmaId = turma; b.NomeAlunoAvulso = "B";
            linhas.Add(a); linhas.Add(b);
        }
        ctx.Aulas.AddRange(linhas);
        await ctx.SaveChangesAsync();

        // Clica na semana 1 (a linha do aluno A).
        var clicada = linhas.First(l => l.DataHora == Semana(1) && l.NomeAlunoAvulso == "A");
        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(clicada.Id);

        var restam = await ctx.Aulas.ToListAsync();
        Assert.Equal(2, restam.Count);
        Assert.All(restam, a => Assert.Equal(Semana(0), a.DataHora));
        Assert.All(restam, a => Assert.False(a.RecorrenciaSemFim));
    }

    [Fact]
    public async Task Aula_avulsa_sem_serie_nao_apaga_nada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var avulsa = Aula(professor, local, Semana(0), Guid.NewGuid());
        avulsa.RecorrenciaId = null;
        avulsa.RecorrenciaSemFim = false;
        var outra = Aula(professor, local, Semana(1), Guid.NewGuid());
        ctx.Aulas.AddRange(avulsa, outra);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).ExcluirAulaEProximas(avulsa.Id);

        // Sem série não há "próximas": quem quer apagar uma aula só usa o Apagar de sempre.
        Assert.Equal(2, await ctx.Aulas.CountAsync());
    }
}
