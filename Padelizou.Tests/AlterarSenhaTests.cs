using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 21/08/2026 — A TELA DE TROCAR A SENHA ESTANDO DENTRO.
//
// Até aqui o sistema só sabia REDEFINIR: o caminho da senha nova passava por dizer que a
// esqueceu, esperar um e-mail e abrir um link de uma hora. Quem NÃO esqueceu — trocou por
// precaução, emprestou o celular, desconfia que alguém viu — tinha que mentir pro próprio site
// pra conseguir. E quem tem senha e não tem e-mail não conseguia de jeito nenhum, porque aquele
// caminho inteiro depende de um destinatário.
//
// O que estes testes protegem é a trava que faz a tela ser segura: a SENHA ATUAL. Sem ela, um
// celular destravado esquecido na mesa do clube troca a senha de alguém em dois toques — e quem
// trocou passa a ser o dono da conta, porque o dono de verdade perde justamente o que usava
// pra entrar.
public class AlterarSenhaTests
{
    private const int IdDoJogador = 7;
    private const string SenhaAntiga = "senha-velha";

    private static readonly PasswordHasher<Jogador> Hasher = new();

    private static Jogador Semear(DbPadelContext ctx, string? senha = SenhaAntiga, string? email = "ana@exemplo.com")
    {
        var jogador = new Jogador
        {
            Id = IdDoJogador, Nome = "Ana Ribeiro", Cpf = "01994784067",
            Email = email, Login = "ana",
        };
        if (senha != null) jogador.SenhaHash = Hasher.HashPassword(jogador, senha);

        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    private static Task<IActionResult> Trocar(DbPadelContext ctx,
        string? senhaAtual, string? senha, string? confirmacao) =>
        TestInfra.NovoAuthController(ctx, IdDoJogador).AlterarSenha(senhaAtual, senha, confirmacao);

    private static bool Confere(Jogador jogador, string senha) =>
        Hasher.VerifyHashedPassword(jogador, jogador.SenhaHash!, senha) != PasswordVerificationResult.Failed;

    // ── A REGRA PURA ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_senha_nova_usa_o_MESMO_minimo_do_esqueci_minha_senha()
    {
        // Duas réguas de senha no mesmo sistema é a que aceita 4 caracteres virando a porta por
        // onde se entra na conta trocada na outra tela.
        Assert.Equal(RecuperacaoSenha.ProblemaComASenha("12345"), TrocaDeSenha.Problema("12345", "12345"));
        Assert.Null(TrocaDeSenha.Problema("123456", "123456"));
    }

    [Fact]
    public void Senha_curta_reclama_do_TAMANHO_e_nao_da_confirmacao()
    {
        // A ordem importa: mandar "as duas não são iguais" pra quem digitou a mesma coisa nos
        // dois campos manda a pessoa consertar o que não está errado.
        var problema = TrocaDeSenha.Problema("123", "123");
        Assert.Contains("6 caracteres", problema);
    }

    [Fact]
    public void As_duas_digitadas_precisam_ser_iguais()
    {
        Assert.NotNull(TrocaDeSenha.Problema("senha-nova", "senha-nova "));
        Assert.NotNull(TrocaDeSenha.Problema("senha-nova", "Senha-Nova"));
        Assert.NotNull(TrocaDeSenha.Problema("senha-nova", null));
    }

    // ── A TRAVA: A SENHA ATUAL ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Senha_atual_errada_NAO_troca_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx);

        var resposta = await Trocar(ctx, "chutei", "senha-nova", "senha-nova");

        // Continua na tela, com o erro — e o banco não se mexeu.
        var view = Assert.IsType<ViewResult>(resposta);
        Assert.Equal("A senha atual está incorreta.", view.ViewData["Erro"]);
        Assert.True(Confere(jogador, SenhaAntiga));
    }

    [Fact]
    public async Task Senha_atual_em_branco_tambem_NAO_troca()
    {
        // O campo vazio não pode ser lido como "não tem senha": é exatamente o que um celular
        // destravado na mesa do clube manda, e seria a troca em dois toques.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx);

        var resposta = await Trocar(ctx, null, "senha-nova", "senha-nova");

        Assert.IsType<ViewResult>(resposta);
        Assert.True(Confere(jogador, SenhaAntiga));
    }

    [Fact]
    public async Task Com_a_senha_atual_certa_a_troca_acontece()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx);

        var resposta = await Trocar(ctx, SenhaAntiga, "senha-nova", "senha-nova");

        Assert.Equal("Perfil", Assert.IsType<RedirectToActionResult>(resposta).ActionName);
        Assert.True(Confere(jogador, "senha-nova"));
        Assert.False(Confere(jogador, SenhaAntiga));
    }

    [Fact]
    public async Task A_senha_vai_pro_banco_com_HASH_e_nunca_em_texto()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx);

        await Trocar(ctx, SenhaAntiga, "senha-nova", "senha-nova");

        Assert.DoesNotContain("senha-nova", jogador.SenhaHash);
    }

    // ── O LINK DE RECUPERAÇÃO PENDENTE ────────────────────────────────────────────────────

    [Fact]
    public async Task Trocar_a_senha_MATA_o_link_de_esqueci_minha_senha_que_estava_de_pe()
    {
        // Quem troca a senha muitas vezes está reagindo a uma desconfiança. Deixar vivo um link
        // pedido minutos antes seria manter aberta a porta que ela veio fechar.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx);
        RecuperacaoSenha.Emitir(jogador, DateTime.Now);
        ctx.SaveChanges();
        Assert.NotNull(jogador.TokenRecuperacao);

        await Trocar(ctx, SenhaAntiga, "senha-nova", "senha-nova");

        Assert.Null(jogador.TokenRecuperacao);
        Assert.Null(jogador.TokenRecuperacaoExpiraEm);
    }

    // ── QUEM NÃO TEM SENHA NENHUMA ────────────────────────────────────────────────────────

    [Fact]
    public async Task Conta_sem_senha_DEFINE_a_primeira_sem_pedir_a_atual()
    {
        // Chega logada pelo modo demonstração e pelo pré-cadastro reivindicado sem senha. Pedir
        // a "senha atual" de quem nunca teve uma é um campo impossível de preencher.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, senha: null);

        var resposta = await Trocar(ctx, null, "senha-nova", "senha-nova");

        Assert.IsType<RedirectToActionResult>(resposta);
        Assert.True(Confere(jogador, "senha-nova"));
    }

    [Fact]
    public async Task A_tela_de_conta_sem_senha_avisa_que_esta_CRIANDO_uma()
    {
        // Sem esta bandeira a view pediria a senha atual de quem não tem — e o campo `required`
        // trancaria a pessoa do lado de fora do próprio formulário.
        using var ctx = TestInfra.NovoContexto();
        Semear(ctx, senha: null);

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoAuthController(ctx, IdDoJogador).AlterarSenha());

        Assert.Equal(false, view.ViewData["TemSenha"]);
    }

    // ── QUEM NÃO TEM E-MAIL ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_tem_senha_e_NAO_tem_email_troca_por_aqui()
    {
        // É o "beco sem saída" de DiagnosticoDeAcesso visto do outro lado: sozinha, essa pessoa
        // não trocava a senha por nada — "esqueci minha senha" não tem destinatário. Estando
        // dentro, ela troca, e nenhum e-mail precisa sair.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Semear(ctx, email: null);

        await Trocar(ctx, SenhaAntiga, "senha-nova", "senha-nova");

        Assert.True(Confere(jogador, "senha-nova"));
    }
}
