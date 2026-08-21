using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// 21/08/2026 — AS DUAS EDIÇÕES DA TELA /Admin/Acesso: LOGIN E SENHA.
//
// A tela nasceu só-leitura de propósito, e a regra que a mantinha assim continua de pé pro
// E-MAIL: trocar o e-mail de uma conta é entregar a conta, calado. Login e senha são o oposto —
// o login não é segredo, e a senha nova é barulhenta (a antiga morre na hora, o dono descobre no
// primeiro login).
//
// O que estes testes seguram são as quatro portas que a edição não pode abrir: escalar poder
// pela conta de outro admin, ressuscitar uma conta excluída, fabricar conta de quem nunca
// aceitou os termos, e criar um login ambíguo que deixa DUAS pessoas sem entrar.
public class AcessoDoJogadorEditavelTests
{
    private const int IdDoRaiz = 1;
    private const int IdDoNomeado = 2;
    private const int IdDoAssistente = 3;
    private const int IdDoJogador = 10;

    private static readonly PasswordHasher<Jogador> Hasher = new();

    private static DbPadelContext Base()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = IdDoRaiz, Nome = "Felipe Bonamigo", Cpf = "99900000001",
                Email = "felipe@exemplo.com", SenhaHash = "hash", IsAdminRaiz = true },
            new Jogador { Id = IdDoNomeado, Nome = "Admin Nomeado", Cpf = "99900000002",
                Email = "nomeado@exemplo.com", SenhaHash = "hash", IsAdminGeral = true },
            new Jogador { Id = IdDoAssistente, Nome = "Foka Assistente", Cpf = "99900000003",
                Email = "foka@exemplo.com", SenhaHash = "hash", IsAssistente = true });
        ctx.SaveChanges();
        return ctx;
    }

    private static Jogador SemearJogador(DbPadelContext ctx, string? login = "corneteiros",
        string? email = "virgili@exemplo.com", string? senha = "senha-velha", DateTime? excluidoEm = null)
    {
        var jogador = new Jogador
        {
            Id = IdDoJogador, Nome = "Anderson Virgili", Apelido = "Virgili", Cpf = "01994784067",
            Login = login, Email = email, ExcluidoEm = excluidoEm,
        };
        if (senha != null) jogador.SenhaHash = Hasher.HashPassword(jogador, senha);

        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    private static AdminController Controlador(DbPadelContext ctx, int logadoId = IdDoRaiz,
        string verbo = "POST")
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
        http.Request.Method = verbo;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            http, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static Task<IActionResult> TrocarLogin(DbPadelContext ctx, string? login, int logadoId = IdDoRaiz) =>
        Controlador(ctx, logadoId).TrocarLoginDoJogador(IdDoJogador, login);

    private static Task<IActionResult> DefinirSenha(DbPadelContext ctx, string? senha,
        int logadoId = IdDoRaiz, int alvoId = IdDoJogador) =>
        Controlador(ctx, logadoId).DefinirSenhaDoJogador(alvoId, senha, Hasher);

    private static string? Erro(AdminController c) => c.TempData["Erro"] as string;

    private static bool Confere(Jogador jogador, string senha) =>
        Hasher.VerifyHashedPassword(jogador, jogador.SenhaHash!, senha) != PasswordVerificationResult.Failed;

    // ── O CASO QUE DEU ORIGEM À TELA: TROCAR O LOGIN ──────────────────────────────────────

    [Fact]
    public async Task O_login_troca_e_a_senha_continua_a_mesma()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        var resposta = await TrocarLogin(ctx, "AndersonVirgili");

        // Volta pra FICHA da pessoa, não pra busca: o termo digitado pode ter sido o login
        // antigo, que a troca acabou de apagar e que não acharia mais ninguém.
        var volta = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Acesso", volta.ActionName);
        Assert.Equal(IdDoJogador, volta.RouteValues!["jogadorId"]);

        Assert.Equal("AndersonVirgili", virgili.Login);
        Assert.True(Confere(virgili, "senha-velha"));
    }

    [Fact]
    public async Task O_login_novo_continua_entrando_e_o_antigo_para_de_entrar()
    {
        // A prova de que a troca vale onde importa: a consulta da ENTRADA (e-mail OU login).
        using var ctx = Base();
        SemearJogador(ctx);

        await TrocarLogin(ctx, "AndersonVirgili");

        Assert.NotNull(await BuscaJogador.PorIdentificadorAsync(ctx, "andersonvirgili"));
        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, "corneteiros"));
    }

    [Fact]
    public async Task Login_que_ja_e_de_outra_pessoa_e_recusado()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);
        ctx.Jogadores.Add(new Jogador { Id = 20, Nome = "Outro", Cpf = "01994784068", Login = "andersonvirgili" });
        ctx.SaveChanges();

        var controlador = Controlador(ctx);
        await controlador.TrocarLoginDoJogador(IdDoJogador, "AndersonVirgili");

        Assert.Equal("corneteiros", virgili.Login);
        Assert.Contains("Já existe outra conta", Erro(controlador));
    }

    [Fact]
    public async Task Login_igual_ao_EMAIL_de_outra_pessoa_tambem_e_recusado()
    {
        // ⚠️ Esta é a que o índice único do banco NÃO pega. A entrada aceita e-mail ou login na
        // MESMA consulta: um login igual ao e-mail alheio deixa as duas contas ambíguas, e a
        // vítima fica sem entrar (a senha confere contra a outra linha) e sem recuperar a senha.
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        var controlador = Controlador(ctx);
        await controlador.TrocarLoginDoJogador(IdDoJogador, "felipe@exemplo.com");

        Assert.Equal("corneteiros", virgili.Login);
        Assert.NotNull(Erro(controlador));
    }

    [Fact]
    public async Task O_proprio_login_de_volta_nao_e_lido_como_repetido()
    {
        // Salvar a tela sem mexer no campo (ou só trocando maiúsculas) não pode dar "já existe".
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        var controlador = Controlador(ctx);
        await controlador.TrocarLoginDoJogador(IdDoJogador, "Corneteiros");

        Assert.Equal("Corneteiros", virgili.Login);
        Assert.Null(Erro(controlador));
    }

    [Theory]
    [InlineData("abc")]                                  // curto demais: mínimo 4
    [InlineData("joaovictordossantosoliveirajunior")]    // 33 — a coluna é varchar(30)
    public async Task Login_fora_da_regua_do_cadastro_e_recusado(string login)
    {
        // A MESMA régua do cadastro, e não uma cópia parecida: o máximo não é gosto — o Postgres
        // RECUSA o que passa de 30, não corta, e o erro sairia como 500 na cara do admin.
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        await TrocarLogin(ctx, login);

        Assert.Equal("corneteiros", virgili.Login);
    }

    // ── DEFINIR A SENHA ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_senha_nova_vale_e_a_antiga_morre_na_hora()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        await DefinirSenha(ctx, "temporaria123");

        Assert.True(Confere(virgili, "temporaria123"));
        Assert.False(Confere(virgili, "senha-velha"));
    }

    [Fact]
    public async Task A_senha_definida_pelo_admin_tambem_vai_com_HASH()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        await DefinirSenha(ctx, "temporaria123");

        Assert.DoesNotContain("temporaria123", virgili.SenhaHash);
    }

    [Fact]
    public async Task Definir_senha_MATA_o_link_de_recuperacao_pendente()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);
        RecuperacaoSenha.Emitir(virgili, DateTime.Now);
        ctx.SaveChanges();

        await DefinirSenha(ctx, "temporaria123");

        Assert.Null(virgili.TokenRecuperacao);
    }

    [Fact]
    public async Task Senha_curta_usa_a_mesma_regua_do_resto_do_sistema()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        var controlador = Controlador(ctx);
        await controlador.DefinirSenhaDoJogador(IdDoJogador, "12345", Hasher);

        Assert.True(Confere(virgili, "senha-velha"));
        Assert.Equal(RecuperacaoSenha.ProblemaComASenha("12345"), Erro(controlador));
    }

    [Fact]
    public async Task Quem_tem_senha_e_nao_tem_email_finalmente_tem_saida()
    {
        // O "beco sem saída" que a tela descreve desde que nasceu. Até aqui a única saída era
        // gravar um e-mail na conta alheia — que é justamente o caminho silencioso que a tela
        // se recusa a abrir.
        using var ctx = Base();
        var virgili = SemearJogador(ctx, email: null);
        Assert.Equal(SituacaoDeAcesso.SemEmail, DiagnosticoDeAcesso.De(virgili));

        await DefinirSenha(ctx, "temporaria123");

        Assert.True(Confere(virgili, "temporaria123"));
    }

    // ── AS PORTAS QUE A EDIÇÃO NÃO PODE ABRIR ─────────────────────────────────────────────

    [Fact]
    public async Task Admin_NOMEADO_nao_define_a_senha_do_admin_RAIZ()
    {
        // ⚠️ A trava de escalada de poder, e ela não é hipotética: definir a senha de uma conta é
        // ENTRAR nela. Sem isto, o nomeado entraria como o raiz — que é quem vê o dinheiro, a
        // única coisa que o nomeado não pode ver (ver PoderesNoSistema.PodeVerDinheiro).
        using var ctx = Base();
        var raiz = ctx.Jogadores.Find(IdDoRaiz)!;

        var controlador = Controlador(ctx, logadoId: IdDoNomeado);
        await controlador.DefinirSenhaDoJogador(IdDoRaiz, "temporaria123", Hasher);

        Assert.Equal("hash", raiz.SenhaHash);
        Assert.NotNull(Erro(controlador));
    }

    [Fact]
    public async Task Admin_NOMEADO_tambem_nao_define_a_senha_do_assistente()
    {
        // Pela mesma porta: só-leitura vira escrita total assim que se entra pela conta do outro.
        using var ctx = Base();
        var assistente = ctx.Jogadores.Find(IdDoAssistente)!;

        await DefinirSenha(ctx, "temporaria123", logadoId: IdDoNomeado, alvoId: IdDoAssistente);

        Assert.Equal("hash", assistente.SenhaHash);
    }

    [Fact]
    public async Task O_RAIZ_define_a_senha_de_quem_administra()
    {
        // A trava é contra ESCALAR, não contra atender: quem já pode tudo não ganha nada.
        using var ctx = Base();
        var nomeado = ctx.Jogadores.Find(IdDoNomeado)!;

        await DefinirSenha(ctx, "temporaria123", logadoId: IdDoRaiz, alvoId: IdDoNomeado);

        Assert.True(Confere(nomeado, "temporaria123"));
    }

    [Fact]
    public async Task O_nomeado_define_a_senha_de_um_jogador_comum()
    {
        // A trava não pode ter fechado a tela pro atendimento normal, que é pra isso que ela existe.
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        await DefinirSenha(ctx, "temporaria123", logadoId: IdDoNomeado);

        Assert.True(Confere(virgili, "temporaria123"));
    }

    [Fact]
    public async Task Pre_cadastro_NAO_ganha_senha_por_aqui()
    {
        // ⚠️ A conta existe porque um ORGANIZADOR digitou o CPF dela numa inscrição: ela nunca
        // esteve no site e NUNCA ACEITOU OS TERMOS. Dar senha aqui fabricaria conta de gente que
        // não pediu conta nenhuma, e a LGPD põe em nós o ônus de provar o consentimento.
        using var ctx = Base();
        var virgili = SemearJogador(ctx, senha: null);
        Assert.Equal(SituacaoDeAcesso.PreCadastro, DiagnosticoDeAcesso.De(virgili));

        var controlador = Controlador(ctx);
        await controlador.DefinirSenhaDoJogador(IdDoJogador, "temporaria123", Hasher);

        Assert.Null(virgili.SenhaHash);
        Assert.Contains("mesmo CPF", Erro(controlador));
    }

    [Fact]
    public async Task Conta_EXCLUIDA_nao_recebe_senha_nem_login_de_volta()
    {
        // A exclusão apagou nome, CPF, e-mail, login e senha a pedido dela (LGPD). Escrever
        // qualquer um deles aqui é repor identificador em quem pediu pra deixar de ser
        // identificável — e devolver acesso a uma conta que não deveria mais abrir.
        using var ctx = Base();
        var saiu = SemearJogador(ctx, login: null, email: null, senha: null,
            excluidoEm: new DateTime(2026, 8, 1));

        await DefinirSenha(ctx, "temporaria123");
        await TrocarLogin(ctx, "voltei");

        Assert.Null(saiu.SenhaHash);
        Assert.Null(saiu.Login);
    }

    // ── QUEM PODE ABRIR OS FORMULÁRIOS ────────────────────────────────────────────────────

    [Fact]
    public async Task O_assistente_do_sistema_NAO_edita_nada()
    {
        // A trava de verdade é o VERBO (ObterJogadorAdminAsync só devolve o assistente em GET).
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        Assert.IsType<ForbidResult>(await TrocarLogin(ctx, "AndersonVirgili", logadoId: IdDoAssistente));
        Assert.IsType<ForbidResult>(
            await Controlador(ctx, IdDoAssistente).DefinirSenhaDoJogador(IdDoJogador, "temporaria123", Hasher));

        Assert.Equal("corneteiros", virgili.Login);
        Assert.True(Confere(virgili, "senha-velha"));
    }

    [Fact]
    public async Task E_a_tela_nem_mostra_os_formularios_pro_assistente()
    {
        // Botão que só serve pra dizer não é armadilha: o assistente vê a ficha e não vê a edição.
        using var ctx = Base();
        SemearJogador(ctx);

        var doAssistente = Assert.IsType<ViewResult>(
            await Controlador(ctx, IdDoAssistente, verbo: "GET").Acesso(null, IdDoJogador));
        var doAdmin = Assert.IsType<ViewResult>(
            await Controlador(ctx, IdDoRaiz, verbo: "GET").Acesso(null, IdDoJogador));

        Assert.Equal(false, doAssistente.ViewData["PodeEditar"]);
        Assert.Equal(true, doAdmin.ViewData["PodeEditar"]);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nenhum_nao_passa()
    {
        using var ctx = Base();
        var virgili = SemearJogador(ctx);

        Assert.IsType<ForbidResult>(await TrocarLogin(ctx, "AndersonVirgili", logadoId: IdDoJogador));
        Assert.Equal("corneteiros", virgili.Login);
    }

    // ── O QUE CONTINUA SEM FORMULÁRIO ─────────────────────────────────────────────────────

    [Fact]
    public void A_tela_NAO_grava_email()
    {
        // ⚠️ A decisão que sobreviveu à abertura da tela: quem controla o e-mail pede quantos
        // "esqueci minha senha" quiser, pra sempre, e o dono nunca fica sabendo. Este teste
        // falha no dia em que alguém acrescentar esse campo por conveniência.
        var codigo = File.ReadAllText(CaminhoDoControllerDeAcesso());

        Assert.DoesNotContain(".Email =", codigo);
    }

    private static string CaminhoDoControllerDeAcesso()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Controllers")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Padelizou", "Controllers", "AdminController.Acesso.cs");
    }
}
