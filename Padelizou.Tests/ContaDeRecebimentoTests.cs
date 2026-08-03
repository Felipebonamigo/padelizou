using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A armadilha mais cara que o sistema teve: o organizador escolhia "pelo site", publicava,
// recebia inscrições — e NENHUMA cobrança nascia, porque a conta de recebimento nunca tinha
// sido conectada. Sem erro, sem aviso. Ele descobria ao ir procurar o dinheiro.
public class ContaDeRecebimentoTests
{
    private static Jogador Organizador(bool ligou, string? carteira) => new()
    {
        Nome = "Organizador",
        Cpf = "11144477735",
        ReceberPagamentoOnline = ligou,
        AsaasWalletId = carteira,
    };

    [Theory]
    [InlineData(true, "carteira-de-verdade", true)]
    [InlineData(true, null, false)]    // ligou a chave mas não disse pra onde vai o dinheiro
    [InlineData(false, "carteira-de-verdade", false)]  // disse pra onde, mas não autorizou
    [InlineData(true, "   ", false)]   // espaço em branco não é conta
    public void Conectada_exige_a_chave_ligada_E_a_conta_informada(bool ligou, string? carteira, bool esperado)
    {
        Assert.Equal(esperado, ContaDeRecebimento.Conectada(Organizador(ligou, carteira)));
    }

    [Fact]
    public void Sem_ninguem_no_cadastro_tambem_nao_esta_conectada()
    {
        Assert.False(ContaDeRecebimento.Conectada(null));
        Assert.NotNull(ContaDeRecebimento.OQueFalta(null));
    }

    [Fact]
    public void Quem_esta_conectado_nao_recebe_recado_nenhum()
    {
        Assert.Null(ContaDeRecebimento.OQueFalta(Organizador(true, "carteira-de-verdade")));
    }

    [Fact]
    public void Gateway_fora_do_ar_e_problema_nosso_e_o_texto_diz_outra_coisa()
    {
        // Não adianta mandar ele configurar a conta: o que está fora do ar é o nosso lado.
        var texto = ContaDeRecebimento.OQueFalta(
            Organizador(true, "carteira-de-verdade"), recebimentoDisponivel: false);

        Assert.NotNull(texto);
        Assert.Contains("fora do ar", texto);
    }

    // ── A recusa de verdade, no controller ────────────────────────────────────────────────

    private static IPagamentoInscricaoService Pagamentos(bool conectado)
    {
        var falso = Substitute.For<IPagamentoInscricaoService>();
        falso.PodeReceberOnline(Arg.Any<Jogador?>()).Returns(conectado);
        falso.OQueFaltaParaReceber(Arg.Any<Jogador?>())
            .Returns(conectado ? null : "Você ainda não disse pra onde quer que o dinheiro vá.");
        return falso;
    }

    private static async Task<(DbPadelContext ctx, IActionResult resultado)> CriarTorneio(
        string formaPagamento, decimal preco, bool contaConectada)
    {
        var ctx = TestInfra.NovoContexto();
        var organizador = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(organizador);
        // Namespace minúsculo: CategoriaPadrao é do lote antigo, em `padelizou.Models`.
        var categoria = new padelizou.Models.CategoriaPadrao
        {
            Nome = "6ª Masculina", Codigo = "6M", Tipo = "Masculina"
        };
        ctx.CategoriasPadrao.Add(categoria);
        var clube = new Clube { Nome = "Clube Teste" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, Pagamentos(contaConectada));
        var torneio = new Torneio
        {
            Nome = "Torneio Teste",
            Codigo = "TST1",
            PrecoInscricao = preco,
            FormaPagamento = formaPagamento,
            ClubeId = clube.Id,
            DataInicio = DateTime.Today.AddDays(30),
        };

        var resultado = await controller.Create(torneio, new[] { categoria.Id }, null, null, null, null);
        return (ctx, resultado);
    }

