using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O "NOVO TORNEIO ABERTO" QUANDO O TORNEIO NASCE ESCONDIDO.
//
// 🕳️ O buraco que fechou (18/08/2026): o aviso só era disparado na APROVAÇÃO do admin, e lá ele
// era pulado quando o torneio estava oculto. Com o fluxo novo — criar oculto, ser aprovado,
// publicar no dia do anúncio — ele nunca sairia: o organizador publicava e ficava esperando um
// movimento que não vinha, sem nada na tela explicando.
//
// Agora são DOIS gatilhos (aprovar e publicar) e UM carimbo (`AvisoDeTorneioNovoEm`). O carimbo
// é o que separa "avisar no momento certo" de "avisar a base inteira toda vez que alguém
// esconder e publicar de novo".
public class AvisoDeTorneioNovoTests
{
    private static async Task<(Torneio torneio, Jogador organizador, Jogador quemQuerSaber)> MontarAsync(
        DbPadelContext ctx, bool oculto, bool aprovado = true)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.Oculto = oculto;
        torneio.AprovadoEm = aprovado ? new DateTime(2026, 8, 18) : null;

        var quemQuerSaber = TestInfra.NovoJogador(70);
        quemQuerSaber.NotificarTorneiosAbertos = true;
        ctx.Jogadores.Add(quemQuerSaber);
        await ctx.SaveChangesAsync();

        return (torneio, organizador, quemQuerSaber);
    }

    private static Task<IActionResult> PublicarAsync(
        DbPadelContext ctx, Torneio torneio, int organizadorId, IPushNotificationService push) =>
        TestInfra.NovoTorneiosController(ctx, organizadorId, push: push)
            .AlternarVisibilidade(torneio.Id, ocultar: false);

    [Fact]
    public async Task Publicar_o_torneio_guardado_dispara_o_aviso_que_a_aprovacao_nao_mandou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, quemQuerSaber) = await MontarAsync(ctx, oculto: true);
        var push = Substitute.For<IPushNotificationService>();

        await PublicarAsync(ctx, torneio, organizador.Id, push);

        await push.Received(1).EnviarParaJogadorAsync(
            quemQuerSaber.Id, "Novo torneio aberto", torneio.Nome, Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeTorneioNovoEm);
    }

    [Fact]
    public async Task Esconder_e_publicar_de_novo_NAO_reanuncia_o_torneio()
    {
        // O motivo de a coluna existir. Sem o carimbo, cada volta do interruptor mandaria o
        // mesmo push pra base inteira — e é o único aviso do sistema proporcional ao tamanho
        // dela, ou seja, o mais caro de errar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, quemQuerSaber) = await MontarAsync(ctx, oculto: true);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push);

        await controller.AlternarVisibilidade(torneio.Id, ocultar: false);
        await controller.AlternarVisibilidade(torneio.Id, ocultar: true);
        await controller.AlternarVisibilidade(torneio.Id, ocultar: false);

        await push.Received(1).EnviarParaJogadorAsync(
            quemQuerSaber.Id, "Novo torneio aberto", Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Torneio_ainda_NAO_APROVADO_nao_anuncia_nada_ao_ser_publicado()
    {
        // A trava de 07/08/2026 continua de pé: quem não passou pelo olhar do Padelizou não
        // alcança a base. Publicar aqui devolve só a página — a vitrine e o aviso esperam o OK.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, quemQuerSaber) = await MontarAsync(ctx, oculto: true, aprovado: false);
        var push = Substitute.For<IPushNotificationService>();

        await PublicarAsync(ctx, torneio, organizador.Id, push);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            quemQuerSaber.Id, "Novo torneio aberto", Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeTorneioNovoEm);
    }

    [Fact]
    public async Task Aprovar_um_torneio_OCULTO_nao_avisa_e_NAO_CARIMBA()
    {
        // ⚠️ O detalhe que faz o resto funcionar: se a aprovação carimbasse "já avisei" só pra
        // registrar que passou por ali, o torneio guardado ficaria mudo pra sempre — publicar
        // depois não mandaria nada, que é exatamente o buraco que esta mudança fecha.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, quemQuerSaber) = await MontarAsync(ctx, oculto: true);
        var push = Substitute.For<IPushNotificationService>();

        var resultado = await AvisoDeTorneioNovo.EnviarSePuderAsync(ctx, push, torneio, "/Torneios/Details/1");

        Assert.False(resultado.Enviou);
        Assert.Null(torneio.AvisoDeTorneioNovoEm);
        await push.DidNotReceive().EnviarParaJogadorAsync(
            quemQuerSaber.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_desligou_o_aviso_de_torneio_novo_continua_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, quemQuerSaber) = await MontarAsync(ctx, oculto: true);
        quemQuerSaber.NotificarTorneiosAbertos = false;
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await PublicarAsync(ctx, torneio, organizador.Id, push);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            quemQuerSaber.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Torneio_sem_ninguem_pra_avisar_carimba_do_mesmo_jeito()
    {
        // Senão ele voltaria a varrer a base a cada publicação só pra redescobrir que não há
        // quem avisar — a mesma lição do AvisoDeMvpEnviadoEm.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador, quemQuerSaber) = await MontarAsync(ctx, oculto: true);
        quemQuerSaber.NotificarTorneiosAbertos = false;
        await ctx.SaveChangesAsync();

        await PublicarAsync(ctx, torneio, organizador.Id, Substitute.For<IPushNotificationService>());

        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.AvisoDeTorneioNovoEm);
    }
}
