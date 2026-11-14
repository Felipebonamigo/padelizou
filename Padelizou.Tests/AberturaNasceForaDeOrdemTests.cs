using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// O JOGO DE ABERTURA QUE JÁ TEM AS DUAS DUPLAS NASCE, MESMO QUE O DE NÚMERO MENOR AINDA ESPERE
// (14/11/2026).
//
// 🗣️ Felipe, no THE LAST DANCE do Chakra, com a Quartas de Final 2 da 4ª Masculina mostrando as
// duas duplas e ainda como prévia: *"O jogo ja esta os dois classificados, tem q permitir votar,
// por em jogo, tudo q os outros jogos 'pronto para comecar' tem"*. A Quartas 1 dependia do Grupo
// A, ainda em quadra, e a abertura nascia EM ORDEM — o laço parava no primeiro confronto sem dono.
//
// A ordem servia à numeração por Id, que deixou de mandar em 13/09/2026 (Partida.NumeroNaFase).
// O avanço entre fases do mata-mata (AvancarFaseAsync) já nascia fora de ordem; a abertura passa
// a fazer o mesmo, e a prévia passa a saber QUAIS números já existem, e não quantos.
public class AberturaNasceForaDeOrdemTests
{
    // Mesmo torneio de AvancoParcialDosGruposTests: 8 duplas → 3 grupos, 2 jogos de abertura.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId,
                               CruzamentoDoMataMata.Mapa Mapa)> SorteadoAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var grupos = await ctx.Set<GrupoTorneio>()
            .Where(g => g.CategoriaId == categoria.Id).Include(g => g.Duplas)
            .OrderBy(g => g.Nome).ToListAsync();
        var mapa = CruzamentoDoMataMata.Padrao(
            grupos.Select(g => g.Nome).ToList(), 2, grupos.Select(g => g.Duplas.Count).ToList())!;
        categoria.CruzamentoDoMataMata = mapa.Escrever();
        await ctx.SaveChangesAsync();
        return (ctx, torneio, categoria, org.Id, mapa);
    }

    private static async Task FecharGruposAsync(DbPadelContext ctx, int orgId, Categoria categoria,
        IEnumerable<string> letras)
    {
        var doGrupo = await ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null && letras.Contains(d.Grupo))
            .Select(d => d.Id).ToListAsync();
        var jogos = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && FasesTorneio.EhFaseDeGrupos(p.Fase)
                     && p.Status != "Finalizada"
                     && (doGrupo.Contains(p.Dupla1Id) || doGrupo.Contains(p.Dupla2Id)))
            .OrderBy(p => p.Id).ToListAsync();
        foreach (var jogo in jogos)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, orgId),
                jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }
    }

    private static List<string> GruposDo(CruzamentoDoMataMata.Mapa mapa, int confronto) =>
        new[] { mapa.Confrontos[confronto].Lado1.Grupo, mapa.Confrontos[confronto].Lado2.Grupo }
            .Distinct().ToList();

    private static Task<List<Partida>> AberturaAsync(DbPadelContext ctx, Categoria categoria, string fase) =>
        ctx.Partidas.Where(p => p.CategoriaId == categoria.Id && p.Fase == fase).ToListAsync();

    [Fact]
    public async Task O_jogo_2_nasce_com_os_grupos_dele_fechados_mesmo_com_o_jogo_1_esperando()
    {
        var (ctx, torneio, categoria, orgId, mapa) = await SorteadoAsync();
        using var _ = ctx;
        var fase = CruzamentoDoMataMata.NomeDaAbertura(mapa);
        // O cenário só prova algo se o jogo 1 depende de um grupo que o jogo 2 não fecha.
        Assert.NotEmpty(GruposDo(mapa, 0).Except(GruposDo(mapa, 1)));

        await FecharGruposAsync(ctx, orgId, categoria, GruposDo(mapa, 1));

        var nascidos = await AberturaAsync(ctx, categoria, fase);
        var jogo2 = Assert.Single(nascidos);
        Assert.Equal(2, jogo2.NumeroNaFase);
        Assert.Equal("Agendada", jogo2.Status);
        Assert.NotNull(jogo2.HorarioPrevisto);

        // A prévia mostra só o jogo 1 — e com o número dele, não o do que nasceu.
        ctx.ChangeTracker.Clear();
        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));
        var previa = ((List<JogoQueVem>)controller.ViewBag.JogosQueVem).Where(j => j.Fase == fase).ToList();
        var previsto = Assert.Single(previa);
        Assert.Equal(1, previsto.Numero);
    }

    [Fact]
    public async Task Depois_o_jogo_1_nasce_com_o_numero_1_e_as_duplas_do_desenho()
    {
        var (ctx, _, categoria, orgId, mapa) = await SorteadoAsync();
        using var _ctx = ctx;
        var fase = CruzamentoDoMataMata.NomeDaAbertura(mapa);

        await FecharGruposAsync(ctx, orgId, categoria, GruposDo(mapa, 1));
        await FecharGruposAsync(ctx, orgId, categoria, GruposDo(mapa, 0));

        ctx.ChangeTracker.Clear();
        var abertura = ReservasDeHorario.NaOrdemDaFase(
            await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync(), fase);
        Assert.Equal(2, abertura.Count);
        Assert.Equal(new int?[] { 1, 2 }, abertura.Select(p => p.NumeroNaFase).ToArray());

        // E as duplas são as do desenho, posição a posição — a mesma régua do "Refazer como previsto".
        var categoriaCompleta = await ctx.Categorias.Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
            .SingleAsync(c => c.Id == categoria.Id);
        var deGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && FasesTorneio.EhFaseDeGrupos(p.Fase)).ToListAsync();
        var classificados = ClassificacaoDeGrupos.Calcular(
            categoriaCompleta.GruposTorneio.SelectMany(g => g.Duplas).ToList(), deGrupo,
            new Dictionary<int, int>(), 2);
        var avaliacao = RefazerComoPrevisto.Avaliar(mapa, true, deGrupo, abertura, classificados);
        Assert.Equal(RefazerComoPrevisto.Estado.NadaAFazer, avaliacao.Estado);
    }
}
