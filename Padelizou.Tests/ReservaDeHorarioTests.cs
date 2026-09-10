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
    private static Cenario Montar(string faseDaB = "Semifinal", bool porOrdem = false, string[]? quadras = null)
    {
        quadras ??= new[] { "Quadra Central" };
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
            QuantidadeQuadras = quadras.Length,
            TempoPrevistoPartidaMinutos = Duracao,
            SemHorarioPrevisto = porOrdem,
        };
        ctx.Torneios.Add(torneio);

        var a = new Categoria { Nome = "3ª Feminina", Codigo = "C3F", Torneio = torneio };
        var b = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.AddRange(a, b);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        foreach (var nome in quadras)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nome });

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
                NomeQuadra = quadras[0],
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

    private static async Task ReservarAsync(Cenario c, Categoria categoria, string fase, int numero, string hora,
        string quadra = "Quadra Central")
    {
        c.Ctx.ReservasDeHorario.Add(new ReservaDeHorario
        {
            CategoriaId = categoria.Id,
            Fase = fase,
            Numero = numero,
            Horario = As(hora),
            NomeQuadra = quadra,
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

    // ═══ O QUE A REVISÃO ADVERSARIAL ACHOU (10/09/2026) ═══

    [Fact]
    public async Task A_reserva_nao_desfaz_a_troca_feita_depois_de_o_jogo_nascer()
    {
        // A Final da A nasce às 22:00 pela reserva. O organizador então troca a Final (real) com a
        // Quartas 4 da B: Final 20:55, B4 22:00. Quando as Quartas da B fecham e a Semifinal da B
        // nasce, a Final da A é reencaixada por estar fora de ordem — e a reserva de 22:00, que
        // continua no banco, NÃO pode voltar por cima da troca: a quadra das 22:00 agora é da B4.
        // A reserva só vale enquanto o jogo estiver nela; se ele saiu, ela some.
        var c = Montar(faseDaB: "Quartas de Final");
        await ReservarAsync(c, c.A, "Final", 1, "22:00");
        await FinalizarAsync(c, "A1", "A2");

        var finalA = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");
        var b4 = await JogoAsync(c, "B4");
        await Controller(c).TrocarHorario(c.Torneio.Id, finalA.Id.ToString(), b4.Id.ToString());
        c.Ctx.ChangeTracker.Clear();
        Assert.Equal(As("20:55"), (await c.Ctx.Partidas.SingleAsync(p => p.Id == finalA.Id)).HorarioPrevisto);

        await FinalizarAsync(c, "B1", "B2", "B3", "B4");

        var final = await c.Ctx.Partidas.SingleAsync(p => p.Id == finalA.Id);
        Assert.NotEqual(As("22:00"), final.HorarioPrevisto);
        Assert.Empty(await ReservasAsync(c));
    }

    [Fact]
    public async Task No_torneio_por_ordem_de_liberacao_a_previa_nao_troca_de_horario()
    {
        // O "por ordem" tem hora desde 09/09 (Services/OrdemDeLiberacao), então a prévia mostra
        // horário — mas o robô não agenda a rodada nova nesse torneio (AgendarNaGradeAsync sai
        // antes), e uma reserva ali seria uma promessa que ninguém cumpre. A troca com prévia é
        // recusada com o motivo; a troca entre jogos reais continua como sempre.
        var c = Montar(porOrdem: true);

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, Previa(c.A, "Final", 1), Previa(c.B, "Final", 1));

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Empty(await ReservasAsync(c));
    }

    [Fact]
    public async Task Mudar_de_quadra_pra_uma_quadra_reservada_troca_de_quadra_com_a_reserva()
    {
        // "Mudar de quadra" só enxergava jogos reais: a Quadra 1 das 22:00 parecia livre, mas
        // estava reservada pra Final da A. Sem isto, o jogo real ia pra lá e a final nascia em
        // cima dele — dois jogos na mesma quadra no mesmo minuto, a única coisa que a grade não
        // pode produzir. A mesma regra do jogo real: quadra com dono TROCA de dono.
        var c = Montar(quadras: new[] { "Quadra 1", "Quadra 2" });
        await ReservarAsync(c, c.A, "Final", 1, "22:00", quadra: "Quadra 1");

        var b2 = await JogoAsync(c, "B2");
        b2.HorarioPrevisto = As("22:00");
        b2.NomeQuadra = "Quadra 2";
        await c.Ctx.SaveChangesAsync();

        await Controller(c).TrocarQuadra(c.Torneio.Id, b2.Id, "Quadra 1");
        c.Ctx.ChangeTracker.Clear();

        Assert.Equal("Quadra 1", (await JogoAsync(c, "B2")).NomeQuadra);
        var reserva = Assert.Single(await ReservasAsync(c));
        Assert.Equal("Quadra 2", reserva.NomeQuadra);
    }

    [Fact]
    public async Task Troca_que_faz_outra_reserva_deixar_de_valer_apaga_a_reserva_e_avisa()
    {
        // A Final da A está reservada pras 21:00 (vale: a Semifinal 2 da A acaba 20:33). Aí o
        // organizador troca a Semifinal 2 da A (20:22) com a Quartas 4 da B (20:55): a semi passa
        // a acabar 21:06, e a reserva das 21:00 deixa de valer. Ela não pode ficar no banco
        // calada — a prévia e o robô a ignorariam e o organizador só descobriria olhando. A
        // troca acontece, a reserva morta some, e a mensagem diz.
        var c = Montar(faseDaB: "Quartas de Final");
        await ReservarAsync(c, c.A, "Final", 1, "21:00");
        var a2 = await JogoAsync(c, "A2");
        var b4 = await JogoAsync(c, "B4");

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, a2.Id.ToString(), b4.Id.ToString());

        Assert.Null(controller.TempData["Erro"]);
        c.Ctx.ChangeTracker.Clear();
        Assert.Equal(As("20:55"), (await JogoAsync(c, "A2")).HorarioPrevisto);
        Assert.Empty(await ReservasAsync(c));

        // ⚠️ NA MENSAGEM DE SUCESSO, e não num TempData["Aviso"] à parte: a página Jogos (de onde
        // o organizador troca) só mostra Sucesso e Erro — o aviso separado era descartado calado.
        var mensagem = Assert.IsType<string>(controller.TempData["Sucesso"]);
        Assert.Contains("deixou de valer", mensagem);
        Assert.Contains("3ª Feminina", mensagem);
    }

    [Fact]
    public async Task A_reserva_nao_poe_a_mesma_pessoa_em_duas_quadras_no_mesmo_minuto()
    {
        // O jogador P joga na A e na B. A Final da A está reservada pras 22:00 na Quadra 1, e a B
        // tem um jogo com P às 22:00 na Quadra 2 (a prévia não sabe quem joga, então não tinha como
        // evitar). Quando a final nasce e P está nela, a reserva não pode valer: o encaixe respeita
        // a agenda da pessoa, e a reserva também tem que respeitar. A final vai pra grade e a
        // reserva morre.
        var c = Montar(quadras: new[] { "Quadra 1", "Quadra 2" });
        await ReservarAsync(c, c.A, "Final", 1, "22:00", quadra: "Quadra 1");

        var a1 = await c.Ctx.Partidas.Include(p => p.Dupla1).SingleAsync(p => p.Codigo == "A1");
        var b2 = await c.Ctx.Partidas.Include(p => p.Dupla1).SingleAsync(p => p.Codigo == "B2");
        a1.Dupla1.Jogador1Id = b2.Dupla1.Jogador1Id;   // P joga nas duas categorias
        b2.HorarioPrevisto = As("22:00");
        b2.NomeQuadra = "Quadra 2";
        await c.Ctx.SaveChangesAsync();

        await FinalizarAsync(c, "A1", "A2");           // a Dupla1 da A1 (com P) vence e vai pra final

        var final = await c.Ctx.Partidas.SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");
        Assert.NotNull(final.HorarioPrevisto);
        Assert.NotEqual(As("22:00"), final.HorarioPrevisto);
        Assert.Empty(await ReservasAsync(c));
    }

    [Fact]
    public async Task A_rodada_de_outra_categoria_nao_ocupa_o_slot_reservado_pra_um_jogo_que_ainda_vai_nascer()
    {
        // Uma quadra. A Final da A está reservada pras 21:06, e a A ainda está na semifinal. As
        // Quartas da B fecham primeiro e a Semifinal da B nasce: sem enxergar a reserva, o encaixe
        // a poria justamente às 21:06 — e a final nasceria em cima dela. A reserva de um jogo que
        // ainda vai nascer entra no encaixe como jogo marcado (ReservasDeHorario.AindaPorNascer).
        var c = Montar(faseDaB: "Quartas de Final");
        await ReservarAsync(c, c.A, "Final", 1, "21:06");

        await FinalizarAsync(c, "B1", "B2", "B3", "B4");

        var semisDaB = await c.Ctx.Partidas.Where(p => p.CategoriaId == c.B.Id && p.Fase == "Semifinal").ToListAsync();
        Assert.Equal(2, semisDaB.Count);
        Assert.All(semisDaB, s => Assert.NotNull(s.HorarioPrevisto));
        Assert.DoesNotContain(semisDaB, s => s.HorarioPrevisto == As("21:06"));
        Assert.Single(await ReservasAsync(c));         // a reserva continua viva, esperando a final
    }
}
