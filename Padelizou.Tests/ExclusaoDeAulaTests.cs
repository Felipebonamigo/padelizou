using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Apagar uma aula é o desfazer de quem lançou errado — dia trocado, série criada duas vezes,
// aluno errado. É diferente de cancelar, que é um fato do mundo e continua no histórico.
public class ExclusaoDeAulaTests
{
    private static Aula Aula(string status, DateTime quando, int? alunoId = null) => new()
    {
        ProfessorId = 1, AlunoId = alunoId, LocalAulaId = 1,
        DataHora = quando, Preco = 110, Status = status,
        NomeAlunoAvulso = alunoId == null ? "Medina" : null,
    };

    private static readonly DateTime Agora = new(2026, 8, 3, 10, 0, 0);

    [Fact]
    public void Aluno_com_conta_e_aula_futura_ativa_precisa_ser_avisado() =>
        Assert.True(ExclusaoDeAula.PrecisaAvisarAluno(Aula("Confirmada", Agora.AddDays(2), alunoId: 9), Agora));

    [Fact]
    public void Aluno_avulso_nao_recebe_aviso()
    {
        // Não tem conta: não há pra onde mandar. O professor combina por fora, como sempre fez.
        Assert.False(ExclusaoDeAula.PrecisaAvisarAluno(Aula("Confirmada", Agora.AddDays(2)), Agora));
    }

    [Fact]
    public void Aula_que_ja_passou_nao_avisa_ninguem()
    {
        // "Sua aula de terça foi apagada" três semanas depois só confunde.
        Assert.False(ExclusaoDeAula.PrecisaAvisarAluno(Aula("Realizada", Agora.AddDays(-20), alunoId: 9), Agora));
    }

    [Fact]
    public void Aula_ja_cancelada_nao_avisa_de_novo()
    {
        // O aluno já sabe que ela não vai acontecer — foi avisado no cancelamento.
        Assert.False(ExclusaoDeAula.PrecisaAvisarAluno(Aula("Cancelada", Agora.AddDays(2), alunoId: 9), Agora));
    }

    [Fact]
    public void A_confirmacao_com_alguem_esperando_oferece_o_cancelamento()
    {
        var texto = ExclusaoDeAula.Confirmacao(Aula("Confirmada", Agora.AddDays(2), alunoId: 9), Agora);

        // Quem quer registrar que a aula foi desmarcada está no botão errado, e a tela tem
        // que dizer isso antes — depois de apagada não há o que recuperar.
        Assert.Contains("Cancelar", texto);
        Assert.Contains("avisado", texto);
    }

    [Fact]
    public void A_confirmacao_de_lancamento_errado_e_curta()
    {
        var texto = ExclusaoDeAula.Confirmacao(Aula("Realizada", Agora.AddDays(-3)), Agora);

        Assert.DoesNotContain("Cancelar", texto);
        Assert.Contains("sem deixar registro", texto);
    }

    // ---- O caminho completo, no controller ----

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    [Fact]
    public async Task Apagar_tira_a_aula_da_agenda_e_do_Google()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Today.AddDays(3), Preco = 110, Status = "Confirmada",
            NomeAlunoAvulso = "Medina", GoogleEventId = "evt-123",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var google = Substitute.For<IGoogleCalendarService>();
        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);

        await controller.ExcluirAula(aula.Id);

        Assert.Empty(await ctx.Aulas.ToListAsync());
        // Sem isto a aula some do Padelizou e continua ocupando o horário no Google — que é
        // onde o professor olha antes de marcar outra coisa.
        await google.Received(1).RemoverEventoAsync(professor.Id, "evt-123");
    }

    [Fact]
    public async Task Apagar_leva_as_anotacoes_da_aula_junto()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Today.AddDays(3), Preco = 110, Status = "Confirmada",
            NomeAlunoAvulso = "Medina",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        ctx.AnotacoesAula.Add(new AnotacaoAula { AulaId = aula.Id, AutorId = professor.Id, Texto = "bandeja" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.ExcluirAula(aula.Id);

        Assert.Empty(await ctx.Aulas.ToListAsync());
        Assert.Empty(await ctx.AnotacoesAula.ToListAsync());
    }

    [Fact]
    public async Task Professor_nao_apaga_aula_de_outro_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000002", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Today.AddDays(3), Preco = 110, Status = "Confirmada",
            NomeAlunoAvulso = "Medina",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, intruso.Id);
        await controller.ExcluirAula(aula.Id);

        Assert.Single(await ctx.Aulas.ToListAsync());
    }

    [Fact]
    public async Task Aluno_com_conta_recebe_aviso_quando_a_aula_futura_e_apagada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aluno = new Jogador { Nome = "Eduarda", Login = "eduarda", Cpf = "99900000003" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        var aula = new Aula
        {
            ProfessorId = professor.Id, AlunoId = aluno.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Now.AddDays(3), Preco = 110, Status = "Confirmada",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoAulasController(ctx, professor.Id, push: push);

        await controller.ExcluirAula(aula.Id);

        await push.Received(1).EnviarParaJogadorAsync(
            aluno.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }
}
