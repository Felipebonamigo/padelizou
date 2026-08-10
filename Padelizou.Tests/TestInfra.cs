using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using padelizou.Controllers;   // AuthController ficou no namespace legado, em minúsculo
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Infra compartilhada: banco EF InMemory novo por teste + montagem de cenário de torneio.
public static class TestInfra
{
    public static DbPadelContext NovoContexto()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseInMemoryDatabase("padelizou_teste_" + Guid.NewGuid())
            .Options;
        return new DbPadelContext(options);
    }

    // Autenticação dublada no HttpContext. Sem ela, `HttpContext.SignInAsync` estoura em
    // `ArgumentNullException: provider` — e é ele que fecha o caminho FELIZ do cadastro e da
    // edição de perfil, os dois lugares onde o dado realmente vai pro banco. Enquanto isto não
    // existia, só dava pra testar as RECUSAS: metade do que importa ficava sem teste.
    private static IServiceProvider ServicosDeAutenticacaoDublados()
    {
        var autenticacao = Substitute.For<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
        autenticacao.SignInAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(), Arg.Any<ClaimsPrincipal>(),
            Arg.Any<Microsoft.AspNetCore.Authentication.AuthenticationProperties?>())
            .Returns(Task.CompletedTask);
        autenticacao.SignOutAsync(Arg.Any<HttpContext>(), Arg.Any<string?>(),
            Arg.Any<Microsoft.AspNetCore.Authentication.AuthenticationProperties?>())
            .Returns(Task.CompletedTask);

        var servicos = new ServiceCollection();
        servicos.AddSingleton(autenticacao);
        return servicos.BuildServiceProvider();
    }

    // AuthController pronto pra testar cadastro e edição de perfil, do começo ao fim — o
    // SignInAsync do final feliz roda contra o dublê acima.
    public static AuthController NovoAuthController(DbPadelContext ctx, int usuarioLogadoId = 0,
        TravaDeEntrada? trava = null, IConsultaDeCep? cep = null)
    {
        var controller = new AuthController(
            ctx,
            Substitute.For<IWebHostEnvironment>(),
            new Microsoft.AspNetCore.Identity.PasswordHasher<Jogador>(),
            new EstatisticasService(ctx),
            Substitute.For<IEmailService>(),
            NullLogger<AuthController>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SuporteSettings()),
            trava ?? new TravaDeEntrada(),
            cep ?? CepQueNaoResponde());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
                RequestServices = ServicosDeAutenticacaoDublados(),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        // Sem isto, `Url.Action` no meio da ação vai atrás do IUrlHelperFactory no provedor —
        // que não existe aqui. Mesmo motivo do JogadoresController logo abaixo.
        controller.Url = UrlDeTeste();
        return controller;
    }

    // Consulta de CEP que se comporta como o ViaCEP fora do ar. É o dublê PADRÃO de propósito:
    // teste nenhum deste projeto deve sair batendo em serviço da internet, e "não consultado"
    // é o desfecho que não muda nada no que estava sendo testado.
    //
    // ⚠️ Sem isto o `Substitute.For<IConsultaDeCep>()` cru devolveria null no lugar do
    // resultado, e toda gravação de perfil estouraria em NullReference — a falha apareceria
    // longe daqui, em testes que não têm nada a ver com CEP.
    public static IConsultaDeCep CepQueNaoResponde()
    {
        var cep = Substitute.For<IConsultaDeCep>();
        cep.BuscarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsultaDeCepResultado.NaoConsultado));
        return cep;
    }

    // Consulta de CEP que devolve sempre o mesmo endereço, pra testar o caminho feliz.
    public static IConsultaDeCep CepQueResponde(EnderecoDoCep endereco)
    {
        var cep = Substitute.For<IConsultaDeCep>();
        cep.BuscarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsultaDeCepResultado.Achou(endereco)));
        return cep;
    }

    // Consulta de CEP que diz "esse CEP não existe".
    public static IConsultaDeCep CepInexistente()
    {
        var cep = Substitute.For<IConsultaDeCep>();
        cep.BuscarAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsultaDeCepResultado.NaoExiste));
        return cep;
    }

    // JogadoresController: perfil, elogio, comentário, seguir e a rede.
    //
    // Mesma razão do PartidasController abaixo — quatro arquivos de teste montavam este
    // construtor à mão, e injetar o push (pros avisos de elogio/comentário/seguidor) quebrou
    // os quatro de uma vez. Agora o próximo parâmetro se resolve aqui.
    public static JogadoresController NovoJogadoresController(
        DbPadelContext ctx, int? usuarioLogadoId = null, IPushNotificationService? push = null)
    {
        // O ranking do parceiro saiu daqui em 08/08/2026 (o perfil não mostra mais a posição
        // da pessoa lá), então esta tela não depende mais daquele serviço.
        var controller = new JogadoresController(
            ctx,
            new EstatisticasService(ctx),
            push ?? Substitute.For<IPushNotificationService>());

        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = UrlDeTeste();
        return controller;
    }

    // PartidasController com os serviços de borda dublados. Fica aqui, e não repetido em
    // cada arquivo de teste, porque toda vez que o construtor ganha uma dependência os
    // testes quebram em vários lugares ao mesmo tempo — foi o que aconteceu ao injetar push.
    // O encerramento de partida (Padelímetro, robôs de chaveamento, avisos) é o MESMO objeto
    // pras duas telas — de verdade, não dublê: os testes que rodam o torneio inteiro só provam
    // alguma coisa se o que roda aqui for o que roda em produção.
    public static EncerramentoDaPartida NovoEncerramento(
        DbPadelContext ctx, IPushNotificationService? push = null) =>
        new(ctx, new PadelimetroService(ctx), push ?? Substitute.For<IPushNotificationService>(),
            NullLogger<EncerramentoDaPartida>.Instance);

    public static PartidasController NovoPartidasController(DbPadelContext ctx, int? usuarioLogadoId,
        IPushNotificationService? push = null)
    {
        push ??= Substitute.For<IPushNotificationService>();

        var controller = new PartidasController(
            ctx,
            Substitute.For<IPalpiteService>(),
            push,
            NullLogger<PartidasController>.Instance,
            NovoEncerramento(ctx, push));

        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = UrlDeTeste();
        return controller;
    }

    // Sem isto, `Url.Action(...)` estoura NullReferenceException dentro do controller — e
    // como as chamadas de push ficam em try/catch (push é acessório, não pode derrubar o
    // placar), o teste passaria verde sem nunca ter executado o trecho que interessa.
    public static Microsoft.AspNetCore.Mvc.IUrlHelper UrlDeTeste()
    {
        var url = Substitute.For<Microsoft.AspNetCore.Mvc.IUrlHelper>();
        url.Action(Arg.Any<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>()).Returns("/rota/de/teste");
        return url;
    }

    // AulasController com as bordas dubladas (e-mail, Google Agenda, push). O `google` é
    // devolvido junto quando quem chama precisa conferir que o evento foi removido da agenda.
    public static AulasController NovoAulasController(
        DbPadelContext ctx, int usuarioLogadoId, IGoogleCalendarService? google = null, IPushNotificationService? push = null)
    {
        var controller = new AulasController(
            ctx,
            Substitute.For<IEmailService>(),
            google ?? Substitute.For<IGoogleCalendarService>(),
            push ?? Substitute.For<IPushNotificationService>(),
            Microsoft.Extensions.Options.Options.Create(new PlanoProfessorSettings()),
            NullLogger<AulasController>.Instance);

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
        controller.Url = UrlDeTeste();
        return controller;
    }

    // O plano do professor: o serviço de pagamento entra de fora porque é ele que decide se a
    // cobrança nasce por Pix direto ou por fatura de gateway.
    public static PlanoProfessorController NovoPlanoProfessorController(
        DbPadelContext ctx, int usuarioLogadoId,
        IPagamentoInscricaoService? pagamentos = null, PlanoProfessorSettings? plano = null,
        AsaasSettings? asaas = null)
    {
        var controller = new PlanoProfessorController(
            ctx,
            pagamentos ?? Substitute.For<IPagamentoInscricaoService>(),
            Microsoft.Extensions.Options.Options.Create(plano ?? new PlanoProfessorSettings()),
            Microsoft.Extensions.Options.Options.Create(asaas ?? new AsaasSettings()));

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
        controller.Url = UrlDeTeste();
        return controller;
    }

    public static TimesController NovoTimesController(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new TimesController(ctx, new EstatisticasService(ctx));

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

    // TorneiosController pronto pra uso nos testes: serviços de borda (e-mail, push,
    // pagamentos...) viram dublês; estatísticas usa o banco de verdade (em memória).
    // `pagamentos` fica aberto porque a tela de criação agora PERGUNTA a ele se a conta de
    // recebimento está conectada — quem testa essa recusa precisa dizer a resposta.
    public static TorneiosController NovoTorneiosController(DbPadelContext ctx, int usuarioLogadoId,
        IPagamentoInscricaoService? pagamentos = null, IPushNotificationService? push = null)
    {
        push ??= Substitute.For<IPushNotificationService>();

        var controller = new TorneiosController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPalpiteService>(),
            Substitute.For<IWebHostEnvironment>(),
            push,
            pagamentos ?? Substitute.For<IPagamentoInscricaoService>(),
            Microsoft.Extensions.Options.Options.Create(new TaxasExibicao()),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()),
            NullLogger<TorneiosController>.Instance,
            // Padelímetro de verdade (não dublê): finalizar partida nos testes deve mover
            // o nível igual à produção — é justamente o que os testes querem ver.
            new PadelimetroService(ctx),
            NovoEncerramento(ctx, push),
            new AvisoDeInscricaoNoTorneio(ctx, push));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        // Sem TempData, qualquer ação que escreve uma mensagem pro usuário estoura com
        // NullReferenceException dentro do teste — e o erro não aponta pra causa nenhuma.
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = UrlDeTeste();
        return controller;
    }

    public static Jogador NovoJogador(int i) => new()
    {
        Nome = $"Jogador {i:00}",
        Cpf = $"999000000{i:00}",
    };

    // Monta um torneio pronto pro sorteio: organizador, 1 categoria e N duplas inscritas.
    public static (Torneio torneio, Categoria categoria, Jogador organizador) MontarTorneio(
        DbPadelContext ctx, int qtdDuplas, string status = "Chaves em Sorteio")
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio
        {
            Nome = "Torneio de Teste",
            Codigo = "TST123",
            Status = status,
            DataInicio = new DateTime(2026, 7, 1, 9, 0, 0),
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "CAT2M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        for (int i = 0; i < qtdDuplas; i++)
        {
            var j1 = NovoJogador(i * 2 + 1);
            var j2 = NovoJogador(i * 2 + 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 });
        }
        ctx.SaveChanges();

        return (torneio, categoria, organizador);
    }

    // Dá um placar à partida (games) e finaliza pelo fluxo real do controller.
    public static async Task FinalizarComPlacarAsync(
        DbPadelContext ctx, TorneiosController controller, Partida partida, int games1, int games2)
    {
        partida.GamesDupla1 = games1;
        partida.GamesDupla2 = games2;
        // O FinalizarPartida decide o vencedor por sets e desempata por games.
        partida.SetsDupla1 = games1 > games2 ? 1 : 0;
        partida.SetsDupla2 = games2 > games1 ? 1 : 0;
        await ctx.SaveChangesAsync();
        await controller.FinalizarPartida(partida.Id);
    }
}
