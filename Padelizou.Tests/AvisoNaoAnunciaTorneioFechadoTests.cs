using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O "NOVO TORNEIO ABERTO" NÃO ANUNCIA O QUE A PESSOA NÃO PODE JOGAR (20/09/2026).
//
// 🐛 O CASO: o push *"Novo torneio aberto — Los Corneteiros | Seletiva QTimes"* saiu pra base
// inteira. 🗣️ Felipe: *"esse torneio é restrito, ai nao deveria aparecer"*.
//
// O `AvisoDeTorneioNovo` tinha TRÊS recusas — não aprovado, oculto, já avisado — e nenhuma
// delas olhava quem pode se INSCREVER. O torneio de um time só entrou em 16/09 e trancou a
// porta da inscrição; o anúncio ficou como estava, então ele continuou convidando a base
// inteira pra uma festa de convidados.
//
// A régua agora, decidida pelo Felipe:
//   · RESTRITO (exige chave) → não anuncia pra ninguém. Quem não tem o papelzinho não entra.
//   · TIME EXCLUSIVO (exige a camisa) → anuncia SÓ pra quem veste ela. Silenciar de vez tiraria
//     o aviso justamente de quem PODE jogar.
//   · OS DOIS JUNTOS (o modelo permite) → o restrito ganha e cala. A chave é entregue na mão
//     pelo organizador; avisar quem não a tem é o mesmo barulho um nível abaixo.
//
// ⚠️ NADA DISSO ESCONDE O TORNEIO: ele continua na listagem pública e na página. Quem some da
// vista é o `Oculto`, que é outra coisa e já era respeitado aqui.
public class AvisoNaoAnunciaTorneioFechadoTests
{
    private const string Titulo = "Novo torneio aberto";

    private static Jogador Torcedor(int i, string? estado = null, int? timeId = null)
    {
        var j = TestInfra.NovoJogador(i);
        j.NotificarTorneiosAbertos = true;
        j.Estado = estado;
        j.TimeId = timeId;
        return j;
    }

    private static async Task<(Torneio torneio, Jogador organizador)> TorneioAprovadoAsync(
        DbPadelContext ctx, string? ufDoOrganizador = null)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.AprovadoEm = new DateTime(2026, 9, 20);
        organizador.Estado = ufDoOrganizador;
        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    private static Task<AvisoDeTorneioNovo.Resultado> AvisarAsync(
        DbPadelContext ctx, Torneio torneio, IPushNotificationService push) =>
        AvisoDeTorneioNovo.EnviarSePuderAsync(ctx, push, torneio, "/Torneios/Details/1");

    // ── Restrito: silêncio ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Torneio_restrito_NAO_anuncia_pra_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioAprovadoAsync(ctx);
        torneio.Restrito = true;
        torneio.ChaveAcesso = "abc123";
        ctx.Jogadores.Add(Torcedor(70));
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        var resultado = await AvisarAsync(ctx, torneio, push);

        Assert.False(resultado.Enviou);
        Assert.Equal(0, resultado.Quantos);
        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Titulo, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Torneio_restrito_NAO_carimba_pra_poder_anunciar_se_a_trava_cair()
    {
        // Mesma razão do OCULTO, que também não carimba: o carimbo é pra sempre, e um torneio
        // que deixou de ser restrito ainda quer o anúncio. Carimbar aqui mataria ele calado.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioAprovadoAsync(ctx);
        torneio.Restrito = true;
        ctx.Jogadores.Add(Torcedor(70));
        await ctx.SaveChangesAsync();

        await AvisarAsync(ctx, torneio, Substitute.For<IPushNotificationService>());

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeTorneioNovoEm);
    }

    // ── Time exclusivo: só a camisa ──────────────────────────────────────────────────────

    [Fact]
    public async Task Torneio_de_um_time_so_avisa_SO_quem_veste_a_camisa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioAprovadoAsync(ctx);
        ctx.Times.Add(new Time { Id = 9, Nome = "Los Corneteiros" });
        var doTime = Torcedor(70, timeId: 9);
        var deFora = Torcedor(71);
        var deOutroTime = Torcedor(72, timeId: 8);
        ctx.Times.Add(new Time { Id = 8, Nome = "Outro Time" });
        ctx.Jogadores.AddRange(doTime, deFora, deOutroTime);
        torneio.TimeExclusivoId = 9;
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        var resultado = await AvisarAsync(ctx, torneio, push);

        Assert.True(resultado.Enviou);
        Assert.Equal(1, resultado.Quantos);
        await push.Received(1).EnviarParaJogadorAsync(
            doTime.Id, Titulo, torneio.Nome, Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
        foreach (var forasteiro in new[] { deFora, deOutroTime })
            await push.DidNotReceive().EnviarParaJogadorAsync(
                forasteiro.Id, Titulo, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

        // Este ANUNCIOU, então carimba como qualquer anúncio que saiu.
        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeTorneioNovoEm);
    }

    [Fact]
    public async Task Jogador_do_time_em_OUTRO_estado_continua_recebendo()
    {
        // A mira por UF existe pra não anunciar em Porto Alegre um torneio de São Paulo. Num
        // torneio DE TIME a camisa é o sinal mais forte que existe: quem é do time se desloca.
        // Deixar a UF cortar aqui silenciaria gente que PODE jogar — e caladinho.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioAprovadoAsync(ctx, ufDoOrganizador: "SP");
        ctx.Times.Add(new Time { Id = 9, Nome = "Los Corneteiros" });
        var doTimeLonge = Torcedor(70, estado: "RS", timeId: 9);
        ctx.Jogadores.Add(doTimeLonge);
        torneio.TimeExclusivoId = 9;
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        var resultado = await AvisarAsync(ctx, torneio, push);

        Assert.Equal(1, resultado.Quantos);
        await push.Received(1).EnviarParaJogadorAsync(
            doTimeLonge.Id, Titulo, torneio.Nome, Arg.Any<string?>(), AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Restrito_e_time_exclusivo_juntos_NAO_anunciam()
    {
        // Os dois podem estar ligados juntos (ver Torneio.TimeExclusivoId). O restrito ganha:
        // nem quem é do time entra sem a chave.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await TorneioAprovadoAsync(ctx);
        ctx.Times.Add(new Time { Id = 9, Nome = "Los Corneteiros" });
        ctx.Jogadores.Add(Torcedor(70, timeId: 9));
        torneio.TimeExclusivoId = 9;
        torneio.Restrito = true;
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        var resultado = await AvisarAsync(ctx, torneio, push);

        Assert.False(resultado.Enviou);
        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Titulo, Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }
}
