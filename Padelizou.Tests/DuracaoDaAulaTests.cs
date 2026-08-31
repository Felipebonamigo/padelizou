using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A aula passou a ter DURAÇÃO (10/08/2026). Antes o sistema inteiro fingia que toda aula era
// de uma hora: o evento na Google Agenda acabava no meio do treino e a trava de conflito
// comparava só o horário de início — 17:00–19:00 e 18:00–19:00 conviviam sem um pio.
public class DuracaoDaAulaTests
{
    // ---- A conta, sozinha ----

    [Fact]
    public void Duracao_fora_da_lista_vira_a_padrao()
    {
        // O campo vem do formulário: minuto livre viraria aula de 7 minutos (ou de 40 horas).
        Assert.Equal(60, DuracaoDaAula.Valida(37));
        Assert.Equal(60, DuracaoDaAula.Valida(null));
        Assert.Equal(90, DuracaoDaAula.Valida(90));
    }

    [Fact]
    public void Rotulo_fala_como_o_professor_fala()
    {
        Assert.Equal("1h", DuracaoDaAula.Rotulo(60));
        Assert.Equal("1h30", DuracaoDaAula.Rotulo(90));
        Assert.Equal("2h", DuracaoDaAula.Rotulo(120));
        Assert.Equal("45 min", DuracaoDaAula.Rotulo(45));
    }

    [Fact]
    public void Aulas_coladas_nao_sao_conflito()
    {
        var dezessete = new DateTime(2026, 8, 14, 17, 0, 0);

        // 17–18 e 18–19: é assim que a quadra funciona.
        Assert.False(DuracaoDaAula.Conflita(dezessete, 60, dezessete.AddHours(1), 60));
        // 17–19 e 18–19: essa é a que passava batido.
        Assert.True(DuracaoDaAula.Conflita(dezessete, 120, dezessete.AddHours(1), 60));
        // A de 2h engole quem começa 30 min depois.
        Assert.True(DuracaoDaAula.Conflita(dezessete.AddMinutes(30), 60, dezessete, 120));
    }

    // ---- O caminho completo, no controller ----

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Marcio", Login = "marcio", Cpf = "66600000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Wallau", PrecoPadrao = 100 };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    [Fact]
    public async Task Aula_manual_guarda_a_duracao_escolhida()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var quando = DateTime.Today.AddDays(4).AddHours(17);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.AdicionarManual(local.Id, "Leonardo", null, DataEHoraDoFormulario.ParaCampoDeData(quando), DataEHoraDoFormulario.ParaCampoDeHora(quando), 100m,
            recorrente: false, semanasRecorrencia: 1, duracaoMinutos: 120);

        var aula = await ctx.Aulas.SingleAsync();
        Assert.Equal(120, aula.DuracaoMinutos);
        Assert.Equal(quando.AddHours(2), DuracaoDaAula.Fim(aula));
    }

    [Fact]
    public async Task Nao_marca_aula_encavalada_na_que_ja_existe()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var dezessete = DateTime.Today.AddDays(4).AddHours(17);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.AdicionarManual(local.Id, "Leonardo", null, DataEHoraDoFormulario.ParaCampoDeData(dezessete), DataEHoraDoFormulario.ParaCampoDeHora(dezessete), 100m,
            recorrente: false, semanasRecorrencia: 1, duracaoMinutos: 120);

        // 18h cai DENTRO da aula das 17h às 19h. Com a trava velha (igualdade de horário)
        // essa segunda aula entrava.
        await controller.AdicionarManual(local.Id, "Outro aluno", null, DataEHoraDoFormulario.ParaCampoDeData(dezessete.AddHours(1)), DataEHoraDoFormulario.ParaCampoDeHora(dezessete.AddHours(1)), 100m,
            recorrente: false, semanasRecorrencia: 1, duracaoMinutos: 60);

        Assert.Equal(1, await ctx.Aulas.CountAsync());
    }

    [Fact]
    public async Task Aula_colada_na_anterior_pode_ser_marcada()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var dezessete = DateTime.Today.AddDays(4).AddHours(17);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.AdicionarManual(local.Id, "Leonardo", null, DataEHoraDoFormulario.ParaCampoDeData(dezessete), DataEHoraDoFormulario.ParaCampoDeHora(dezessete), 100m,
            recorrente: false, semanasRecorrencia: 1, duracaoMinutos: 90);

        // 18:30 começa quando a de 1h30 termina — professor dá aula em sequência o dia todo.
        await controller.AdicionarManual(local.Id, "Outro aluno", null, DataEHoraDoFormulario.ParaCampoDeData(dezessete.AddMinutes(90)), DataEHoraDoFormulario.ParaCampoDeHora(dezessete.AddMinutes(90)), 100m,
            recorrente: false, semanasRecorrencia: 1, duracaoMinutos: 60);

        Assert.Equal(2, await ctx.Aulas.CountAsync());
    }

    [Fact]
    public async Task Editar_muda_a_duracao_e_avisa_o_Google()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = DateTime.Now.AddDays(3),
            Preco = 100, Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Leonardo",
            DuracaoMinutos = 60, GoogleEventId = "evt-1",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var google = Substitute.For<IGoogleCalendarService>();
        google.AtualizarEventoAsync(Arg.Any<Aula>()).Returns("evt-1");

        var controller = TestInfra.NovoAulasController(ctx, professor.Id, google);
        await controller.Editar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora), hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora), 100m, duracaoMinutos: 120);

        Assert.Equal(120, (await ctx.Aulas.SingleAsync()).DuracaoMinutos);
        // O evento tem que esticar junto: senão o professor marca outra coisa em cima do que
        // a agenda dele diz estar livre.
        await google.Received(1).AtualizarEventoAsync(Arg.Is<Aula>(a => a != null && a.Id == aula.Id));
    }

    [Fact]
    public async Task Formulario_sem_duracao_nao_encolhe_a_aula()
    {
        // Aba antiga (ou tela que só mexe no preço) não pode transformar a aula de 2h em 1h
        // em silêncio.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aula = new Aula
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id, DataHora = DateTime.Now.AddDays(3),
            Preco = 100, Status = PoliticaAula.Confirmada, NomeAlunoAvulso = "Leonardo",
            DuracaoMinutos = 120,
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.Editar(aula.Id, local.Id, data: DataEHoraDoFormulario.ParaCampoDeData(aula.DataHora), hora: DataEHoraDoFormulario.ParaCampoDeHora(aula.DataHora), 130m);

        var salva = await ctx.Aulas.SingleAsync();
        Assert.Equal(120, salva.DuracaoMinutos);
        Assert.Equal(130m, salva.Preco);
    }
}
