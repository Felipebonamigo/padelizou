using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// TIE-BREAK NO EMPATE DA PENÚLTIMA (25/11/2026). 🗣️ Organizador do Treta: "um set até 6. Tie em
// 5:5". Hoje o até 6 estende no 5x5 (vai pro 7º game) e só abre tie-break no 6x6 (fecha 7x6).
// Com a opção ligada, o 5x5 abre o tie-break e o set fecha 6x5 — como o até 9 fecha 9x8.
public class TieBreakNaPenultimaTests
{
    private static readonly FormatoDaPartida.Formato Ate6ComTieNoCincoCinco =
        new(1, 6, ContagemDeGamesDoTorneio.Ate, TieBreakDoJogo.PontosPadrao, TieBreakNaPenultima: true);

    private static readonly FormatoDaPartida.Formato Ate6DeSempre =
        new(1, 6, ContagemDeGamesDoTorneio.Ate, TieBreakDoJogo.PontosPadrao);

    [Fact]
    public void No_5x5_abre_o_tie_break()
    {
        Assert.Equal(5, TieBreakDoJogo.EmpateDoTieBreak(Ate6ComTieNoCincoCinco));
        Assert.True(TieBreakDoJogo.EmAndamento(Ate6ComTieNoCincoCinco, 5, 5));
        Assert.True(TieBreakDoJogo.PodeAcontecer(Ate6ComTieNoCincoCinco));
    }

    [Fact]
    public void O_set_fecha_6x5_e_nao_vai_ao_7()
    {
        Assert.True(FormatoDaPartida.PodeEncerrar(Ate6ComTieNoCincoCinco, 6, 5));
        Assert.Equal(6, FormatoDaPartida.TetoDoLado(Ate6ComTieNoCincoCinco, 5, 5));
        Assert.Equal((6, 5), FormatoDaPartida.PlacarValido(Ate6ComTieNoCincoCinco, 7, 5));
    }

    [Fact]
    public void Fechar_o_tie_break_escreve_6x5()
    {
        Assert.Equal((6, 5), TieBreakDoJogo.GamesAoFechar(Ate6ComTieNoCincoCinco, 7, 5));
    }

    [Fact]
    public void Desligado_o_ate_6_continua_como_sempre()
    {
        Assert.Equal(7, FormatoDaPartida.TetoDoLado(Ate6DeSempre, 5, 5));
        Assert.Equal(6, TieBreakDoJogo.EmpateDoTieBreak(Ate6DeSempre));
    }

    [Fact]
    public void O_formato_da_fase_leva_a_opcao_do_torneio()
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x", GamesFaseGrupos = 6, SetsFaseGrupos = 1, TieBreakNaPenultima = true };
        Assert.True(FormatoDaPartida.De(torneio, "Grupo A").TieBreakNaPenultima);
        Assert.True(FormatoDaPartida.De(torneio, new Partida { Fase = "Final", GamesDoJogo = 6 }).TieBreakNaPenultima);
        Assert.False(FormatoDaPartida.De(new Torneio { Nome = "x", Codigo = "x" }, "Grupo A").TieBreakNaPenultima);
    }

    [Fact]
    public void A_Mesa_recebe_a_opcao()
    {
        var mesa = File.ReadAllText(Path.Combine(TestInfra.PastaDasFontesDeVerdade(), "..", "..", "Views", "Torneios", "MesaControle.cshtml"));
        Assert.Contains("tieNaPenultima:", mesa);
    }
}

// A opção grava pela gestão do torneio — e a aba antiga (campo ausente) não a apaga.
public class TieBreakNaPenultimaNaGestaoTests
{
    private static Task EditarAsync(DbPadelContext ctx, Torneio torneio, int organizadorId, bool? tieBreakNaPenultima) =>
        TestInfra.NovoTorneiosController(ctx, organizadorId).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio, precoInscricao: torneio.PrecoInscricao,
            clubeId: torneio.ClubeId, quantidadeQuadras: 1, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: torneio.RestricaoCategoria, capa: null,
            tempoPrevistoPartidaMinutos: 50,
            tieBreakNaPenultima: tieBreakNaPenultima);

    [Fact]
    public async Task Ligar_grava_e_campo_ausente_mantem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        await EditarAsync(ctx, torneio, org.Id, tieBreakNaPenultima: true);
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.TieBreakNaPenultima);

        await EditarAsync(ctx, torneio, org.Id, tieBreakNaPenultima: null);
        Assert.True((await ctx.Torneios.FindAsync(torneio.Id))!.TieBreakNaPenultima);

        await EditarAsync(ctx, torneio, org.Id, tieBreakNaPenultima: false);
        Assert.False((await ctx.Torneios.FindAsync(torneio.Id))!.TieBreakNaPenultima);
    }
}
