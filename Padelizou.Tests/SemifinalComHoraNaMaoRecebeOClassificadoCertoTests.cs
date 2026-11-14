using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// A SEMIFINAL COM HORA DIGITADA NA MÃO RECEBE O CLASSIFICADO CERTO (14/11/2026).
//
// 🗣️ Felipe, no THE LAST DANCE do Chakra, depois de pôr a Semifinal 1 da 6ª Masculina (ainda
// prévia) às 08:10 de domingo: *"depois q finalizar o jogo vai distribuir o classificado na
// chave corretamente?"*.
//
// O cenário é o do torneio: dois grupos de duas duplas, o Grupo B já fechado (Ygor/Rodrigo 9×5
// Hygor/Thiago), o Grupo A com o único jogo dele ainda por jogar, UMA quadra. A prévia mostra
// Semifinal 1 = 1º do A × 2º do B e Semifinal 2 = 1º do B × 2º do A. A hora mexe só no QUANDO:
// a reserva é presa a (categoria, fase, número), e o cruzamento é o desenho congelado.
public class SemifinalComHoraNaMaoRecebeOClassificadoCertoTests
{
    private static readonly DateTime Sabado = new(2026, 11, 14);
    private static readonly DateTime Domingo0810 = new(2026, 11, 15, 8, 10, 0);

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Categoria Cat, int OrgId,
        Dupla A1, Dupla A2, Dupla B1, Dupla B2, Partida JogoDoA);

    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var org = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(org);

        var torneio = new Torneio
        {
            Nome = "The Last Dance",
            Codigo = "TLD26",
            Status = "Fase de Grupos",
            DataInicio = Sabado.AddHours(9),
            DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(9, 0, 0),
            HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(20, 0, 0),
            QuantidadeQuadras = 1,
            TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);
        var cat = new Categoria { Nome = "6ª Categoria Masculina", Codigo = "C6M", Torneio = torneio };
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = org.Id });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });

        var grupoA = new GrupoTorneio { Categoria = cat, Nome = "Grupo A" };
        var grupoB = new GrupoTorneio { Categoria = cat, Nome = "Grupo B" };
        ctx.AddRange(grupoA, grupoB);

        int n = 1;
        Dupla Dupla(GrupoTorneio g)
        {
            var j1 = TestInfra.NovoJogador(n++);
            var j2 = TestInfra.NovoJogador(n++);
            ctx.Jogadores.AddRange(j1, j2);
            var d = new Dupla { Categoria = cat, Jogador1 = j1, Jogador2 = j2, GrupoTorneio = g, Grupo = g.Nome.Substring(6) };
            ctx.Duplas.Add(d);
            return d;
        }
        var a1 = Dupla(grupoA); var a2 = Dupla(grupoA);
        var b1 = Dupla(grupoB); var b2 = Dupla(grupoB);   // b1 = Ygor/Rodrigo, b2 = Hygor/Thiago
        ctx.SaveChanges();

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id, Categoria = cat, Dupla1 = b1, Dupla2 = b2, Codigo = "B1",
            Fase = "Grupo B", Status = "Finalizada", HorarioPrevisto = Sabado.AddHours(9),
            NomeQuadra = "Quadra A", GamesDupla1 = 9, GamesDupla2 = 5, SetsDupla1 = 1, SetsDupla2 = 0,
            VencedorId = b1.Id,
        });
        var jogoDoA = new Partida
        {
            TorneioId = torneio.Id, Categoria = cat, Dupla1 = a1, Dupla2 = a2, Codigo = "A1",
            Fase = "Grupo A", Status = "Agendada", HorarioPrevisto = Sabado.AddHours(13).AddMinutes(10),
            NomeQuadra = "Quadra A",
        };
        ctx.Partidas.Add(jogoDoA);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();

        return new Cenario(ctx, torneio, cat, org.Id, a1, a2, b1, b2, jogoDoA);
    }

    private static async Task<List<JogoQueVem>> PreviaAsync(Cenario c)
    {
        c.Ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.OrgId);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));
        return (List<JogoQueVem>)controller.ViewBag.JogosQueVem;
    }

    private static async Task<(Partida S1, Partida S2)> SemisAsync(Cenario c)
    {
        c.Ctx.ChangeTracker.Clear();
        var semis = ReservasDeHorario.NaOrdemDaFase(
            await c.Ctx.Partidas.Where(p => p.CategoriaId == c.Cat.Id).ToListAsync(), "Semifinal");
        Assert.Equal(2, semis.Count);
        return (semis[0], semis[1]);
    }

    [Fact]
    public async Task Semifinal_remarcada_na_mao_nasce_na_hora_nova_com_os_classificados_certos()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var antes = await PreviaAsync(c);
        var s1Previa = antes.Single(j => j.CategoriaId == c.Cat.Id && j.FaseNumerada == "Semifinal 1");
        var s2Previa = antes.Single(j => j.CategoriaId == c.Cat.Id && j.FaseNumerada == "Semifinal 2");
        Assert.NotEqual(Domingo0810, s1Previa.Horario);

        // A mão do organizador: Semifinal 1 pra domingo 08:10.
        c.Ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(c.Ctx, c.OrgId);
        await controller.DefinirHorario(c.Torneio.Id,
            ReferenciaDoJogo.Prevista(c.Cat.Id, "Semifinal", 1).ToString(), Domingo0810);
        Assert.Null(controller.TempData["Erro"]);

        var depois = await PreviaAsync(c);
        Assert.Equal(Domingo0810, depois.Single(j => j.CategoriaId == c.Cat.Id && j.FaseNumerada == "Semifinal 1").Horario);
        var s2Prometida = depois.Single(j => j.CategoriaId == c.Cat.Id && j.FaseNumerada == "Semifinal 2").Horario;

        // O último jogo do Grupo A acaba: A2 vence (vira o 1º do A).
        c.Ctx.ChangeTracker.Clear();
        var jogoDoA = await c.Ctx.Partidas.SingleAsync(p => p.Codigo == "A1");
        await TestInfra.FinalizarComPlacarAsync(c.Ctx, TestInfra.NovoTorneiosController(c.Ctx, c.OrgId), jogoDoA, 4, 9);

        var (s1, s2) = await SemisAsync(c);

        // Semifinal 1 = 1º do A (A2) × 2º do B (B2, o "Hygor/Thiago"), às 08:10 de domingo.
        Assert.Equal(new[] { c.A2.Id, c.B2.Id }.OrderBy(x => x), new[] { s1.Dupla1Id, s1.Dupla2Id }.OrderBy(x => x));
        Assert.Equal(Domingo0810, s1.HorarioPrevisto);

        // Semifinal 2 = 1º do B (B1, "Ygor/Rodrigo") × 2º do A (A1), na hora que a prévia prometia.
        Assert.Equal(new[] { c.B1.Id, c.A1.Id }.OrderBy(x => x), new[] { s2.Dupla1Id, s2.Dupla2Id }.OrderBy(x => x));
        Assert.Equal(s2Prometida, s2.HorarioPrevisto);

        // A Final continua prevista depois das duas semis.
        var final = (await PreviaAsync(c)).Single(j => j.CategoriaId == c.Cat.Id && j.Fase == "Final");
        Assert.True(final.Horario > Domingo0810, $"Final prevista {final.Horario}");
    }
}
