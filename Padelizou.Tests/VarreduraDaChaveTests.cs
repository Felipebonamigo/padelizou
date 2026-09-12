using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CHAVE QUE FICOU PRA TRÁS É MONTADA DEPOIS, SOZINHA.
//
// 🕳️ O INCIDENTE (ER PADEL TOUR, 12/09/2026). Duas categorias amanheceram com a fase de grupos
// TODA fechada e o mata-mata não montado — em silêncio, sem erro em lugar nenhum. A causa não
// era o chaveamento: `MontarMataMataDosGruposAsync` só roda no INSTANTE em que um jogo de grupo
// é finalizado (`EncerramentoDaPartida`), e **nada tentava de novo** se aquela chamada se
// perdesse. Bateu com os dois restarts de deploy do dia: o placar gravou, o processo morreu
// antes do robô, e a categoria ficou parada esperando um evento que não volta.
//
// ⚠️ NÃO É UM DEFEITO DE CHAVEAMENTO, É DE ENTREGA — e por isso o conserto não mexe em nenhuma
// régua: a varredura chama os MESMOS dois robôs, que já são guardados contra rodar duas vezes
// (`mataMataJaGerado` e o contador `jaCriados`). O que muda é só passar a chamá-los de novo.
//
// 🔒 SOB A MESMA TRAVA do encerramento (`UmDeCadaVezPorTorneioAsync`): a varredura e um placar
// sendo lançado na Mesa no mesmo segundo montariam a fase duas vezes, que é exatamente o buraco
// que aquela trava existe pra fechar.
public class VarreduraDaChaveTests
{
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        TorneioComGruposFechadosAsync(int qtdDuplas = 8)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();
        for (int i = 0; i < jogos.Count; i++)
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), jogos[i], 9, new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);
        }

        return (ctx, torneio, categoria, org.Id);
    }

    private static Task<List<Partida>> MataMataAsync(DbPadelContext ctx, int categoriaId) =>
        ctx.Partidas
            .Where(p => p.CategoriaId == categoriaId
                     && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
            .OrderBy(p => p.Id)
            .ToListAsync();

    // O estado exato do ER: a chamada do robô se perdeu, então o mata-mata não existe.
    private static async Task ApagarOMataMataAsync(DbPadelContext ctx, int categoriaId)
    {
        ctx.Partidas.RemoveRange(await MataMataAsync(ctx, categoriaId));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Monta_o_mata_mata_que_a_chamada_perdida_deixou_pra_tras()
    {
        var (ctx, torneio, categoria, _) = await TorneioComGruposFechadosAsync();
        using var _ctx = ctx;

        await ApagarOMataMataAsync(ctx, categoria.Id);
        Assert.Empty(await MataMataAsync(ctx, categoria.Id));

        await TestInfra.NovaVarreduraDaChave(ctx).PassarAsync(default);

        Assert.NotEmpty(await MataMataAsync(ctx, categoria.Id));
    }

    [Fact]
    public async Task Nao_monta_de_novo_o_que_ja_esta_montado()
    {
        var (ctx, torneio, categoria, _) = await TorneioComGruposFechadosAsync();
        using var _ctx = ctx;

        var antes = (await MataMataAsync(ctx, categoria.Id)).Select(p => p.Id).ToList();
        Assert.NotEmpty(antes);

        await TestInfra.NovaVarreduraDaChave(ctx).PassarAsync(default);
        await TestInfra.NovaVarreduraDaChave(ctx).PassarAsync(default);

        Assert.Equal(antes, (await MataMataAsync(ctx, categoria.Id)).Select(p => p.Id).ToList());
    }

    [Fact]
    public async Task Avanca_a_fase_que_a_chamada_perdida_deixou_pra_tras()
    {
        var (ctx, torneio, categoria, orgId) = await TorneioComGruposFechadosAsync();
        using var _ctx = ctx;

        // Fecha a abertura inteira — a fase seguinte nasceria sozinha...
        var abertura = await MataMataAsync(ctx, categoria.Id);
        var faseDaAbertura = abertura[0].Fase;
        foreach (var jogo in abertura.Where(p => p.Fase == faseDaAbertura).ToList())
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, orgId), jogo, 9, 4);
        }

        // ...e aí a chamada se perde: some com tudo que nasceu depois da abertura.
        var depois = (await MataMataAsync(ctx, categoria.Id)).Where(p => p.Fase != faseDaAbertura).ToList();
        Assert.NotEmpty(depois);
        ctx.Partidas.RemoveRange(depois);
        await ctx.SaveChangesAsync();

        await TestInfra.NovaVarreduraDaChave(ctx).PassarAsync(default);

        Assert.NotEmpty((await MataMataAsync(ctx, categoria.Id)).Where(p => p.Fase != faseDaAbertura));
    }

    [Fact]
    public async Task Nao_encosta_em_torneio_que_nem_sorteou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        await ctx.SaveChangesAsync();

        await TestInfra.NovaVarreduraDaChave(ctx).PassarAsync(default);

        Assert.Empty(await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync());
    }
}
