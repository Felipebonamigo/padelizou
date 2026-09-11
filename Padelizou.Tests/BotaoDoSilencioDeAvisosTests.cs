using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O SILÊNCIO GERAL: um botão no painel que cala push, e-mail e WhatsApp de uma vez.
//
// Pedido do Felipe (11/09/2026): *"crie um botão para desabilitar todas notificações no painel
// admin"*. Até aqui, estancar um envio em fuga exigia ssh no servidor e restart do serviço —
// e o momento em que isso é preciso é justamente o momento em que ninguém tem o notebook na
// mão. Mesmo molde do portão de acesso (ver BotaoDoPortaoTests): o banco guarda, o singleton
// lê, e a decisão sobrevive ao deploy.
//
// ⚠️ A REGRA QUE DÁ A FORMA AO RESTO: mudo é "não incomoda", NÃO é "apaga". A Caixa de Avisos
// continua sendo gravada, então religar não perde recado nenhum — a pessoa só não foi
// acordada na hora.
public class BotaoDoSilencioDeAvisosTests
{
    // ── A CHAVE ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_ninguem_ter_mexido_o_sistema_avisa_normalmente()
    {
        var silencio = new SilencioDeAvisos();

        Assert.False(silencio.Ligado);
        Assert.False(silencio.DecididoPeloAdmin);
    }

    // ⚠️ O TESTE QUE JUSTIFICA A TABELA, e é o mesmo raciocínio do portão: um valor só em
    // memória voltaria ao padrão no primeiro restart — e restart é o que todo deploy faz. O
    // sistema voltaria a falar sozinho no meio da noite, e ninguém ligaria uma coisa à outra.
    [Fact]
    public async Task A_decisao_sobrevive_ao_restart()
    {
        using var ctx = TestInfra.NovoContexto();
        await new SilencioDeAvisos().DefinirAsync(ctx, silenciar: true, porJogadorId: 1);

        var depoisDoRestart = new SilencioDeAvisos();
        await depoisDoRestart.CarregarAsync(ctx);

        Assert.True(depoisDoRestart.Ligado);
        Assert.True(depoisDoRestart.DecididoPeloAdmin);
    }

    [Fact]
    public async Task Religar_grava_e_nao_empilha_linha()
    {
        using var ctx = TestInfra.NovoContexto();
        var silencio = new SilencioDeAvisos();

        await silencio.DefinirAsync(ctx, silenciar: true, porJogadorId: 1);
        await silencio.DefinirAsync(ctx, silenciar: false, porJogadorId: 1);

        Assert.Single(ctx.ConfiguracoesDoSistema.Where(c => c.Chave == SilencioDeAvisos.Chave));
        Assert.False(silencio.Ligado);

        var depoisDoRestart = new SilencioDeAvisos();
        await depoisDoRestart.CarregarAsync(ctx);
        Assert.False(depoisDoRestart.Ligado);
    }

    // ── QUEM PODE MEXER ──────────────────────────────────────────────────────────────────

    // ⚠️ Só o admin RAIZ, mesma régua do portão: calar o sistema inteiro é tão global quanto
    // abrir o cadastro pro mundo. Administrador nomeado entra pra operar o dia a dia.
    [Fact]
    public async Task Administrador_nomeado_nao_cala_o_sistema()
    {
        using var ctx = TestInfra.NovoContexto();
        var nomeado = new Jogador { Nome = "Ajudante", Cpf = "99900000011", IsAdminGeral = true };
        ctx.Jogadores.Add(nomeado);
        await ctx.SaveChangesAsync();

        var silencio = new SilencioDeAvisos();
        var resposta = await Controlador(ctx, nomeado.Id)
            .AlternarSilencioDeAvisos(silencio, silenciar: true);

        Assert.IsType<ForbidResult>(resposta);
        Assert.False(silencio.Ligado);
        Assert.Empty(ctx.ConfiguracoesDoSistema);
    }

    [Fact]
    public async Task Admin_raiz_cala_o_sistema_pelo_painel()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = new Jogador { Nome = "Felipe", Cpf = "99900000001", IsAdminRaiz = true };
        ctx.Jogadores.Add(raiz);
        await ctx.SaveChangesAsync();

        var silencio = new SilencioDeAvisos();
        var resposta = await Controlador(ctx, raiz.Id)
            .AlternarSilencioDeAvisos(silencio, silenciar: true);

        Assert.IsType<RedirectToActionResult>(resposta);
        Assert.True(silencio.Ligado);

