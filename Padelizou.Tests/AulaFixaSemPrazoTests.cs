using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A aula fixa SEM PRAZO ("todo sábado às 9h, e pronto").
//
// Antes o professor tinha que inventar um número de semanas que ele não tem: o combinado com o
// aluno não tem data pra acabar. Marcava 26 e a série morria calada no fim do semestre;
// marcava 4 e voltava na tela todo mês.
public class AulaFixaSemPrazoTests
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

    [Fact]
    public async Task Sem_prazo_cria_o_horizonte_inteiro_e_marca_a_serie()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.AdicionarManual(local.Id, "Leonardo", null,
            DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(17)), DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(17)), 100m,
            recorrente: true, semanasRecorrencia: 4, semPrazo: true);

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.Equal(RenovacaoDaAulaFixa.HorizonteSemanas, aulas.Count);
        Assert.All(aulas, a => Assert.True(a.RecorrenciaSemFim));
        // Uma série só: sem o id compartilhado o renovador não acha a repetição pra continuar.
        Assert.Single(aulas.Select(a => a.RecorrenciaId).Distinct());
    }

    [Fact]
    public async Task Serie_com_prazo_nao_vira_serie_sem_fim()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.AdicionarManual(local.Id, "Leonardo", null,
            DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(17)), DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(17)), 100m,
            recorrente: true, semanasRecorrencia: 4, semPrazo: false);

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.Equal(4, aulas.Count);
        Assert.All(aulas, a => Assert.False(a.RecorrenciaSemFim));
    }

    [Fact]
    public async Task Renovador_repoe_as_semanas_que_o_tempo_consumiu()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var proxima = DateTime.Today.AddDays(3).AddHours(9);

        // Sobraram só 2 semanas à frente — é o estado de uma série que já rodou meses.
        ctx.Aulas.AddRange(
            Aula(professor, local, proxima, serie),
            Aula(professor, local, proxima.AddDays(7), serie));
        await ctx.SaveChangesAsync();

        var criadas = await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        Assert.Equal(RenovacaoDaAulaFixa.HorizonteSemanas - 2, criadas);
        Assert.Equal(RenovacaoDaAulaFixa.HorizonteSemanas, await ctx.Aulas.CountAsync());

        // As novas continuam de 7 em 7 dias, no mesmo horário e com a mesma duração.
        var ultimas = await ctx.Aulas.OrderBy(a => a.DataHora).ToListAsync();
        Assert.All(ultimas, a => Assert.Equal(proxima.TimeOfDay, a.DataHora.TimeOfDay));
        Assert.All(ultimas, a => Assert.Equal(90, a.DuracaoMinutos));
    }

    [Fact]
    public async Task Renovar_duas_vezes_nao_duplica_nada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        ctx.Aulas.Add(Aula(professor, local, DateTime.Today.AddDays(3).AddHours(9), serie));
        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);
        var depoisDaPrimeira = await ctx.Aulas.CountAsync();

        var criadasDeNovo = await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        Assert.Equal(0, criadasDeNovo);
        Assert.Equal(depoisDaPrimeira, await ctx.Aulas.CountAsync());
    }

    [Fact]
    public async Task Encerrar_a_repeticao_para_de_renovar_e_nao_apaga_nada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.AdicionarManual(local.Id, "Leonardo", null,
            DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(17)), DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(17)), 100m,
            recorrente: true, semanasRecorrencia: 4, semPrazo: true);

        var antes = await ctx.Aulas.CountAsync();
        var uma = await ctx.Aulas.FirstAsync();

        await controller.EncerrarRepeticao(uma.Id);

        // O aluno já contou com as aulas que estão na agenda (algumas podem estar pagas):
        // encerrar para a REPOSIÇÃO, não apaga o que existe.
        Assert.Equal(antes, await ctx.Aulas.CountAsync());
        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.False(a.RecorrenciaSemFim));
        Assert.Equal(0, await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now));
    }

    [Fact]
    public async Task Professor_nao_encerra_serie_de_outro_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "55500000002", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var serie = Guid.NewGuid();
        var aula = Aula(professor, local, DateTime.Today.AddDays(3).AddHours(9), serie);
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, intruso.Id).EncerrarRepeticao(aula.Id);

        Assert.True((await ctx.Aulas.FindAsync(aula.Id))!.RecorrenciaSemFim);
    }

    [Fact]
    public async Task Semana_ocupada_e_pulada_sem_derrubar_a_serie()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var serie = Guid.NewGuid();
        var proxima = DateTime.Today.AddDays(3).AddHours(9);
        ctx.Aulas.Add(Aula(professor, local, proxima, serie));

        // Um torneio encaixado no lugar da aula de daqui a duas semanas.
        ctx.Aulas.Add(new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = proxima.AddDays(14),
            Preco = 100, Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Outro aluno",
            DuracaoMinutos = 90,
        });
        await ctx.SaveChangesAsync();

        await RenovacaoDaAulaFixa.RenovarAsync(ctx, DateTime.Now);

        // A data ocupada não ganhou uma segunda aula, e as outras semanas continuaram nascendo.
        Assert.Equal(1, await ctx.Aulas.CountAsync(a => a.DataHora == proxima.AddDays(14)));
        Assert.True(await ctx.Aulas.CountAsync(a => a.RecorrenciaId == serie) > 1);
    }

    private static Aula Aula(Jogador professor, LocalAula local, DateTime quando, Guid serie) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 90,
        Preco = 100,
        Status = PoliticaAula.Confirmada,
        NomeAlunoAvulso = "Leonardo",
        RecorrenciaId = serie,
        RecorrenciaSemFim = true,
    };
}
