using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// TROCAR O HORÁRIO DE UMA ELIMINATÓRIA QUE AINDA NÃO NASCEU (10/09/2026).
//
// 🗣️ Felipe, olhando as finais do Er com o selo "prévia": *"permita também trocar de horário as
// eliminatórias, não apenas as de chave"*. A troca de horário existia desde 31/07, mas só pra
// jogo REAL — e a Semifinal e a Final de um torneio de grupos só nascem quando a fase anterior
// fecha. Até lá são prévia, e a prévia não tinha botão nenhum.
//
// A troca do organizador vira uma RESERVA (Models/ReservaDeHorario): "a Final 1 desta categoria é
// às 22:00, na Quadra Central". Três lugares precisam obedecê-la, e cada um tem o próprio teste
// aqui: a prévia (é onde ele vê a troca), o robô que cria a rodada (é onde a troca vira jogo de
// verdade) e o reencaixe que o robô faz quando outra categoria avança (é onde ela sumiria calada).
public class ReservaDeHorarioTests
{
    private const int Duracao = 11;
    private static readonly DateTime Dia = new(2026, 9, 12);

    private static DateTime As(string hora) => DateTime.Parse($"2026-09-12 {hora}");

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Categoria A, Categoria B);

    // Duas categorias, UMA quadra, semifinais intercaladas: A1 20:00, B1 20:11, A2 20:22, B2 20:33.
    // As finais projetadas caem 20:44 (A) e 20:55 (B) — e são trocáveis entre si sem pôr nenhuma
    // antes da própria semifinal, que é a única troca que a regra não aceita.
    //
    // Com `faseDaB` = Quartas, a B tem quatro jogos e a Semifinal dela nasce DEPOIS da Final da A:
    // é o cenário em que o robô reencaixa a Final da A por estar "fora de ordem".
    private static Cenario Montar(string faseDaB = "Semifinal")
    {
        var ctx = TestInfra.NovoContexto();

        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio da Reserva",
            Codigo = "RSV1",
            Status = "Fase de Grupos",
            DataInicio = Dia.AddHours(8),
            HoraInicioDoDia = new TimeSpan(8, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 1,
            TempoPrevistoPartidaMinutos = Duracao,
        };
        ctx.Torneios.Add(torneio);

        var a = new Categoria { Nome = "3ª Feminina", Codigo = "C3F", Torneio = torneio };
        var b = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.AddRange(a, b);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra Central" });

        int proximo = 1;
        Dupla Dupla(Categoria categoria)
        {
            var j1 = TestInfra.NovoJogador(proximo++);
            var j2 = TestInfra.NovoJogador(proximo++);
            ctx.Jogadores.AddRange(j1, j2);
            var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(dupla);
            return dupla;
        }

        void Jogo(Categoria categoria, string fase, string hora, string codigo) =>
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                Categoria = categoria,
                Dupla1 = Dupla(categoria),
                Dupla2 = Dupla(categoria),
                Codigo = codigo,
                Status = "Agendada",
                Fase = fase,
                HorarioPrevisto = As(hora),
                NomeQuadra = "Quadra Central",
            });

        Jogo(a, "Semifinal", "20:00", "A1");
        Jogo(b, faseDaB, "20:11", "B1");
        Jogo(a, "Semifinal", "20:22", "A2");
        Jogo(b, faseDaB, "20:33", "B2");
        if (faseDaB == "Quartas de Final")
        {
            Jogo(b, faseDaB, "20:44", "B3");
            Jogo(b, faseDaB, "20:55", "B4");
        }

        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, organizador, a, b);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c)
    {
        // Contexto limpo: em produção cada requisição nasce sem nada rastreado, e um teste que
        // enxerga navegação preenchida "de graça" não prova que a consulta a carregou.
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
    }

    // A prévia como a aba Jogos entrega — pelo mesmo caminho da tela.
    private static async Task<List<JogoQueVem>> PreviaAsync(Cenario c)
    {
        var controller = Controller(c);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        return (List<JogoQueVem>)controller.ViewBag.JogosQueVem;
    }

    private static JogoQueVem FinalDa(List<JogoQueVem> previa, Categoria categoria) =>
        previa.Single(j => j.Fase == "Final" && j.CategoriaId == categoria.Id);

    private static string Previa(Categoria categoria, string fase, int numero) =>
        ReferenciaDoJogo.Prevista(categoria.Id, fase, numero).ToString();

    private static Task<Partida> JogoAsync(Cenario c, string codigo) =>
        c.Ctx.Partidas.SingleAsync(p => p.Codigo == codigo);

    private static Task<List<ReservaDeHorario>> ReservasAsync(Cenario c) =>
        c.Ctx.ReservasDeHorario.OrderBy(r => r.CategoriaId).ToListAsync();

    private static async Task ReservarAsync(Cenario c, Categoria categoria, string fase, int numero, string hora)
    {
        c.Ctx.ReservasDeHorario.Add(new ReservaDeHorario
        {
            CategoriaId = categoria.Id,
            Fase = fase,
            Numero = numero,
            Horario = As(hora),
            NomeQuadra = "Quadra Central",
        });
        await c.Ctx.SaveChangesAsync();
        c.Ctx.ChangeTracker.Clear();
    }

    private static async Task FinalizarAsync(Cenario c, params string[] codigos)
    {
        var controller = Controller(c);
        foreach (var codigo in codigos)
            await TestInfra.FinalizarComPlacarAsync(c.Ctx, controller, await JogoAsync(c, codigo), 9, 3);
        c.Ctx.ChangeTracker.Clear();
    }

    // O cenário promete 20:44 e 20:55: os outros testes contam com esses números, e este é o que
    // os ancora — se a grade mudar de régua, cai aqui primeiro, com o motivo certo.
    [Fact]
    public async Task O_cenario_projeta_as_duas_finais_uma_atras_da_outra()
    {
        var c = Montar();

        var previa = await PreviaAsync(c);

        Assert.Equal(As("20:44"), FinalDa(previa, c.A).Horario);
        Assert.Equal(As("20:55"), FinalDa(previa, c.B).Horario);
        Assert.All(previa, j => Assert.Equal("Quadra Central", j.Quadra));
    }

    // ═══ A TROCA ═══

    [Fact]
    public async Task Trocar_duas_previas_grava_as_reservas_e_a_previa_obedece()
    {
        var c = Montar();
        var antes = await PreviaAsync(c);
        var finalA = FinalDa(antes, c.A);
        var finalB = FinalDa(antes, c.B);

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, Previa(c.A, "Final", 1), Previa(c.B, "Final", 1));

        Assert.Null(controller.TempData["Erro"]);

        var reservas = await ReservasAsync(c);
        Assert.Equal(2, reservas.Count);
        Assert.Equal(finalB.Horario, reservas.Single(r => r.CategoriaId == c.A.Id).Horario);
        Assert.Equal(finalA.Horario, reservas.Single(r => r.CategoriaId == c.B.Id).Horario);

        var depois = await PreviaAsync(c);
        Assert.Equal(finalB.Horario, FinalDa(depois, c.A).Horario);
        Assert.Equal(finalA.Horario, FinalDa(depois, c.B).Horario);
    }

    [Fact]
    public async Task Trocar_previa_com_jogo_real_move_o_real_e_reserva_a_previa()
    {
        // A Semifinal 2 da B (real, 20:33) troca com a Final da A (prévia, 20:44): o jogo real
        // recebe o slot projetado, e a prévia recebe o slot que era do jogo real.
        var c = Montar();
        var antes = await PreviaAsync(c);
        var finalA = FinalDa(antes, c.A);
        var b2 = await JogoAsync(c, "B2");

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, b2.Id.ToString(), Previa(c.A, "Final", 1));

        Assert.Null(controller.TempData["Erro"]);
        c.Ctx.ChangeTracker.Clear();

        Assert.Equal(finalA.Horario, (await JogoAsync(c, "B2")).HorarioPrevisto);

        var reserva = Assert.Single(await ReservasAsync(c));
        Assert.Equal(c.A.Id, reserva.CategoriaId);
        Assert.Equal("Final", reserva.Fase);
        Assert.Equal(1, reserva.Numero);
        Assert.Equal(As("20:33"), reserva.Horario);
        Assert.Equal("Quadra Central", reserva.NomeQuadra);

        Assert.Equal(As("20:33"), FinalDa(await PreviaAsync(c), c.A).Horario);
    }

    // ═══ O ROBÔ ═══

    [Fact]
    public async Task A_final_nasce_no_horario_e_na_quadra_reservados()
    {
        var c = Montar();
        await ReservarAsync(c, c.A, "Final", 1, "22:00");

        await FinalizarAsync(c, "A1", "A2");

        var final = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");
        Assert.Equal(As("22:00"), final.HorarioPrevisto);
        Assert.Equal("Quadra Central", final.NomeQuadra);
    }

    [Fact]
    public async Task A_reserva_sobrevive_quando_a_rodada_de_outra_categoria_nasce()
    {
        // A Final da A nasce às 22:00 (reserva). Depois as Quartas da B fecham e a Semifinal da B
        // nasce — posto MENOR que a final, então o robô reencaixa tudo que ficou "fora de ordem",
        // e a Final da A está nessa lista. Sem reaplicar a reserva ali, a escolha do organizador
        // sumia calada, exatamente no momento em que o torneio anda.
        var c = Montar(faseDaB: "Quartas de Final");
        await ReservarAsync(c, c.A, "Final", 1, "22:00");

        await FinalizarAsync(c, "A1", "A2");
        Assert.Equal(As("22:00"),
            (await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final")).HorarioPrevisto);

        await FinalizarAsync(c, "B1", "B2", "B3", "B4");

        var semisDaB = await c.Ctx.Partidas.Where(p => p.CategoriaId == c.B.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.Equal(2, semisDaB.Count);   // a rodada nova nasceu — é ela que dispara o reencaixe

        var final = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");
        Assert.Equal(As("22:00"), final.HorarioPrevisto);
        Assert.Equal("Quadra Central", final.NomeQuadra);
    }

    // ═══ QUEM APAGA A RESERVA ═══

    [Fact]
    public async Task Refazer_grade_apaga_as_reservas()
    {
        // "Recalcular horários" refaz a grade do zero — e a reserva é um remendo por cima da
        // grade, como a troca de dois jogos reais, que o recálculo também desfaz.
        var c = Montar();
        await ReservarAsync(c, c.A, "Final", 1, "22:00");

        await Controller(c).RefazerGrade(c.Torneio.Id);

        Assert.Empty(await ReservasAsync(c));
    }

    [Fact]
    public async Task Desfazer_o_sorteio_apaga_as_reservas()
    {
        var c = Montar();
        await ReservarAsync(c, c.A, "Final", 1, "22:00");

        var torneio = await c.Ctx.Torneios.SingleAsync(t => t.Id == c.Torneio.Id);
        torneio.Status = AprovacaoDeChaves.Pendente;
        await c.Ctx.SaveChangesAsync();

        await Controller(c).DesfazerSorteio(c.Torneio.Id);

        Assert.Empty(await c.Ctx.Partidas.ToListAsync());
        Assert.Empty(await ReservasAsync(c));
    }

    // ═══ A CONSULTA VIRA SQL? ═══
    //
    // O InMemory da suíte não traduz nada (ver TraducaoDasConsultasDePalpiteTests): a consulta
    // que atravessa a categoria pra chegar no torneio é compilada aqui contra o Npgsql, sem banco.
    [Fact]
    public void As_reservas_de_um_torneio_viram_SQL()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);

        Assert.Contains("SELECT", ReservasDeHorario.DoTorneio(ctx, 1).ToQueryString());
    }

    // ═══ A RESERVA QUE DEIXOU DE SER POSSÍVEL ═══

    [Fact]
    public async Task Troca_que_poria_a_final_antes_da_propria_semifinal_e_recusada()
    {
        // A Semifinal 2 da A (real, 20:22) com a Final da A (prévia, 20:44): a final ficaria às
        // 20:22 e a semi que a decide iria pras 20:44 — a final antes da própria semi. A troca é
        // conferida na prévia antes de gravar, e recusada: nada muda e nada é reservado.
        var c = Montar();
        var a2 = await JogoAsync(c, "A2");

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, a2.Id.ToString(), Previa(c.A, "Final", 1));

        Assert.NotNull(controller.TempData["Erro"]);
        c.Ctx.ChangeTracker.Clear();
        Assert.Equal(As("20:22"), (await JogoAsync(c, "A2")).HorarioPrevisto);
        Assert.Empty(await ReservasAsync(c));
    }

    [Fact]
    public async Task A_reserva_antes_do_fim_da_semifinal_nao_vale_no_robo_e_o_jogo_volta_pra_grade()
    {
        // A reserva foi feita quando cabia; o torneio atrasou e a Semifinal 2 da A foi remarcada
        // pra depois dela. Quando a final nasce, a reserva não pode valer — seria marcar a final
        // com os finalistas ainda em quadra — e o jogo volta pra grade, na primeira vaga possível.
        var c = Montar();
        await ReservarAsync(c, c.A, "Final", 1, "20:40");

        var a2 = await JogoAsync(c, "A2");
        a2.HorarioPrevisto = As("21:06");
        await c.Ctx.SaveChangesAsync();

        await FinalizarAsync(c, "A1", "A2");

        var final = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");
        Assert.NotNull(final.HorarioPrevisto);
        Assert.True(final.HorarioPrevisto >= As("21:17"),
            $"a final nasceu {final.HorarioPrevisto:HH:mm}, antes de a semifinal das 21:06 acabar");
    }
}
