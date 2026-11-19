using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CAIXA "Mesma duração em todas as fases" (19/11/2026) — na gestão e na criação. Marcada, as
// três durações próprias zeram (= igual aos grupos); desmarcada, gravam o que veio. Na gestão,
// campo ausente (aba antiga) mantém o gravado.
public class DuracaoPorFaseNaTelaTests
{
    private static Task<IActionResult> Editar(Padelizou.Controllers.TorneiosController c, int id,
        bool? mesma, int? mataMata = null, int? semi = null, int? final = null) =>
        c.Editar(id, "Torneio de Teste", null, null, precoInscricao: 0m, clubeId: 0,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            mesmaDuracaoEmTodasAsFases: mesma,
            tempoMataMataMinutos: mataMata, tempoSemifinalMinutos: semi, tempoFinalMinutos: final);

    [Fact]
    public async Task Desmarcada_grava_a_duracao_de_cada_fase()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, false, 50, 70, 80);

        var t = ctx.Torneios.Find(torneio.Id)!;
        Assert.Equal((50, 70, 80), (t.TempoMataMataMinutos, t.TempoSemifinalMinutos, t.TempoFinalMinutos));
    }

    [Fact]
    public async Task Marcada_zera_as_tres_mesmo_com_os_campos_escondidos_mandando_valor()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.TempoFinalMinutos = 80;
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, true, 50, 70, 80);

        Assert.True(DuracaoDaPartida.TodasIguais(ctx.Torneios.Find(torneio.Id)!));
    }

    [Fact]
    public async Task Aba_antiga_sem_o_campo_mantem_o_gravado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.TempoFinalMinutos = 80;
        ctx.SaveChanges();

        await Editar(TestInfra.NovoTorneiosController(ctx, organizador.Id), torneio.Id, null);

        Assert.Equal(80, ctx.Torneios.Find(torneio.Id)!.TempoFinalMinutos);
    }

    [Fact]
    public async Task Duracao_de_fase_fora_da_faixa_e_recusada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await Editar(c, torneio.Id, false, 50, 0, 80);

        Assert.NotNull(c.TempData["Erro"]);
        Assert.Equal(0, ctx.Torneios.Find(torneio.Id)!.TempoFinalMinutos);
    }

    // ── NA CRIAÇÃO ──────────────────────────────────────────────────────────────────────
    private static Torneio TorneioValido(int final) => new()
    {
        Nome = "Copa Teste", ClubeId = 1, Status = "Inscrições Abertas",
        SetsFaseGrupos = 1, GamesFaseGrupos = 6, RestricaoCategoria = "Livre", FormaPagamento = "Externo",
        TempoPrevistoPartidaMinutos = 50, TempoFinalMinutos = final, QuantidadeQuadras = 2,
    };

    private static DbPadelContext ContextoComClube()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao { Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina" });
        ctx.SaveChanges();
        return ctx;
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 80)]
    public async Task Na_criacao_a_caixa_decide_se_a_final_tem_duracao_propria(bool mesma, int esperado)
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(final: 80), new[] { 3 }, null, null, null, null,
            mesmaDuracaoEmTodasAsFases: mesma);

        Assert.Equal(esperado, ctx.Torneios.Single().TempoFinalMinutos);
    }
}
