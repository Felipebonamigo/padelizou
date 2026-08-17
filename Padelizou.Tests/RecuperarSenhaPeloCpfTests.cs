using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 17/08/2026 — "o usuário do CPF 019.947.840-67 não está conseguindo recuperar sua senha".
//
// A tela de recuperar senha tinha TRÊS jeitos de falhar em silêncio, e os três terminavam na
// mesma caixa verde "link a caminho":
//
//   1. o CPF não era identificador aceito (a busca olhava só e-mail e login), então quem
//      digitava o CPF não era achado — e a tela dizia que o link tinha saído;
//   2. conta de PRÉ-CADASTRO (inscrita num torneio por outra pessoa, nunca teve senha) não tem
//      o que recuperar: o que falta é CRIAR a conta. A tela mandava esperar o e-mail;
//   3. conta COM senha e SEM e-mail não tem pra onde mandar o link. A tela mandava procurar no
//      spam um e-mail que nunca teve destinatário.
//
// Estes testes fixam os três desfechos. O que eles protegem não é o código: é a pessoa que só
// chega nessa tela porque JÁ não consegue entrar — se ela sai de lá sem caminho, sai de vez.
public class RecuperarSenhaPeloCpfTests
{
    private const string CpfDoJogador = "01994784067";

    private static Jogador Semear(DbPadelContext ctx, string? email, string? login, string? senhaHash)
    {
        var jogador = new Jogador
        {
            Nome = "Lucas Andrade",
            Cpf = CpfDoJogador,
            Email = email,
            Login = login,
            SenhaHash = senhaHash,
        };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    // ── A busca ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("01994784067")]
    [InlineData("019.947.840-67")]   // colado do app do banco, com máscara
    [InlineData("  01994784067  ")]  // espaço que o teclado do celular gruda
    public async Task Acha_pelo_CPF_com_ou_sem_mascara(string digitado)
    {
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        var achado = await BuscaJogador.ParaRecuperarSenhaAsync(ctx, digitado);

        Assert.NotNull(achado);
        Assert.Equal("Lucas Andrade", achado!.Nome);
    }

    [Fact]
    public async Task E_continua_achando_por_email_e_por_login()
    {
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        Assert.NotNull(await BuscaJogador.ParaRecuperarSenhaAsync(ctx, "LUCAS@Exemplo.com"));
        Assert.NotNull(await BuscaJogador.ParaRecuperarSenhaAsync(ctx, "Lucas"));
    }

    [Fact]
    public async Task A_ENTRADA_continua_NAO_aceitando_CPF()
    {
        // A assimetria é o ponto, e é ela que este teste existe pra segurar: se um dia alguém
        // "unificar" as duas buscas por simetria, o CPF — que não é segredo no Brasil — vira
        // metade da credencial de todo mundo. Na recuperação ele é inofensivo (o link vai pro
        // e-mail JÁ gravado na conta); no login, não.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, CpfDoJogador));
    }

    [Theory]
    [InlineData("019947")]        // pedaço do CPF
    [InlineData("0199478406")]    // 10 dígitos: falta um
    [InlineData("")]
    [InlineData(null)]
    public async Task CPF_parcial_nao_acha_ninguem(string? digitado)
    {
        // Sem isto, "esqueci minha senha" viraria uma varredura de documentos da base, um
        // pedaço por vez — a mesma razão pela qual a busca de jogador exige o CPF inteiro.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        Assert.Null(await BuscaJogador.ParaRecuperarSenhaAsync(ctx, digitado));
    }

    [Fact]
    public async Task Login_que_por_acaso_e_numero_nao_e_atropelado_pela_busca_de_CPF()
    {
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", CpfDoJogador, "hash-qualquer");
        ctx.Jogadores.Add(new Jogador
        {
            Nome = "Outra pessoa",
            Cpf = CpfDoJogador.Replace("0199", "0299"),
            Email = "outra@exemplo.com",
            SenhaHash = "hash-qualquer",
        });
        ctx.SaveChanges();

        // O login é o que a pessoa ESCOLHEU, então ele ganha: quem digita aquilo está dizendo
        // quem é, não pedindo uma busca por documento.
        var achado = await BuscaJogador.ParaRecuperarSenhaAsync(ctx, CpfDoJogador);

        Assert.Equal("Lucas Andrade", achado!.Nome);
    }

    [Fact]
    public async Task Conta_EXCLUIDA_nao_e_alcancavel_pelo_CPF()
    {
        // Quem pediu pra sair (LGPD) não pode ser reencontrado por documento. Não é por sorte:
        // a anonimização troca o CPF por "EX000000123", que tem letra e por isso nem parece CPF.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");
        ExclusaoDeConta.Anonimizar(jogador, DateTime.Now);
        await ctx.SaveChangesAsync();

        Assert.Null(await BuscaJogador.ParaRecuperarSenhaAsync(ctx, CpfDoJogador));
    }

    // ── A tela, ponta a ponta ─────────────────────────────────────────────────────────────

    private static async Task<(ViewResult vista, IEmailService email)> PedirRecuperacao(
        DbPadelContext ctx, string digitado)
    {
        var email = Substitute.For<IEmailService>();
        var controller = TestInfra.NovoAuthController(ctx, email: email);

        var resultado = await controller.EsqueciSenha(digitado);

        return (Assert.IsType<ViewResult>(resultado), email);
    }

    private static Task NaoMandouEmail(IEmailService email) =>
        email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);

