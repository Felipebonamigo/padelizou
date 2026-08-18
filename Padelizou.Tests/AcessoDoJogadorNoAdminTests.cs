using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A tela /Admin/Acesso responde "por que essa pessoa não entra". O que ela protege é a
// RESPOSTA, não a consulta: os quatro estados mandam a pessoa pra quatro lugares diferentes, e
// três deles NÃO se resolvem com "esqueci minha senha". Confundir dois é fazer o Felipe mandar
// alguém esperar um e-mail que nunca vai sair.
public class AcessoDoJogadorNoAdminTests
{
    private const int IdDoAdmin = 1;

    private static DbPadelContext Base()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = IdDoAdmin, Nome = "Felipe Bonamigo", Cpf = "99900000001",
            Email = "felipe@exemplo.com", SenhaHash = "hash", IsAdminRaiz = true,
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
        // A tela é só-leitura, e o assistente do sistema só passa no gate quando o verbo é GET.
        http.Request.Method = HttpMethods.Get;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static async Task<AcessoDoJogadorVM> Procurar(
        DbPadelContext ctx, string? busca, int? jogadorId = null, int logadoId = IdDoAdmin)
    {
        var resposta = await Controlador(ctx, logadoId).Acesso(busca, jogadorId);
        return Assert.IsType<AcessoDoJogadorVM>(Assert.IsType<ViewResult>(resposta).Model);
    }

