using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANTOS GRUPOS NO SORTEIO, POR CATEGORIA (27/11/2026).
//
// 🗣️ Felipe, pelo Treta: "módulo branco 8 duplas chaves 4/4, preto 10 duplas 3/3/4, verde 7
// duplas 3/4". O sorteio sempre fechou grupos de 3 (os melhores resolvem o resto em grupos de 2):
// 8 duplas viravam 2/2/3/3. Com `Categoria.QuantidadeGrupos` preenchido numa categoria comum (o
// campo já existia pras de times), o sorteio reparte em N grupos pela MESMA régua de faixas (1º
// faixa A→C, as demais C→A), e o tamanho sai da conta: 8 em 2 = 4/4, 10 em 3 = 3/3/4, 7 em 2 = 3/4.
// Nulo = a regra de sempre.
public class QuantosGruposNoSorteioTests
{
    [Theory]
    [InlineData(8, 2, new[] { 4, 4 })]
    [InlineData(10, 3, new[] { 3, 3, 4 })]
    [InlineData(7, 2, new[] { 3, 4 })]
    public void Reparte_no_numero_de_grupos_pedido(int duplas, int grupos, int[] tamanhos)
    {
        var divididos = SorteioDosGrupos.EmGrupos(Enumerable.Range(1, duplas).ToList(), grupos);

        Assert.Equal(tamanhos, divididos.Select(g => g.Count).ToArray());
    }

    [Fact]
    public void Os_cabecas_de_chave_abrem_um_grupo_cada_e_a_faixa_seguinte_volta()
    {
        // Ranking 1..8 em 2 grupos: A = 1, 4, 6, 8 · B = 2, 3, 5, 7 — a régua de 09/09/2026 (toda
        // faixa depois da primeira entra invertida: A,B · B,A · B,A · B,A).
        var divididos = SorteioDosGrupos.EmGrupos(Enumerable.Range(1, 8).ToList(), 2);

        Assert.Equal(new[] { 1, 4, 6, 8 }, divididos[0]);
        Assert.Equal(new[] { 2, 3, 5, 7 }, divididos[1]);
    }

    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador org)> MontarAsync(int duplas, int? grupos)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, cat, org) = TestInfra.MontarTorneio(ctx, duplas);
        var sabado = DateTime.Today.AddDays(14);
        while (sabado.DayOfWeek != DayOfWeek.Saturday) sabado = sabado.AddDays(1);
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        cat.QuantidadeGrupos = grupos;
        await ctx.SaveChangesAsync();
        return (ctx, torneio, cat, org);
    }

    [Fact]
    public async Task O_sorteio_faz_os_grupos_de_4_do_Branco()
    {
        var (ctx, torneio, cat, org) = await MontarAsync(duplas: 8, grupos: 2);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        ctx.ChangeTracker.Clear();
        var grupos = await ctx.Set<GrupoTorneio>().Include(g => g.Duplas).Where(g => g.CategoriaId == cat.Id).ToListAsync();
        Assert.Equal(new[] { 4, 4 }, grupos.OrderBy(g => g.Nome).Select(g => g.Duplas.Count).ToArray());
        // Todos contra todos: 6 jogos em cada grupo de 4.
        Assert.Equal(12, await ctx.Partidas.CountAsync(p => p.CategoriaId == cat.Id));
    }

    [Fact]
    public async Task Sem_a_opcao_continua_a_regra_dos_grupos_de_3()
    {
        var (ctx, torneio, cat, org) = await MontarAsync(duplas: 8, grupos: null);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        ctx.ChangeTracker.Clear();
        var grupos = await ctx.Set<GrupoTorneio>().Include(g => g.Duplas).Where(g => g.CategoriaId == cat.Id).ToListAsync();
        Assert.Equal(new[] { 2, 3, 3 }, grupos.OrderBy(g => g.Nome).Select(g => g.Duplas.Count).ToArray());
    }

    [Fact]
    public async Task Organizador_define_os_grupos_antes_do_sorteio()
    {
        var (ctx, torneio, cat, org) = await MontarAsync(duplas: 8, grupos: null);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).DefinirQuantidadeDeGrupos(cat.Id, 2);
        Assert.Equal(2, (await ctx.Categorias.AsNoTracking().FirstAsync(c => c.Id == cat.Id)).QuantidadeGrupos);

        await TestInfra.NovoTorneiosController(ctx, org.Id).DefinirQuantidadeDeGrupos(cat.Id, null);
        Assert.Null((await ctx.Categorias.AsNoTracking().FirstAsync(c => c.Id == cat.Id)).QuantidadeGrupos);
    }

    [Fact]
    public async Task Depois_do_sorteio_nao_muda_e_quem_nao_organiza_nao_mexe()
    {
        var (ctx, torneio, cat, org) = await MontarAsync(duplas: 8, grupos: 2);
        using var _ = ctx;
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        ctx.ChangeTracker.Clear();

        await TestInfra.NovoTorneiosController(ctx, org.Id).DefinirQuantidadeDeGrupos(cat.Id, 4);
        Assert.Equal(2, (await ctx.Categorias.AsNoTracking().FirstAsync(c => c.Id == cat.Id)).QuantidadeGrupos);

        var intruso = TestInfra.NovoJogador(999);
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await TestInfra.NovoTorneiosController(ctx, intruso.Id).DefinirQuantidadeDeGrupos(cat.Id, 3));
    }
}

