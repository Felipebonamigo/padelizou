using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A EXCEÇÃO DO SUPORTE, que até hoje só existia como promessa de texto.
//
// `TrocaDeNome.Recusa` diz, pra quem já gastou a troca única: *"Se precisa mesmo mudar, fale
// com a gente pelo 'Reportar problema'"*. Do outro lado dessa frase não havia nada — nenhuma
// tela destravava, e a única saída era SSH + UPDATE no banco de produção. É o mesmo buraco que
// a /Admin/Acesso nasceu pra fechar em 18/08, um degrau adiante.
//
// ⚠️ A LIBERAÇÃO NÃO TROCA O NOME DE NINGUÉM — ela devolve UMA troca, e quem troca é a própria
// pessoa, no perfil dela. É por isso que ela cabe numa tela que, de propósito, não edita conta
// alheia: o admin não escreve o nome novo, não vê o nome novo e não escolhe o nome novo.
//
// ⚠️ E o "trava de novo" não é código nenhum: `PodeTrocarNome` já lê "carimbo nulo → pode", e o
// próprio salvamento dela recarimba. Zerar o carimbo é exatamente "mais uma vez, e só".
public class LiberarTrocaDeNomeTests
{
    private const int IdDoAdmin = 1;
    private const int IdDaCarol = 2;

    private static readonly DateTime TrocouAnoPassado = new(2025, 3, 10, 9, 0, 0);

