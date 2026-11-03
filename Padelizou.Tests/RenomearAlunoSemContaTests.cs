using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Tests;

// 🗣️ Jônatas (Portal Padel), 31/10/2026: "queria conseguir alterar as informações dos alunos
// cadastrados — tipo o nome do aluno". O aluno sem conta É o nome: a mesma string liga as
// aulas, a ficha, o preço combinado e as faturas. Renomear só a aula partiria o aluno em dois.
public class RenomearAlunoSemContaTests
{
    private static async Task<(DbPadelContext ctx, Jogador professor, LocalAula local)> Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var professor = new Jogador { Nome = "Professor Jonatas", Cpf = "11144477735", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena", PrecoPadrao = 100m, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();
        return (ctx, professor, local);
    }

    private static Aula Aula(int professorId, LocalAula local, string nome, int dias = 7) => new()
    {
        ProfessorId = professorId,
        NomeAlunoAvulso = nome,
        LocalAulaId = local.Id,
        DataHora = DateTime.Today.AddDays(dias),
        Preco = 100m,
        Status = "Confirmada",
    };

    private static FaturaDoAluno Fatura(int professorId, string nome) => new()
    {
        ProfessorId = professorId,
        NomeAvulso = nome,
        Ano = 2026,
        Mes = 9,
        Valor = 400m,
        QuantidadeAulas = 4,
        PagadorNome = nome,
        FechadaEm = new DateTime(2026, 10, 1),
        Vencimento = new DateTime(2026, 10, 10),
        Status = "Paga",
    };

    [Fact]
    public async Task Renomeia_aulas_ficha_preco_e_fatura_juntos()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao", -14), Aula(professor.Id, local, "Joao", 7));
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", Celular = "51999990000" });
        ctx.PrecosDeAluno.Add(new PrecoDeAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", Preco = 80m });
        ctx.FaturasDeAlunos.Add(Fatura(professor.Id, "Joao"));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("Joao", "João Pedro Silva");

        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.Equal("João Pedro Silva", a.NomeAlunoAvulso));
        Assert.Equal("João Pedro Silva", (await ctx.CadastrosDeAlunos.SingleAsync()).NomeAvulso);
        Assert.Equal("João Pedro Silva", (await ctx.PrecosDeAluno.SingleAsync()).NomeAvulso);
        Assert.Equal("João Pedro Silva", (await ctx.FaturasDeAlunos.SingleAsync()).NomeAvulso);
    }

    [Fact]
    public async Task A_fatura_ja_fechada_guarda_o_pagador_como_foi_cobrado()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao"));
        ctx.FaturasDeAlunos.Add(Fatura(professor.Id, "Joao"));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("Joao", "João Pedro");

        // A fatura é documento: o nome em que ela foi cobrada não muda depois.
        Assert.Equal("Joao", (await ctx.FaturasDeAlunos.SingleAsync()).PagadorNome);
    }

    [Fact]
    public async Task Pega_as_grafias_com_maiuscula_diferente_do_mesmo_aluno()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        // O sistema já trata "joao" e "Joao " como a mesma pessoa (PrecoDaAula.Chave).
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao"), Aula(professor.Id, local, "joao "));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("Joao", "João Pedro");

        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.Equal("João Pedro", a.NomeAlunoAvulso));
    }

    [Fact]
    public async Task So_corrigir_maiuscula_e_acento_e_permitido()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "joao"));
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("joao", "Joao");

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync()).NomeAlunoAvulso);
    }

    [Fact]
    public async Task Nao_mexe_no_aluno_de_mesmo_nome_de_outro_professor()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var outro = new Jogador { Nome = "Outro Professor", Cpf = "52998224725", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao"), Aula(outro.Id, local, "Joao"));
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = outro.Id, NomeAvulso = "Joao" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("Joao", "João Pedro");

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync(a => a.ProfessorId == outro.Id)).NomeAlunoAvulso);
        Assert.Equal("Joao", (await ctx.CadastrosDeAlunos.SingleAsync()).NomeAvulso);
    }

    [Fact]
    public async Task Nome_de_outro_aluno_e_recusado_porque_renomear_nao_junta_dois()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao"), Aula(professor.Id, local, "Maria"));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.RenomearAluno("Joao", " maria ");

        Assert.Equal(1, await ctx.Aulas.CountAsync(a => a.NomeAlunoAvulso == "Joao"));
        Assert.Contains("Já existe", controller.TempData["ErroPrecoAluno"]?.ToString());
    }

    [Fact]
    public async Task Nome_que_so_existe_na_ficha_tambem_conta_como_ocupado()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao"));
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Maria" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.RenomearAluno("Joao", "Maria");

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync()).NomeAlunoAvulso);
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Nome_em_branco_e_recusado(string? novo)
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao"));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.RenomearAluno("Joao", novo);

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync()).NomeAlunoAvulso);
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }

    [Fact]
    public async Task Nome_maior_que_a_coluna_e_recusado()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao"));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.RenomearAluno("Joao", new string('a', 101));

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync()).NomeAlunoAvulso);
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }

    [Fact]
    public async Task Aluno_que_nao_esta_na_agenda_e_recusado()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Fantasma" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.RenomearAluno("Fantasma", "Outro");

        Assert.Equal("Fantasma", (await ctx.CadastrosDeAlunos.SingleAsync()).NomeAvulso);
        Assert.NotNull(controller.TempData["ErroPrecoAluno"]);
    }

    [Fact]
    public async Task Aula_de_aluno_com_conta_nao_e_renomeada()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var comConta = new Jogador { Nome = "Joao Conta", Cpf = "22255588846" };
        ctx.Jogadores.Add(comConta);
        await ctx.SaveChangesAsync();
        var daConta = Aula(professor.Id, local, "Joao");
        daConta.AlunoId = comConta.Id;
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao"), daConta);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).RenomearAluno("Joao", "João Pedro");

        Assert.Equal("Joao", (await ctx.Aulas.SingleAsync(a => a.AlunoId == comConta.Id)).NomeAlunoAvulso);
    }
}
