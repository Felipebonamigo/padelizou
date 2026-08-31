using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// IMPORTAR AULAS DO GOOGLE AGENDA (pedido do Felipe, 27/08/2026).
//
// O caminho inverso do que sempre existiu: o professor que marcava as aulas direto no Google
// traz tudo pra dentro numa tela de conferência. O que estes testes travam:
//
//   • a lista de candidatos EXCLUI o que já é do Padelizou — evento cujo id já está em
//     `Aula.GoogleEventId` é o próprio sistema se reconhecendo no espelho, e reimportá-lo
//     criaria a aula em dobro;
//   • o POST reconfere contra o banco no instante do clique (a regra do RemoverNaoPagos):
//     nem o que o formulário mandou, nem o que a tela mostrou, decidem sozinhos;
//   • local e preço são do LOTE, e o local tem que ser do professor logado — POST montado à
//     mão não grava aula na quadra de outro.
public class ImportarDoGoogleTests
{
    private const int Professor = 1;
    private const int LocalDoProfessor = 10;
    private const int LocalDeOutro = 99;

    private static readonly DateTime Amanha = DateTime.Today.AddDays(1).AddHours(18);

    private static DbPadelContext NovoBanco()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = Professor, Nome = "Professor", Cpf = "1", IsProfessor = true });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Outro Professor", Cpf = "2", IsProfessor = true });
        ctx.LocaisAula.Add(new LocalAula { Id = LocalDoProfessor, ProfessorId = Professor, Nome = "Arena Central", Ativo = true });
        ctx.LocaisAula.Add(new LocalAula { Id = LocalDeOutro, ProfessorId = 2, Nome = "Quadra Alheia", Ativo = true });
        ctx.SaveChanges();
        return ctx;
    }

    private static IGoogleCalendarService GoogleCom(params EventoDaAgenda[] eventos)
    {
        var google = Substitute.For<IGoogleCalendarService>();
        google.EstaConectadoAsync(Professor).Returns(true);
        google.ListarEventosAsync(Professor, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(eventos.ToList());
        return google;
    }

    private static EventoDaAgenda Evento(string id, string titulo, DateTime inicio, int duracaoMinutos = 60) =>
        new(id, titulo, inicio, inicio.AddMinutes(duracaoMinutos), null);

    // ── A lista de candidatos (GET) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Evento_que_ja_virou_aula_nao_aparece_na_lista()
    {
        using var ctx = NovoBanco();
        ctx.Aulas.Add(new Aula
        {
            ProfessorId = Professor,
            LocalAulaId = LocalDoProfessor,
            DataHora = Amanha,
            Preco = 80m,
            Status = PoliticaAula.Confirmada,
            GoogleEventId = "ja-importado",
        });
        ctx.SaveChanges();

        var google = GoogleCom(
            Evento("ja-importado", "Aula Marina", Amanha),
            Evento("novo", "Aula João", Amanha.AddHours(1)));

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogle(de: null, ate: null);

        var view = Assert.IsType<ViewResult>(resultado);
        var vm = Assert.IsType<ImportarDoGoogleVM>(view.Model);

        Assert.Single(vm.Eventos);
        Assert.Equal("novo", vm.Eventos[0].Id);
    }

    // A dedução vale pro professor INTEIRO, não só pro período da tela: o mesmo evento não
    // pode reaparecer como candidato só porque a aula dele caiu fora da janela consultada.
    [Fact]
    public async Task A_deducao_olha_todas_as_aulas_do_professor_nao_so_o_periodo()
    {
        using var ctx = NovoBanco();
        ctx.Aulas.Add(new Aula
        {
            ProfessorId = Professor,
            LocalAulaId = LocalDoProfessor,
            DataHora = Amanha.AddMonths(-6),           // aula VELHA, fora de qualquer janela
            Preco = 80m,
            Status = PoliticaAula.Realizada,
            GoogleEventId = "evento-recorrente",
        });
        ctx.SaveChanges();

        var google = GoogleCom(Evento("evento-recorrente", "Aula fixa", Amanha));

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogle(de: null, ate: null);

        var vm = Assert.IsType<ImportarDoGoogleVM>(Assert.IsType<ViewResult>(resultado).Model);
        Assert.Empty(vm.Eventos);
    }

    [Fact]
    public async Task Sem_google_conectado_a_tela_volta_pra_agenda_com_aviso()
    {
        using var ctx = NovoBanco();
        var google = Substitute.For<IGoogleCalendarService>();
        google.EstaConectadoAsync(Professor).Returns(false);

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogle(de: null, ate: null);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("MinhaAgenda", redirect.ActionName);
    }

    // Os locais do select vêm de consulta própria — o `Model.Locais` da agenda só carrega
    // quando há fila de reposição, e um select montado dele viria vazio.
    [Fact]
    public async Task A_tela_lista_so_os_locais_ativos_do_professor()
    {
        using var ctx = NovoBanco();
        ctx.LocaisAula.Add(new LocalAula { Id = 11, ProfessorId = Professor, Nome = "Desativado", Ativo = false });
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, GoogleCom())
            .ImportarDoGoogle(de: null, ate: null);

        var vm = Assert.IsType<ImportarDoGoogleVM>(Assert.IsType<ViewResult>(resultado).Model);

        var local = Assert.Single(vm.Locais);
        Assert.Equal(LocalDoProfessor, local.Id);
    }

    // ── A gravação (POST) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Importar_grava_a_aula_com_o_evento_casado()
    {
        using var ctx = NovoBanco();
        var google = GoogleCom(Evento("ev-1", "Aula Marina", Amanha, duracaoMinutos: 90));

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogleConfirmar(
                eventos: new[] { "ev-1" },
                localAulaId: LocalDoProfessor,
                preco: 80m,
                de: Amanha.Date, ate: Amanha.Date.AddDays(30));

        Assert.IsType<RedirectToActionResult>(resultado);

        var aula = Assert.Single(await ctx.Aulas.ToListAsync());
        Assert.Equal(Professor, aula.ProfessorId);
        Assert.Equal(LocalDoProfessor, aula.LocalAulaId);
        Assert.Equal(Amanha, aula.DataHora);
        Assert.Equal(90, aula.DuracaoMinutos);
        Assert.Equal(80m, aula.Preco);
        Assert.Equal(PoliticaAula.Confirmada, aula.Status);
        // O título vira o nome do aluno avulso, sem conta — vincular depois já existe
        // (VincularAlunoAConta); o id do evento fica casado pra edição e pra não reimportar.
        Assert.Equal("Aula Marina", aula.NomeAlunoAvulso);
        Assert.Null(aula.AlunoId);
        Assert.Equal("ev-1", aula.GoogleEventId);
    }

    // A regra do RemoverNaoPagos: o formulário diz o que o professor QUIS, quem manda é o
    // banco no instante do clique. Um duplo-clique no confirmar (ou duas abas) não pode
    // gravar a aula duas vezes.
    [Fact]
    public async Task Confirmar_duas_vezes_nao_duplica_a_aula()
    {
        using var ctx = NovoBanco();
        var google = GoogleCom(Evento("ev-1", "Aula Marina", Amanha));
        var controller = TestInfra.NovoAulasController(ctx, Professor, google);

        await controller.ImportarDoGoogleConfirmar(new[] { "ev-1" }, LocalDoProfessor, 80m,
            Amanha.Date, Amanha.Date.AddDays(30));
        await controller.ImportarDoGoogleConfirmar(new[] { "ev-1" }, LocalDoProfessor, 80m,
            Amanha.Date, Amanha.Date.AddDays(30));

        Assert.Single(await ctx.Aulas.ToListAsync());
    }

    // O id marcado tem que existir na resposta do GOOGLE no instante do clique — id inventado
    // num POST montado à mão não vira aula às cegas.
    [Fact]
    public async Task Id_que_o_google_nao_devolveu_e_ignorado()
    {
        using var ctx = NovoBanco();
        var google = GoogleCom(Evento("ev-1", "Aula Marina", Amanha));

        await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogleConfirmar(new[] { "ev-1", "forjado" }, LocalDoProfessor, 80m,
                Amanha.Date, Amanha.Date.AddDays(30));

        var aula = Assert.Single(await ctx.Aulas.ToListAsync());
        Assert.Equal("ev-1", aula.GoogleEventId);
    }

    [Fact]
    public async Task Local_de_outro_professor_e_recusado_sem_gravar_nada()
    {
        using var ctx = NovoBanco();
        var google = GoogleCom(Evento("ev-1", "Aula Marina", Amanha));

        var resultado = await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogleConfirmar(new[] { "ev-1" }, LocalDeOutro, 80m,
                Amanha.Date, Amanha.Date.AddDays(30));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Empty(await ctx.Aulas.ToListAsync());
    }

    [Fact]
    public async Task Preco_negativo_e_recusado_sem_gravar_nada()
    {
        using var ctx = NovoBanco();
        var google = GoogleCom(Evento("ev-1", "Aula Marina", Amanha));

        await TestInfra.NovoAulasController(ctx, Professor, google)
            .ImportarDoGoogleConfirmar(new[] { "ev-1" }, LocalDoProfessor, -1m,
                Amanha.Date, Amanha.Date.AddDays(30));

        Assert.Empty(await ctx.Aulas.ToListAsync());
    }

    // ── A tradução do evento cru do Google (estática, sem rede) ───────────────────────────

    // Dia inteiro não tem hora — não é aula. Cancelado é lixo que o `Events.List` ainda
    // devolve. Os dois caem AQUI, na tradução, pra nenhum chamador precisar lembrar disso.
    [Fact]
    public void Evento_de_dia_inteiro_ou_cancelado_nao_vira_candidato()
    {
        var comHora = new Google.Apis.Calendar.v3.Data.Event
        {
            Id = "ok",
            Summary = "Aula Marina",
            Status = "confirmed",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T18:00:00-03:00" },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T19:00:00-03:00" },
        };
        var diaInteiro = new Google.Apis.Calendar.v3.Data.Event
        {
            Id = "feriado",
            Summary = "Feriado",
            Status = "confirmed",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime { Date = "2026-08-28" },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime { Date = "2026-08-29" },
        };
        var cancelado = new Google.Apis.Calendar.v3.Data.Event
        {
            Id = "morto",
            Summary = "Aula desmarcada",
            Status = "cancelled",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T18:00:00-03:00" },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T19:00:00-03:00" },
        };

        Assert.NotNull(EventoDaAgenda.De(comHora));
        Assert.Null(EventoDaAgenda.De(diaInteiro));
        Assert.Null(EventoDaAgenda.De(cancelado));
    }

    // A hora gravada tem que ser a hora LOCAL do evento — o mesmo cuidado (ao contrário) do
    // envio: `Aula.DataHora` é hora de Brasília sem fuso embutido.
    [Fact]
    public void A_hora_do_evento_vira_hora_local_sem_fuso()
    {
        var evento = new Google.Apis.Calendar.v3.Data.Event
        {
            Id = "ok",
            Summary = "Aula",
            Status = "confirmed",
            Start = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T18:00:00-03:00" },
            End = new Google.Apis.Calendar.v3.Data.EventDateTime { DateTimeRaw = "2026-08-28T19:30:00-03:00" },
        };

        var traduzido = EventoDaAgenda.De(evento);

        Assert.NotNull(traduzido);
        Assert.Equal(new DateTime(2026, 8, 28, 18, 0, 0), traduzido!.Inicio);
        Assert.Equal(new DateTime(2026, 8, 28, 19, 30, 0), traduzido.Fim);
    }
}
