using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Apagar um local de aula. Até 03/08/2026 só existia Desativar, e o caso mais comum de todos
// — errar o nome no primeiro cadastro — deixava o local errado na lista pra sempre.
public class RemocaoDeLocalTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batat Padel", PrecoPadrao = 110 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    [Fact]
    public void Local_sem_aula_pode_ser_apagado() =>
        Assert.Null(RemocaoDeLocal.MotivoParaNaoApagar(0));

    [Fact]
    public void Local_com_aula_nao_pode_ser_apagado()
    {
        var motivo = RemocaoDeLocal.MotivoParaNaoApagar(3);

        Assert.NotNull(motivo);
        // A mensagem tem que dizer o que fazer no lugar, senão vira só um "não".
        Assert.Contains("Desativar", motivo);
        Assert.Contains("3 aulas", motivo);
    }

    [Fact]
    public void A_confirmacao_avisa_o_que_vai_junto()
    {
        Assert.Contains("2 horários", RemocaoDeLocal.Consequencia(2, 0));
        Assert.Contains("1 pacote", RemocaoDeLocal.Consequencia(0, 1));

        var ambos = RemocaoDeLocal.Consequencia(3, 2);
        Assert.Contains("3 horários", ambos);
        Assert.Contains("2 pacotes", ambos);

        // Local recém-criado não tem nada pendurado: a frase não pode prometer que vai levar
        // coisa nenhuma junto.
        Assert.DoesNotContain("Vai junto", RemocaoDeLocal.Consequencia(0, 0));
    }

    [Fact]
    public async Task Apagar_leva_horarios_e_pacotes_junto()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        ctx.HorariosDisponiveis.Add(new HorarioDisponivel
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DiaSemana = 1, HoraInicio = new TimeSpan(8, 0, 0), HoraFim = new TimeSpan(12, 0, 0), DuracaoMinutos = 60,
        });
        ctx.PacotesDeAulas.Add(new PacoteDeAulas { LocalAulaId = local.Id, QuantidadeAulas = 4, Preco = 440 });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.ExcluirLocal(local.Id);

        Assert.Empty(await ctx.LocaisAula.ToListAsync());
        Assert.Empty(await ctx.HorariosDisponiveis.ToListAsync());
        Assert.Empty(await ctx.PacotesDeAulas.ToListAsync());
    }

    [Fact]
    public async Task Local_com_aula_sobrevive_a_tentativa_de_apagar()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        ctx.Aulas.Add(new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Today.AddDays(-7), Preco = 110, Status = "Realizada",
            NomeAlunoAvulso = "Medina",
        });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.ExcluirLocal(local.Id);

        // O histórico do que ele ganhou ali é o motivo de a trava existir.
        Assert.Single(await ctx.LocaisAula.ToListAsync());
        Assert.Single(await ctx.Aulas.ToListAsync());
        Assert.NotNull(controller.TempData["ErroPacote"]);
    }

    [Fact]
    public async Task Professor_nao_apaga_local_de_outro_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000002", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, intruso.Id);
        await controller.ExcluirLocal(local.Id);

        Assert.Single(await ctx.LocaisAula.ToListAsync());
    }
}