    [Fact]
    public async Task Conta_com_senha_pedindo_pelo_CPF_recebe_o_link_no_email_dela()
    {
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        var (vista, email) = await PedirRecuperacao(ctx, "019.947.840-67");

        Assert.Equal(true, vista.ViewData["Enviado"]);
        await email.Received(1).EnviarAsync("lucas@exemplo.com", "Lucas Andrade",
            Arg.Any<string>(), Arg.Any<string>());

        // E o token ficou gravado: sem isso o link do e-mail abre em "esse link expirou".
        var jogador = ctx.Jogadores.Single();
        Assert.False(string.IsNullOrWhiteSpace(jogador.TokenRecuperacao));
        Assert.NotNull(jogador.TokenRecuperacaoExpiraEm);
    }

    [Fact]
    public async Task Pre_cadastro_e_mandado_pro_CADASTRO_em_vez_de_esperar_email()
    {
        // A conta existe e tem histórico de torneio dentro, mas nunca teve senha. Antes desta
        // correção a pessoa recebia "link a caminho" e ia esperar pra sempre.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, email: null, login: null, senhaHash: null);

        var (vista, email) = await PedirRecuperacao(ctx, CpfDoJogador);

        Assert.Equal(true, vista.ViewData["PrecisaCriarConta"]);
        Assert.Null(vista.ViewData["Enviado"]);        // a caixa verde NÃO pode aparecer aqui
        await NaoMandouEmail(email);
    }

    [Fact]
    public async Task Pre_cadastro_com_email_que_o_organizador_digitou_TAMBEM_vai_pro_cadastro()
    {
        // Ter e-mail na linha não muda o fato: sem senha não há senha pra recuperar. E esse
        // e-mail pode nem ser dela — foi outra pessoa que digitou ao inscrever a dupla.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "organizador@exemplo.com", login: null, senhaHash: null);

        var (vista, email) = await PedirRecuperacao(ctx, CpfDoJogador);

        Assert.Equal(true, vista.ViewData["PrecisaCriarConta"]);
        await NaoMandouEmail(email);
    }

    [Fact]
    public async Task Conta_com_senha_e_SEM_email_avisa_em_vez_de_mentir()
    {
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, email: null, login: "lucas", senhaHash: "hash-qualquer");

        var (vista, email) = await PedirRecuperacao(ctx, CpfDoJogador);

        Assert.Equal(true, vista.ViewData["SemEmail"]);
        Assert.Null(vista.ViewData["Enviado"]);
        await NaoMandouEmail(email);
    }

    [Fact]
    public async Task CPF_desconhecido_continua_na_resposta_uniforme()
    {
        // Aqui a resposta uniforme SEGUE valendo: dizer "esse CPF não existe" entregaria de
        // graça quem está cadastrado no site.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");

        var (vista, email) = await PedirRecuperacao(ctx, "11144477735");

        Assert.Equal(true, vista.ViewData["Enviado"]);
        Assert.Null(vista.ViewData["PrecisaCriarConta"]);
        await NaoMandouEmail(email);
    }

    [Fact]
    public async Task Conta_excluida_nao_ganha_tela_especial_nenhuma()
    {
        // Conta excluída fica sem SenhaHash igual ao pré-cadastro. Se algum dia ela chegar até
        // aqui, NÃO pode receber "crie sua conta" — quem saiu não é gente esperando entrar.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, "lucas@exemplo.com", "lucas", "hash-qualquer");
        ExclusaoDeConta.Anonimizar(jogador, DateTime.Now);
        jogador.Cpf = CpfDoJogador;   // à força, pra exercitar o ramo mesmo sendo inalcançável
        await ctx.SaveChangesAsync();

        var (vista, email) = await PedirRecuperacao(ctx, CpfDoJogador);

        Assert.Equal(true, vista.ViewData["Enviado"]);
        Assert.Null(vista.ViewData["PrecisaCriarConta"]);
        Assert.Null(vista.ViewData["SemEmail"]);
        await NaoMandouEmail(email);
    }

    // ── A tela precisa CONTAR que aceita CPF ──────────────────────────────────────────────

    [Fact]
    public void O_campo_diz_que_aceita_CPF()
    {
        // Aceitar em silêncio não resolve nada: quem só sabe o próprio CPF é exatamente quem
        // não vai adivinhar que pode digitá-lo num campo escrito "e-mail ou login".
        var tela = Tela();

        Assert.Contains("E-mail, login ou CPF", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void E_o_pre_cadastro_encontra_o_caminho_do_cadastro_na_tela()
    {
        var tela = Tela();
        var bloco = tela.IndexOf("PrecisaCriarConta", StringComparison.Ordinal);

        Assert.True(bloco >= 0, "A tela perdeu o desfecho de conta sem senha — ela volta a mandar esperar um e-mail que não existe.");
        Assert.Contains("asp-action=\"Cadastro\"", tela[bloco..], StringComparison.Ordinal);
    }

    private static string Tela() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Auth", "EsqueciSenha.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Não achei a pasta do projeto subindo a partir de {AppContext.BaseDirectory}");
    }
}
