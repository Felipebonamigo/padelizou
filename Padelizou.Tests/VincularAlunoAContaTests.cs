using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ordem natural é essa: o professor marca a aula de quem ainda não usa o app, e a pessoa
// cria conta depois. Sem ligar as duas coisas, o histórico fica partido — as aulas antigas
// num nome solto, as novas na conta — e o aluno nunca vê no próprio app o que já fez.
public class VincularAlunoAContaTests
{
    private static async Task<(DbPadelContext ctx, Jogador professor, Jogador aluno, LocalAula local)> Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var professor = new Jogador { Nome = "Professor Jonatas", Cpf = "11144477735", IsProfessor = true };
        var aluno = new Jogador { Nome = "Pedro Henrique", Cpf = "22255588846" };
        ctx.Jogadores.AddRange(professor, aluno);
        await ctx.SaveChangesAsync();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena", PrecoPadrao = 100m, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();

        return (ctx, professor, aluno, local);
    }

    private static Aula AulaAvulsa(Jogador professor, LocalAula local, string nome, int diasAtras) => new()
    {
        ProfessorId = professor.Id,
        AlunoId = null,
        NomeAlunoAvulso = nome,
        LocalAulaId = local.Id,
        DataHora = DateTime.Today.AddDays(-diasAtras),
        Preco = 100m,
        Status = "Confirmada",
    };

    [Fact]
    public async Task Aulas_antigas_passam_para_a_conta_do_aluno()
    {
        var (ctx, professor, aluno, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.AddRange(
            AulaAvulsa(professor, local, "Pedrinho", 21),
            AulaAvulsa(professor, local, "Pedrinho", 14),
            AulaAvulsa(professor, local, "Outro", 7));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Pedrinho", aluno.Id);

        var doPedrinho = await ctx.Aulas.Where(a => a.NomeAlunoAvulso == "Pedrinho").ToListAsync();
        Assert.All(doPedrinho, a => Assert.Equal(aluno.Id, a.AlunoId));

        // A aula de outra pessoa não foi arrastada junto.
        Assert.Null((await ctx.Aulas.FirstAsync(a => a.NomeAlunoAvulso == "Outro")).AlunoId);
    }

    [Fact]
    public async Task O_acordo_de_preco_vai_junto_porque_foi_combinado_com_a_pessoa()
    {
        var (ctx, professor, aluno, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(AulaAvulsa(professor, local, "Pedrinho", 7));
        ctx.PrecosDeAluno.Add(new PrecoDeAluno
        {
            ProfessorId = professor.Id, AlunoId = null, NomeAvulso = "Pedrinho", Preco = 80m,
        });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Pedrinho", aluno.Id);

        // Se o desconto ficasse preso ao nome antigo, ele sumiria no dia em que a pessoa
        // criou conta — e o professor cobraria o valor cheio de quem tinha acordo.
        var acordo = await ctx.PrecosDeAluno.SingleAsync();
        Assert.Equal(aluno.Id, acordo.AlunoId);
        Assert.Null(acordo.NomeAvulso);
        Assert.Equal(80m, acordo.Preco);
    }

    [Fact]
    public async Task Nao_mexe_na_agenda_de_outro_professor()
    {
        var (ctx, professor, aluno, local) = await Montar();
        using var _ = ctx;

        var outro = new Jogador { Nome = "Outro Professor", Cpf = "52998224725", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        var localDoOutro = new LocalAula { ProfessorId = outro.Id, Nome = "Outra Arena", PrecoPadrao = 100m, Ativo = true };
        ctx.LocaisAula.Add(localDoOutro);
        await ctx.SaveChangesAsync();

        ctx.Aulas.Add(AulaAvulsa(outro, localDoOutro, "Pedrinho", 7));
        await ctx.SaveChangesAsync();

        // O professor logado é o `professor`, que NÃO tem aula do Pedrinho.
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Pedrinho", aluno.Id);

        Assert.Null((await ctx.Aulas.SingleAsync()).AlunoId);
    }

    [Fact]
    public async Task Aula_que_ja_tem_conta_ligada_nao_e_reescrita()
    {
        var (ctx, professor, aluno, local) = await Montar();
        using var _ = ctx;

        var terceiro = new Jogador { Nome = "Terceiro", Cpf = "52998224725" };
        ctx.Jogadores.Add(terceiro);
        await ctx.SaveChangesAsync();

        var jaLigada = AulaAvulsa(professor, local, "Pedrinho", 30);
        jaLigada.AlunoId = terceiro.Id;
        ctx.Aulas.Add(jaLigada);
        ctx.Aulas.Add(AulaAvulsa(professor, local, "Pedrinho", 7));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Pedrinho", aluno.Id);

        // A que já tinha dono continua com ele; só a solta foi ligada.
        var aulas = await ctx.Aulas.OrderBy(a => a.DataHora).ToListAsync();
        Assert.Equal(terceiro.Id, aulas[0].AlunoId);
        Assert.Equal(aluno.Id, aulas[1].AlunoId);
    }

    [Fact]
    public async Task O_professor_nao_vira_aluno_de_si_mesmo()
    {
        var (ctx, professor, _aluno, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(AulaAvulsa(professor, local, "Pedrinho", 7));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Pedrinho", professor.Id);

        Assert.Null((await ctx.Aulas.SingleAsync()).AlunoId);
    }

    [Fact]
    public async Task Nome_que_nao_existe_na_agenda_e_recusado()
    {
        var (ctx, professor, aluno, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(AulaAvulsa(professor, local, "Pedrinho", 7));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.VincularAlunoAConta("Fantasma", aluno.Id);

        Assert.Null((await ctx.Aulas.SingleAsync()).AlunoId);
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }
}
