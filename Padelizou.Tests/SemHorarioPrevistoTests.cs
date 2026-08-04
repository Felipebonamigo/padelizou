using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Tests;

// Torneio "por ordem de liberação": os jogos saem numa ORDEM, sem hora marcada, e entram na
// quadra conforme ela vaga. É como roda a maioria dos internos de clube — ninguém prevê
// quanto dura um jogo de 4 games, e uma grade que atrasa 40 min logo cedo passa o resto do
// dia mentindo pra todo mundo que confiou nela.
public class SemHorarioPrevistoTests
{
    [Fact]
    public async Task Com_a_chave_ligada_nenhum_jogo_nasce_com_hora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        Assert.Equal(6, jogos.Count);                                  // as chaves saem normalmente
        Assert.All(jogos, j => Assert.Null(j.HorarioPrevisto));        // e nenhuma com hora
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Desligada_a_chave_a_grade_continua_marcando_hora_como_sempre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public async Task A_ordem_dos_jogos_sem_hora_e_a_mesma_em_que_eles_entrariam_na_grade()
    {
        // Sem hora, a ORDEM é a única promessa que sobra — e ela é a de criação. Se os jogos
        // saíssem embaralhados, o organizador não teria por onde chamar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();

        // Os jogos do Grupo A vêm antes dos do Grupo B: é a ordem em que o sorteio os criou.
        var fases = jogos.Select(j => j.Fase).ToList();
        Assert.Equal(fases.OrderBy(f => f, StringComparer.Ordinal), fases);
    }

    [Fact]
    public async Task O_mata_mata_gerado_depois_tambem_nasce_sem_hora()
    {
        // O robô de avanço agenda na grade por um caminho PRÓPRIO (AgendarNaGradeAsync).
        // Se ele ignorasse a chave, a fase de grupos ficaria sem hora e a semifinal apareceria
        // marcada pras 3h da manhã — o pior dos dois mundos.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.SemHorarioPrevisto = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        foreach (var jogo in await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync())
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 6, 2);

        var mataMata = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && !p.Fase.StartsWith("Grupo "))
            .ToListAsync();

        Assert.NotEmpty(mataMata);
        Assert.All(mataMata, j => Assert.Null(j.HorarioPrevisto));
    }
}
