using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Middleware;
using Padelizou.Services;

namespace Padelizou.Tests;

// O gate de acesso antecipado guarda um hash de usuário+senha num cookie. Prod e dev
// passaram a ter senhas diferentes, e trocar a senha precisa expulsar quem já entrou.
public class AcessoAntecipadoTests
{
    private static AcessoAntecipadoSettings Gate(string usuario, string senha) =>
        new() { Habilitado = true, Usuario = usuario, Senha = senha };

    [Fact]
    public void Trocar_a_senha_invalida_o_cookie_de_quem_ja_tinha_entrado()
    {
        var antes = AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "senha-velha"));
        var depois = AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "senha-nova"));

        Assert.NotEqual(antes, depois);
    }

    [Fact]
    public void Cookie_de_um_ambiente_nao_abre_o_outro()
    {
        // Se prod e dev gerassem o mesmo hash, quem entrou num entraria no outro de graça.
        var prod = AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "senha-de-prod"));
        var dev = AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "senha-de-dev"));

        Assert.NotEqual(prod, dev);
    }

    [Fact]
    public void Mesma_configuracao_gera_sempre_o_mesmo_hash()
    {
        // Senão o cookie morreria a cada reinício do app e todo mundo teria que redigitar.
        Assert.Equal(
            AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "natapadel")),
            AcessoAntecipadoMiddleware.CalcularHash(Gate("padelizou", "natapadel")));
    }

    [Fact]
    public void Login_automatico_vem_desligado_quando_ninguem_configura()
    {
        // O padrão seguro é ninguém entrar logado como outra pessoa: quem quiser, se cadastra.
        var padrao = new AcessoAntecipadoSettings();

        Assert.True(string.IsNullOrWhiteSpace(padrao.LoginAutomaticoCpf));
    }

    // ── Quem já tem conta entra pelo próprio login (05/08/2026) ───────────────────────────
    //
    // O portão vinha antes de tudo, inclusive do login. Um jogador de verdade que trocasse de
    // celular, limpasse o navegador ou passasse dos 90 dias do cookie ficava trancado do lado
    // de fora do PRÓPRIO cadastro, e a única saída era pedir de novo uma senha compartilhada
    // que ele não tem motivo pra guardar.

    [Theory]
    [InlineData("/Auth/Login")]
    [InlineData("/auth/login")]          // a caixa da URL não pode decidir quem entra
    [InlineData("/Auth/EsqueciSenha")]   // quem esqueceu a senha da conta é justamente quem não tem a do portão
    [InlineData("/Auth/RedefinirSenha")]
    [InlineData("/Auth/Logout")]
    public void O_caminho_de_entrar_com_a_propria_conta_fica_aberto(string caminho)
    {
        Assert.True(AcessoAntecipadoMiddleware.EhCaminhoLiberado(caminho));
    }

    [Theory]
    [InlineData("/Auth/Cadastro")]   // conta NOVA segue por convite — é o que o portão existe pra controlar
    [InlineData("/Auth/Perfil")]
    [InlineData("/Auth/EditarPerfil")]
    [InlineData("/Torneios")]
    [InlineData("/Jogadores/Ranking")]
    [InlineData("/")]
    [InlineData("/Pagamentos")]      // só o webhook é liberado, não o extrato
    [InlineData("/Pagamentos/Meus")]
    public void O_resto_do_site_continua_atras_do_portao(string caminho)
    {
        Assert.False(AcessoAntecipadoMiddleware.EhCaminhoLiberado(caminho));
    }

    [Fact]
    public void Liberar_o_login_nao_pode_liberar_o_Auth_inteiro()
    {
        // O erro fácil aqui é escrever "/Auth" na lista e abrir o cadastro junto. Este teste
        // existe pra quebrar quando alguém fizer isso.
        Assert.DoesNotContain("/Auth", AcessoAntecipadoMiddleware.PrefixosLiberados);
        Assert.False(AcessoAntecipadoMiddleware.EhCaminhoLiberado("/Auth"));
    }

    [Fact]
    public void O_webhook_de_pagamento_continua_liberado_e_sozinho()
    {
        // Regressão do que já valia antes: liberar "/Pagamentos" inteiro abriria extrato e
        // configuração de recebimento pra qualquer visitante.
        Assert.True(AcessoAntecipadoMiddleware.EhCaminhoLiberado("/Pagamentos/Webhook"));
        Assert.False(AcessoAntecipadoMiddleware.EhCaminhoLiberado("/Pagamentos/Configurar"));
    }

    // ── O portão em ação: quem passa e quem é mandado pra tela de senha ───────────────────

    private static async Task<(bool passou, string? destino)> Bater(
        bool logado, bool comCookieDoPortao, string caminho = "/Torneios", bool portaoLigado = true)
    {
        var settings = Gate("Corneteiros", "corneta");
        settings.Habilitado = portaoLigado;

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = caminho;
        if (comCookieDoPortao)
        {
            ctx.Request.Headers.Cookie =
                $"{AcessoAntecipadoMiddleware.NomeCookie}={AcessoAntecipadoMiddleware.CalcularHash(settings)}";
        }

        var autenticacao = Substitute.For<IAuthenticationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "9") }, CookieAuthenticationDefaults.AuthenticationScheme));
        autenticacao.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string?>())
            .Returns(logado
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    principal, CookieAuthenticationDefaults.AuthenticationScheme))
                : AuthenticateResult.NoResult());

        var servicos = new ServiceCollection();
        servicos.AddSingleton(autenticacao);
        ctx.RequestServices = servicos.BuildServiceProvider();

        var passou = false;
        var middleware = new AcessoAntecipadoMiddleware(_ => { passou = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(ctx, Options.Create(settings), TestInfra.NovoContexto());

        return (passou, ctx.Response.Headers.Location.ToString() is { Length: > 0 } d ? d : null);
    }

    [Fact]
    public async Task Quem_esta_logado_entra_sem_a_senha_compartilhada()
    {
        // O pedido do Felipe: quem já tem conta em produção não vê mais a tela do portão.
        var (passou, destino) = await Bater(logado: true, comCookieDoPortao: false);

        Assert.True(passou);
        Assert.Null(destino);
    }

    [Fact]
    public async Task Quem_nao_tem_conta_nem_a_senha_continua_barrado()
    {
        var (passou, destino) = await Bater(logado: false, comCookieDoPortao: false);

        Assert.False(passou);
        Assert.StartsWith("/AcessoAntecipado/Entrar", destino);
    }

    [Fact]
    public async Task A_senha_compartilhada_continua_abrindo_pra_quem_ainda_nao_tem_conta()
    {
        // O convidado que vai se cadastrar precisa entrar de alguma forma.
        var (passou, _) = await Bater(logado: false, comCookieDoPortao: true);

        Assert.True(passou);
    }

    [Fact]
    public async Task Estar_logado_vale_em_qualquer_tela_do_site()
    {
        // A parte que faltava: sem isso a pessoa fazia login e o CLIQUE SEGUINTE a jogava de
        // volta pro portão, porque a checagem só olhava o cookie da senha compartilhada.
        foreach (var caminho in new[] { "/", "/Torneios/Details/18", "/Aulas/Solicitar", "/Pagamentos/Meus" })
        {
            var (passou, destino) = await Bater(logado: true, comCookieDoPortao: false, caminho);

            Assert.True(passou, $"deveria passar em {caminho}");
            Assert.Null(destino);
        }
    }

    [Fact]
    public async Task Com_o_portao_desligado_ninguem_e_barrado()
    {
        // É o caso de Development, e será o caso do lançamento público.
        var (passou, _) = await Bater(logado: false, comCookieDoPortao: false, portaoLigado: false);

        Assert.True(passou);
    }

    [Fact]
    public void Aviso_de_beta_vem_ligado_por_padrao()
    {
        // Enquanto o sistema não abrir de verdade, esquecer de ligar não pode ser uma opção.
        var padrao = new BetaSettings();

        Assert.True(padrao.Habilitado);
        Assert.False(string.IsNullOrWhiteSpace(padrao.Rotulo));
        Assert.False(string.IsNullOrWhiteSpace(padrao.Texto));
    }
}