    private static DbPadelContext BaseComCarolTravada(
        DateTime? nomeAlteradoEm = null, DateTime? apelidoAlteradoEm = null)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = IdDoAdmin, Nome = "Felipe Bonamigo", Cpf = "99900000001",
            Email = "felipe@exemplo.com", SenhaHash = "hash", IsAdminRaiz = true,
        });
        ctx.Jogadores.Add(new Jogador
        {
            Id = IdDaCarol, Nome = "Carol", Apelido = "Carolzinha", Cpf = "03842585063",
            Email = "carol@exemplo.com", SenhaHash = "hash",
            NomeAlteradoEm = nomeAlteradoEm ?? TrocouAnoPassado,
            ApelidoAlteradoEm = apelidoAlteradoEm ?? TrocouAnoPassado,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static AdminController Controlador(DbPadelContext ctx, int usuarioLogadoId = IdDoAdmin)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
        };
        // ⚠️ O VERBO É A TRAVA do assistente do sistema (ver AdminController.ObterJogadorAdminAsync):
        // ele passa no gate quando a leitura é GET e é recusado em qualquer POST. Um teste que
        // esquecesse esta linha estaria medindo outra coisa.
        http.Request.Method = HttpMethods.Post;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, Substitute.For<ITempDataProvider>());
        return controller;
    }

    private static async Task<Jogador> CarolAsync(DbPadelContext ctx) =>
        (await ctx.Jogadores.FindAsync(IdDaCarol))!;

    // ── O QUE A LIBERAÇÃO FAZ ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Liberar_devolve_a_troca_de_nome_pra_quem_ja_tinha_gastado()
    {
        using var ctx = BaseComCarolTravada();
        Assert.False(TrocaDeNome.PodeTrocarNome(TrocouAnoPassado, DateTime.Now).Pode);

        await Controlador(ctx).LiberarTrocaDeNome(IdDaCarol);

        var carol = await CarolAsync(ctx);
        Assert.Null(carol.NomeAlteradoEm);
        Assert.True(TrocaDeNome.PodeTrocarNome(carol.NomeAlteradoEm, DateTime.Now).Pode);
    }

    [Fact]
    public async Task Liberar_devolve_a_troca_de_apelido()
    {
        using var ctx = BaseComCarolTravada(apelidoAlteradoEm: DateTime.Now.AddDays(-3));
        await Controlador(ctx).LiberarTrocaDeApelido(IdDaCarol);

        var carol = await CarolAsync(ctx);
        Assert.Null(carol.ApelidoAlteradoEm);
        Assert.True(TrocaDeNome.PodeTrocarApelido(carol.ApelidoAlteradoEm, DateTime.Now).Pode);
    }

    [Fact]
    public async Task Liberar_um_nao_libera_o_outro_de_brinde()
    {
        // São duas réguas diferentes (o nome é uma vez só; o apelido, a cada mês) e dois
        // botões diferentes. Um que zerasse os dois carimbos daria uma troca que ninguém pediu.
        using var ctx = BaseComCarolTravada();

        await Controlador(ctx).LiberarTrocaDeNome(IdDaCarol);
        var depoisDoNome = await CarolAsync(ctx);
        Assert.Null(depoisDoNome.NomeAlteradoEm);
        Assert.Equal(TrocouAnoPassado, depoisDoNome.ApelidoAlteradoEm);

        await Controlador(ctx).LiberarTrocaDeApelido(IdDaCarol);
        Assert.Null((await CarolAsync(ctx)).ApelidoAlteradoEm);
    }

    [Fact]
    public async Task Liberar_nao_toca_no_nome_nem_no_apelido_gravados()
    {
        // O admin devolve a TROCA, não escreve o nome. Esta tela não edita conta alheia — é a
        // razão de ela poder existir aqui sem contrariar o que a /Admin/Acesso decidiu ser.
        using var ctx = BaseComCarolTravada();

        await Controlador(ctx).LiberarTrocaDeNome(IdDaCarol);
        await Controlador(ctx).LiberarTrocaDeApelido(IdDaCarol);

        var carol = await CarolAsync(ctx);
        Assert.Equal("Carol", carol.Nome);
        Assert.Equal("Carolzinha", carol.Apelido);
        Assert.Equal("carol@exemplo.com", carol.Email);
    }

    [Fact]
    public async Task Liberar_volta_pra_ficha_da_pessoa_e_nao_pra_busca_vazia()
    {
        // Sem o id no redirect o admin cairia na tela em branco e teria que procurar o CPF de
        // novo pra conferir se funcionou.
        using var ctx = BaseComCarolTravada();

        var resposta = await Controlador(ctx).LiberarTrocaDeNome(IdDaCarol);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal(nameof(AdminController.Acesso), redirect.ActionName);
        Assert.Equal(IdDaCarol, redirect.RouteValues!["jogadorId"]);
    }

    // ── "MAIS UMA VEZ, E SÓ" — O PEDIDO INTEIRO, PONTA A PONTA ───────────────────────────

    [Fact]
    public async Task Depois_de_usar_a_liberacao_o_nome_trava_de_novo_sozinho()
    {
        // O teste que trava o pedido: liberar dá UMA troca, não abre a porteira. Quem recarimba
        // é o próprio salvamento dela — não existe código de "voltar a travar", e é justamente
        // por isso que este caminho precisa ser exercitado de ponta a ponta.
        using var ctx = BaseComCarolTravada();
        await Controlador(ctx).LiberarTrocaDeNome(IdDaCarol);

        var perfil = TestInfra.NovoAuthController(ctx, IdDaCarol);
        await perfil.EditarPerfil("Caroline Souza", "carol@exemplo.com", null,
            cidade: null, estado: null, isProfessor: false, foto: null);

        var depoisDaTroca = await CarolAsync(ctx);
        Assert.Equal("Caroline Souza", depoisDaTroca.Nome);
        Assert.NotNull(depoisDaTroca.NomeAlteradoEm);

        // E a segunda tentativa bate na trava, sem precisar de mais nada.
        var deNovo = TestInfra.NovoAuthController(ctx, IdDaCarol);
        await deNovo.EditarPerfil("Outro Nome Qualquer", "carol@exemplo.com", null,
            cidade: null, estado: null, isProfessor: false, foto: null);

        Assert.Equal("Caroline Souza", (await CarolAsync(ctx)).Nome);
    }

    // ── A PORTA ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Assistente_do_sistema_nao_libera()
    {
        // Ele enxerga esta tela inteira (é GET) e não muda nada. A liberação é o primeiro POST
        // da /Admin/Acesso — se ele passasse aqui, a premissa "só o Felipe edita" caía junto.
        using var ctx = BaseComCarolTravada();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 9, Nome = "Foka", Cpf = "33333333333", SenhaHash = "h", IsAssistente = true,
        });
        await ctx.SaveChangesAsync();

        var resposta = await Controlador(ctx, usuarioLogadoId: 9).LiberarTrocaDeNome(IdDaCarol);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal(TrocouAnoPassado, (await CarolAsync(ctx)).NomeAlteradoEm);
    }

    [Fact]
    public async Task Jogador_comum_nao_libera_a_troca_de_ninguem()
    {
        using var ctx = BaseComCarolTravada();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Jogador Comum", Cpf = "33333333333", SenhaHash = "h" });
        await ctx.SaveChangesAsync();

        var resposta = await Controlador(ctx, usuarioLogadoId: 9).LiberarTrocaDeApelido(IdDaCarol);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal(TrocouAnoPassado, (await CarolAsync(ctx)).ApelidoAlteradoEm);
    }

    [Fact]
    public async Task Id_que_nao_existe_nao_derruba_a_tela()
    {
        using var ctx = BaseComCarolTravada();

        Assert.IsType<NotFoundResult>(await Controlador(ctx).LiberarTrocaDeNome(4242));
    }

    // ── A TELA ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_pergunta_a_regua_em_vez_de_reler_o_carimbo()
    {
        // Botão pra quem NÃO está travado não libera nada e ainda sugere que a pessoa depende de
        // um clique do admin pra se corrigir. E quem responde "está travado?" tem que ser a mesma
        // função que trava a pessoa no perfil dela: um `AlteradoEm != null` escrito aqui é uma
        // segunda cópia da régua — ofereceria botão pro apelido de quem já saiu da carência
        // sozinho, e discordaria da outra no dia em que a regra mudasse.
        var tela = File.ReadAllText(CaminhoDaTela());

        Assert.Contains("LiberarTrocaDeNome", tela, StringComparison.Ordinal);
        Assert.Contains("LiberarTrocaDeApelido", tela, StringComparison.Ordinal);
        Assert.Contains("TrocaDeNome.PodeTrocarNome(p.NomeAlteradoEm", tela, StringComparison.Ordinal);
        Assert.Contains("TrocaDeNome.PodeTrocarApelido(p.ApelidoAlteradoEm", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("p.NomeAlteradoEm != null", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("p.ApelidoAlteradoEm != null", tela, StringComparison.Ordinal);
    }

    private static string CaminhoDaTela()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Padelizou", "Views", "Admin", "Acesso.cshtml");
    }
}
