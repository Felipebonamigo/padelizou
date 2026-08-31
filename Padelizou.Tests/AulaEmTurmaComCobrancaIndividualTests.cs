using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A turma com N alunos, cada um com a PRÓPRIA cobrança — o pedido do Felipe depois de ver a
// tela "Em trio" só aceitando um nome: "teria que conseguir selecionar os 3 alunos aqui. Cada
// um recebe sua cobrança individualmente". Isto aqui é a fiação da decisão: o preço da turma
// racha igual entre os N (PrecoDaAula.DivididoIgualmente), as N linhas de Aula levam o mesmo
// TurmaId, e a Google Agenda ganha UM evento só pra sessão inteira.
public class AulaEmTurmaComCobrancaIndividualTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula
        {
            ProfessorId = professor.Id, Nome = "Batata Padel",
            PrecoPadrao = 110, Ativo = true,
            PrecosDeTurma = new List<PrecoDeTurma>
            {
                new() { QuantidadeAlunos = 3, Preco = 180 },
            },
        };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> MarcarEmTrio(
        AulasController controller, LocalAula local, List<string> nomes, decimal? preco = null,
        bool recorrente = false, int semanasRecorrencia = 0, bool semPrazo = false,
        List<string>? datas = null, IGoogleCalendarService? google = null) =>
        controller.AdicionarManual(
            localId: local.Id, nomeAluno: nomes.FirstOrDefault() ?? "", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: preco,
            recorrente: recorrente, semanasRecorrencia: semanasRecorrencia, quantidadeAlunos: 3,
            datas: datas, semPrazo: semPrazo, nomesAlunos: nomes,
            alunoIds: nomes.Select(_ => (int?)null).ToList(),
            telefonesAlunos: nomes.Select(_ => (string?)null).ToList());

    [Fact]
    public async Task Tres_nomes_viram_tres_linhas_com_o_mesmo_TurmaId()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await MarcarEmTrio(TestInfra.NovoAulasController(ctx, professor.Id), local,
            ["Medina", "Coello", "Lima"]);

        var aulas = await ctx.Aulas.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(3, aulas.Count);
        Assert.All(aulas, a => Assert.Equal(3, a.QuantidadeAlunos));
        Assert.NotNull(aulas[0].TurmaId);
        Assert.All(aulas, a => Assert.Equal(aulas[0].TurmaId, a.TurmaId));
        Assert.Equal(["Medina", "Coello", "Lima"], aulas.Select(a => a.NomeAlunoAvulso));
    }

    [Fact]
    public async Task O_preco_da_turma_racha_igual_entre_os_tres_sem_perder_centavo()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        // 100 em 3 não fecha exato — testa que a soma das linhas bate com o total mesmo assim.
        await MarcarEmTrio(TestInfra.NovoAulasController(ctx, professor.Id), local,
            ["Medina", "Coello", "Lima"], preco: 100);

        var precos = await ctx.Aulas.OrderBy(a => a.Id).Select(a => a.Preco).ToListAsync();
        Assert.Equal(100m, precos.Sum());
        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, precos);
    }

    [Fact]
    public async Task Cada_aluno_da_turma_mantem_a_propria_recorrencia()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await MarcarEmTrio(TestInfra.NovoAulasController(ctx, professor.Id), local,
            ["Medina", "Coello", "Lima"], recorrente: true, semanasRecorrencia: 3);

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.Equal(9, aulas.Count); // 3 alunos × 3 semanas

        var porAluno = aulas.GroupBy(a => a.NomeAlunoAvulso);
        Assert.Equal(3, porAluno.Count());
        foreach (var grupo in porAluno)
        {
            // As 3 semanas do MESMO aluno compartilham RecorrenciaId...
            var ids = grupo.Select(a => a.RecorrenciaId).Distinct().ToList();
            Assert.Single(ids);
            Assert.NotNull(ids[0]);
        }

        // ...mas alunos DIFERENTES têm séries independentes.
        var idsPorAluno = porAluno.Select(g => g.First().RecorrenciaId).ToList();
        Assert.Equal(3, idsPorAluno.Distinct().Count());
    }

    [Fact]
    public async Task Faltando_o_nome_de_um_aluno_nao_cria_nenhuma_aula()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await MarcarEmTrio(controller, local, ["Medina", "", "Lima"]);

        Assert.Empty(await ctx.Aulas.ToListAsync());
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task A_google_agenda_ganha_um_evento_so_pra_turma_inteira()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var google = Substitute.For<IGoogleCalendarService>();
        google.CriarEventoAsync(Arg.Any<Aula>()).Returns("evt-turma-1");

        await TestInfra.NovoAulasController(ctx, professor.Id, google)
            .AdicionarManual(
                localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
                data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
                recorrente: false, semanasRecorrencia: 0, quantidadeAlunos: 3,
                nomesAlunos: ["Medina", "Coello", "Lima"],
                alunoIds: [null, null, null], telefonesAlunos: [null, null, null]);

        await google.Received(1).CriarEventoAsync(Arg.Any<Aula>());

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.Equal(3, aulas.Count);
        Assert.All(aulas, a => Assert.Equal("evt-turma-1", a.GoogleEventId));
    }

    [Fact]
    public async Task Sem_nomesAlunos_continua_sendo_uma_linha_so_como_sempre_foi()
    {
        // O caminho de sempre: turma de 3, um nome só, os outros dois são "Acompanhantes"
        // (campo que esta ação nem vê). Não pode virar 3 linhas sem o professor pedir.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0, quantidadeAlunos: 3);

        var aula = await ctx.Aulas.SingleAsync();
        Assert.Equal(180, aula.Preco);
        Assert.Equal(3, aula.QuantidadeAlunos);
        Assert.Null(aula.TurmaId);
    }
}