    // ── AS QUATRO SITUAÇÕES ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Conta_normal_manda_a_pessoa_pro_esqueci_minha_senha()
    {
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Lucas Almeida", Cpf = "01517900000",
            Login = "foka", Email = "lucas@exemplo.com", SenhaHash = "hash",
        });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, "01517900000");

        Assert.Equal(SituacaoDeAcesso.PodeRecuperar, vm.Situacao);
        Assert.Equal("lucas@exemplo.com", vm.Achado!.Email);
        Assert.Equal("foka", vm.Achado.Login);
    }

    [Fact]
    public async Task Pre_cadastro_NAO_e_lido_como_conta_normal()
    {
        // O caso que mais engana: a pessoa aparece no site, com histórico de torneio, e nunca
        // teve senha. Se a tela dissesse "manda no esqueci minha senha", ela ia esperar pra
        // sempre um e-mail que a ação pública nem chega a gerar.
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Inscrito Pelo Organizador", Cpf = "01517900000", SenhaHash = null,
        });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, "01517900000");

        Assert.Equal(SituacaoDeAcesso.PreCadastro, vm.Situacao);
    }

    [Fact]
    public async Task Pre_cadastro_COM_email_continua_sendo_pre_cadastro()
    {
        // O organizador pode ter digitado o e-mail dela na inscrição. Ter e-mail não cria senha
        // — e é justamente o que faria uma leitura apressada classificar como conta normal.
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Inscrita Com Email", Cpf = "01517900000",
            Email = "ela@exemplo.com", SenhaHash = null,
        });
        await ctx.SaveChangesAsync();

        Assert.Equal(SituacaoDeAcesso.PreCadastro, (await Procurar(ctx, "01517900000")).Situacao);
    }

    [Fact]
    public async Task Com_senha_e_sem_email_e_o_beco_sem_saida()
    {
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Assumiu Sem Email", Cpf = "01517900000",
            Email = null, SenhaHash = "hash",
        });
        await ctx.SaveChangesAsync();

        Assert.Equal(SituacaoDeAcesso.SemEmail, (await Procurar(ctx, "01517900000")).Situacao);
    }

    [Fact]
    public async Task Conta_excluida_nao_e_confundida_com_pre_cadastro()
    {
        // A exclusão TAMBÉM zera o SenhaHash. Na ordem errada, quem pediu pra sair apareceria
        // como "é só se cadastrar de novo" — dizendo a quem foi embora que a porta continua
        // aberta. Aqui o admin chega pelo id, porque a busca (de propósito) não a alcança.
        using var ctx = Base();
        var jogador = new Jogador { Id = 2, Nome = "Quem Saiu", Cpf = "01517900000", SenhaHash = "hash" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        ExclusaoDeConta.Anonimizar(jogador, new DateTime(2026, 8, 1));
        jogador.ExcluidoEm = new DateTime(2026, 8, 1);
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, busca: null, jogadorId: 2);

        Assert.Equal(SituacaoDeAcesso.ContaExcluida, vm.Situacao);
    }

    // ── A BUSCA ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Homonimos_viram_lista_pra_escolher_em_vez_de_um_chute()
    {
        // Responder ao Lucas errado é pior que não responder: o admin conclui que resolveu.
        using var ctx = Base();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 2, Nome = "Lucas Almeida", Cpf = "11111111111", SenhaHash = "h" },
            new Jogador { Id = 3, Nome = "Lucas Pereira", Cpf = "22222222222", SenhaHash = "h" });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, "Lucas");

        Assert.Null(vm.Achado);
        Assert.Equal(2, vm.Candidatos.Count);
    }

    [Fact]
    public async Task Escolher_na_lista_nao_procura_de_novo()
    {
        // Sem este caminho, clicar no homônimo procuraria pelo mesmo nome, acharia os dois de
        // novo e devolveria a lista — um laço sem saída.
        using var ctx = Base();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 2, Nome = "Lucas Almeida", Cpf = "11111111111", SenhaHash = "h", Email = "a@x.com" },
            new Jogador { Id = 3, Nome = "Lucas Pereira", Cpf = "22222222222", SenhaHash = "h", Email = "b@x.com" });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, busca: null, jogadorId: 3);

        Assert.Equal(3, vm.Achado!.Id);
        Assert.Empty(vm.Candidatos);
    }

    [Fact]
    public async Task CPF_pela_metade_nao_varre_a_base()
    {
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Alguém", Cpf = "01517900000", SenhaHash = "h" });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, "015179");

        Assert.Null(vm.Achado);
        Assert.Empty(vm.Candidatos);
        Assert.True(vm.Procurou);   // "procurei e não achei" não é a mesma tela de "acabei de abrir"
    }

    [Fact]
    public async Task Tela_recem_aberta_nao_diz_que_nao_achou_ninguem()
    {
        using var ctx = Base();

        var vm = await Procurar(ctx, busca: null);

        Assert.False(vm.Procurou);
    }

    // ── A PORTA ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_e_admin_nao_entra()
    {
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Jogador Comum", Cpf = "33333333333", SenhaHash = "h" });
        await ctx.SaveChangesAsync();

        var resposta = await Controlador(ctx, usuarioLogadoId: 9).Acesso("01517900000", null);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Perfil", redirect.ActionName);
    }

    [Fact]
    public async Task Assistente_do_sistema_enxerga_a_tela()
    {
        // Ele vê tudo e não muda nada — e esta tela não muda nada, então ele entra inteiro.
        using var ctx = Base();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Foka", Cpf = "33333333333", SenhaHash = "h", IsAssistente = true });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Alguém", Cpf = "01517900000", SenhaHash = "h", Email = "a@x.com" });
        await ctx.SaveChangesAsync();

        var vm = await Procurar(ctx, "01517900000", logadoId: 9);

        Assert.Equal(2, vm.Achado!.Id);
    }

    // ── A SENHA NÃO SAI ──────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_nunca_imprime_o_hash_da_senha()
    {
        // O que se pode mostrar é SE existe senha, nunca o campo. Um `@p.SenhaHash` na tela
        // entregaria o hash pra qualquer print de tela do painel.
        var tela = File.ReadAllText(CaminhoDaTela());

        Assert.DoesNotContain("@p.SenhaHash", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("@Model.Achado.SenhaHash", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void O_CPF_aparece_mascarado_na_tela()
    {
        // Uma busca por nome devolve até dez pessoas. Com os onze dígitos impressos, a tela de
        // suporte vira um extrator de documentos — o miolo já responde "é esse mesmo?".
        Assert.Equal("•••.456.789-••", Documentos.CpfMascarado("11145678900"));
        Assert.Equal("•••.456.789-••", Documentos.CpfMascarado("111.456.789-00"));

        var tela = File.ReadAllText(CaminhoDaTela());
        Assert.Contains("Documentos.CpfMascarado", tela, StringComparison.Ordinal);
        Assert.DoesNotContain("@p.Cpf<", tela, StringComparison.Ordinal);
    }

    [Fact]
    public void O_CPF_de_quem_saiu_nao_e_mascarado()
    {
        // "EX000000002" é o carimbo da exclusão (ExclusaoDeConta.CpfDeQuemSaiu). Mascará-lo
        // esconderia justamente o que ele denuncia.
        Assert.Equal("EX000000002", Documentos.CpfMascarado("EX000000002"));
    }

    [Fact]
    public void O_pre_cadastro_encontra_o_caminho_do_cadastro_na_tela()
    {
        // Não basta classificar certo: a tela tem que DIZER o que fazer, senão o admin lê
        // "pré-cadastro" e continua sem saber o que responder.
        var tela = File.ReadAllText(CaminhoDaTela());

        var bloco = tela.IndexOf("PreCadastro", StringComparison.Ordinal);
        Assert.True(bloco > 0);
        Assert.Contains("cadastrar com esse mesmo CPF", tela, StringComparison.OrdinalIgnoreCase);
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
