using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// "Vai recuperar": o aluno não vem, a aula é COBRADA assim mesmo e ele repõe outro dia.
//
// O sistema só sabia dizer "cancelada" (não cobra) ou "faltou + cobra" (cobra e não gera
// crédito nenhum). Quem cobra por mês precisa das duas coisas ao mesmo tempo — e precisa que
// o horário VAGUE, senão o encaixe do dia seguinte não cabe na agenda.
public class AulaARecuperarTests
{
    private static (DbPadelContext ctx, Jogador professor, Jogador aluno, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Joao", Login = "joao", Cpf = "99900000011", IsProfessor = true };
        var aluno = new Jogador { Nome = "Eduarda", Login = "duda", Cpf = "99900000012" };
        ctx.Jogadores.AddRange(professor, aluno);
        ctx.SaveChanges();

        var local = new LocalAula
        {
            ProfessorId = professor.Id, Nome = "Arena Beach", PrecoPadrao = 110, Ativo = true,
        };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, aluno, local);
    }

    private static Aula Marcada(DbPadelContext ctx, Jogador professor, Jogador? aluno, LocalAula local,
        DateTime quando, string status = PoliticaAula.Confirmada, decimal preco = 110)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id,
            AlunoId = aluno?.Id,
            NomeAlunoAvulso = aluno?.Nome ?? "Medina",
            LocalAulaId = local.Id,
            DataHora = quando,
            Preco = preco,
            Status = status,
            QuantidadeAlunos = 1,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    // ─── Marcar ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Vai_recuperar_tira_da_agenda_e_mantem_a_cobranca()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(1).AddHours(9));

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var depois = await ctx.Aulas.SingleAsync();
        Assert.Equal(PoliticaAula.ARecuperar, depois.Status);

        // As duas metades do pedido, e é a segunda que o "Cancelada" nunca deu: a aula sai da
        // agenda (não conta como ativa) mas o dinheiro continua de pé.
        Assert.False(PoliticaAula.ContaComoAtiva(depois.Status));
        Assert.True(depois.CobrarMesmoFaltando);
    }

    [Fact]
    public async Task O_horario_da_aula_a_recuperar_VAGA_pra_outro_aluno()
    {
        // É o pedido inteiro: "sai da agenda do dia". Sem isto o professor não consegue pôr
        // ninguém no lugar de quem não vem, e a hora de quadra se perde igual.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var quando = DateTime.Today.AddDays(1).AddHours(9);
        var aula = Marcada(ctx, professor, aluno, local, quando);

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Outro aluno", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(quando), hora: DataEHoraDoFormulario.ParaCampoDeHora(quando), preco: null, recorrente: false, semanasRecorrencia: 0);

        Assert.True(await ctx.Aulas.AnyAsync(a => a.NomeAlunoAvulso == "Outro aluno"));
    }

    [Fact]
    public async Task O_evento_sai_da_Google_Agenda_junto()
    {
        // O horário vagou aqui dentro; deixar o evento lá faria o professor olhar o celular
        // e achar que continua ocupado.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(1).AddHours(9));
        aula.GoogleEventId = "evento-123";
        await ctx.SaveChangesAsync();

        var google = Substitute.For<IGoogleCalendarService>();
        await TestInfra.NovoAulasController(ctx, professor.Id, google).MarcarParaRecuperar(aula.Id);

        await google.Received(1).RemoverEventoAsync(professor.Id, "evento-123");
        Assert.Null((await ctx.Aulas.SingleAsync()).GoogleEventId);
    }

    [Theory]
    [InlineData(PoliticaAula.Pendente)]
    [InlineData(PoliticaAula.Realizada)]
    [InlineData(PoliticaAula.Cancelada)]
    [InlineData(PoliticaAula.Faltou)]
    public async Task So_aula_confirmada_entra_na_fila(string status)
    {
        // Pendente nem foi aceita; realizada aconteceu; cancelada e falta já são fato
        // registrado. Nenhuma delas tem o que repor.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(1).AddHours(9), status);

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        Assert.Equal(status, (await ctx.Aulas.SingleAsync()).Status);
    }

    [Fact]
    public async Task Professor_nao_mexe_na_aula_de_outro_professor()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Intruso", Login = "intruso", Cpf = "99900000013", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(1).AddHours(9));

        await TestInfra.NovoAulasController(ctx, intruso.Id).MarcarParaRecuperar(aula.Id);

        Assert.Equal(PoliticaAula.Confirmada, (await ctx.Aulas.SingleAsync()).Status);
    }

    // ─── A fila ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_aula_fica_na_fila_ate_ser_encaixada()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var naFila = await FilaAsync(ctx, professor.Id);
        Assert.Equal(aula.Id, Assert.Single(naFila).Id);

        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(
            aulaId: aula.Id, localId: local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(10)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(10)));

        Assert.Empty(await FilaAsync(ctx, professor.Id));
    }

    [Fact]
    public async Task A_fila_aparece_na_agenda_em_qualquer_data()
    {
        // A aula que ficou devendo é quase sempre de outra semana. Se ela só aparecesse na
        // janela na tela, sumiria justamente de quem precisa encaixá-la.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-40).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).MinhaAgenda(null, "semana", DateTime.Today);
        var vm = Assert.IsType<AgendaProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(aula.Id, Assert.Single(vm.ARecuperar).Id);
        // Os locais só viajam quando há fila — é o único formulário da tela que precisa deles.
        Assert.NotEmpty(vm.Locais);
    }

    // ─── Encaixar ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_reposicao_nasce_ligada_a_original_e_sem_cobrar_de_novo()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var quando = DateTime.Today.AddDays(4).AddHours(10);
        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(quando), hora: DataEHoraDoFormulario.ParaCampoDeHora(quando));

        var reposicao = await ctx.Aulas.SingleAsync(a => a.RecuperaAulaId != null);

        Assert.Equal(aula.Id, reposicao.RecuperaAulaId);
        Assert.Equal(quando, reposicao.DataHora);
        Assert.Equal(PoliticaAula.Confirmada, reposicao.Status);
        // O dinheiro ficou na original, que segue cobrável. Preço aqui seria cobrar duas
        // vezes a mesma aula — o oposto do combinado com o aluno.
        Assert.Equal(0, reposicao.Preco);
        Assert.Equal(aluno.Id, reposicao.AlunoId);
    }

    [Fact]
    public void A_reposicao_nao_entra_na_serie_da_aula_fixa()
    {
        // Herdar a recorrência faria o renovador da aula fixa repetir a reposição toda
        // semana — uma aula de graça por semana, pra sempre.
        var original = new Aula
        {
            ProfessorId = 1, AlunoId = 7, LocalAulaId = 3, DataHora = DateTime.Today,
            Preco = 110, QuantidadeAlunos = 3, Status = PoliticaAula.ARecuperar,
            RecorrenciaId = Guid.NewGuid(), RecorrenciaSemFim = true, Id = 42,
        };

        var reposicao = Reposicao.Encaixar(original, localId: 3, quando: DateTime.Today.AddDays(3), duracaoMinutos: 90);

        Assert.Null(reposicao.RecorrenciaId);
        Assert.False(reposicao.RecorrenciaSemFim);
        // O que ela HERDA: é a mesma aula, noutro dia.
        Assert.Equal(3, reposicao.QuantidadeAlunos);
        Assert.Equal(7, reposicao.AlunoId);
        Assert.Equal(90, reposicao.DuracaoMinutos);
    }

    [Fact]
    public async Task Nao_da_pra_encaixar_a_mesma_aula_duas_vezes()
    {
        // Dois cliques no botão (ou dois celulares na mesma tela) dariam duas aulas de graça.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(10)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(10)));
        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(5).AddHours(10)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(5).AddHours(10)));

        Assert.Equal(1, await ctx.Aulas.CountAsync(a => a.RecuperaAulaId == aula.Id));
    }

    [Fact]
    public async Task O_encaixe_respeita_horario_ja_ocupado()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var ocupado = DateTime.Today.AddDays(4).AddHours(10);
        Marcada(ctx, professor, null, local, ocupado);

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(ocupado), hora: DataEHoraDoFormulario.ParaCampoDeHora(ocupado));

        Assert.False(await ctx.Aulas.AnyAsync(a => a.RecuperaAulaId != null));
    }

    [Fact]
    public async Task O_aluno_com_conta_e_avisado_do_dia_da_reposicao()
    {
        // Ele combinou repor e agora tem dia e hora — pessoal, urgente e acionável.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoAulasController(ctx, professor.Id, push: push)
            .Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(4).AddHours(10)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(4).AddHours(10)));

        await push.Received(1).EnviarParaJogadorAsync(
            aluno.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppEWhatsApp);
    }

    // ─── Desistir da reposição ────────────────────────────────────────────────────────

    [Fact]
    public async Task Nao_vai_mais_recuperar_vira_falta_cobrada_e_sai_da_fila()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        await TestInfra.NovoAulasController(ctx, professor.Id).NaoVaiRecuperar(aula.Id);

        var depois = await ctx.Aulas.SingleAsync();
        Assert.Equal(PoliticaAula.Faltou, depois.Status);
        // O combinado era cobrar; foi só a reposição que caiu.
        Assert.True(depois.CobrarMesmoFaltando);
        Assert.Empty(await FilaAsync(ctx, professor.Id));
    }

    [Fact]
    public async Task Registrar_presenca_nao_apaga_a_cobranca_de_quem_esta_na_fila()
    {
        // Sem a trava, "faltou, não cobra" gravaria CobrarMesmoFaltando = false por baixo: a
        // aula sairia do financeiro calada e continuaria na fila de reposição.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddDays(-3).AddHours(9));
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .RegistrarPresenca(aula.Id, compareceu: false, cobrarMesmoAssim: false);

        var depois = await ctx.Aulas.SingleAsync();
        Assert.Equal(PoliticaAula.ARecuperar, depois.Status);
        Assert.True(depois.CobrarMesmoFaltando);
    }

    // ─── O dinheiro ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_aula_a_recuperar_continua_no_financeiro_como_a_receber()
    {
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddHours(9), preco: 150);
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(150, vm.AReceber);
        Assert.Equal(150, Assert.Single(vm.Devedores).Valor);
    }

    [Fact]
    public async Task A_reposicao_nao_infla_o_faturamento()
    {
        // O risco de cobrar duas vezes é aqui: a original conta como a receber e a reposição
        // como realizada. Se ela tivesse preço, o mês fecharia com o dobro daquela aula.
        var (ctx, professor, aluno, local) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, aluno, local, DateTime.Today.AddHours(9), preco: 150);
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(aula.Id);
        await TestInfra.NovoAulasController(ctx, professor.Id)
            .Encaixar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(10)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(10)));

        var reposicao = await ctx.Aulas.SingleAsync(a => a.RecuperaAulaId != null);
        reposicao.Status = PoliticaAula.Realizada;
        await ctx.SaveChangesAsync();

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).Financeiro("mes");
        var vm = Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(150, vm.AReceber);
        Assert.Equal(0, vm.Recebido);
    }

    private static async Task<List<Aula>> FilaAsync(DbPadelContext ctx, int professorId)
    {
        var aRecuperar = await ctx.Aulas
            .Where(a => a.ProfessorId == professorId && a.Status == PoliticaAula.ARecuperar)
            .ToListAsync();

        var jaEncaixadas = await ctx.Aulas
            .Where(a => a.ProfessorId == professorId && a.RecuperaAulaId != null)
            .Select(a => a.RecuperaAulaId!.Value)
            .ToListAsync();

        return Reposicao.AindaSemEncaixe(aRecuperar, jaEncaixadas.ToHashSet());
    }
}
