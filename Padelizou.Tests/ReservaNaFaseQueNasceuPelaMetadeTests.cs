using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// A RESERVA DE UM JOGO PREVISTO NUMA FASE QUE JÁ NASCEU PELA METADE (14/11/2026).
//
// 🗣️ Felipe, no THE LAST DANCE do Chakra, tentando trocar a Semifinal 1 da 5ª Masculina (já real,
// a Quartas 1 tinha acabado) com a Semifinal 2 (prévia, esperando a Quartas 2): *"Esta dizendo
// isso sendo que é as duas semi da mesma categoria"* — a tela recusou com "a fase anterior dessa
// categoria ainda não terminou".
//
// 🕳️ A regra das reservas filtrava por (categoria, FASE): "uma fase nasce inteira". Não nasce
// desde que o jogo pode nascer fora de ordem — com a Semifinal 1 real, toda reserva de Semifinal
// da categoria era tratada como consumida, a da Semifinal 2 inclusive. A prévia a ignorava, a
// conferência não achava a Semifinal 2 na hora trocada, e o erro culpava a fase anterior.
// A chave certa é o JOGO: (categoria, fase, número).
public class ReservaNaFaseQueNasceuPelaMetadeTests
{
    private static readonly DateTime Dia = new(2026, 11, 14);
    private static DateTime As(string hora) => DateTime.Parse($"2026-11-14 {hora}");

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Categoria Cat, int OrgId, Partida S1);

    // UMA quadra, jogos de 50 min. Quartas 1 e 4 acabaram, a Semifinal 1 nasceu (12:20); Quartas 2
    // e 3 ainda vão jogar, então a Semifinal 2 é prévia — depois da Quartas 3 (11:30), às 13:10.
    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);
        var torneio = new Torneio
        {
            Nome = "The Last Dance", Codigo = "TLD26", Status = "Em Andamento",
            DataInicio = Dia.AddHours(9), DataFim = Dia,
            HoraInicioDoDia = new TimeSpan(9, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 1, TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);
        var cat = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });

        int n = 1;
        Dupla Dupla()
        {
            var j1 = TestInfra.NovoJogador(n++);
            var j2 = TestInfra.NovoJogador(n++);
            ctx.Jogadores.AddRange(j1, j2);
            var d = new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(d);
            return d;
        }

        Partida Jogo(string fase, int numero, string hora, Dupla d1, Dupla d2, bool acabou)
        {
            var p = new Partida
            {
                TorneioId = torneio.Id, Categoria = cat, Dupla1 = d1, Dupla2 = d2,
                Codigo = $"{fase[0]}{numero}", Fase = fase, NumeroNaFase = numero,
                Status = acabou ? "Finalizada" : "Agendada",
                HorarioPrevisto = As(hora), NomeQuadra = "Quadra A",
            };
            if (acabou)
            {
                p.GamesDupla1 = 9; p.GamesDupla2 = 4; p.SetsDupla1 = 1; p.SetsDupla2 = 0;
            }
            ctx.Partidas.Add(p);
            return p;
        }

        var duplas = Enumerable.Range(0, 8).Select(_ => Dupla()).ToList();
        ctx.SaveChanges();
        // O quadro do motor: Semifinal 1 = Vencedor Q1 × Vencedor Q4; Semifinal 2 = Q2 × Q3.
        var q1 = Jogo("Quartas de Final", 1, "09:00", duplas[0], duplas[1], acabou: true);
        var q4 = Jogo("Quartas de Final", 4, "09:50", duplas[6], duplas[7], acabou: true);
        Jogo("Quartas de Final", 2, "10:40", duplas[2], duplas[3], acabou: false);
        Jogo("Quartas de Final", 3, "11:30", duplas[4], duplas[5], acabou: false);
        ctx.SaveChanges();
        q1.VencedorId = duplas[0].Id;
        q4.VencedorId = duplas[6].Id;
        var s1 = Jogo("Semifinal", 1, "12:20", duplas[0], duplas[6], acabou: false);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, cat, org.Id, s1);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c)
    {
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, c.OrgId);
    }

    private static async Task<JogoQueVem> Semifinal2Async(Cenario c)
    {
        var controller = Controller(c);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        return ((List<JogoQueVem>)controller.ViewBag.JogosQueVem)
            .Single(j => j.CategoriaId == c.Cat.Id && j.FaseNumerada == "Semifinal 2");
    }

    private static string S2(Cenario c) => ReferenciaDoJogo.Prevista(c.Cat.Id, "Semifinal", 2).ToString();

    [Fact]
    public async Task Troca_a_semifinal_real_com_a_semifinal_prevista_da_mesma_categoria()
    {
        var c = Montar();
        using var _ = c.Ctx;
        Assert.Equal(As("13:10"), (await Semifinal2Async(c)).Horario);

        var controller = Controller(c);
        await controller.TrocarHorario(c.Torneio.Id, c.S1.Id.ToString(), S2(c));

        Assert.Null(controller.TempData["Erro"]);
        c.Ctx.ChangeTracker.Clear();
        Assert.Equal(As("13:10"), (await c.Ctx.Partidas.SingleAsync(p => p.Id == c.S1.Id)).HorarioPrevisto);
        Assert.Equal(As("12:20"), (await Semifinal2Async(c)).Horario);
        // A da Semifinal 2 (prévia). Desde 22/11/2026 a Semifinal 1 real, movida na mão, também tem a
        // dela — é o que a segura nas 13:10 (ReservasDeHorario.FixarNaMao).
        var reservas = await c.Ctx.ReservasDeHorario.ToListAsync();
        var reserva = Assert.Single(reservas, r => r.Numero == 2);
        Assert.Equal(As("12:20"), reserva.Horario);
        Assert.Equal(As("13:10"), Assert.Single(reservas, r => r.Numero == 1).Horario);

        // A Final espera as DUAS semis — inclusive a real, que agora é a mais tarde (13:10).
        var controllerDaTela = Controller(c);
        Assert.IsType<ViewResult>(await controllerDaTela.Jogos(c.Torneio.Id, null, null));
        var final = ((List<JogoQueVem>)controllerDaTela.ViewBag.JogosQueVem)
            .Single(j => j.CategoriaId == c.Cat.Id && j.Fase == "Final");
        Assert.True(final.Horario >= As("14:00"), $"Final prevista {final.Horario:HH:mm}, antes do fim da Semifinal 1");

        // E quando as Quartas 2 e 3 acabam, a Semifinal 2 NASCE na hora trocada, com os vencedores.
        foreach (var codigo in new[] { "Q2", "Q3" })
        {
            c.Ctx.ChangeTracker.Clear();
            var quartas = await c.Ctx.Partidas.SingleAsync(p => p.Codigo == codigo);
            await TestInfra.FinalizarComPlacarAsync(c.Ctx, TestInfra.NovoTorneiosController(c.Ctx, c.OrgId), quartas, 9, 3);
        }
        c.Ctx.ChangeTracker.Clear();
        var todas = await c.Ctx.Partidas.ToListAsync();
        var s2 = ReservasDeHorario.NaOrdemDaFase(todas, "Semifinal")[1];
        var q2 = todas.Single(p => p.Codigo == "Q2");
        var q3 = todas.Single(p => p.Codigo == "Q3");
        Assert.Equal(As("12:20"), s2.HorarioPrevisto);
        Assert.Equal(new[] { q2.Dupla1Id, q3.Dupla1Id }.OrderBy(x => x), new[] { s2.Dupla1Id, s2.Dupla2Id }.OrderBy(x => x));
        Assert.Equal(As("13:10"), todas.Single(p => p.Id == c.S1.Id).HorarioPrevisto);
    }

    [Fact]
    public async Task A_final_prevista_espera_a_semifinal_real_que_acaba_mais_tarde()
    {
        // O outro lado do conserto: a Semifinal 2 deixa de esperar a irmã real, mas a FINAL não.
        var c = Montar();
        using var _ = c.Ctx;
        var s1 = await c.Ctx.Partidas.SingleAsync(p => p.Id == c.S1.Id);
        s1.HorarioPrevisto = As("20:00");
        await c.Ctx.SaveChangesAsync();

        var controller = Controller(c);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        var previa = (List<JogoQueVem>)controller.ViewBag.JogosQueVem;

        Assert.True(previa.Single(j => j.FaseNumerada == "Semifinal 2").Horario < As("20:00"));
        Assert.True(previa.Single(j => j.Fase == "Final").Horario >= As("20:50"));
    }

    [Fact]
    public async Task Hora_na_mao_na_semifinal_prevista_vale_com_a_irma_ja_real()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var controller = Controller(c);
        await controller.DefinirHorario(c.Torneio.Id, S2(c), As("18:00"));

        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(As("18:00"), (await Semifinal2Async(c)).Horario);
        Assert.Single(await c.Ctx.ReservasDeHorario.ToListAsync());
    }

    [Fact]
    public async Task A_reserva_do_jogo_que_ainda_nao_nasceu_segura_a_vaga_no_encaixe()
    {
        // O robô, ao encaixar outra coisa, tem que ver a vaga da Semifinal 2 reservada mesmo com
        // a Semifinal 1 já no banco. Pela fase, ela sumia de `AindaPorNascer`.
        var c = Montar();
        using var _ = c.Ctx;
        var reserva = new ReservaDeHorario
        {
            CategoriaId = c.Cat.Id, Fase = "Semifinal", Numero = 2, Horario = As("18:00"), NomeQuadra = "Quadra A",
        };

        var reais = await c.Ctx.Partidas.ToListAsync();
        var porNascer = ReservasDeHorario.AindaPorNascer(new[] { reserva },
            ReservasDeHorario.JogosQueExistem(reais), (_, _) => null);

        Assert.Equal(As("18:00"), Assert.Single(porNascer).HorarioPrevisto);
    }
}
