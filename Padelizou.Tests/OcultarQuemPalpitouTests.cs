using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// "OCULTAR QUEM VOTOU" NO PALPITÔMETRO, POR TORNEIO (21/11/2026). 🗣️ Felipe: *"Para a organização
// do torneio, criar a opção de ocultar o nome de quem votou no palpitômetro"*. Decisões dele: some
// SÓ o "ver quem votou" de cada jogo (a aba Palpiteiros fica), e o ORGANIZADOR continua vendo.
public class OcultarQuemPalpitouTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida partida, Jogador org, Jogador torcedor)> MontarAsync(bool ocultar)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.OcultarQuemPalpitou = ocultar;
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id,
            Status = "Agendada", Fase = FasesTorneio.FaseDeGrupos, Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        var torcedor = new Jogador { Nome = "Torcedor", Cpf = "52998224725" };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        await new PalpiteService(ctx).RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 9, 4);
        return (ctx, torneio, partida, org, torcedor);
    }

    [Fact]
    public async Task Com_a_opcao_ligada_quem_nao_organiza_nao_ve_os_nomes()
    {
        var (ctx, _, partida, _, torcedor) = await MontarAsync(ocultar: true);
        using var _ = ctx;

        Assert.IsType<ForbidResult>(await TestInfra.NovoPartidasController(ctx, torcedor.Id, palpites: new PalpiteService(ctx)).VerVotos(partida.Id));
        Assert.IsType<ForbidResult>(await TestInfra.NovoPartidasController(ctx, null, palpites: new PalpiteService(ctx)).VerVotos(partida.Id));
    }

    [Fact]
    public async Task Com_a_opcao_ligada_o_organizador_continua_vendo()
    {
        var (ctx, _, partida, org, _) = await MontarAsync(ocultar: true);
        using var _ = ctx;

        var json = Assert.IsType<JsonResult>(await TestInfra.NovoPartidasController(ctx, org.Id, palpites: new PalpiteService(ctx)).VerVotos(partida.Id));
        Assert.Equal("Torcedor", Assert.Single(Assert.IsType<VotantesPartidaVM>(json.Value).VotantesDupla1).Nome);
    }

    [Fact]
    public async Task Com_a_opcao_desligada_todo_mundo_ve_como_antes()
    {
        var (ctx, _, partida, _, torcedor) = await MontarAsync(ocultar: false);
        using var _ = ctx;

        Assert.IsType<JsonResult>(await TestInfra.NovoPartidasController(ctx, torcedor.Id, palpites: new PalpiteService(ctx)).VerVotos(partida.Id));
    }

    // O botão some da lista de jogos pra quem não organiza (a lista lê ViewBag.VerQuemPalpitou).
    [Fact]
    public async Task A_lista_de_jogos_nao_oferece_o_botao_pra_quem_nao_organiza()
    {
        var (ctx, torneio, _, org, torcedor) = await MontarAsync(ocultar: true);
        using var _ = ctx;

        var doTorcedor = TestInfra.NovoTorneiosController(ctx, torcedor.Id);
        await doTorcedor.Jogos(torneio.Id, null, null);
        var doOrganizador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doOrganizador.Jogos(torneio.Id, null, null);

        Assert.False(doTorcedor.ViewBag.VerQuemPalpitou as bool? ?? true);
        Assert.True(doOrganizador.ViewBag.VerQuemPalpitou as bool? ?? false);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task A_gestao_liga_e_desliga(bool pedido, bool esperado)
    {
        var (ctx, torneio, _, org, _) = await MontarAsync(ocultar: !pedido);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).Editar(torneio.Id, "Torneio de Teste", null, null,
            precoInscricao: 0m, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null, ocultarQuemPalpitou: pedido);

        Assert.Equal(esperado, (await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id)).OcultarQuemPalpitou);
    }

    [Fact]
    public async Task Aba_antiga_sem_o_campo_mantem_o_gravado()
    {
        var (ctx, torneio, _, org, _) = await MontarAsync(ocultar: true);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).Editar(torneio.Id, "Torneio de Teste", null, null,
            precoInscricao: 0m, clubeId: 0, quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null);

        Assert.True((await ctx.Torneios.AsNoTracking().SingleAsync(t => t.Id == torneio.Id)).OcultarQuemPalpitou);
    }

    [Fact]
    public void A_copia_do_torneio_leva_a_opcao()
    {
        Assert.Contains(nameof(Torneio.OcultarQuemPalpitou), DuplicacaoDeTorneio.Copiadas);
    }
}
