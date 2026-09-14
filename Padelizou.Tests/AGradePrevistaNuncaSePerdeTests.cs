using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 14/09/2026 — A GRADE PREVISTA NUNCA SE PERDE.
//
// 🗣️ Felipe: *"temos que seguir a grade prevista, por que o usuario se baseia [...] talvez
// devamos criar campos separados (Horario chaveamento, Horario atualizado)"*.
//
// O desenho anterior era DESLIZAR o horário quando o torneio atrasasse. Ele resolve a operação e
// perde a informação: o jogador que se programou pelas 14:40 vê 16:20 e não tem como saber que
// mudou, nem do quê pra quê. Com dois campos, a promessa fica.
//
//   `HorarioDoSorteio`  — escrito UMA VEZ, quando o jogo nasce. Nunca mais.
//   `HorarioPrevisto`   — a operação do dia: anda quando precisa.
//
// ⚠️ NULO NO CAMPO NOVO = jogo de antes desta mudança, e a tela se comporta como sempre.
public class AGradePrevistaNuncaSePerdeTests
{
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComAsQuartasMontadasAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var deGrupo = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id)
            .OrderBy(p => p.Id).ToListAsync();
        for (int i = 0; i < deGrupo.Count; i++)
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), deGrupo[i], 9,
                new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);

        return (ctx, torneio, categoria, org.Id);
    }

    private static async Task<List<Partida>> DaFaseAsync(DbPadelContext ctx, int categoriaId, string fase)
    {
        var todas = await ctx.Partidas.Where(p => p.CategoriaId == categoriaId).ToListAsync();
        return ReservasDeHorario.NaOrdemDaFase(todas, fase);
    }

    private static async Task<List<Partida>> SemifinaisAsync(
        DbPadelContext ctx, Categoria categoria, int orgId)
    {
        foreach (var q in await DaFaseAsync(ctx, categoria.Id, "Quartas de Final"))
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, orgId), q, 9, 3);

        return await DaFaseAsync(ctx, categoria.Id, "Semifinal");
    }

    [Fact]
    public async Task O_jogo_nasce_com_os_dois_horarios_iguais()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        var semi = (await SemifinaisAsync(ctx, categoria, orgId))[0];

        // No caso normal — torneio no horário — a promessa e a operação são a mesma coisa, e a
        // tela não tem nada a explicar.
        Assert.NotNull(semi.HorarioDoSorteio);
        Assert.Equal(semi.HorarioPrevisto, semi.HorarioDoSorteio);
    }

    [Fact]
    public async Task Remanejar_o_jogo_muda_a_operacao_e_preserva_a_promessa()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        var semi = (await SemifinaisAsync(ctx, categoria, orgId))[0];
        var prometido = semi.HorarioDoSorteio;
        Assert.NotNull(prometido);

        // O dia atrasou e o organizador empurra o jogo.
        var maisTarde = semi.HorarioPrevisto!.Value.AddHours(2);
        await TestInfra.NovoTorneiosController(ctx, orgId)
            .DefinirHorario(torneio.Id, ReferenciaDoJogo.Real(semi.Id).ToString(), maisTarde);

        ctx.ChangeTracker.Clear();
        var depois = await ctx.Partidas.FindAsync(semi.Id);

        // ⚠️ É AQUI QUE O DESENHO SE PAGA: a operação anda, a promessa fica. Sem o segundo campo,
        // o 14:40 que o jogador leu desapareceria sem deixar rastro.
        Assert.Equal(maisTarde, depois!.HorarioPrevisto);
        Assert.Equal(prometido, depois.HorarioDoSorteio);
        Assert.NotEqual(depois.HorarioPrevisto, depois.HorarioDoSorteio);
    }

    [Fact]
    public async Task O_jogo_de_grupo_tambem_nasce_com_a_promessa()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        // Não é só eliminatória: o jogo de grupo sai do sorteio com hora, e essa hora é promessa
        // igual — é por ela que o jogador se programa no primeiro dia.
        var deGrupo = (await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .ToListAsync())
            .First(p => FasesTorneio.EhFaseDeGrupos(p.Fase));

        Assert.NotNull(deGrupo.HorarioDoSorteio);
    }
}
