using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // AuthController pronto pra testar cadastro e edição de perfil. Serve pros caminhos de
    // RECUSA: o final feliz das duas ações chama HttpContext.SignInAsync, que precisa da
    // pilha de autenticação de verdade e não vale montar aqui.
    public static AuthController NovoAuthController(DbPadelContext ctx, int usuarioLogadoId = 0, TravaDeEntrada? trava = null)
    {
        var controller = new AuthController(
            ctx,
            Substitute.For<IWebHostEnvironment>(),
            new Microsoft.AspNetCore.Identity.PasswordHasher<Jogador>(),
            new EstatisticasService(ctx),
            Substitute.For<IEmailService>(),
            NullLogger<AuthController>.Instance,
            Microsoft.Extensions.Options.Options.Create(new SuporteSettings()),
            trava ?? new TravaDeEntrada());

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
            Substitute.For<IEmailService>(),
            push,
            pagamentos ?? Substitute.For<IPagamentoInscricaoService>(),
            Microsoft.Extensions.Options.Options.Create(new TaxasExibicao()),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()),
            NullLogger<TorneiosController>.Instance,
            // Padelímetro de verdade (não dublê): finalizar partida nos testes deve mover
            // o nível igual à produção — é justamente o que os testes querem ver.
            new PadelimetroService(ctx),
            NovoEncerramento(ctx, push));

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
