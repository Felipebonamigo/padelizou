using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O REENCAIXE NÃO DESFAZ A TROCA DO ORGANIZADOR (14/11/2026).
//
// 🗣️ Felipe, no THE LAST DANCE do Chakra: *"Voltou para 10:40 do igor ao terminar um jogo, falei
// q os sistema tava fazendo isso"*. Ele tinha trocado as semis da 5ª (a 2 às 10:40, a 1 às 11:30);
// quando as Quartas da 6ª Feminina nasceram, o robô reencaixou todo jogo de fase mais adiantada
// (LevasDaGrade.ForaDeOrdem) — e reencaixou POR ID, a ordem de criação. A Semifinal 1 voltou pra
// frente, calada.
//
// A régua de "fora de ordem" fica: ela é o que impede uma final antes das quartas de outra
// categoria. O que muda é a ordem DENTRO do reencaixe: a que os jogos já tinham na grade.
public class ReencaixeMantemAOrdemDaTrocaTests
{
    private static readonly DateTime Domingo = new(2026, 11, 15);
    private static DateTime As(string hora) => DateTime.Parse($"2026-11-15 {hora}");

    [Fact]
    public async Task Semis_trocadas_na_mao_continuam_trocadas_quando_outra_categoria_avanca()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "The Last Dance", Codigo = "TLD26", Status = "Em Andamento",
            DataInicio = Domingo.AddHours(9), DataFim = Domingo,
            HoraInicioDoDia = new TimeSpan(9, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(9, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0),
            QuantidadeQuadras = 1, TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);
        var quinta = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", Torneio = torneio };
        var sextaF = new Categoria { Nome = "6ª Categoria Feminina", Codigo = "C6F", Torneio = torneio };
        ctx.Categorias.AddRange(quinta, sextaF);
        ctx.SaveChanges();
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra A" });

        int n = 1;
        Dupla Dupla(Categoria c)
        {
            var j1 = TestInfra.NovoJogador(n++);
            var j2 = TestInfra.NovoJogador(n++);
            ctx.Jogadores.AddRange(j1, j2);
            var d = new Dupla { Categoria = c, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(d);
            return d;
        }

        // As semis da 5ª, já reais, TROCADAS na mão: a 2 antes da 1.
        var s1 = new Partida
        {
            TorneioId = torneio.Id, Categoria = quinta, Dupla1 = Dupla(quinta), Dupla2 = Dupla(quinta),
            Codigo = "S1", Fase = "Semifinal", NumeroNaFase = 1, Status = "Agendada",
            HorarioPrevisto = As("11:30"), NomeQuadra = "Quadra A",
        };
        var s2 = new Partida
        {
            TorneioId = torneio.Id, Categoria = quinta, Dupla1 = Dupla(quinta), Dupla2 = Dupla(quinta),
            Codigo = "S2", Fase = "Semifinal", NumeroNaFase = 2, Status = "Agendada",
            HorarioPrevisto = As("10:40"), NomeQuadra = "Quadra A",
        };
        ctx.Partidas.AddRange(s1, s2);
        ctx.SaveChanges();

        // As Quartas da 6ª Feminina nascem — fase menos adiantada, então as semis da 5ª entram no
        // reencaixe como "fora de ordem".
        var quartas = new List<Partida>
        {
            new() { TorneioId = torneio.Id, CategoriaId = sextaF.Id, Dupla1 = Dupla(sextaF), Dupla2 = Dupla(sextaF),
                    Codigo = "Q1", Fase = "Quartas de Final", NumeroNaFase = 1, Status = "Agendada" },
            new() { TorneioId = torneio.Id, CategoriaId = sextaF.Id, Dupla1 = Dupla(sextaF), Dupla2 = Dupla(sextaF),
                    Codigo = "Q2", Fase = "Quartas de Final", NumeroNaFase = 2, Status = "Agendada" },
        };
        ctx.SaveChanges();
        await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas()).AgendarNaGradeAsync(quartas, torneio.Id);
        ctx.Partidas.AddRange(quartas);
        await ctx.SaveChangesAsync();

        ctx.ChangeTracker.Clear();
        var semi1 = await ctx.Partidas.SingleAsync(p => p.Codigo == "S1");
        var semi2 = await ctx.Partidas.SingleAsync(p => p.Codigo == "S2");
        Assert.NotNull(semi1.HorarioPrevisto);
        Assert.NotNull(semi2.HorarioPrevisto);
        Assert.True(semi2.HorarioPrevisto < semi1.HorarioPrevisto,
            $"A troca foi desfeita: Semifinal 2 às {semi2.HorarioPrevisto:HH:mm}, Semifinal 1 às {semi1.HorarioPrevisto:HH:mm}");
    }
}
