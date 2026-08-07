using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Middleware;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O vigia de erros de produção: todo 500 vira registro, o PRIMEIRO de cada tipo vira aviso
// pros admins raiz, e a janela de silêncio segura as repetições — alerta que se repete vira
// ruído, e ruído ensina a ignorar.
public class RegistroDeErrosTests
{
    private static RegistroDeErros NovoRegistro(DbPadelContext ctx, IPushNotificationService? push = null) =>
        new(ctx, push ?? Substitute.For<IPushNotificationService>(), NullLogger<RegistroDeErros>.Instance);

    private static Jogador NovoAdminRaiz(DbPadelContext ctx)
    {
        var admin = new Jogador { Nome = "Admin", Cpf = "00000000001", IsAdminRaiz = true };
        ctx.Jogadores.Add(admin);
        ctx.SaveChanges();
        return admin;
    }

    // --- A regra pura da janela de silêncio ---

    [Fact]
    public void PrimeiroErroSempreAvisa()
    {
        Assert.True(VigiaDeErros.DeveAvisar(null, DateTime.Now));
    }

    [Fact]
    public void RepeticaoDentroDaJanelaFicaCalada()
    {
        var agora = DateTime.Now;
        Assert.False(VigiaDeErros.DeveAvisar(agora.AddHours(-5), agora));
    }

    [Fact]
    public void ErroQuePersisteDepoisDaJanelaReavisa()
    {
        var agora = DateTime.Now;
        Assert.True(VigiaDeErros.DeveAvisar(agora - VigiaDeErros.JanelaDeSilencio, agora));
    }

    // --- O registro e o aviso ---