        // Fica registrado QUEM calou: a pergunta que se faz depois é "por que ninguém está
        // recebendo nada?".
        var linha = Assert.Single(ctx.ConfiguracoesDoSistema);
        Assert.Equal(raiz.Id, linha.AtualizadoPorJogadorId);
        Assert.Equal("true", linha.Valor);
    }

    // ── O QUE O SILÊNCIO FAZ NA ENTREGA ──────────────────────────────────────────────────

    [Fact]
    public async Task Mudo_nao_manda_email_nem_WhatsApp()
    {
        using var ctx = ContextoComJogador();
        var email = Substitute.For<IEmailService>();
        var filaDeWhats = new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance);

        await Servico(ctx, email, filaDeWhats, Calado()).EntregarAgoraAsync(
            new AvisoPendente(7, "As chaves saíram", "seu jogo é 14h", "/t/1", AlcanceDoAviso.AppEWhatsApp));

        await email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);
        Assert.Equal(0, filaDeWhats.Pendentes);
    }

    // O par de controle do teste acima: sem o silêncio, os dois canais saem. Sem ele, um
    // defeito que quebrasse a entrega inteira passaria como "o silêncio funciona".
    [Fact]
    public async Task Falando_normalmente_o_email_e_o_WhatsApp_saem()
    {
        using var ctx = ContextoComJogador();
        var email = Substitute.For<IEmailService>();
        var filaDeWhats = new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance);

        await Servico(ctx, email, filaDeWhats, new SilencioDeAvisos()).EntregarAgoraAsync(
            new AvisoPendente(7, "As chaves saíram", "seu jogo é 14h", "/t/1", AlcanceDoAviso.AppEWhatsApp));

        await email.Received(1).EnviarAsync("fulano@exemplo.com", "Fulano", "As chaves saíram", Arg.Any<string>());
        Assert.Equal(1, filaDeWhats.Pendentes);
    }

    // ⚠️ O QUE O SILÊNCIO **NÃO** CALA. Mudo é "não incomoda", não "apaga": a Caixa de Avisos
    // é o único canal que não depende de entrega nenhuma, e ela continua sendo gravada. Sem
    // isto, religar significaria "perdi tudo que aconteceu enquanto estava mudo".
    [Fact]
    public async Task Mudo_continua_GUARDANDO_o_aviso_na_caixa()
    {
        using var ctx = ContextoComJogador();

        await Servico(ctx, Substitute.For<IEmailService>(),
                new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance), Calado())
            .EntregarAgoraAsync(new AvisoPendente(7, "As chaves saíram", "seu jogo é 14h", "/t/1",
                AlcanceDoAviso.AppEWhatsApp));

        var guardado = Assert.Single(ctx.AvisosDoJogador);
        Assert.Equal("As chaves saíram", guardado.Titulo);
    }

    // O PUSH não tem substituto pra espiar: ele sai por HTTP direto do WebPushClient. O que
    // delata a TENTATIVA é o log — a inscrição abaixo tem chave de mentira, então qualquer
    // envio estoura e cai no `catch` que registra o aviso. Mudo, nada é tentado e o log fica
    // vazio. Os dois lados estão testados de propósito: sem o par de controle, uma mudança que
    // matasse o push inteiro passaria como "o silêncio funciona".
    [Fact]
    public async Task Mudo_nao_tenta_push_nenhum()
    {
        using var ctx = await ContextoComAparelho();
        var log = new LogQueConta();

        await Servico(ctx, Substitute.For<IEmailService>(),
                new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance), Calado(), log)
            .EntregarAgoraAsync(new AvisoPendente(7, "As chaves saíram", "seu jogo é 14h", "/t/1",
                AlcanceDoAviso.SoApp));

        Assert.Equal(0, log.Entradas);
    }

    [Fact]
    public async Task Falando_normalmente_o_push_e_tentado()
    {
        using var ctx = await ContextoComAparelho();
        var log = new LogQueConta();

        await Servico(ctx, Substitute.For<IEmailService>(),
                new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance), new SilencioDeAvisos(), log)
            .EntregarAgoraAsync(new AvisoPendente(7, "As chaves saíram", "seu jogo é 14h", "/t/1",
                AlcanceDoAviso.SoApp));

        Assert.True(log.Entradas > 0, "o push precisa ter sido TENTADO quando o sistema não está mudo");
    }

    // ⚠️ O PLACAR AO VIVO É SÓ PUSH e não entra na caixa (ver AvisoPendente.ApenasPush): mudo,
    // ele é descartado inteiro, e é o certo — placar de meia hora atrás não vale ser guardado.
    [Fact]
    public async Task Mudo_descarta_o_placar_ao_vivo_sem_sobra()
    {
        using var ctx = await ContextoComAparelho();
        var log = new LogQueConta();

        await Servico(ctx, Substitute.For<IEmailService>(),
                new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance), Calado(), log)
            .EntregarAgoraAsync(new AvisoPendente(7, "Placar ao vivo", "4 x 3", "/x", AlcanceDoAviso.SoApp,
                Tag: "partida-1", ApenasPush: true));

        Assert.Equal(0, log.Entradas);
        Assert.Empty(ctx.AvisosDoJogador);
    }

    // ── Apoio ────────────────────────────────────────────────────────────────────────────

    private static SilencioDeAvisos Calado()
    {
        using var ctx = TestInfra.NovoContexto();
        var silencio = new SilencioDeAvisos();
        silencio.DefinirAsync(ctx, silenciar: true, porJogadorId: 1).GetAwaiter().GetResult();
        return silencio;
    }

    private static DbPadelContext ContextoComJogador()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 7, Nome = "Fulano", Cpf = "99900000077", Login = "fulano",
            Email = "fulano@exemplo.com", NotificarEmail = true,
            Celular = "51977776666", NotificarWhatsApp = true,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<DbPadelContext> ContextoComAparelho()
    {
        var ctx = ContextoComJogador();
        ctx.Add(new PushSubscriptionJogador
        {
            JogadorId = 7,
            Endpoint = "https://push.exemplo.local/inscricao-7",
            P256dh = "chave-p256dh",
            Auth = "chave-auth",
        });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static PushNotificationService Servico(DbPadelContext ctx, IEmailService email,
        FilaDeWhatsApp filaDeWhats, SilencioDeAvisos silencio,
        ILogger<PushNotificationService>? log = null) =>
        new(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            Substitute.For<IWhatsAppService>(),
            filaDeWhats,
            new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance),
            email,
            Options.Create(new SiteSettings { Url = "https://padelizou.com.br" }),
            PorteiroDeTeste.Saida(),
            silencio,
            log ?? NullLogger<PushNotificationService>.Instance);

    private static AdminController Controlador(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }

    // Um log que só conta. Serve pra saber se o push chegou a ser TENTADO — é a única pista
    // que sobra de um canal que sai por HTTP direto, sem interface no meio pra substituir.
    private sealed class LogQueConta : ILogger<PushNotificationService>
    {
        public int Entradas { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entradas++;
    }
}
