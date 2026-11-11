using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// SE NÃO CABE, O SORTEIO PERGUNTA (11/11/2026).
//
// 🗣️ Felipe: *"caso não caiba, avise na hora de gerar as chaves, e pergunte o que fazer com o que
// falta, se vai adicionar mais horários ou vai adicionar mais quadras"*.
//
// Duas horas de saber:
//   • ANTES de sortear, pela conta do Planejamento (vagas × jogos projetados): faltou → não sorteia,
//     leva pro Planejamento com a pergunta (mais horários / mais quadras / sortear mesmo assim);
//   • DEPOIS de sortear, pela grade de verdade (a soma não enxerga que a final espera a semifinal):
//     sobrou jogo sem hora → as chaves ficam sorteadas (ainda sem aprovação), e a mesma pergunta.
public class GerarChavesPerguntaQuandoNaoCabeTests
{
    private static readonly DateTime Quinta = new(2026, 11, 12);

    private static async Task<(DbPadelContext ctx, Torneio torneio, Jogador org)> TorneioAsync(
        int quadras, string horariosPorDia)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, 9);
        torneio.DataInicio = Quinta;
        torneio.DataFim = Quinta.AddDays(1);
        torneio.HoraInicioDoDia = new TimeSpan(18, 0, 0);
        torneio.HoraInicioDiasSeguintes = new TimeSpan(18, 0, 0);
        torneio.HoraFimDoDia = new TimeSpan(22, 10, 0);
        torneio.HorariosPorDia = horariosPorDia;
        torneio.QuantidadeQuadras = quadras;
        torneio.TempoPrevistoPartidaMinutos = 50;
        for (int q = 0; q < quadras; q++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Quadra {(char)('A' + q)}" });
        await ctx.SaveChangesAsync();
        return (ctx, torneio, org);
    }

    [Fact]
    public async Task Quando_falta_horario_nao_sorteia_e_leva_pro_planejamento_com_a_pergunta()
    {
        // 1 quadra, só quinta 18:00–19:40: 3 vagas pra 9 jogos de grupo + mata-mata.
        var (ctx, torneio, org) = await TorneioAsync(1, "2026-11-12=18:00-19:40;2026-11-13=18:00-18:00");
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var resposta = await controller.GerarChaves(torneio.Id);

        var ir = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Planejamento", ir.ActionName);
        Assert.NotNull(controller.TempData["NaoCabe"]);
        Assert.Empty(ctx.Partidas.Where(p => p.TorneioId == torneio.Id));
        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id)).Status);
    }

    [Fact]
    public async Task Sortear_mesmo_assim_sorteia()
    {
        var (ctx, torneio, org) = await TorneioAsync(1, "2026-11-12=18:00-19:40;2026-11-13=18:00-18:00");

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id, sortearMesmoSemCaber: true);

        Assert.NotEmpty(ctx.Partidas.Where(p => p.TorneioId == torneio.Id));
    }

    [Fact]
    public async Task Cabe_na_conta_mas_a_grade_deixa_jogo_sem_hora_sorteia_e_pergunta()
    {
        // 2 quadras; quinta 6 rodadas + sexta 1 = 14 vagas pra 14 jogos: a conta diz que cabe, mas
        // quartas, semis e final não rodam em paralelo com os grupos e a final fica sem hora.
        var (ctx, torneio, org) = await TorneioAsync(2, "2026-11-13=18:00-18:10");
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        var resposta = await controller.GerarChaves(torneio.Id);

        Assert.NotEmpty(ctx.Partidas.Where(p => p.TorneioId == torneio.Id));
        var ir = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Planejamento", ir.ActionName);
        Assert.NotNull(controller.TempData["NaoCabeDepoisDoSorteio"]);
    }

    [Fact]
    public void O_planejamento_pergunta_mais_horarios_ou_mais_quadras()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "Planejamento.cshtml"));

        Assert.Contains("TempData[\"NaoCabe\"]", tela);
        Assert.Contains("TempData[\"NaoCabeDepoisDoSorteio\"]", tela);
        Assert.Contains("href=\"#dia-a-dia\"", tela);
        Assert.Contains("href=\"#quadras-do-torneio\"", tela);
        Assert.Contains("name=\"sortearMesmoSemCaber\"", tela);
        Assert.Contains("id=\"dia-a-dia\"", tela);
        Assert.Contains("id=\"quadras-do-torneio\"", tela);
    }
}
