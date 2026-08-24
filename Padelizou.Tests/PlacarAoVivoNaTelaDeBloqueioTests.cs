using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "Seguir este jogo" (16/08/2026, pedido do Felipe: "algo parecido com o placar que o Google
// mostra na tela de bloqueio"). Quem segue um jogo AO VIVO recebe UMA notificação por jogo que
// se ATUALIZA sozinha a cada game — não uma nova pra cada um.
//
// ⚠️ O gancho vive em TRÊS lugares (Mesa de Controle offline-first, o lote da lista AO VIVO e
// o Finalizar) porque são as três portas por onde um placar muda — mesma lição da "regra
// duplicada": a segunda cópia é sempre a que ninguém lembra de atualizar. Estes testes cobrem
// as três, não só a mais óbvia.
public class PlacarAoVivoNaTelaDeBloqueioTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida aoVivo, Jogador org, Jogador fa)>
        ComUmJogoAoVivoESeguidorAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899901" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        ctx.Add(new SeguidorDePartida { JogadorId = fa.Id, PartidaId = jogo.Id });
        await ctx.SaveChangesAsync();

        return (ctx, torneio, jogo, org, fa);
    }

    // ===================== O SERVIÇO (Services/AvisoDePlacarAoVivo) =====================

    [Fact]
    public async Task Avisa_cada_seguidor_com_a_MESMA_tag_do_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = 3;
        await ctx.SaveChangesAsync();

        var a = new Jogador { Nome = "Ana", Cpf = "77788899902" };
        var b = new Jogador { Nome = "Beto", Cpf = "77788899903" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        ctx.AddRange(
            new SeguidorDePartida { JogadorId = a.Id, PartidaId = jogo.Id },
            new SeguidorDePartida { JogadorId = b.Id, PartidaId = jogo.Id });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var servico = new AvisoDePlacarAoVivo(ctx, push);

        await servico.AvisarSeguidoresAsync(jogo.Id, "/Torneios/Details/1");

        var tag = $"partida-{jogo.Id}";
        await push.Received(1).EnviarPlacarAoVivoAsync(a.Id, Arg.Any<string>(),
            Arg.Is<string>(c => c != null && c.Contains("4") && c.Contains("3")), "/Torneios/Details/1", tag);
        await push.Received(1).EnviarPlacarAoVivoAsync(b.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), tag);
    }

    // Jogo sem seguidor nenhum — o caso comum — não pode custar a consulta cara (a partida com
    // as duas duplas carregadas). O teste prova o efeito observável: zero push.
    [Fact]
    public async Task Jogo_sem_seguidor_nao_manda_push_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        await push.DidNotReceiveWithAnyArgs().EnviarPlacarAoVivoAsync(
            default, default!, default!, default!, default!);
    }

    // O jogo acabou: manda o placar FINAL e não deixa a linha pra trás — não há mais "ao
    // vivo" pra essa pessoa acompanhar, e uma linha esquecida nunca mais dispara nada.
    [Fact]
    public async Task Ao_terminar_manda_o_placar_final_e_para_de_seguir()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.Status = "Finalizada";
        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 5;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarFimEPararDeSeguirAsync(jogo.Id, "/x");

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id,
            Arg.Is<string>(t => t != null && t.Contains("encerrado")),
            Arg.Is<string>(c => c != null && c.Contains("9") && c.Contains("5")),
            "/x", $"partida-{jogo.Id}");

        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == jogo.Id));
    }

    // ===================== AS TRÊS PORTAS QUE MUDAM O PLACAR =====================

    [Fact]
    public async Task Mesa_de_Controle_avisa_os_seguidores_ao_sincronizar()
    {
        var (ctx, _, jogo, org, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.SincronizarPlacar(jogo.Id, games1: 3, games2: 2, sets1: 0, sets2: 0,
            marcadoEm: DateTimeOffset.Now.ToUnixTimeMilliseconds());

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), $"partida-{jogo.Id}");
    }

    [Fact]
    public async Task Lote_da_lista_ao_vivo_avisa_so_o_jogo_que_MUDOU()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 5;
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        var aoVivo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Take(2).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899904" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();
        // Só segue o SEGUNDO jogo do lote.
        ctx.Add(new SeguidorDePartida { JogadorId = fa.Id, PartidaId = aoVivo[1].Id });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.SalvarPlacaresAoVivo(torneio.Id, aoVivo.Select(p => p.Id).ToArray(),
            new[] { 4, 4 }, new[] { 1, 1 });

        // Um aviso só, pro jogo que ele segue — não dois, e não pro jogo que ninguém segue.
        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), $"partida-{aoVivo[1].Id}");
    }

    [Fact]
    public async Task Finalizar_manda_o_placar_final_e_encerra_o_seguir()
    {
        var (ctx, _, jogo, org, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 4;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.FinalizarPartida(jogo.Id);

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id,
            Arg.Is<string>(t => t != null && t.Contains("encerrado")), Arg.Any<string>(), Arg.Any<string>(),
            $"partida-{jogo.Id}");
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == jogo.Id));
    }

    // ===================== SEGUIR / PARAR DE SEGUIR =====================

    [Fact]
    public async Task Seguir_um_jogo_ao_vivo_cria_a_linha()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899905" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.SeguirPartidaAoVivo(jogo.Id);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(resposta);
        Assert.Contains("\"seguindo\":true", System.Text.Json.JsonSerializer.Serialize(json.Value));
        Assert.True(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // Clicar duas vezes (duplo toque, duas abas) não pode virar duas linhas mandando o mesmo
    // aviso duas vezes — o índice único no banco é a segunda trava; esta é a primeira.
    [Fact]
    public async Task Seguir_duas_vezes_nao_duplica_a_linha()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        await controller.SeguirPartidaAoVivo(jogo.Id);
        await controller.SeguirPartidaAoVivo(jogo.Id);

        Assert.Equal(1, await ctx.Set<SeguidorDePartida>().CountAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // Jogo agendado ou já finalizado: seguir não faz sentido (não há placar mudando), e a
    // requisição costuma ser uma tela velha em cache.
    [Fact]
    public async Task Nao_da_pra_seguir_jogo_que_nao_esta_ao_vivo()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var agendado = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899906" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.SeguirPartidaAoVivo(agendado.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resposta);
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == agendado.Id));
    }

    [Fact]
    public async Task Parar_de_seguir_apaga_a_linha()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.PararDeSeguirPartidaAoVivo(jogo.Id);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(resposta);
        Assert.Contains("\"seguindo\":false", System.Text.Json.JsonSerializer.Serialize(json.Value));
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // ===================== O PUSH-ONLY NÃO ENTRA NA CAIXA NEM MANDA E-MAIL =====================
    //
    // Mesmo motivo do AvisoNaoSeguraOCliqueTests: um jogo de 9 games não pode virar 9 linhas
    // na Caixa de Avisos nem 9 e-mails — é acompanhamento em tempo real, não recado.
    [Fact]
    public async Task ApenasPush_nao_entra_na_caixa_de_avisos_nem_manda_email()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 7, Nome = "Fulano", Cpf = "77788899907", Login = "fulano7",
            Email = "fulano7@exemplo.com", NotificarEmail = true,
        });
        await ctx.SaveChangesAsync();

        var email = Substitute.For<IEmailService>();
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var servico = new PushNotificationService(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            fila, email,
            Options.Create(new SiteSettings { Url = "https://padelizou.com.br" }),
            PorteiroDeTeste.Saida(),
            NullLogger<PushNotificationService>.Instance);

        await servico.EnviarPlacarAoVivoAsync(7, "Placar ao vivo", "4 x 3", "/x", "partida-1");

        Assert.True(fila.TentarLer(out var aviso));
        Assert.True(aviso!.ApenasPush);
        Assert.Equal("partida-1", aviso.Tag);

        await servico.EntregarAgoraAsync(aviso);

        Assert.Empty(ctx.AvisosDoJogador);
        await email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);
    }
}
