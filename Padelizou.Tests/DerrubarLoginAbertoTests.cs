using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 21/08/2026 — DERRUBAR UM LOGIN JÁ ABERTO.
//
// O buraco que isto fecha: o cookie deste site é AUTO-SUFICIENTE. Ele carrega quem a pessoa é,
// assinado, e o servidor não guarda lista de sessão nenhuma — não sabe quantos aparelhos estão
// logados numa conta e não teria como apagar um. Com o ExpireTimeSpan de 90 dias DESLIZANTE, um
// cookie copiado valia três meses renovando-se sozinho, MESMO depois de a pessoa trocar a senha.
// Trocar a senha fechava a porta da frente e não tirava de dentro quem já tinha entrado.
//
// O carimbo é o contrapeso: vai dentro do cookie e fica na conta, e os dois são comparados a cada
// request. Trocar o carimbo mata todo cookie emitido antes, sem lista de sessão nenhuma existir.
public class DerrubarLoginAbertoTests
{
    private const int IdDoRaiz = 1;
    private const int IdDoNomeado = 2;
    private const int IdDoJogador = 10;

    private static readonly PasswordHasher<Jogador> Hasher = new();

    private static Jogador Semear(DbPadelContext ctx, string? carimbo = null)
    {
        var jogador = new Jogador
        {
            Id = IdDoJogador, Nome = "Anderson Virgili", Cpf = "01994784067",
            Login = "andersonvirgili", Email = "virgili@exemplo.com", CarimboDeSessao = carimbo,
        };
        jogador.SenhaHash = Hasher.HashPassword(jogador, "senha-velha");
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    private static ClaimsPrincipal CookieCom(string? carimbo) =>
        new(new ClaimsIdentity(
            carimbo == null ? Array.Empty<Claim>() : new[] { new Claim(SessoesAbertas.TipoDaClaim, carimbo) },
            "Teste"));

    // ── A DECISÃO, PURA ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Conta_que_nunca_foi_derrubada_nao_confere_nada()
    {
        // ⚠️ É o que evita deslogar a BASE INTEIRA no deploy da coluna: os cookies que já estavam
        // na rua não têm a claim, e sem esta regra todos seriam recusados de uma vez só.
        Assert.True(SessoesAbertas.CookieAindaVale(null, CookieCom(null)));
        Assert.True(SessoesAbertas.CookieAindaVale("", CookieCom(null)));
    }

    [Fact]
    public void Depois_do_primeiro_carimbo_o_cookie_SEM_claim_e_recusado()
    {
        // A outra metade da regra acima: já derrubada uma vez, cookie sem claim é cookie antigo.
        Assert.False(SessoesAbertas.CookieAindaVale("carimbo-1", CookieCom(null)));
    }

    [Fact]
    public void Cookie_com_o_carimbo_da_conta_passa_e_com_o_anterior_nao()
    {
        Assert.True(SessoesAbertas.CookieAindaVale("carimbo-2", CookieCom("carimbo-2")));
        Assert.False(SessoesAbertas.CookieAindaVale("carimbo-2", CookieCom("carimbo-1")));
    }

    [Fact]
    public void Derrubar_sempre_troca_o_carimbo()
    {
        // Se um "derrubar" repetisse o valor anterior, ele não derrubaria nada — e nada na tela
        // denunciaria isso: o botão responderia "pronto" e a sessão continuaria de pé.
        var jogador = new Jogador { Nome = "Ana", Cpf = "1" };
        SessoesAbertas.Derrubar(jogador);
        var primeiro = jogador.CarimboDeSessao;
        SessoesAbertas.Derrubar(jogador);

        Assert.NotNull(primeiro);
        Assert.NotEqual(primeiro, jogador.CarimboDeSessao);
    }

    [Fact]
    public void O_carimbo_viaja_dentro_do_cookie()
    {
        // Sem esta claim a conferência a cada request não teria contra o que comparar — e, pior,
        // ela passaria sempre, porque o cookie recusado é o que NÃO casa.
        var jogador = new Jogador { Id = 5, Nome = "Ana", Cpf = "1", CarimboDeSessao = "carimbo-3" };

        var claims = IdentidadeJogador.ClaimsDe(jogador);

        Assert.Equal("carimbo-3", claims.Single(c => c.Type == SessoesAbertas.TipoDaClaim).Value);
    }

    // ── PELO JOGADOR: TROCAR A PRÓPRIA SENHA ──────────────────────────────────────────────

    [Fact]
    public async Task Trocar_a_propria_senha_derruba_os_outros_aparelhos()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, carimbo: "carimbo-antigo");

        await TestInfra.NovoAuthController(ctx, IdDoJogador)
            .AlterarSenha("senha-velha", "senha-nova", "senha-nova");

