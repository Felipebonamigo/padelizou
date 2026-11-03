using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🗣️ Jônatas (Portal Padel), 31/10/2026: "se ele paga quadra para mim sempre ou pro clube".
// Até aqui a escolha só existia aula a aula, na hora de marcar. Agora a ficha do aluno guarda
// o PADRÃO, as aulas futuras mudam junto quando ele muda (Felipe, 02/11: "futuras mudam
// junto"), e o Editar da aula troca uma aula só.
public class QuadraPorForaNaFichaTests
{
    private static async Task<(DbPadelContext ctx, Jogador professor, LocalAula local)> Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var professor = new Jogador { Nome = "Professor Jonatas", Cpf = "11144477735", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();
        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena", PrecoPadrao = 100m, CustoPorAula = 50m, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();
        return (ctx, professor, local);
    }

    private static Aula Aula(int professorId, LocalAula local, string nome, int dias,
        string status = "Confirmada", bool pagaQuadra = false, int? alunoId = null) => new()
    {
        ProfessorId = professorId,
        AlunoId = alunoId,
        NomeAlunoAvulso = nome,
        LocalAulaId = local.Id,
        DataHora = DateTime.Today.AddDays(dias).AddHours(10),
        Preco = 100m,
        Status = status,
        AlunoPagaQuadra = pagaQuadra,
    };

    private static Task Salvar(AulasController c, string nome, bool? pagaQuadra, int? alunoId = null) =>
        c.SalvarCadastroDoAluno(alunoId, alunoId == null ? nome : null, null, null, null, null, null, pagaQuadra);

