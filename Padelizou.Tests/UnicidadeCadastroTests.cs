using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// A regra ("e-mail, CPF e login identificam uma pessoa cada um") vive em IdentidadeJogador
// e tem teste próprio. Aqui é o outro lado: as duas TELAS que gravam identificador
// realmente aplicam a regra?
//
// Vale testar as duas separado porque o buraco era diferente em cada uma — o cadastro
// checava só login-contra-login, e a edição de perfil não checava absolutamente nada.
public class UnicidadeCadastroTests
{
    private static Jogador Existente(DbPadelContext ctx, string cpf, string? email, string? login, bool comSenha = true)
    {
        var jogador = new Jogador
        {
            Nome = "Felipe Bonamigo",
            Cpf = cpf,
            Email = email,
            Login = login,
            SenhaHash = comSenha ? "hash-qualquer" : null,
        };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    private static Task<IActionResult> ChamarCadastro(
        padelizou.Controllers.AuthController controller, string cpf, string login, string email) =>
        controller.Cadastro(
            nome: "Novo Jogador", cpf: cpf, login: login, email: email, senha: "segredo123",
            celular: null, isProfessor: false, foto: null!,
            ladoQuadra: null, lateralidade: null, instagram: null,
            notificarEmail: false, notificarWhatsApp: false,
            categoriasSelecionadas: null, clubesSelecionados: null, diasHorariosSelecionados: null);

    private static async Task<string?> ErroDoCadastro(
        DbPadelContext ctx, string cpf, string login, string email)
    {
        var controller = TestInfra.NovoAuthController(ctx);

        var resultado = await ChamarCadastro(controller, cpf, login, email);

        Assert.IsType<ViewResult>(resultado);          // recusado = volta pro formulário
        return controller.ViewBag.Erro as string;
    }

    // ── Cadastro ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("felipe@exemplo.com")]
    [InlineData("Felipe@Exemplo.com")]   // trocar a caixa não cria uma pessoa nova
    public async Task Cadastro_recusa_email_ja_usado(string tentativa)
    {
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", "felipe@exemplo.com", "bona");

        var erro = await ErroDoCadastro(ctx, cpf: "52998224725", login: "novato", email: tentativa);

        Assert.NotNull(erro);
        Assert.Contains("e-mail", erro!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ctx.Jogadores.CountAsync());
    }

    [Fact]
    public async Task Cadastro_recusa_login_igual_ao_email_de_outra_pessoa()
    {
        // Este era o pior caso: nada barrava, e o Felipe parava de conseguir entrar com o
        // e-mail dele — a consulta da entrada casa e-mail OU login e pode devolver a linha
        // errada, cuja senha não é a dele.
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", "felipe@exemplo.com", "bona");

        var erro = await ErroDoCadastro(ctx, cpf: "52998224725",
            login: "felipe@exemplo.com", email: "outro@exemplo.com");

        Assert.NotNull(erro);
        Assert.Equal(1, await ctx.Jogadores.CountAsync());
    }

    [Fact]
    public async Task Cadastro_recusa_email_igual_ao_login_de_outra_pessoa()
    {
        // Caminho inverso: login é texto livre, alguém pode ter escolhido um e-mail como login.
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", email: null, login: "contato@clube.com");

        var erro = await ErroDoCadastro(ctx, cpf: "52998224725",
            login: "novato", email: "contato@clube.com");

        Assert.NotNull(erro);
        Assert.Equal(1, await ctx.Jogadores.CountAsync());
    }

    [Fact]
    public async Task Cadastro_recusa_login_ja_usado()
    {
        // A checagem que já existia — fica aqui pra não regredir na reorganização.
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", "felipe@exemplo.com", "bona");

        var erro = await ErroDoCadastro(ctx, cpf: "52998224725", login: "BONA", email: "novo@exemplo.com");

        Assert.NotNull(erro);
        Assert.Contains("login", erro!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ctx.Jogadores.CountAsync());
    }

    [Fact]
    public async Task Quem_reivindica_o_proprio_pre_cadastro_nao_e_barrado_pelo_proprio_email()
    {
        // O organizador inscreveu o Felipe digitando nome, CPF e e-mail; o Felipe nunca teve
        // senha. Quando ele vem criar a conta, o e-mail que já está lá é DELE — barrar aqui
        // deixaria a pessoa sem conseguir se cadastrar de jeito nenhum.
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", "felipe@exemplo.com", login: null, comSenha: false);

        var controller = TestInfra.NovoAuthController(ctx);

        // A ação vai até o fim e só então esbarra no SignInAsync, que precisa da pilha de
        // autenticação de verdade. Chegar lá É a prova de que a unicidade não barrou: uma
        // recusa teria voltado um ViewResult sem encostar no banco.
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ChamarCadastro(controller, cpf: "11144477735", login: "bona", email: "felipe@exemplo.com"));

        var salvo = await ctx.Jogadores.SingleAsync();
        Assert.NotNull(salvo.SenhaHash);          // reivindicou a conta: agora tem senha
        Assert.Equal("bona", salvo.Login);
        Assert.Equal("felipe@exemplo.com", salvo.Email);
    }

