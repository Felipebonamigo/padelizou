using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A DTN PREVISTA COM HORÁRIO E QUADRA NA MÃO (27/11/2026). 🗣️ Rafael (Treta), com o print da
// prévia: "os DTN não dá pra mudar horário/quadra". A troca grava uma RESERVA presa a
// (categoria, fase, número) — e a DTN só virava categoria quando os grupos fechavam, então a
// prévia não tinha categoria pra prender. Agora a categoria DTN nasce VAZIA no sorteio (ou ao
// salvar o DTN da categoria), a prévia leva o Id dela, e o robô só a preenche no fim dos grupos —
// e a reserva vale quando o jogo nasce.
public class DtnComHorarioNaMaoTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador org)> SorteadoAsync(bool dtnAntes = true)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, cat, org) = TestInfra.MontarTorneio(ctx, 8);
        var sabado = DateTime.Today.AddDays(14);
        while (sabado.DayOfWeek != DayOfWeek.Saturday) sabado = sabado.AddDays(1);
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        torneio.ForaDoRanking = true;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });
        cat.QuantidadeGrupos = 2;
        if (dtnAntes) cat.DuplasNoDtnPorGrupo = 1;
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, cat, org);
    }

    private static Task<Categoria?> DtnAsync(DbPadelContext ctx, int moduloId) =>
        ctx.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.CategoriaDeOrigemId == moduloId && c.PapelDaChave == ChaveDtn.Dtn);

    [Fact]
    public async Task O_sorteio_ja_cria_a_categoria_DTN_vazia()
    {
        var (ctx, _, cat, _) = await SorteadoAsync();
        using var _c = ctx;

        var dtn = await DtnAsync(ctx, cat.Id);
        Assert.NotNull(dtn);
        Assert.True(dtn!.ChaveDireta);
        Assert.False(await ctx.Duplas.AnyAsync(d => d.CategoriaId == dtn.Id));
    }

    [Fact]
    public async Task Salvar_o_DTN_depois_do_sorteio_tambem_cria()
    {
        // O caso do Rafael: o torneio já foi sorteado.
        var (ctx, _, cat, org) = await SorteadoAsync(dtnAntes: false);
        using var _c = ctx;
        Assert.Null(await DtnAsync(ctx, cat.Id));

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(cat.Id, 1, null);

        Assert.NotNull(await DtnAsync(ctx, cat.Id));
    }

    [Fact]
    public async Task A_previa_da_DTN_leva_o_Id_da_categoria_DTN()
    {
        var (ctx, torneio, cat, _) = await SorteadoAsync();
        using var _c = ctx;
        var dtn = (await DtnAsync(ctx, cat.Id))!;

        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var jogos = (await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas())
            .ProjetarProximasFasesAsync(torneio.Id, todas)).Jogos;

        var final = Assert.Single(jogos, j => j.Categoria == dtn.Nome);
        Assert.Equal(dtn.Id, final.CategoriaId);
    }

    [Fact]
    public async Task Quando_os_grupos_fecham_a_DTN_vazia_e_preenchida_e_o_horario_reservado_vale()
    {
        var (ctx, torneio, cat, _) = await SorteadoAsync();
        using var _c = ctx;
        var dtn = (await DtnAsync(ctx, cat.Id))!;

        // O organizador põe a final DTN às 21:00 na Q2 (o que o botão da prévia grava).
        var reservado = torneio.DataInicio!.Value.Date.AddHours(21);
        ctx.ReservasDeHorario.Add(new ReservaDeHorario
        {
            CategoriaId = dtn.Id, Fase = "Final", Numero = 1, Horario = reservado, NomeQuadra = "Q2",
        });
        await ctx.SaveChangesAsync();

        foreach (var p in await ctx.Partidas.Where(p => p.CategoriaId == cat.Id).ToListAsync())
        {
            bool venceO1 = p.Dupla1Id < p.Dupla2Id;
            p.Status = "Finalizada";
            p.VencedorId = venceO1 ? p.Dupla1Id : p.Dupla2Id;
            p.GamesDupla1 = venceO1 ? 6 : 2;
            p.GamesDupla2 = venceO1 ? 2 : 6;
        }
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas()).MontarMataMataDosGruposAsync(cat.Id, torneio.Id);
        ctx.ChangeTracker.Clear();

        // Uma DTN só — a vazia foi preenchida, não duplicada.
        Assert.Equal(1, await ctx.Categorias.CountAsync(c => c.CategoriaDeOrigemId == cat.Id && c.PapelDaChave == ChaveDtn.Dtn));
        var final = await ctx.Partidas.SingleAsync(p => p.CategoriaId == dtn.Id);
        Assert.Equal("Final", final.Fase);
        Assert.Equal(reservado, final.HorarioPrevisto);
        Assert.Equal("Q2", final.NomeQuadra);
    }

    [Fact]
    public async Task Desligar_o_DTN_tira_a_categoria_vazia()
    {
        var (ctx, _, cat, org) = await SorteadoAsync();
        using var _c = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).ConfigurarDtnDaCategoria(cat.Id, 0, null);

        Assert.Null(await DtnAsync(ctx, cat.Id));
    }

    [Fact]
    public async Task Duplicar_o_torneio_nao_copia_a_DTN()
    {
        var (ctx, torneio, cat, org) = await SorteadoAsync();
        using var _c = ctx;
        Assert.NotNull(await DtnAsync(ctx, cat.Id));
        // Duplicar exige o perfil de organizador, como o Create.
        (await ctx.Jogadores.FindAsync(org.Id))!.IsOrganizadorTorneio = true;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).Duplicar(torneio.Id);

        var novo = await ctx.Torneios.AsNoTracking().Where(t => t.Id != torneio.Id).OrderByDescending(t => t.Id).FirstAsync();
        Assert.DoesNotContain(await ctx.Categorias.AsNoTracking().Where(c => c.TorneioId == novo.Id).ToListAsync(),
            c => c.Nome.StartsWith("DTN"));
    }
}
