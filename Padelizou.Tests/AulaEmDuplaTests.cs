using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A aula lançada à mão pelo professor, agora com tamanho e com o acordo particular do aluno.
// O caminho todo: o que chega do formulário vira o preço certo no banco.
public class AulaEmDuplaTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar(
        decimal individual = 110, decimal? dupla = 150, decimal? trio = 180)
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula
        {
            ProfessorId = professor.Id, Nome = "Batata Padel",
            PrecoPadrao = individual, PrecoDupla = dupla, PrecoTrio = trio, Ativo = true,
        };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> Marcar(
        AulasController controller, LocalAula local, string aluno, int quantos, decimal? preco = null) =>
        controller.AdicionarManual(
            localId: local.Id, nomeAluno: aluno, telefoneAluno: "(51) 99999-0000",
            dataHora: DateTime.Today.AddDays(2).AddHours(7), preco: preco,
            recorrente: false, semanasRecorrencia: 0, quantidadeAlunos: quantos);

    [Theory]
    [InlineData(1, 110)]
    [InlineData(2, 150)]
    [InlineData(3, 180)]
    public async Task A_aula_nasce_com_o_preco_do_tamanho(int quantos, decimal esperado)
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos);

        var aula = await ctx.Aulas.SingleAsync();
        Assert.Equal(esperado, aula.Preco);
        Assert.Equal(quantos, aula.QuantidadeAlunos);
    }

    [Fact]
    public async Task O_preco_digitado_pelo_professor_vence_a_sugestao()
    {
        // A tela sugere, o professor decide: tem dia que ele cobra diferente e ponto.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 2, preco: 200);

        Assert.Equal(200, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task Aluno_com_preco_combinado_paga_o_dele_na_individual()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        ctx.PrecosDeAluno.Add(new PrecoDeAluno { ProfessorId = professor.Id, NomeAvulso = "Medina", Preco = 90 });
        await ctx.SaveChangesAsync();

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 1);

        Assert.Equal(90, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task O_acordo_do_aluno_nao_derruba_o_preco_da_dupla()
    {
        // O desconto é da pessoa, não da quadra: valendo aqui, o lugar do acompanhante sairia
        // de graça sem o professor perceber.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        ctx.PrecosDeAluno.Add(new PrecoDeAluno { ProfessorId = professor.Id, NomeAvulso = "Medina", Preco = 90 });
        await ctx.SaveChangesAsync();

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 2);

        Assert.Equal(150, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task O_acordo_de_um_professor_nao_vale_na_agenda_de_outro()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var outroProfessor = new Jogador { Nome = "Índio", Login = "indio", Cpf = "99900000004", IsProfessor = true };
        ctx.Jogadores.Add(outroProfessor);
        await ctx.SaveChangesAsync();

        // O acordo é do OUTRO professor com o mesmo nome de aluno.
        ctx.PrecosDeAluno.Add(new PrecoDeAluno { ProfessorId = outroProfessor.Id, NomeAvulso = "Medina", Preco = 60 });
        await ctx.SaveChangesAsync();

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 1);

        Assert.Equal(110, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task Local_sem_preco_de_dupla_cobra_o_individual()
    {
        // Professor que só anunciou um valor continua funcionando exatamente como antes —
        // era o único preço que existia até 03/08/2026.
        var (ctx, professor, local) = Montar(individual: 110, dupla: null, trio: null);
        using var _ = ctx;

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 2);

        Assert.Equal(110, (await ctx.Aulas.SingleAsync()).Preco);
    }

    // ---- O outro caminho: quem marca é o ALUNO ----
    // A regra é a mesma, mas a fiação é outra (AulasController.Aluno). Um erro só aqui
    // significaria o aluno marcar aula pra dois e ser cobrado o preço de um.

    private static async Task<(DbPadelContext ctx, Jogador professor, LocalAula local, Jogador aluno)>
        MontarComAlunoAsync()
    {
        var (ctx, professor, local) = Montar();

        var aluno = new Jogador
        {
            Nome = "Eduarda Barth", Login = "eduarda", Cpf = "99900000005",
            Email = "eduarda@teste.local",
        };
        professor.Email = "jonatas@teste.local";
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        return (ctx, professor, local, aluno);
    }

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> Solicitar(
        AulasController controller, Jogador professor, LocalAula local, int quantos) =>
        controller.Solicitar(
            professorId: professor.Id, localId: local.Id,
            dataHora: DateTime.Today.AddDays(4).AddHours(8),
            ehPacote: false, recorrente: false, semanasRecorrencia: 0,
            nomeCompleto: "Eduarda", acompanhantes: null, pacoteId: null,
            quantidadeAlunos: quantos);

    [Theory]
    [InlineData(1, 110)]
    [InlineData(2, 150)]
    [InlineData(3, 180)]
    public async Task Quando_o_aluno_marca_o_preco_tambem_segue_o_tamanho(int quantos, decimal esperado)
    {
        var (ctx, professor, local, aluno) = await MontarComAlunoAsync();
        using var _ = ctx;

        await Solicitar(TestInfra.NovoAulasController(ctx, aluno.Id), professor, local, quantos);

        var aula = await ctx.Aulas.SingleAsync();
        Assert.Equal(esperado, aula.Preco);
        Assert.Equal(quantos, aula.QuantidadeAlunos);
    }

    [Fact]
    public async Task O_acordo_do_aluno_com_conta_vale_quando_ele_mesmo_marca()
    {
        var (ctx, professor, local, aluno) = await MontarComAlunoAsync();
        using var _ = ctx;

        ctx.PrecosDeAluno.Add(new PrecoDeAluno { ProfessorId = professor.Id, AlunoId = aluno.Id, Preco = 90 });
        await ctx.SaveChangesAsync();

        await Solicitar(TestInfra.NovoAulasController(ctx, aluno.Id), professor, local, quantos: 1);

        Assert.Equal(90, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task Pagina_antiga_sem_o_campo_novo_continua_marcando_aula_individual()
    {
        // Aba aberta antes deste deploy não manda `quantidadeAlunos`. O padrão do parâmetro
        // segura: vira individual, que é o que ela sempre marcou.
        var (ctx, professor, local, aluno) = await MontarComAlunoAsync();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, aluno.Id).Solicitar(
            professorId: professor.Id, localId: local.Id,
            dataHora: DateTime.Today.AddDays(4).AddHours(8),
            ehPacote: false, recorrente: false, semanasRecorrencia: 0);

        var aula = await ctx.Aulas.SingleAsync();
        Assert.Equal(1, aula.QuantidadeAlunos);
        Assert.Equal(110, aula.Preco);
    }

    // ---- Guardar o acordo ----

    [Fact]
    public async Task Salvar_de_novo_reajusta_em_vez_de_criar_uma_segunda_linha()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        // Precisa existir aula com esse aluno: é o que prova que ele é aluno DESTE professor.
        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 1);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.SalvarPrecoDeAluno(alunoId: null, nomeAvulso: "Medina", preco: 90, observacao: "aluno antigo");
        await controller.SalvarPrecoDeAluno(alunoId: null, nomeAvulso: "Medina", preco: 95, observacao: null);

        var acordo = await ctx.PrecosDeAluno.SingleAsync();
        Assert.Equal(95, acordo.Preco);
    }

    [Fact]
    public async Task Nao_da_pra_combinar_preco_com_quem_nao_e_seu_aluno()
    {
        var (ctx, professor, _) = Montar();
        using var _ctx = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.SalvarPrecoDeAluno(alunoId: null, nomeAvulso: "Desconhecido", preco: 10, observacao: null);

        Assert.Empty(await ctx.PrecosDeAluno.ToListAsync());
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }

    [Fact]
    public async Task Preco_zero_ou_negativo_e_recusado()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(TestInfra.NovoAulasController(ctx, professor.Id), local, "Medina", quantos: 1);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.SalvarPrecoDeAluno(alunoId: null, nomeAvulso: "Medina", preco: 0, observacao: null);
        await controller.SalvarPrecoDeAluno(alunoId: null, nomeAvulso: "Medina", preco: -50, observacao: null);

        Assert.Empty(await ctx.PrecosDeAluno.ToListAsync());
    }

    [Fact]
    public async Task Professor_nao_remove_o_acordo_de_outro_professor()
    {
        var (ctx, professor, _) = Montar();
        using var _ctx = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000002", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var acordo = new PrecoDeAluno { ProfessorId = professor.Id, NomeAvulso = "Medina", Preco = 90 };
        ctx.PrecosDeAluno.Add(acordo);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, intruso.Id).RemoverPrecoDeAluno(acordo.Id);

        Assert.Single(await ctx.PrecosDeAluno.ToListAsync());
    }
}