    [Fact]
    public async Task ErroVira_RegistroCompleto_E_AvisoPraCadaAdminRaiz()
    {
        using var ctx = TestInfra.NovoContexto();
        var admin = NovoAdminRaiz(ctx);
        var naoAdmin = new Jogador { Nome = "Comum", Cpf = "00000000002" };
        ctx.Jogadores.Add(naoAdmin);
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        var registro = NovoRegistro(ctx, push);

        await registro.RegistrarAsync(new InvalidOperationException("estourou"),
            "/Torneios/FinalizarPartida", "POST", jogadorId: naoAdmin.Id);

        var erro = Assert.Single(ctx.ErrosDoSistema);
        Assert.Equal("InvalidOperationException", erro.Tipo);
        Assert.Equal("/Torneios/FinalizarPartida", erro.Caminho);
        Assert.Equal("POST", erro.Metodo);
        Assert.Equal("estourou", erro.Mensagem);
        Assert.Equal(naoAdmin.Id, erro.JogadorId);
        Assert.True(erro.AvisoEnviado);
        Assert.Contains("InvalidOperationException", erro.Detalhe);

        // Aviso foi pro admin raiz — e SÓ pra ele. O jogador comum não é canal de alerta.
        await push.Received(1).EnviarParaJogadorAsync(admin.Id, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.DidNotReceive().EnviarParaJogadorAsync(naoAdmin.Id, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task MesmoErroRepetido_RegistraTodos_MasAvisaUmaVez()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        var push = Substitute.For<IPushNotificationService>();
        var registro = NovoRegistro(ctx, push);

        for (var i = 0; i < 3; i++)
        {
            await registro.RegistrarAsync(new InvalidOperationException("estourou"),
                "/Torneios/FinalizarPartida", "POST", jogadorId: null);
        }

        // As três ocorrências ficam no registro — o histórico é o que diz "está acontecendo
        // direto" pra quem investiga — mas só a primeira acorda alguém.
        Assert.Equal(3, ctx.ErrosDoSistema.Count());
        Assert.Equal(1, ctx.ErrosDoSistema.Count(e => e.AvisoEnviado));
        await push.Received(1).EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task ErroDiferenteNoMesmoCaminho_AvisaTambem()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        var push = Substitute.For<IPushNotificationService>();
        var registro = NovoRegistro(ctx, push);

        await registro.RegistrarAsync(new InvalidOperationException("um"), "/X", "GET", null);
        await registro.RegistrarAsync(new NullReferenceException("outro"), "/X", "GET", null);

        // Tipo diferente = problema diferente: a janela de silêncio de um não cala o outro.
        Assert.Equal(2, ctx.ErrosDoSistema.Count(e => e.AvisoEnviado));
        await push.Received(2).EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task AvisoAntigoNaoCalaMais_DepoisDaJanela()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        // Um aviso do mesmo erro, mais velho que a janela de silêncio.
        ctx.ErrosDoSistema.Add(new ErroDoSistema
        {
            QuandoEm = DateTime.Now - VigiaDeErros.JanelaDeSilencio - TimeSpan.FromMinutes(1),
            Caminho = "/X",
            Metodo = "GET",
            Tipo = "InvalidOperationException",
            Mensagem = "antigo",
            Detalhe = "antigo",
            AvisoEnviado = true,
        });
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        var registro = NovoRegistro(ctx, push);

        await registro.RegistrarAsync(new InvalidOperationException("de novo"), "/X", "GET", null);

        // Erro que continua acontecendo 6h depois é erro que ninguém corrigiu — reavisar é
        // exatamente o que a janela existe pra permitir.
        await push.Received(1).EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task RegistroAntigoDemais_SaiNaLimpezaQuePegaCaronaNoAviso()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        ctx.ErrosDoSistema.Add(new ErroDoSistema
        {
            QuandoEm = DateTime.Now - VigiaDeErros.Retencao - TimeSpan.FromDays(1),
            Caminho = "/velho",
            Metodo = "GET",
            Tipo = "Exception",
            Mensagem = "do passado",
            Detalhe = "do passado",
        });
        ctx.SaveChanges();

        await NovoRegistro(ctx).RegistrarAsync(new InvalidOperationException("novo"), "/X", "GET", null);

        // O velho saiu, o novo ficou.
        var erro = Assert.Single(ctx.ErrosDoSistema);
        Assert.Equal("/X", erro.Caminho);
    }

    // ⚠️ Este prende o defeito achado na primeira verificação em tela: o UseExceptionHandler
    // reescreve o caminho da requisição pra "/Home/Error" ANTES de chamar o handler, e a
    // primeira versão gravava esse destino. Onze erros foram registrados apontando pro mesmo
    // lugar — o registro existe justamente pra dizer ONDE quebrou, então ele estava gravando
    // a única informação que não serve pra nada.
    [Fact]
    public async Task GravaOndeQUEBROU_NaoAPaginaDeErroPraOndeOFrameworkReescreveu()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(NovoRegistro(ctx))
                .BuildServiceProvider(),
        };
        // O estado exato em que o framework entrega o contexto pro handler.
        http.Request.Path = "/Home/Error";
        http.Request.Method = "POST";
        http.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Path = "/Torneios/FinalizarPartida",
            Error = new InvalidOperationException("estourou no meio do torneio"),
        });

        await new CapturaDeErro(NullLogger<CapturaDeErro>.Instance).TryHandleAsync(
            http, new InvalidOperationException("estourou no meio do torneio"), default);

        var erro = Assert.Single(ctx.ErrosDoSistema);
        Assert.Equal("/Torneios/FinalizarPartida", erro.Caminho);
    }

    [Fact]
    public async Task SemAFeature_CaiNoCaminhoDaRequisicao()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(NovoRegistro(ctx))
                .BuildServiceProvider(),
        };
        http.Request.Path = "/Torneios/Details/7";
        http.Request.Method = "GET";

        await new CapturaDeErro(NullLogger<CapturaDeErro>.Instance).TryHandleAsync(
            http, new InvalidOperationException("sem feature"), default);

        Assert.Equal("/Torneios/Details/7", Assert.Single(ctx.ErrosDoSistema).Caminho);
    }

    // Registrar não é tratar: quem vê a tela continua vendo a página amiga de sempre.
    [Fact]
    public async Task NuncaDizQueTratou()
    {
        using var ctx = TestInfra.NovoContexto();
        var http = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(NovoRegistro(ctx))
                .BuildServiceProvider(),
        };

        var tratou = await new CapturaDeErro(NullLogger<CapturaDeErro>.Instance).TryHandleAsync(
            http, new InvalidOperationException("x"), default);

        Assert.False(tratou);
    }

    // O vigia não pode piorar a queda: se o banco é justamente o que caiu, registrar falha
    // também — e a requisição não pode estourar uma SEGUNDA vez por causa disso.
    [Fact]
    public async Task FalhaAoRegistrar_NaoEscapaDoHandler()
    {
        var http = new DefaultHttpContext
        {
            // Sem o RegistroDeErros no contêiner: o GetRequiredService lança lá dentro.
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };

        var tratou = await new CapturaDeErro(NullLogger<CapturaDeErro>.Instance).TryHandleAsync(
            http, new InvalidOperationException("x"), default);

        Assert.False(tratou);
    }

    [Fact]
    public async Task MensagemGigante_NaoDerrubaORegistro()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoAdminRaiz(ctx);

        var mensagem = new string('x', 20000);
        await NovoRegistro(ctx).RegistrarAsync(new InvalidOperationException(mensagem), "/X", "GET", null);

        // Perder o REGISTRO por causa do tamanho da mensagem seria o vigia falhando na hora
        // em que foi chamado — os tetos das colunas cortam, não recusam.
        var erro = Assert.Single(ctx.ErrosDoSistema);
        Assert.Equal(1000, erro.Mensagem.Length);
        Assert.Equal(VigiaDeErros.TamanhoMaximoDoDetalhe, erro.Detalhe.Length);
    }
}