    [Fact]
    public async Task Pelo_site_sem_conta_conectada_e_recusado()
    {
        var (ctx, resultado) = await CriarTorneio("OnlinePix", 150m, contaConectada: false);
        using var _ = ctx;

        // Volta pro formulário com o motivo — e, principalmente, NÃO cria o torneio: ele
        // nasceria anunciando pagamento pelo site sem ter como cobrar ninguém.
        var view = Assert.IsType<ViewResult>(resultado);
        Assert.NotNull(view.ViewData["Erro"]);
        Assert.Empty(ctx.Torneios);
    }

    [Fact]
    public async Task Pelo_site_com_conta_conectada_passa()
    {
        var (ctx, resultado) = await CriarTorneio("OnlinePix", 150m, contaConectada: true);
        using var _ = ctx;

        Assert.Single(ctx.Torneios);
        Assert.Equal("OnlinePix", ctx.Torneios.Single().FormaPagamento);
    }

    [Fact]
    public async Task Por_fora_nunca_depende_de_conta_conectada()
    {
        // É justamente a saída de quem não conectou nada: o dinheiro não passa pelo sistema.
        var (ctx, _resultado) = await CriarTorneio("Externo", 150m, contaConectada: false);
        using var _ = ctx;

        Assert.Single(ctx.Torneios);
        Assert.Equal("Externo", ctx.Torneios.Single().FormaPagamento);
    }

    [Fact]
    public async Task Torneio_de_graca_pelo_site_passa_sem_conta()
    {
        // Preço zero não gera cobrança nenhuma — exigir conta aqui seria barrar por um
        // problema que não existe.
        var (ctx, _resultado) = await CriarTorneio("OnlinePix", 0m, contaConectada: false);
        using var _ = ctx;

        Assert.Single(ctx.Torneios);
    }

    [Fact]
    public async Task Recusado_o_formulario_volta_em_por_fora_em_vez_de_repetir_a_recusa()
    {
        using var ctx = TestInfra.NovoContexto();
        var organizador = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(organizador);
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Nome = "6ª Masculina", Codigo = "6M", Tipo = "Masculina"
        });
        var clube = new Clube { Nome = "Clube Teste" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();
        var categoriaId = ctx.CategoriasPadrao.Single().Id;

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, Pagamentos(conectado: false));

        // É isto que o model binder deixa pra trás no POST — e é DAQUI que o asp-for tira o
        // rádio marcado, não do objeto. Só corrigir o objeto deixava a tela voltar com "pelo
        // site" marcado e desativado: o envio seguinte repetia a recusa, sem saída.
        controller.ModelState.SetModelValue("FormaPagamento", "OnlinePix", "OnlinePix");

        var resultado = await controller.Create(
            new Torneio
            {
                Nome = "Torneio Teste", Codigo = "TST2", PrecoInscricao = 150m,
                FormaPagamento = "OnlinePix", ClubeId = clube.Id, DataInicio = DateTime.Today.AddDays(30),
            },
            new[] { categoriaId }, null, null, null, null);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.False(view.ViewData.ModelState.ContainsKey("FormaPagamento"));
        Assert.Equal("Externo", Assert.IsType<Torneio>(view.Model).FormaPagamento);
    }

    [Fact]
    public void O_servico_de_verdade_concorda_com_a_regra()
    {
        // Os testes acima usam dublê; este amarra o dublê na implementação real, senão a
        // recusa poderia passar a valer só no teste.
        using var ctx = TestInfra.NovoContexto();
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));

        Assert.False(servico.PodeReceberOnline(Organizador(true, null)));
        Assert.NotNull(servico.OQueFaltaParaReceber(Organizador(true, null)));

        Assert.True(servico.PodeReceberOnline(Organizador(true, "carteira-de-verdade")));
        Assert.Null(servico.OQueFaltaParaReceber(Organizador(true, "carteira-de-verdade")));
    }
}