    [Fact]
    public async Task Cadastro_com_cpf_que_ja_tem_senha_continua_recusado()
    {
        // Regressão da tomada de conta por CPF, fechada em 27/07: o cadastro sobrescrevia a
        // senha de quem já usava o site. A reordenação desta ação não pode ter afrouxado isso.
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", "felipe@exemplo.com", "bona");

        var erro = await ErroDoCadastro(ctx, cpf: "11144477735", login: "novato", email: "novo@exemplo.com");

        Assert.NotNull(erro);
        Assert.Contains("CPF", erro!);
        Assert.Equal(1, await ctx.Jogadores.CountAsync());
    }

    // ── Edição de perfil ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editar_perfil_recusa_o_email_de_outra_conta()
    {
        // Aqui não havia checagem NENHUMA: dava pra pôr o e-mail de outra pessoa na sua
        // conta e trancá-la fora da dela.
        using var ctx = TestInfra.NovoContexto();
        var felipe = Existente(ctx, "11144477735", "felipe@exemplo.com", "bona");
        var ana = Existente(ctx, "52998224725", "ana@exemplo.com", "ana");

        var controller = TestInfra.NovoAuthController(ctx, ana.Id);
        var resultado = await controller.EditarPerfil(
            nome: "Ana", email: "felipe@exemplo.com", celular: null,
            cidade: null, estado: null, isProfessor: false, foto: null);

        Assert.IsType<ViewResult>(resultado);
        Assert.NotNull(controller.ViewBag.Erro as string);

        // E o e-mail do Felipe continua sendo do Felipe.
        Assert.Equal("felipe@exemplo.com", (await ctx.Jogadores.FindAsync(felipe.Id))!.Email);
        Assert.Equal("ana@exemplo.com", (await ctx.Jogadores.FindAsync(ana.Id))!.Email);
    }

    [Fact]
    public async Task Editar_perfil_recusa_email_igual_ao_login_de_outra_conta()
    {
        using var ctx = TestInfra.NovoContexto();
        Existente(ctx, "11144477735", email: null, login: "contato@clube.com");
        var ana = Existente(ctx, "52998224725", "ana@exemplo.com", "ana");

        var controller = TestInfra.NovoAuthController(ctx, ana.Id);
        var resultado = await controller.EditarPerfil(
            nome: "Ana", email: "contato@clube.com", celular: null,
            cidade: null, estado: null, isProfessor: false, foto: null);

        Assert.IsType<ViewResult>(resultado);
        Assert.Equal("ana@exemplo.com", (await ctx.Jogadores.FindAsync(ana.Id))!.Email);
    }
}