    [Fact]
    public async Task Marcar_na_ficha_grava_o_padrao_e_muda_as_aulas_futuras()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Joao", 3), Aula(professor.Id, local, "Joao", 10, "Pendente"));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", true);

        Assert.True((await ctx.CadastrosDeAlunos.SingleAsync()).PagaQuadraPorFora);
        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.True(a.AlunoPagaQuadra));
    }

    [Fact]
    public async Task Aula_que_ja_passou_fica_como_estava_porque_ja_entrou_no_financeiro()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.AddRange(
            Aula(professor.Id, local, "Joao", -7, "Realizada"),
            Aula(professor.Id, local, "Joao", -1, "Confirmada"),   // passou e ninguém concluiu
            Aula(professor.Id, local, "Joao", 5, "Cancelada"));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", true);

        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.False(a.AlunoPagaQuadra));
    }

    [Fact]
    public async Task Desmarcar_devolve_as_futuras_pro_professor()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", PagaQuadraPorFora = true });
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao", 3, pagaQuadra: true));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", false);

        Assert.False((await ctx.CadastrosDeAlunos.SingleAsync()).PagaQuadraPorFora);
        Assert.False((await ctx.Aulas.SingleAsync()).AlunoPagaQuadra);
    }

    [Fact]
    public async Task Salvar_a_ficha_sem_mudar_o_padrao_nao_desfaz_a_aula_trocada_a_mao()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", PagaQuadraPorFora = true });
        // Nesta semana o professor trocou: a quadra é por conta dele.
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao", 3, pagaQuadra: false));
        await ctx.SaveChangesAsync();

        // Só foi corrigir o celular — o padrão segue o mesmo.
        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", true);

        Assert.False((await ctx.Aulas.SingleAsync()).AlunoPagaQuadra);
    }

    [Fact]
    public async Task Formulario_que_nao_manda_o_campo_nao_mexe_em_nada()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", PagaQuadraPorFora = true });
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao", 3, pagaQuadra: true));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", null);

        Assert.True((await ctx.CadastrosDeAlunos.SingleAsync()).PagaQuadraPorFora);
        Assert.True((await ctx.Aulas.SingleAsync()).AlunoPagaQuadra);
    }

    [Fact]
    public async Task Nao_mexe_em_outro_aluno_nem_em_outro_professor()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var outro = new Jogador { Nome = "Outro Professor", Cpf = "52998224725", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Maria", 3), Aula(outro.Id, local, "Joao", 3), Aula(professor.Id, local, "Joao", 3));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "Joao", true);

        Assert.False((await ctx.Aulas.SingleAsync(a => a.NomeAlunoAvulso == "Maria")).AlunoPagaQuadra);
        Assert.False((await ctx.Aulas.SingleAsync(a => a.ProfessorId == outro.Id)).AlunoPagaQuadra);
        Assert.True((await ctx.Aulas.SingleAsync(a => a.ProfessorId == professor.Id && a.NomeAlunoAvulso == "Joao")).AlunoPagaQuadra);
    }

    [Fact]
    public async Task Aluno_com_conta_e_achado_pela_conta()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var aluno = new Jogador { Nome = "Pedro Henrique", Cpf = "22255588846" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();
        // O nome escrito fica na aula mesmo com conta — e pode até ser outro.
        ctx.Aulas.AddRange(Aula(professor.Id, local, "Pedrinho", 3, alunoId: aluno.Id), Aula(professor.Id, local, "Pedrinho", 4));
        await ctx.SaveChangesAsync();

        await Salvar(TestInfra.NovoAulasController(ctx, professor.Id), "", true, aluno.Id);

        Assert.True((await ctx.Aulas.SingleAsync(a => a.AlunoId == aluno.Id)).AlunoPagaQuadra);
        // O homônimo sem conta é OUTRA pessoa pra régua da chave.
        Assert.False((await ctx.Aulas.SingleAsync(a => a.AlunoId == null)).AlunoPagaQuadra);
    }

    [Fact]
    public async Task A_tela_de_marcar_aula_recebe_quem_paga_a_quadra_por_fora()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.CadastrosDeAlunos.AddRange(
            new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", PagaQuadraPorFora = true },
            new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Maria", PagaQuadraPorFora = false });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.AdicionarManual();

        object? noViewBag = controller.ViewBag.QuadraPorFora;
        var chaves = Assert.IsAssignableFrom<IEnumerable<string>>(noViewBag);
        Assert.Equal(new List<string> { PrecoDaAula.Chave(null, "Joao") }, chaves.ToList());
    }

    [Fact]
    public async Task O_painel_mostra_o_padrao_da_ficha_pra_caixa_abrir_marcada()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        ctx.Aulas.Add(Aula(professor.Id, local, "Joao", 3));
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno { ProfessorId = professor.Id, NomeAvulso = "Joao", PagaQuadraPorFora = true });
        // O painel só abre com a escada do professor completa (cidade, local e horário).
        var cidade = new Cidade { Nome = "Porto Alegre" };
        ctx.Cidades.Add(cidade);
        await ctx.SaveChangesAsync();
        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = professor.Id, CidadeId = cidade.Id });
        ctx.HorariosDisponiveis.Add(new HorarioDisponivel
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DiaSemana = 1, HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0),
            DuracaoMinutos = 60, Ativo = true,
        });
        await ctx.SaveChangesAsync();

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).Dashboard();
        var vm = Assert.IsType<Padelizou.ViewModels.PainelProfessorViewModel>(
            Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.True(Assert.Single(vm.Alunos).PagaQuadraPorFora);
    }

    // ---- Editar uma aula só ----

    private static Task Editar(AulasController c, Aula aula, LocalAula local, bool? pagaQuadra) =>
        c.Editar(aula.Id, local.Id,
            data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora),
            hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora),
            preco: aula.Preco, duracaoMinutos: aula.DuracaoMinutos, esporte: aula.Esporte,
            alunoPagaQuadra: pagaQuadra);

    [Fact]
    public async Task Editar_troca_so_esta_aula_mesmo_mudando_nada_alem_disso()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var serie = Guid.NewGuid();
        var esta = Aula(professor.Id, local, "Joao", 3);
        var proxima = Aula(professor.Id, local, "Joao", 10);
        esta.RecorrenciaId = proxima.RecorrenciaId = serie;
        ctx.Aulas.AddRange(esta, proxima);
        await ctx.SaveChangesAsync();

        await Editar(TestInfra.NovoAulasController(ctx, professor.Id), esta, local, true);

        Assert.True((await ctx.Aulas.SingleAsync(a => a.Id == esta.Id)).AlunoPagaQuadra);
        Assert.False((await ctx.Aulas.SingleAsync(a => a.Id == proxima.Id)).AlunoPagaQuadra);
    }

    [Fact]
    public async Task Editar_sem_o_campo_mantem_como_estava()
    {
        var (ctx, professor, local) = await Montar();
        using var _ = ctx;
        var esta = Aula(professor.Id, local, "Joao", 3, pagaQuadra: true);
        ctx.Aulas.Add(esta);
        await ctx.SaveChangesAsync();

        await Editar(TestInfra.NovoAulasController(ctx, professor.Id), esta, local, null);

        Assert.True((await ctx.Aulas.SingleAsync()).AlunoPagaQuadra);
    }
}
