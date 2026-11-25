using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// TRETA NA CRIAÇÃO E NA GESTÃO (25/11/2026) — ver TRETA.md.
//
// As categorias vêm do catálogo (não dá pra nomear "Branco/Preto/Verde" na criação), então o
// "formato Treta" é: uma caixa na criação que liga o torneio-festa (fora do ranking, tie-break
// no 5x5, DTN em toda categoria) e, na gestão, por categoria, quantos vão pro DTN e com quem
// ela faz a Finalíssima. Configurável porque as regras "ainda mudam".
public class TretaNaGestaoTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria a, Categoria b, Categoria c, Jogador org) Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, a, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        var b = new Categoria { Nome = "Verde", Codigo = "VD", TorneioId = torneio.Id };
        var c = new Categoria { Nome = "Preto", Codigo = "PT", TorneioId = torneio.Id };
        ctx.Categorias.AddRange(b, c);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, a, b, c, org);
    }

    [Fact]
    public async Task Organizador_liga_o_DTN_e_o_par_da_Finalissima()
    {
        var (ctx, torneio, a, b, _, org) = Montar();
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(a.Id, duplasNoDtnPorGrupo: 1, parDaFinalissimaId: b.Id);

        var salva = await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == a.Id);
        Assert.Equal(1, salva.DuplasNoDtnPorGrupo);
        Assert.Equal(b.Id, salva.ParDaFinalissimaId);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_configura()
    {
        var (ctx, _, a, b, _, _) = Montar();
        using var __ = ctx;
        var intruso = TestInfra.NovoJogador(77);
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, intruso.Id).ConfigurarDtnDaCategoria(a.Id, 1, b.Id);

        Assert.IsType<ForbidResult>(r);
        Assert.Equal(0, (await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == a.Id)).DuplasNoDtnPorGrupo);
    }

    [Fact]
    public async Task Par_de_outro_torneio_ou_ela_mesma_e_recusado()
    {
        var (ctx, _, a, _, _, org) = Montar();
        using var __ = ctx;
        var (_, deOutro, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(a.Id, 1, deOutro.Id);
        Assert.Null((await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == a.Id)).ParDaFinalissimaId);

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(a.Id, 1, a.Id);
        Assert.Null((await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == a.Id)).ParDaFinalissimaId);
    }

    [Fact]
    public async Task Um_modulo_so_tem_um_par()
    {
        // A↔B e depois A↔C: o par antigo sai, senão A estaria em duas Finalíssimas.
        var (ctx, _, a, b, c, org) = Montar();
        using var _ = ctx;
        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(b.Id, 1, a.Id);
        ctx.ChangeTracker.Clear();

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(a.Id, 1, c.Id);

        Assert.Null((await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == b.Id)).ParDaFinalissimaId);
        Assert.Equal(c.Id, (await ctx.Categorias.AsNoTracking().FirstAsync(x => x.Id == a.Id)).ParDaFinalissimaId);
    }

    [Fact]
    public async Task Fora_do_ranking_grava_pela_gestao_e_campo_ausente_mantem()
    {
        var (ctx, torneio, _, _, _, org) = Montar();
        using var _ = ctx;
        Task Editar(bool? fora) => TestInfra.NovoTorneiosController(ctx, org.Id).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao,
            clubeId: torneio.ClubeId, quantidadeQuadras: 1, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: torneio.RestricaoCategoria, capa: null,
            tempoPrevistoPartidaMinutos: 50, foraDoRanking: fora);

        await Editar(true);
        Assert.True((await ctx.Torneios.AsNoTracking().FirstAsync(t => t.Id == torneio.Id)).ForaDoRanking);
        await Editar(null);
        Assert.True((await ctx.Torneios.AsNoTracking().FirstAsync(t => t.Id == torneio.Id)).ForaDoRanking);
        await Editar(false);
        Assert.False((await ctx.Torneios.AsNoTracking().FirstAsync(t => t.Id == torneio.Id)).ForaDoRanking);
    }

    [Fact]
    public async Task Criar_como_Treta_liga_a_festa_inteira()
    {
        using var ctx = TestInfra.NovoContexto();
        var organizador = TestInfra.NovoJogador(1);
        organizador.IsOrganizadorTorneio = true;
        ctx.Jogadores.Add(organizador);
        var cat1 = new padelizou.Models.CategoriaPadrao { Nome = "6ª Masculina", Codigo = "6M", Tipo = "Masculina" };
        var cat2 = new padelizou.Models.CategoriaPadrao { Nome = "5ª Masculina", Codigo = "5M", Tipo = "Masculina" };
        ctx.CategoriasPadrao.AddRange(cat1, cat2);
        var clube = new Clube { Nome = "Clube Teste" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.PodeReceberOnline(Arg.Any<Jogador?>()).Returns(true);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos);
        var torneio = new Torneio
        {
            Nome = "Treta Padel", Codigo = "TRETA1", PrecoInscricao = 0m, FormaPagamento = "Externo",
            ClubeId = clube.Id, DataInicio = DateTime.Today.AddDays(30), Formato = "Padrao",
        };

        await controller.Create(torneio, new[] { cat1.Id, cat2.Id }, null, null, null, null, treta: true);

        var salvo = await ctx.Torneios.AsNoTracking().SingleAsync();
        Assert.True(salvo.ForaDoRanking);
        Assert.True(salvo.TieBreakNaPenultima);
        Assert.Equal("Padrao", salvo.Formato);
        Assert.All(await ctx.Categorias.AsNoTracking().ToListAsync(), c => Assert.Equal(1, c.DuplasNoDtnPorGrupo));
    }
}
