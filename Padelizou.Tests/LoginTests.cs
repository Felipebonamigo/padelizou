using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Entrada aceita e-mail OU login, sem diferenciar maiúsculas.
// Antes só o e-mail servia, e exato — quem se cadastrou com login nunca entrava por ele.
public class LoginTests
{
    private static DbPadelContext ComJogador(string email, string? login)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Nome = "Felipe Bonamigo",
            Cpf = "11144477735",
            Email = email,
            Login = login,
            SenhaHash = "hash-qualquer",
        });
        ctx.SaveChanges();
        return ctx;
    }

    [Theory]
    [InlineData("Bona")]
    [InlineData("bona")]
    [InlineData("bOnA")]
    [InlineData("BONA")]
    [InlineData("  Bona  ")]   // espaço colado quando digita no celular
    public async Task Acha_pelo_login_em_qualquer_caixa(string digitado)
    {
        using var ctx = ComJogador("felipe@exemplo.com", "Bona");

        var achado = await BuscaJogador.PorIdentificadorAsync(ctx, digitado);

        Assert.NotNull(achado);
        Assert.Equal("Felipe Bonamigo", achado!.Nome);
    }

    [Theory]
    [InlineData("felipe@exemplo.com")]
    [InlineData("Felipe@Exemplo.com")]
    [InlineData("FELIPE@EXEMPLO.COM")]
    public async Task Acha_pelo_email_em_qualquer_caixa(string digitado)
    {
        using var ctx = ComJogador("felipe@exemplo.com", "Bona");

        Assert.NotNull(await BuscaJogador.PorIdentificadorAsync(ctx, digitado));
    }

    [Fact]
    public async Task Identificador_desconhecido_ou_vazio_nao_acha_ninguem()
    {
        using var ctx = ComJogador("felipe@exemplo.com", "Bona");

        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, "naoexiste"));
        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, ""));
        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, "   "));
        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, null));
    }

    [Fact]
    public async Task Nao_confunde_login_de_um_com_email_de_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Nome = "Dono do login", Cpf = "1", Login = "bona", Email = "a@x.com" },
            new Jogador { Nome = "Outro", Cpf = "2", Login = "outro", Email = "bona@x.com" });
        ctx.SaveChanges();

        // "bona" bate no login do primeiro; "bona@x.com" é o e-mail do segundo.
        Assert.Equal("Dono do login", (await BuscaJogador.PorIdentificadorAsync(ctx, "BONA"))!.Nome);
        Assert.Equal("Outro", (await BuscaJogador.PorIdentificadorAsync(ctx, "Bona@X.com"))!.Nome);
    }

    [Fact]
    public async Task Login_duplicado_so_por_caixa_nao_pode_existir()
    {
        // Documenta a razão da checagem de unicidade no cadastro ser ToLower: se
        // "Bona" e "bona" coexistissem, a entrada ficaria ambígua e o FirstOrDefault
        // escolheria um dos dois sem critério.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Nome = "Primeiro", Cpf = "1", Login = "Bona" },
            new Jogador { Nome = "Segundo", Cpf = "2", Login = "bona" });
        ctx.SaveChanges();

        var achados = ctx.Jogadores.Count(j => j.Login!.ToLower() == "bona");

        Assert.Equal(2, achados);   // <- é isto que o cadastro passa a impedir
    }
}