        Assert.NotEqual("carimbo-antigo", jogador.CarimboDeSessao);
    }

    [Fact]
    public async Task E_reemite_o_cookie_DESTA_aba_com_o_carimbo_novo()
    {
        // A derrubada não sabe distinguir aparelho. Sem a reemissão, a pessoa cairia na tela de
        // entrar no instante seguinte a trocar a senha — e ser deslogado bem ali parece que a
        // troca deu errado, que é justamente a hora de não deixar dúvida.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, carimbo: "carimbo-antigo");
        var controller = TestInfra.NovoAuthController(ctx, IdDoJogador);
        var autenticacao = controller.HttpContext.RequestServices
            .GetRequiredService<IAuthenticationService>();

        await controller.AlterarSenha("senha-velha", "senha-nova", "senha-nova");

        await autenticacao.Received().SignInAsync(
            Arg.Any<HttpContext>(),
            CookieAuthenticationDefaults.AuthenticationScheme,
            Arg.Is<ClaimsPrincipal>(p =>
                p.FindFirst(SessoesAbertas.TipoDaClaim)!.Value == jogador.CarimboDeSessao),
            Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Senha_atual_errada_NAO_derruba_ninguem()
    {
        // Senão o botão vira ferramenta de incômodo: quem tem só a sessão (celular na mesa) e não
        // a senha derrubaria os aparelhos do dono a cada tentativa.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, carimbo: "carimbo-antigo");

        await TestInfra.NovoAuthController(ctx, IdDoJogador)
            .AlterarSenha("chutei", "senha-nova", "senha-nova");

        Assert.Equal("carimbo-antigo", jogador.CarimboDeSessao);
    }

    // ── PELO "ESQUECI MINHA SENHA" ────────────────────────────────────────────────────────

    [Fact]
    public async Task Redefinir_pelo_link_do_email_derruba_tudo()
    {
        // É o caminho onde isso mais importa: quem chega aqui muitas vezes chega porque PERDEU a
        // conta, e a senha nova não valeria nada se o invasor continuasse logado.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, carimbo: "carimbo-antigo");
        RecuperacaoSenha.Emitir(jogador, DateTime.Now);
        ctx.SaveChanges();

        await TestInfra.NovoAuthController(ctx)
            .RedefinirSenha(jogador.TokenRecuperacao, "senha-nova");

        Assert.NotEqual("carimbo-antigo", jogador.CarimboDeSessao);
    }

    // ── PELO ADMIN ────────────────────────────────────────────────────────────────────────

    private static DbPadelContext BaseComAdmins()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = IdDoRaiz, Nome = "Felipe Bonamigo", Cpf = "99900000001",
                Email = "felipe@exemplo.com", SenhaHash = "hash", IsAdminRaiz = true },
            new Jogador { Id = IdDoNomeado, Nome = "Admin Nomeado", Cpf = "99900000002",
                Email = "nomeado@exemplo.com", SenhaHash = "hash", IsAdminGeral = true });
        ctx.SaveChanges();
        return ctx;
    }

    private static AdminController Admin(DbPadelContext ctx, int logadoId = IdDoRaiz)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, logadoId.ToString()) }, "Teste")),
        };
        http.Request.Method = HttpMethods.Post;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            http, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    [Fact]
    public async Task O_botao_de_derrubar_NAO_mexe_na_senha()
    {
        // É a razão de ele existir separado do de definir senha: às vezes a senha está boa e o
        // que sobrou foi uma sessão na rua (conta aberta no computador do clube, celular
        // emprestado). Obrigar a trocar a senha pra fechar uma sessão tiraria da pessoa o acesso
        // que ela ainda tinha, por um problema que não era a senha.
        using var ctx = BaseComAdmins();
        var virgili = Semear(ctx, carimbo: "carimbo-antigo");
        var hashAntes = virgili.SenhaHash;

        await Admin(ctx).DerrubarAcessoDoJogador(IdDoJogador);

        Assert.NotEqual("carimbo-antigo", virgili.CarimboDeSessao);
        Assert.Equal(hashAntes, virgili.SenhaHash);
    }

    [Fact]
    public async Task Definir_senha_pelo_admin_derruba_junto()
    {
        // As duas coisas andam juntas de propósito: o caso que traz alguém à tela de suporte é a
        // conta que OUTRA pessoa está usando agora.
        using var ctx = BaseComAdmins();
        var virgili = Semear(ctx, carimbo: "carimbo-antigo");

        await Admin(ctx).DefinirSenhaDoJogador(IdDoJogador, "temporaria123", Hasher);

        Assert.NotEqual("carimbo-antigo", virgili.CarimboDeSessao);
    }

    [Fact]
    public async Task Admin_NOMEADO_nao_derruba_o_acesso_do_RAIZ()
    {
        // Espelho da trava do definir-senha: derrubar não dá acesso a ninguém, mas TIRA — e tirar
        // o acesso de quem administra o sistema é decisão de quem manda no sistema.
        using var ctx = BaseComAdmins();
        var raiz = ctx.Jogadores.Find(IdDoRaiz)!;
        raiz.CarimboDeSessao = "carimbo-do-raiz";
        ctx.SaveChanges();

        await Admin(ctx, logadoId: IdDoNomeado).DerrubarAcessoDoJogador(IdDoRaiz);

        Assert.Equal("carimbo-do-raiz", raiz.CarimboDeSessao);
    }

    [Fact]
    public async Task E_o_assistente_do_sistema_nao_derruba_ninguem()
    {
        using var ctx = BaseComAdmins();
        var virgili = Semear(ctx, carimbo: "carimbo-antigo");
        ctx.Jogadores.Add(new Jogador { Id = 3, Nome = "Foka", Cpf = "99900000003",
            Email = "foka@exemplo.com", SenhaHash = "hash", IsAssistente = true });
        ctx.SaveChanges();

        Assert.IsType<ForbidResult>(await Admin(ctx, logadoId: 3).DerrubarAcessoDoJogador(IdDoJogador));
        Assert.Equal("carimbo-antigo", virgili.CarimboDeSessao);
    }
}
