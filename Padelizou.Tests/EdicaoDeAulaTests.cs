using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Mudar horário, local e valor de uma aula JÁ MARCADA. Até 09/08/2026 o único conserto era
// apagar e lançar de novo — o que levava as anotações junto (cascade), trocava o id (matando
// o link do caderno que o aluno já tinha) e mandava pro aluno um "aula apagada" seguido de
// nada.
public class EdicaoDeAulaTests
{
    private static readonly DateTime Agora = new(2026, 8, 9, 10, 0, 0);

    private static Aula Aula(string status) => new()
    {
        ProfessorId = 1, LocalAulaId = 1, DataHora = Agora.AddDays(4),
        Preco = 110, Status = status, NomeAlunoAvulso = "Medina",
    };

    // ---- A regra, sozinha ----

    [Fact]
    public void Aula_confirmada_e_pendente_podem_ser_editadas()
    {
        Assert.True(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Confirmada)));
        Assert.True(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Pendente)));
    }

    [Fact]
    public void Aula_que_ja_virou_fato_nao_e_editavel()
    {
        // Realizada e Faltou já entraram no financeiro; Cancelada e Recusada são registro do
        // que aconteceu. Mexer no horário delas reescreveria o passado.
        Assert.False(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Realizada)));
        Assert.False(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Faltou)));
        Assert.False(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Cancelada)));
        Assert.False(EdicaoDeAula.PodeEditar(Aula(PoliticaAula.Recusada)));
    }

    [Fact]
    public void Horario_novo_vai_pelo_WhatsApp_preco_novo_nao()
    {
        // Quem não souber do horário novo VAI À QUADRA NA HORA ERRADA — é pessoal, urgente e
        // acionável, os três critérios do canal. Preço o aluno acerta quando chegar.
        var mudouHorario = new MudancaDaAula(Agora, Agora.AddHours(1), "Batata", "Batata", 110, 110);
        var mudouPreco = new MudancaDaAula(Agora, Agora, "Batata", "Batata", 110, 130);

        Assert.Equal(AlcanceDoAviso.AppEWhatsApp, EdicaoDeAula.CanalDoAviso(mudouHorario));
        Assert.Equal(AlcanceDoAviso.SoApp, EdicaoDeAula.CanalDoAviso(mudouPreco));
    }

    [Fact]
    public void O_recado_diz_de_onde_pra_onde()
    {
        // "Sua aula é dia 14 às 9h" sozinho parece lembrete: quem tinha 13 às 8h anotado não
        // percebe que mudou.
        var mudanca = new MudancaDaAula(
            new DateTime(2026, 8, 13, 8, 0, 0), new DateTime(2026, 8, 14, 9, 0, 0),
            "Batata Padel", "Hangar", 110, 110);

        var recado = EdicaoDeAula.Recado(mudanca);

        Assert.Contains("13/08 às 08:00", recado);
        Assert.Contains("14/08 às 09:00", recado);
        Assert.Contains("Batata Padel", recado);
        Assert.Contains("Hangar", recado);
    }

    [Fact]
    public void Aluno_avulso_nao_recebe_aviso()
    {
        // Não tem conta: não há pra onde mandar. O professor combina por fora.
        var aula = Aula(PoliticaAula.Confirmada);
        var mudanca = new MudancaDaAula(aula.DataHora, aula.DataHora.AddHours(1), "Batata", "Batata", 110, 110);

        Assert.False(EdicaoDeAula.PrecisaAvisarAluno(aula, mudanca, Agora));
    }

    [Fact]
    public void Aula_que_ja_passou_nao_avisa_ninguem()
    {
        var aula = Aula(PoliticaAula.Confirmada);
        aula.AlunoId = 9;
        var mudanca = new MudancaDaAula(Agora.AddDays(-5), Agora.AddDays(-4), "Batata", "Batata", 110, 110);

        Assert.False(EdicaoDeAula.PrecisaAvisarAluno(aula, mudanca, Agora));
    }

    // ---- O caminho completo, no controller ----

    private static (DbPadelContext ctx, Jogador professor, LocalAula batata, LocalAula hangar) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var batata = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110 };
        var hangar = new LocalAula { ProfessorId = professor.Id, Nome = "Hangar", PrecoPadrao = 150 };
        ctx.LocaisAula.AddRange(batata, hangar);
        ctx.SaveChanges();

        return (ctx, professor, batata, hangar);
    }

    private static Aula Marcada(DbPadelContext ctx, Jogador professor, LocalAula local,
        DateTime quando, int? alunoId = null, string? googleEventId = null)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id, AlunoId = alunoId, LocalAulaId = local.Id,
            DataHora = quando, Preco = 110, Status = PoliticaAula.Confirmada,
            NomeAlunoAvulso = alunoId == null ? "Medina" : null,
            GoogleEventId = googleEventId,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    [Fact]
    public async Task Editar_muda_horario_local_e_valor()
    {
        var (ctx, professor, batata, hangar) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));
        var novoHorario = DateTime.Today.AddDays(5).AddHours(9);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, hangar.Id, novoHorario, 150m);

        var salva = await ctx.Aulas.SingleAsync();
        Assert.Equal(novoHorario, salva.DataHora);
        Assert.Equal(hangar.Id, salva.LocalAulaId);
        Assert.Equal(150m, salva.Preco);
    }

    [Fact]
    public async Task Editar_nao_apaga_as_anotacoes_da_aula()
    {
        // É a diferença inteira entre editar e "apagar e lançar de novo": as anotações caem por
        // cascade quando a aula é apagada, e o caderno é justamente o histórico do aluno.
        var (ctx, professor, batata, hangar) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));
        ctx.AnotacoesAula.Add(new AnotacaoAula { AulaId = aula.Id, AutorId = professor.Id, Texto = "bandeja" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, hangar.Id, DateTime.Today.AddDays(5).AddHours(9), 150m);

        var anotacao = await ctx.AnotacoesAula.SingleAsync();
        Assert.Equal(aula.Id, anotacao.AulaId);
    }

    [Fact]
    public async Task Horario_novo_vai_pro_Google()
    {
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8), googleEventId: "evt-1");

        var google = Substitute.For<IGoogleCalendarService>();
        google.AtualizarEventoAsync(Arg.Any<Aula>()).Returns("evt-1");

        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);
        await controller.Editar(aula.Id, batata.Id, DateTime.Today.AddDays(4).AddHours(9), 110m);

        // Sem isto a aula é corrigida no Padelizou e o Google segue com o horário velho — que
        // é onde o professor olha antes de marcar outra coisa.
        await google.Received(1).AtualizarEventoAsync(Arg.Is<Aula>(a => a != null && a.Id == aula.Id));
    }

    [Fact]
    public async Task Mudar_so_o_preco_nao_incomoda_o_Google()
    {
        // O preço não vai pro evento: disparar "aula atualizada" na agenda do aluno por causa
        // de R$ 20 é barulho à toa.
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8), googleEventId: "evt-1");

        var google = Substitute.For<IGoogleCalendarService>();
        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);

        await controller.Editar(aula.Id, batata.Id, aula.DataHora, 130m);

        await google.DidNotReceive().AtualizarEventoAsync(Arg.Any<Aula>());
        Assert.Equal(130m, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task Nao_remarca_por_cima_de_outra_aula()
    {
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var ocupado = DateTime.Today.AddDays(4).AddHours(9);
        Marcada(ctx, professor, batata, ocupado);
        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, batata.Id, ocupado, 110m);

        // Dois alunos no mesmo horário é o erro que a agenda existe pra evitar.
        Assert.Equal(DateTime.Today.AddDays(4).AddHours(8), (await ctx.Aulas.FindAsync(aula.Id))!.DataHora);
    }

    [Fact]
    public async Task A_propria_aula_nao_bloqueia_a_si_mesma()
    {
        // Sem tirar a própria aula da conta de conflito, mudar só o preço seria recusado por
        // "já existe aula nesse horário" — a dela mesma.
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, batata.Id, aula.DataHora, 95m);

        Assert.Equal(95m, (await ctx.Aulas.SingleAsync()).Preco);
    }

    [Fact]
    public async Task Professor_nao_edita_aula_de_outro_professor()
    {
        var (ctx, professor, batata, hangar) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000002", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, intruso.Id);
        await controller.Editar(aula.Id, hangar.Id, DateTime.Today.AddDays(9), 999m);

        var intacta = await ctx.Aulas.SingleAsync();
        Assert.Equal(batata.Id, intacta.LocalAulaId);
        Assert.Equal(110m, intacta.Preco);
    }

    [Fact]
    public async Task Nao_da_pra_mudar_o_local_pro_de_outro_professor()
    {
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var outro = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000002", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();

        var quadraAlheia = new LocalAula { ProfessorId = outro.Id, Nome = "Quadra do outro", PrecoPadrao = 200 };
        ctx.LocaisAula.Add(quadraAlheia);
        await ctx.SaveChangesAsync();

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(4).AddHours(8));

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, quadraAlheia.Id, aula.DataHora, 110m);

        Assert.Equal(batata.Id, (await ctx.Aulas.SingleAsync()).LocalAulaId);
    }

    [Fact]
    public async Task Aula_ja_realizada_nao_muda()
    {
        var (ctx, professor, batata, hangar) = Montar();
        using var _ = ctx;

        var aula = Marcada(ctx, professor, batata, DateTime.Today.AddDays(-3).AddHours(8));
        aula.Status = PoliticaAula.Realizada;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, hangar.Id, DateTime.Today.AddDays(5), 300m);

        var intacta = await ctx.Aulas.SingleAsync();
        Assert.Equal(batata.Id, intacta.LocalAulaId);
        Assert.Equal(110m, intacta.Preco);
    }

    [Fact]
    public async Task Aluno_com_conta_e_avisado_da_mudanca()
    {
        var (ctx, professor, batata, hangar) = Montar();
        using var _ = ctx;

        var aluno = new Jogador { Nome = "Eduarda", Login = "eduarda", Cpf = "99900000003" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        var aula = Marcada(ctx, professor, batata, DateTime.Now.AddDays(4), alunoId: aluno.Id);

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoAulasController(ctx, professor.Id, push: push);

        await controller.Editar(aula.Id, hangar.Id, DateTime.Now.AddDays(5), 150m);

        await push.Received(1).EnviarParaJogadorAsync(
            aluno.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppEWhatsApp);
    }

    // ---- Sincronizar com o Google: as aulas que ficaram pra trás ----

    [Fact]
    public async Task Sincronizar_manda_pro_Google_a_aula_que_ficou_sem_evento()
    {
        // O caso real: o professor marcou aulas ANTES de conectar a Google Agenda. O evento só
        // nascia no instante da criação, nada nunca tentava de novo, e da tela isso aparecia
        // como "algumas aulas vão pro Google, outras não".
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        var semEvento = Marcada(ctx, professor, batata, DateTime.Now.AddDays(4));
        var comEvento = Marcada(ctx, professor, batata, DateTime.Now.AddDays(5), googleEventId: "evt-ja-existe");

        var google = Substitute.For<IGoogleCalendarService>();
        google.EstaConectadoAsync(professor.Id).Returns(true);
        google.CriarEventoAsync(Arg.Any<Aula>()).Returns("evt-novo");

        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);
        await controller.SincronizarGoogle();

        Assert.Equal("evt-novo", (await ctx.Aulas.FindAsync(semEvento.Id))!.GoogleEventId);
        // A que já estava lá não pode virar evento duplicado.
        Assert.Equal("evt-ja-existe", (await ctx.Aulas.FindAsync(comEvento.Id))!.GoogleEventId);
        await google.Received(1).CriarEventoAsync(Arg.Any<Aula>());
    }

    [Fact]
    public async Task Sincronizar_nao_ressuscita_aula_passada_nem_cancelada()
    {
        // Encher a agenda de eventos de três meses atrás não ajuda ninguém — e cada um deles
        // mandaria convite pro aluno.
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        Marcada(ctx, professor, batata, DateTime.Now.AddDays(-10));

        var cancelada = Marcada(ctx, professor, batata, DateTime.Now.AddDays(4));
        cancelada.Status = PoliticaAula.Cancelada;
        await ctx.SaveChangesAsync();

        var google = Substitute.For<IGoogleCalendarService>();
        google.EstaConectadoAsync(professor.Id).Returns(true);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);
        await controller.SincronizarGoogle();

        await google.DidNotReceive().CriarEventoAsync(Arg.Any<Aula>());
    }

    [Fact]
    public async Task Sincronizar_sem_conta_conectada_avisa_em_vez_de_tentar()
    {
        var (ctx, professor, batata, _) = Montar();
        using var _ctx = ctx;

        Marcada(ctx, professor, batata, DateTime.Now.AddDays(4));

        var google = Substitute.For<IGoogleCalendarService>();
        google.EstaConectadoAsync(professor.Id).Returns(false);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);
        await controller.SincronizarGoogle();

        await google.DidNotReceive().CriarEventoAsync(Arg.Any<Aula>());
        Assert.NotNull(controller.TempData["Erro"]);
    }
}
