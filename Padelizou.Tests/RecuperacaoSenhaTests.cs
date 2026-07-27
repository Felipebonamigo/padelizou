using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Até 27/07/2026 não havia recuperação de senha, e quem esquecia se cadastrava de novo com o
// mesmo CPF — o cadastro sobrescrevia a senha. "Funcionava", e era uma tomada de conta: CPF
// não é segredo no Brasil.
public class RecuperacaoSenhaTests
{
    private static readonly DateTime Agora = new(2026, 7, 27, 15, 0, 0);

    private static Jogador ComToken(DateTime agora)
    {
        var jogador = new Jogador { Nome = "Felipe", Cpf = "11144477735" };
        RecuperacaoSenha.Emitir(jogador, agora);
        return jogador;
    }

    [Fact]
    public void Token_recem_emitido_vale()
    {
        var jogador = ComToken(Agora);

        Assert.True(RecuperacaoSenha.TokenValido(jogador, jogador.TokenRecuperacao, Agora));
    }

    [Fact]
    public void Token_vencido_nao_vale_mais()
    {
        var jogador = ComToken(Agora);

        var depoisDoPrazo = Agora.Add(RecuperacaoSenha.Validade).AddMinutes(1);

        Assert.False(RecuperacaoSenha.TokenValido(jogador, jogador.TokenRecuperacao, depoisDoPrazo));
    }

    [Fact]
    public void Token_e_de_uso_unico()
    {
        var jogador = ComToken(Agora);
        var token = jogador.TokenRecuperacao;

        RecuperacaoSenha.Consumir(jogador);

        Assert.False(RecuperacaoSenha.TokenValido(jogador, token, Agora));
        Assert.Null(jogador.TokenRecuperacao);
        Assert.Null(jogador.TokenRecuperacaoExpiraEm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("token-chutado")]
    public void Token_errado_ou_vazio_nunca_passa(string? chute)
    {
        var jogador = ComToken(Agora);

        Assert.False(RecuperacaoSenha.TokenValido(jogador, chute, Agora));
    }

    [Fact]
    public void Quem_nunca_pediu_recuperacao_nao_tem_token_valido()
    {
        var jogador = new Jogador { Nome = "Felipe", Cpf = "11144477735" };

        Assert.False(RecuperacaoSenha.TokenValido(jogador, "qualquer-coisa", Agora));
        // Nem com null, que é o valor guardado — senão bastaria não mandar token nenhum.
        Assert.False(RecuperacaoSenha.TokenValido(jogador, null, Agora));
    }

    [Fact]
    public void Cada_pedido_gera_um_token_diferente()
    {
        var tokens = Enumerable.Range(0, 50).Select(_ => RecuperacaoSenha.GerarToken()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.True(t.Length >= 40));
        // Base64 seguro pra URL: nada de +, / ou = pra o link não quebrar no e-mail.
        Assert.All(tokens, t => Assert.DoesNotContain('+', t));
        Assert.All(tokens, t => Assert.DoesNotContain('/', t));
        Assert.All(tokens, t => Assert.DoesNotContain('=', t));
    }

    [Fact]
    public void Pedir_de_novo_invalida_o_link_anterior()
    {
        var jogador = ComToken(Agora);
        var primeiro = jogador.TokenRecuperacao;

        RecuperacaoSenha.Emitir(jogador, Agora.AddMinutes(5));

        Assert.False(RecuperacaoSenha.TokenValido(jogador, primeiro, Agora.AddMinutes(5)));
        Assert.True(RecuperacaoSenha.TokenValido(jogador, jogador.TokenRecuperacao, Agora.AddMinutes(5)));
    }

    [Theory]
    [InlineData(null, "Digite a nova senha.")]
    [InlineData("", "Digite a nova senha.")]
    [InlineData("12345", "A senha precisa ter pelo menos 6 caracteres.")]
    public void Senha_fraca_e_recusada(string? senha, string esperado)
    {
        Assert.Equal(esperado, RecuperacaoSenha.ProblemaComASenha(senha));
    }

    [Fact]
    public void Senha_valida_passa()
    {
        Assert.Null(RecuperacaoSenha.ProblemaComASenha("123456"));
    }
}