// GRUPOS DE TAMANHOS DIFERENTES: a comparação entre grupos vai POR JOGO (27/11/2026).
// 🗣️ Felipe: "faça o que você recomenda" — no Preto 3/3/4 o 1º do grupo de 4 joga 3 jogos, e
// em número absoluto ganharia a folga sempre; o 4º do grupo de 4 seria sempre o "pior" do DTN.
public class ComparacaoPorJogoEntreGruposTests
{
    private static ChaveamentoMataMata.Classificado C(int id, string grupo, int vitorias, int saldo, int jogos) =>
        new(id, grupo, vitorias, saldo, Posicao: 1, Jogos: jogos);

    [Fact]
    public void Na_folga_pela_campanha_quem_venceu_tudo_em_2_jogos_empata_com_quem_venceu_tudo_em_3()
    {
        // A: 2 vitórias em 2 (saldo +10, 5/jogo). C (grupo de 4): 3 vitórias em 3 (saldo +12, 4/jogo).
        // Por jogo: os dois 100%; o saldo por jogo decide pro A. Em absoluto, o C levaria.
        var ordem = ChaveamentoMataMata.OrdemDosByesPelaCampanha(new[]
        {
            C(1, "A", 2, 10, 2),
            C(3, "C", 3, 12, 3),
        }).Select(c => c.DuplaId).ToList();

        Assert.Equal(new[] { 1, 3 }, ordem);
    }

    [Fact]
    public void No_DTN_o_ultimo_do_grupo_de_4_nao_e_o_pior_so_por_ter_jogado_mais()
    {
        // 3º do grupo de 3: 0 vitórias em 2, saldo -10 (-5/jogo). 4º do grupo de 4: 0 em 3, saldo
        // -12 (-4/jogo). Por jogo, o do grupo de 3 foi PIOR, e é ele que vai direto à final DTN.
        Dupla D(int id) => new() { Id = id, Jogador1Id = id };
        var grupoDe3 = new List<ClassificacaoDeGrupos.Linha>
        {
            new(D(1), 2, 2, 0, 10, 12, 2), new(D(2), 2, 1, 1, 0, 8, 8), new(D(3), 2, 0, 2, -10, 2, 12),
        };
        var grupoDe4 = new List<ClassificacaoDeGrupos.Linha>
        {
            new(D(4), 3, 3, 0, 12, 18, 6), new(D(5), 3, 2, 1, 4, 14, 10), new(D(6), 3, 1, 2, -4, 10, 14),
            new(D(7), 3, 0, 3, -12, 6, 18),
        };

        var doPiorProMelhor = ChaveDtn.QuemVaiProDtn(new[] { grupoDe3, grupoDe4 }, porGrupo: 1, classificados: 2);

        Assert.Equal(new[] { 3, 7 }, doPiorProMelhor);
    }
}
