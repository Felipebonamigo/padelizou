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
