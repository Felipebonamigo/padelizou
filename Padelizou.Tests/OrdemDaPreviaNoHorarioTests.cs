using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// A ORDEM DAS PRÉVIAS DENTRO DO MESMO HORÁRIO (10/09/2026).
//
// 🗣️ *"Arrumar a ordem do domingo: Semifinal 6 masc / 6 fem / 5 masc / 3 fem / 4 masc..."* — as
// semifinais e finais de domingo do Er ainda não nasceram: são PRÉVIA. Uma ordem que só alcançasse
// jogo real não resolveria justamente o caso que originou o pedido.
//
// A posição da prévia mora na reserva (Models/ReservaDeHorario.OrdemNoHorario), no mesmo lugar em
// que a hora e a quadra dela já moravam — e vai junto com elas quando o robô transforma a reserva
// em jogo de verdade.
public class OrdemDaPreviaNoHorarioTests
{
    private const int Duracao = 11;
    private static readonly DateTime Dia = new(2026, 9, 12);
    private static DateTime As(string hora) => DateTime.Parse($"2026-09-12 {hora}");

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Categoria A, Categoria B);

    // Duas categorias, DUAS quadras: as duas semifinais de cada uma rodam em paralelo, e as duas
    // finais projetadas caem no MESMO horário — que é o cenário do pedido.
    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio da Ordem", Codigo = "ORD1", Status = "Fase de Grupos",
            DataInicio = Dia.AddHours(8),
            HoraInicioDoDia = new TimeSpan(8, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 2, TempoPrevistoPartidaMinutos = Duracao,
        };
        ctx.Torneios.Add(torneio);

        var a = new Categoria { Nome = "3ª Feminina", Codigo = "C3F", Torneio = torneio };
        var b = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.AddRange(a, b);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });

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
        void Jogo(Categoria categoria, string hora, string quadra, string codigo) =>
            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id, Categoria = categoria, Dupla1 = Dupla(categoria), Dupla2 = Dupla(categoria),
                Codigo = codigo, Status = "Agendada", Fase = "Semifinal",
                HorarioPrevisto = As(hora), NomeQuadra = quadra,
            });

        Jogo(a, "20:00", "Quadra 1", "A1");
        Jogo(a, "20:00", "Quadra 2", "A2");
        Jogo(b, "20:11", "Quadra 1", "B1");
        Jogo(b, "20:11", "Quadra 2", "B2");

        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, organizador, a, b);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c)
    {
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, c.Organizador.Id);
    }

    private static async Task<List<JogoQueVem>> PreviaAsync(Cenario c)
    {
        var controller = Controller(c);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        return (List<JogoQueVem>)controller.ViewBag.JogosQueVem;
    }

    private static string Previa(Categoria categoria, string fase, int numero) =>
        ReferenciaDoJogo.Prevista(categoria.Id, fase, numero).ToString();

    // Âncora do cenário: as duas finais previstas caem no mesmo minuto. Se a grade mudar de régua,
    // cai aqui primeiro, com o motivo certo.
    [Fact]
    public async Task O_cenario_poe_as_duas_finais_previstas_no_mesmo_horario()
    {
        var previa = await PreviaAsync(Montar());
        var finais = previa.Where(j => j.Fase == "Final").ToList();

        Assert.Equal(2, finais.Count);
        Assert.Single(finais.Select(f => f.Horario).Distinct());
    }

    [Fact]
    public async Task Subir_uma_previa_no_mesmo_horario_troca_a_posicao_das_duas()
    {
        var c = Montar();
        var antes = OrdemNoHorario.Ordenar(Array.Empty<Partida>(), (await PreviaAsync(c)).Where(j => j.Fase == "Final"), new Dictionary<int, DateTime>());
        var segunda = antes[1].Previsto!;

        var controller = Controller(c);
        await controller.MoverNoHorario(c.Torneio.Id, Previa(
            c.Ctx.Categorias.Single(x => x.Id == segunda.CategoriaId), "Final", segunda.Numero), "cima");

        Assert.Null(controller.TempData["Erro"]);

        var depois = OrdemNoHorario.Ordenar(Array.Empty<Partida>(), (await PreviaAsync(c)).Where(j => j.Fase == "Final"), new Dictionary<int, DateTime>());
        Assert.Equal(segunda.CategoriaId, depois[0].Previsto!.CategoriaId);
        Assert.Equal(1, depois[0].Previsto!.OrdemNoHorario);
        Assert.Equal(2, depois[1].Previsto!.OrdemNoHorario);

        var reservas = await c.Ctx.ReservasDeHorario.AsNoTracking().ToListAsync();
        Assert.Equal(new[] { 1, 2 }, reservas.OrderBy(r => r.OrdemNoHorario).Select(r => r.OrdemNoHorario!.Value));
    }

    // A ordem reservada vai junto com a hora e a quadra quando a rodada nasce de verdade — senão
    // a fila que o organizador montou sumiria no instante em que o jogo passa a existir.
    [Fact]
    public async Task A_ordem_reservada_vira_a_ordem_do_jogo_quando_ele_nasce()
    {
        var c = Montar();
        c.Ctx.ReservasDeHorario.Add(new ReservaDeHorario
        {
            CategoriaId = c.A.Id, Fase = "Final", Numero = 1,
            Horario = As("21:00"), NomeQuadra = "Quadra 1", OrdemNoHorario = 3,
        });
        await c.Ctx.SaveChangesAsync();
        c.Ctx.ChangeTracker.Clear();

        var controller = Controller(c);
        foreach (var codigo in new[] { "A1", "A2" })
            await TestInfra.FinalizarComPlacarAsync(c.Ctx, controller,
                await c.Ctx.Partidas.SingleAsync(p => p.Codigo == codigo), 9, 3);
        c.Ctx.ChangeTracker.Clear();

        var final = await c.Ctx.Partidas.AsNoTracking()
            .SingleAsync(p => p.CategoriaId == c.A.Id && p.Fase == "Final");

        Assert.Equal(As("21:00"), final.HorarioPrevisto);
        Assert.Equal(3, final.OrdemNoHorario);
    }
}
