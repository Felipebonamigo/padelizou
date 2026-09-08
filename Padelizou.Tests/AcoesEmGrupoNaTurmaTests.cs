using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// A tela mostra UM card pra turma inteira (ver Services/AgendaDeTurma) — então as ações desse
// card (Concluir, Cancelar, Apagar, Editar horário) precisam valer pros N alunos, senão o
// professor clica uma vez achando que resolveu a turma toda e 2 de 3 alunos ficam do jeito
// que estavam, quietos, sem ele saber.
public class AcoesEmGrupoNaTurmaTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local, Guid turma) MontarTurma()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        var turma = Guid.NewGuid();
        var quando = DateTime.Today.AddDays(2).AddHours(9);

        ctx.Aulas.AddRange(
            Linha(professor, local, quando, turma, "Medina", 60),
            Linha(professor, local, quando, turma, "Coello", 60),
            Linha(professor, local, quando, turma, "Lima", 60));
        ctx.SaveChanges();

        return (ctx, professor, local, turma);
    }

    private static Aula Linha(Jogador professor, LocalAula local, DateTime quando, Guid turma, string nome, decimal preco) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 90,
        Preco = preco,
        Status = PoliticaAula.Confirmada,
        QuantidadeAlunos = 3,
        TurmaId = turma,
        NomeAlunoAvulso = nome,
        GoogleEventId = "evt-turma",
    };

    [Fact]
    public async Task Concluir_uma_linha_da_turma_conclui_as_tres()
    {
        var (ctx, professor, _, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.FirstAsync(a => a.TurmaId == turma);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(umaDaTurma.Id, PoliticaAula.Realizada);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync();
        Assert.Equal(3, todas.Count);
        Assert.All(todas, a => Assert.Equal(PoliticaAula.Realizada, a.Status));
        Assert.All(todas, a => Assert.True(a.Compareceu));
    }

    [Fact]
    public async Task Cancelar_uma_linha_da_turma_cancela_as_tres()
    {
        var (ctx, professor, _, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.FirstAsync(a => a.TurmaId == turma);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .AtualizarStatus(umaDaTurma.Id, PoliticaAula.Cancelada);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync();
        Assert.All(todas, a => Assert.Equal(PoliticaAula.Cancelada, a.Status));
        Assert.All(todas, a => Assert.NotNull(a.CanceladaEm));
    }

    [Fact]
    public async Task Aula_sozinha_sem_turma_continua_so_afetando_a_si_mesma()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;

        var professor = new Jogador { Nome = "Marcio", Login = "marcio", Cpf = "55500000009", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();
        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();

        var solo = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = DateTime.Today.AddDays(2).AddHours(9),
            DuracaoMinutos = 60, Preco = 100, Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Leonardo",
        };
        var outra = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = DateTime.Today.AddDays(3).AddHours(9),
            DuracaoMinutos = 60, Preco = 100, Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Outro",
        };
        ctx.Aulas.AddRange(solo, outra);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).AtualizarStatus(solo.Id, PoliticaAula.Realizada);

        Assert.Equal(PoliticaAula.Realizada, (await ctx.Aulas.FindAsync(solo.Id))!.Status);
        Assert.Equal(PoliticaAula.Confirmada, (await ctx.Aulas.FindAsync(outra.Id))!.Status);
    }

    [Fact]
    public async Task Apagar_uma_linha_da_turma_apaga_as_tres_e_remove_o_evento_uma_vez_so()
    {
        var (ctx, professor, _, turma) = MontarTurma();
        using var _ = ctx;

        var google = Substitute.For<IGoogleCalendarService>();
        var umaDaTurma = await ctx.Aulas.FirstAsync(a => a.TurmaId == turma);

        await TestInfra.NovoAulasController(ctx, professor.Id, google).ExcluirAula(umaDaTurma.Id);

        Assert.Empty(await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync());
        await google.Received(1).RemoverEventoAsync(professor.Id, "evt-turma");
    }

    // 🔁 REVISTO 08/09/2026 — pedido do Felipe: "quando marco 4 atletas as vezes ele buga e
    // da uns valores doidos". Esta era a versão que travava o comportamento ERRADO: editar
    // preço numa turma gravava o número digitado só na linha clicada e deixava os colegas com
    // a fatia antiga — a soma seguinte (AgendaDeTurma.Colapsar, que é o que o card da agenda
    // mostra) inflava a cada edição, porque o professor via o TOTAL no card, abria "Editar",
    // via a FATIA de uma pessoa só (bem menor) e "corrigia" pro total que tinha visto — sem
    // saber que só aquela linha mudava. Agora o campo fala do TOTAL dos dois lados (GET e
    // POST), e o valor novo é rachado de novo em fatias iguais.
    [Fact]
    public async Task Editar_a_turma_racha_o_novo_total_em_fatias_iguais()
    {
        var (ctx, professor, local, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);
        var novoHorario = umaDaTurma.DataHora.AddHours(3);

        // Total de 180 (60+60+60) vira 120 — cada um passa a valer 40, não "40 pra quem foi
        // editado e 60 pros outros dois" (o que a soma seguinte mostraria como 160, não 120).
        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(umaDaTurma.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(novoHorario), hora: DataEHoraDoFormulario.ParaCampoDeHora(novoHorario), preco: 120, duracaoMinutos: 90);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).OrderBy(a => a.Id).ToListAsync();
        Assert.All(todas, a => Assert.Equal(novoHorario, a.DataHora));
        Assert.All(todas, a => Assert.Equal(40m, a.Preco));
        Assert.Equal(120m, todas.Sum(a => a.Preco));
    }

    [Fact]
    public async Task Editar_so_o_horario_mantem_o_total_da_turma_intacto()
    {
        // Mesmo total (180) reenviado junto com o horário novo: as fatias voltam a ser
        // rachadas, mas o resultado é idêntico ao que já era — ninguém deveria notar diferença
        // só por causa da rachadura de novo.
        var (ctx, professor, local, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);
        var novoHorario = umaDaTurma.DataHora.AddHours(3);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(umaDaTurma.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(novoHorario), hora: DataEHoraDoFormulario.ParaCampoDeHora(novoHorario), preco: 180, duracaoMinutos: 90);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync();
        Assert.All(todas, a => Assert.Equal(60m, a.Preco));
    }

    // Racha sem perder centavo — mesma garantia de PrecoDaAula.DivididoIgualmente na criação,
    // agora também vale na edição.
    [Fact]
    public async Task Editar_racha_o_novo_total_sem_perder_centavo()
    {
        var (ctx, professor, local, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Editar(umaDaTurma.Id, local.Id,
                data: DataEHoraDoFormulario.ParaCampoDeData(umaDaTurma.DataHora),
                hora: DataEHoraDoFormulario.ParaCampoDeHora(umaDaTurma.DataHora),
                preco: 100);

        var todas = await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync();
        Assert.Equal(100m, todas.Sum(a => a.Preco));
    }

    // O CORAÇÃO DO PEDIDO: a tela de editar precisa mostrar o TOTAL da turma (o mesmo número
    // que o card da agenda já soma — ver AgendaDeTurma.Colapsar), não a fatia da linha clicada.
    // Sem isso o professor nunca sabe que número está editando de verdade.
    [Fact]
    public async Task Editar_GET_mostra_o_total_da_turma_no_campo_preco()
    {
        var (ctx, professor, _, turma) = MontarTurma();
        using var _ = ctx;
        var umaDaTurma = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id).Editar(umaDaTurma.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        var model = Assert.IsType<Aula>(view.Model);
        Assert.Equal(180m, model.Preco);
    }

    // O aluno da turma precisa saber da PRÓPRIA fatia, não do total — "sua aula subiu pra
    // R$210" seria mentira sobre o que ELE deve quando só a fatia dele foi de 60 pra 70.
    [Fact]
    public async Task Aluno_da_turma_e_avisado_da_propria_fatia_nao_do_total()
    {
        var (ctx, professor, local, turma) = MontarTurma();
        using var _ = ctx;

        var aluno = new Jogador { Nome = "Eduarda", Login = "eduarda", Cpf = "99900000003" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        var editada = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);
        editada.AlunoId = aluno.Id;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoAulasController(ctx, professor.Id, push: push);

        // Total sobe de 180 pra 210 — a fatia dela sobe de 60 pra 70.
        await controller.Editar(editada.Id, local.Id,
            data: DataEHoraDoFormulario.ParaCampoDeData(editada.DataHora),
            hora: DataEHoraDoFormulario.ParaCampoDeHora(editada.DataHora),
            preco: 210);

        await push.Received(1).EnviarParaJogadorAsync(
            aluno.Id, Arg.Any<string>(),
            Arg.Is<string>(msg => msg != null && msg.Contains("60") && msg.Contains("70") && !msg.Contains("210")),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Editar_o_horario_da_turma_toda_nao_esbarra_nos_proprios_colegas()
    {
        // Sem a exclusão por TurmaId na checagem de ocupação (HorarioOcupadoAsync), mover o
        // horário da sessão acharia que a quadra já está ocupada pelos próprios colegas —
        // que, com a edição cascadeando (ver o teste acima), estão indo pro MESMO horário novo.
        var (ctx, professor, local, turma) = MontarTurma();
        using var _ = ctx;

        var umaDaTurma = await ctx.Aulas.OrderBy(a => a.Id).FirstAsync(a => a.TurmaId == turma);
        var novoHorario = umaDaTurma.DataHora.AddHours(1);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(umaDaTurma.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(novoHorario), hora: DataEHoraDoFormulario.ParaCampoDeHora(novoHorario), preco: 60, duracaoMinutos: 90);

        Assert.Null(controller.TempData["Erro"]);
        Assert.All(await ctx.Aulas.Where(a => a.TurmaId == turma).ToListAsync(),
            a => Assert.Equal(novoHorario, a.DataHora));
    }

    [Fact]
    public async Task MinhaAgenda_mostra_a_turma_como_um_card_so()
    {
        var (ctx, professor, _, _) = MontarTurma();
        using var _ = ctx;

        var resultado = await TestInfra.NovoAulasController(ctx, professor.Id)
            .MinhaAgenda(vista: "lista", periodo: "semana", data: DateTime.Today.AddDays(2));

        var vm = (AgendaProfessorVM)((Microsoft.AspNetCore.Mvc.ViewResult)resultado).Model!;

        Assert.Single(vm.NoPeriodo);
        Assert.Equal("Medina, Coello e Lima", vm.NoPeriodo[0].NomeCompletoAluno);
        Assert.Equal(180, vm.NoPeriodo[0].Preco);
    }
}
