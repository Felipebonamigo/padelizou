using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// E-mail, CPF e login identificam UMA pessoa cada um.
//
// Antes de 27/07/2026 a regra existia pela metade: o cadastro só comparava login contra
// login, e a edição de perfil gravava e-mail sem checar nada. Como a entrada casa e-mail
// OU login na MESMA consulta (BuscaJogador.PorIdentificadorAsync, com FirstOrDefault),
// duas contas podiam responder pelo mesmo identificador — e a vítima ficava sem entrar
// (a senha confere contra a outra linha) e sem recuperar a senha (o link ia pro e-mail da
// outra conta). Não é tomada de conta, é trancar alguém fora da conta dela.
public class IdentidadeJogadorTests
{
    private static int _proximoCpf = 1;

    private static DbPadelContext Com(params (string? email, string? login)[] contas)
    {
        var ctx = TestInfra.NovoContexto();
        foreach (var (email, login) in contas)
        {
            ctx.Jogadores.Add(new Jogador
            {
                Nome = "Fulano",
                Cpf = $"1114447773{_proximoCpf++ % 10}",
                Email = email,
                Login = login,
            });
        }
        ctx.SaveChanges();
        return ctx;
    }

    // ── E-mail ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("felipe@exemplo.com")]
    [InlineData("Felipe@Exemplo.com")]   // maiúscula não cria uma segunda pessoa
    [InlineData("FELIPE@EXEMPLO.COM")]
    [InlineData("  felipe@exemplo.com  ")]
    public async Task Email_ja_usado_e_recusado_em_qualquer_caixa(string tentativa)
    {
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"));

        Assert.True(await IdentidadeJogador.EmUsoAsync(ctx, tentativa));
    }

    [Fact]
    public async Task Email_livre_passa()
    {
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"));

        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, "outro@exemplo.com"));
    }

    // ── Login ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("bona")]
    [InlineData("Bona")]
    [InlineData("BONA")]
    public async Task Login_ja_usado_e_recusado_em_qualquer_caixa(string tentativa)
    {
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"));

        Assert.True(await IdentidadeJogador.EmUsoAsync(ctx, tentativa));
    }

    // ── O cruzamento: é aqui que morava o buraco ──────────────────────────────────────

    [Fact]
    public async Task Login_igual_ao_email_de_outra_pessoa_e_recusado()
    {
        // Sem isto, eu me cadastro com login "felipe@exemplo.com" e o Felipe para de
        // conseguir entrar: a consulta pode devolver a MINHA linha, e a senha dele não bate.
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"));

        Assert.True(await IdentidadeJogador.EmUsoAsync(ctx, "felipe@exemplo.com"));
    }

    [Fact]
    public async Task Email_igual_ao_login_de_outra_pessoa_e_recusado()
    {
        // O caminho inverso: login é texto livre, nada impede alguém de ter escolhido
        // "contato@clube.com" como login antes de o dono do e-mail se cadastrar.
        using var ctx = Com((email: null, login: "contato@clube.com"));

        Assert.True(await IdentidadeJogador.EmUsoAsync(ctx, "contato@clube.com"));
    }

    // ── "Eu mesmo" não me bloqueia ────────────────────────────────────────────────────

    [Fact]
    public async Task A_propria_conta_nao_bloqueia_a_si_mesma()
    {
        // Editar perfil sem trocar o e-mail, ou reivindicar um pré-cadastro em que o
        // organizador já gravou o e-mail: nos dois casos o dono é o próprio identificador.
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"));
        var eu = ctx.Jogadores.Single();

        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, "felipe@exemplo.com", exceto: eu.Id));
        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, "bona", exceto: eu.Id));
    }

    [Fact]
    public async Task Excecao_vale_so_pra_propria_conta()
    {
        using var ctx = Com((email: "felipe@exemplo.com", login: "bona"),
                            (email: "ana@exemplo.com", login: "ana"));
        var eu = ctx.Jogadores.Single(j => j.Login == "bona");

        // Mesmo me poupando, o e-mail da Ana continua sendo da Ana.
        Assert.True(await IdentidadeJogador.EmUsoAsync(ctx, "ana@exemplo.com", exceto: eu.Id));
    }

    // ── Pré-cadastro ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Jogador_sem_email_e_sem_login_nao_atrapalha_ninguem()
    {
        // Quem o organizador inscreveu digitando só nome e CPF nasce sem os dois campos.
        // Vários assim convivem — nulo não é identificador.
        using var ctx = Com((email: null, login: null), (email: null, login: null));

        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, "qualquer@exemplo.com"));
        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, null));
        Assert.False(await IdentidadeJogador.EmUsoAsync(ctx, "   "));
    }
}
