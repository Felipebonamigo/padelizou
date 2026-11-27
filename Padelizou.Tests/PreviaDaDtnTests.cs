using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// OS JOGOS DA DTN NA PRÉVIA (27/11/2026). 🗣️ Rafael (Treta): "só não tem os jogos da DTN
// previstos". A DTN só nasce quando os grupos do módulo fecham (é aí que se sabe quem foi
// último); até lá a prévia mostrava as semis e a final do Principal e nada da DTN. Agora a prévia
// promete também a DTN — com hora e quadra na mesma grade —, e na DTN já nascida a vaga da fase
// seguinte é do PERDEDOR, não do "Vencedor".
public class PreviaDaDtnTests
{
    [Fact]
    public void Dois_grupos_de_4_dao_a_final_DTN_entre_os_4os()
    {
        var cadeia = ProximasFasesDaChave.DaDtn(
            new[] { "Grupo A", "Grupo B" }, new[] { 4, 4 }, classificadosPorGrupo: 2, duplasNoDtnPorGrupo: 1,
            fimDosGrupos: null, categoria: "DTN Branco");

        var final = Assert.Single(cadeia.Rodadas);
        Assert.Equal("Final", final.Fase);
        var (l1, l2) = Assert.Single(final.Confrontos);
        Assert.Equal(new[] { "4º do Grupo A", "4º do Grupo B" }, new[] { l1.Rotulo, l2.Rotulo }.OrderBy(x => x));
        Assert.Equal("DTN Branco", cadeia.Categoria);
    }

    [Fact]
    public void Tres_grupos_dao_a_semi_dos_melhores_ultimos_e_a_final_contra_o_pior()
    {
        var cadeia = ProximasFasesDaChave.DaDtn(
            new[] { "Grupo A", "Grupo B", "Grupo C" }, new[] { 3, 3, 4 }, 2, 1, null, "DTN Preto");

        Assert.Equal(new[] { "Semifinal", "Final" }, cadeia.Rodadas.Select(r => r.Fase).ToArray());
        var (s1, s2) = Assert.Single(cadeia.Rodadas[0].Confrontos);
        Assert.Equal(new[] { "2º melhor último", "Melhor último" }, new[] { s1.Rotulo, s2.Rotulo }.OrderBy(x => x));
        var (f1, f2) = Assert.Single(cadeia.Rodadas[1].Confrontos);
        Assert.Equal(new[] { "Perdedor Semifinal 1", "Pior último" }, new[] { f1.Rotulo, f2.Rotulo }.OrderBy(x => x));
    }

    [Fact]
    public void Na_DTN_ja_nascida_quem_segue_e_o_perdedor()
    {
        var cadeia = ProximasFasesDaChave.Montar(
            new[] { new ProximasFasesDaChave.PartidaDaChave(1, "Semifinal", "Dupla X", "Dupla Y", null) },
            new[] { "Dupla Z" }, "DTN Preto", categoriaId: 9, invertida: true);

        var (l1, l2) = Assert.Single(Assert.Single(cadeia.Rodadas).Confrontos);
        Assert.Contains("Perdedor Semifinal 1", new[] { l1.Rotulo, l2.Rotulo });
        Assert.DoesNotContain(new[] { l1.Rotulo, l2.Rotulo }, r => r.StartsWith("Vencedor"));
    }

    [Fact]
    public async Task A_previa_do_torneio_traz_a_final_DTN_com_horario()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, cat, org) = TestInfra.MontarTorneio(ctx, 8);
        var sabado = DateTime.Today.AddDays(14);
        while (sabado.DayOfWeek != DayOfWeek.Saturday) sabado = sabado.AddDays(1);
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });
        cat.QuantidadeGrupos = 2;
        cat.DuplasNoDtnPorGrupo = 1;
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        ctx.ChangeTracker.Clear();

        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var jogos = (await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas())
            .ProjetarProximasFasesAsync(torneio.Id, todas)).Jogos;

        var dtn = Assert.Single(jogos, j => j.Categoria == ChaveDtn.NomeDaDtn(cat.Nome));
        Assert.Equal("Final", dtn.Fase);
        Assert.NotNull(dtn.Horario);
        // O Principal continua lá.
        Assert.Contains(jogos, j => j.CategoriaId == cat.Id && j.Fase == "Final");
    }
}
