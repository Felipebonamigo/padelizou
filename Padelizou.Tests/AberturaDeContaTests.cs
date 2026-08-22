using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Abrir a conta de recebimento sem sair do Padelizou. O caminho antigo — ir no site do meio
// de pagamento, achar um código chamado "Wallet ID", voltar e colar — continua existindo pra
// quem já tem conta, mas deixou de ser o único.
public class AberturaDeContaTests
{
    private static Jogador Completo() => new()
    {
        Nome = "Anderson Virgili",
        Email = "anderson@exemplo.test",
        Cpf = "11144477735",
        Celular = "51999998888",
    };

    // ── O que o cadastro dele já precisa ter ──────────────────────────────────────────────

    [Fact]
    public void Perfil_completo_passa()
    {
        Assert.Null(AberturaDeConta.FaltaNoPerfil(Completo()));
    }

    [Fact]
    public void Sem_celular_nao_da_pra_abrir_conta()
    {
        // Conta antiga do Padelizou nasceu sem celular, e o meio de pagamento exige.
        var j = Completo();
        j.Celular = null;

        var falta = AberturaDeConta.FaltaNoPerfil(j);
        Assert.NotNull(falta);
        Assert.Contains("celular", falta, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cpf_invalido_e_barrado_antes_de_bater_no_gateway()
    {
        var j = Completo();
        j.Cpf = "11111111111";  // dígito repetido: formato certo, CPF falso

        Assert.NotNull(AberturaDeConta.FaltaNoPerfil(j));
    }

    [Fact]
    public void Nome_que_nao_parece_nome_e_barrado()
    {
        // O mesmo "." que apareceu na inscrição do torneio real.
        var j = Completo();
        j.Nome = ".";

        Assert.NotNull(AberturaDeConta.FaltaNoPerfil(j));
    }

    [Fact]
    public void Sem_email_nao_ha_como_ativar_a_conta()
    {
        var j = Completo();
        j.Email = "";

        var falta = AberturaDeConta.FaltaNoPerfil(j);
        Assert.NotNull(falta);
        Assert.Contains("ativada", falta);
    }

    // ── O formulário pedido na hora ───────────────────────────────────────────────────────

    private static readonly DateTime Nascimento = new(1990, 5, 20);

    [Theory]
    [InlineData(null, "91520000", "Rua X", "10", "Centro")]        // sem faturamento
    [InlineData(0, "91520000", "Rua X", "10", "Centro")]           // faturamento zero
    [InlineData(3000, "9152000", "Rua X", "10", "Centro")]         // CEP com 7 dígitos
    [InlineData(3000, "91520000", "  ", "10", "Centro")]           // rua em branco
    [InlineData(3000, "91520000", "Rua X", "", "Centro")]          // sem número
    [InlineData(3000, "91520000", "Rua X", "10", null)]            // sem bairro
    public void Campo_faltando_ou_torto_e_recusado(
        int? faturamento, string cep, string endereco, string numero, string? bairro)
    {
        Assert.NotNull(AberturaDeConta.ProblemaNoFormulario(
            faturamento is int f ? f : null, Nascimento, cep, endereco, numero, bairro));
    }

    [Fact]
    public void Sem_data_de_nascimento_e_recusado()
    {
        // A documentação do Asaas NÃO lista este campo entre os obrigatórios, mas a API
        // recusa sem ele. Descoberto batendo no sandbox — sem isso, todo organizador de
        // verdade teria esbarrado no erro na primeira tentativa.
        Assert.NotNull(AberturaDeConta.ProblemaNoFormulario(
            3000m, null, "91520000", "Rua X", "10", "Centro"));
    }

    [Theory]
    [InlineData("2010-05-20", false)]  // 16 anos em 2026: o gateway não aceita
    [InlineData("2008-05-20", true)]   // 18 recém-feitos
    [InlineData("1850-01-01", false)]  // digitou o ano errado
    public void Idade_e_conferida_antes_de_bater_no_gateway(string nascimento, bool deveriaPassar)
    {
        var problema = AberturaDeConta.ProblemaNoFormulario(
            3000m, DateTime.Parse(nascimento), "91520000", "Rua X", "10", "Centro",
            hoje: new DateTime(2026, 8, 3));

        Assert.Equal(deveriaPassar, problema == null);
    }

    [Fact]
    public void Aniversario_que_ainda_nao_chegou_no_ano_nao_conta()
    {
        // Faz 18 em dezembro; em agosto ainda tem 17. Subtrair só os anos daria 18 e deixaria
        // passar uma pessoa que o gateway vai recusar.
        var problema = AberturaDeConta.ProblemaNoFormulario(
            3000m, new DateTime(2008, 12, 31), "91520000", "Rua X", "10", "Centro",
            hoje: new DateTime(2026, 8, 3));

        Assert.NotNull(problema);
    }

    [Fact]
    public void Formulario_completo_passa_e_o_cep_aceita_mascara()
    {
        Assert.Null(AberturaDeConta.ProblemaNoFormulario(
            3000m, Nascimento, "91520-000", "Av. Assis Brasil", "1234", "Sarandi"));
    }

    [Fact]
    public void Montar_limpa_os_espacos_e_arruma_o_nome()
    {
        var j = Completo();
        j.Nome = "  Anderson   Virgili ";

        var dados = AberturaDeConta.Montar(j, 3000m, Nascimento, " 91520-000 ", "  Av. Assis Brasil ", " 1234 ", " Sarandi ");

        Assert.Equal("Anderson Virgili", dados.Nome);
        Assert.Equal("Av. Assis Brasil", dados.Endereco);
        Assert.Equal("1234", dados.Numero);
        Assert.Equal("Sarandi", dados.Bairro);
        Assert.Equal(3000m, dados.FaturamentoMensal);
        Assert.Equal(Nascimento, dados.DataNascimento);
    }

    [Fact]
    public void O_recado_de_sucesso_nao_promete_que_ja_pode_receber()
    {
        // O titular ainda ativa por e-mail e manda documento. Prometer "pronto" aqui geraria
        // o suporte de "criei a conta e não caiu nada".
        Assert.Contains("e-mail", AberturaDeConta.DepoisDeCriar);
        Assert.Contains("documentos", AberturaDeConta.DepoisDeCriar);
    }

    // ── O caminho completo, no controller ─────────────────────────────────────────────────

    private static (PagamentosController c, DbPadelContext ctx, Jogador j) Montar(IAsaasService asaas)
    {
        var ctx = TestInfra.NovoContexto();
        var jogador = Completo();
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        var controller = new PagamentosController(
            ctx, Options.Create(new AsaasSettings()),
            Substitute.For<IPagamentoInscricaoService>(), asaas,
            NullLogger<PagamentosController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new TempDataDictionary(
            controller.HttpContext, Substitute.For<ITempDataProvider>());

        return (controller, ctx, jogador);
    }

    private static IAsaasService GatewayQueAceita(string walletId = "carteira-nova")
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.CriarSubcontaAsync(Arg.Any<DadosDaSubconta>())
            .Returns(((SubcontaCriada?)new SubcontaCriada(walletId), (FalhaAoCriarSubconta?)null));
        return asaas;
    }

    [Fact]
    public async Task Conta_criada_ja_fica_conectada()
    {
        var (c, ctx, j) = Montar(GatewayQueAceita());
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        var salvo = ctx.Jogadores.Find(j.Id)!;
        Assert.Equal("carteira-nova", salvo.AsaasWalletId);
        Assert.True(salvo.ReceberPagamentoOnline);
        Assert.True(ContaDeRecebimento.Conectada(salvo));
    }

    [Fact]
    public async Task O_endereco_nao_fica_guardado_em_lugar_nenhum()
    {
        // A promessa está escrita na tela: os dados vão pro meio de pagamento e não ficam
        // aqui. Se um dia alguém acrescentar colunas de endereço no Jogador, este teste cai.
        var asaas = GatewayQueAceita();
        var (c, ctx, j) = Montar(asaas);
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        // Chegou ao gateway...
        await asaas.Received(1).CriarSubcontaAsync(Arg.Is<DadosDaSubconta>(
            d => d != null && d.Endereco == "Av. Assis Brasil" && d.Bairro == "Sarandi"));

        // ...e não sobrou em nenhuma coluna de texto do jogador.
        var salvo = ctx.Jogadores.Find(j.Id)!;
        foreach (var texto in new[] { salvo.Nome, salvo.Email, salvo.Celular, salvo.AsaasWalletId, salvo.Apelido })
        {
            Assert.DoesNotContain("Assis Brasil", texto ?? "");
            Assert.DoesNotContain("Sarandi", texto ?? "");
        }
    }

    [Fact]
    public async Task Gateway_recusando_nao_deixa_a_conta_meio_conectada()
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.CriarSubcontaAsync(Arg.Any<DadosDaSubconta>()).Returns(
            ((SubcontaCriada?)null,
             (FalhaAoCriarSubconta?)new FalhaAoCriarSubconta("Já existe conta com este CPF.", JaTemConta: true)));

        var (c, ctx, j) = Montar(asaas);
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        var salvo = ctx.Jogadores.Find(j.Id)!;
        Assert.Null(salvo.AsaasWalletId);
        Assert.False(salvo.ReceberPagamentoOnline);
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Teto_de_subcontas_do_periodo_de_avaliacao_nao_deixa_o_organizador_sem_saida()
    {
        // O Período de Avaliação do Asaas deixa a conta-mãe criar no máximo 10 subcontas nos
        // primeiros 60 dias. Na décima primeira, a recusa não fala nada de "conta já existe" —
        // e a versão anterior deste código só oferecia o caminho manual quando reconhecia o
        // texto do erro. Resultado: tela sem saída justo pro organizador de número 11.
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.CriarSubcontaAsync(Arg.Any<DadosDaSubconta>()).Returns(
            ((SubcontaCriada?)null,
             (FalhaAoCriarSubconta?)new FalhaAoCriarSubconta(
                 "Limite de subcontas do período de avaliação atingido.", JaTemConta: false)));

        var (c, ctx, _j) = Montar(asaas);
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        var erro = c.TempData["Erro"] as string;
        Assert.NotNull(erro);
        Assert.Contains("Limite de subcontas", erro);
        // O que importa: a saída continua na tela. Colar o código funciona em QUALQUER recusa.
        Assert.Contains("colar o código", erro);
    }

    [Fact]
    public async Task Formulario_torto_nem_chega_a_bater_no_gateway()
    {
        var asaas = GatewayQueAceita();
        var (c, ctx, _j) = Montar(asaas);
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "123", "Av. Assis Brasil", "1234", "Sarandi", null);

        await asaas.DidNotReceive().CriarSubcontaAsync(Arg.Any<DadosDaSubconta>());
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_ja_esta_conectado_nao_abre_outra_conta()
    {
        var asaas = GatewayQueAceita();
        var (c, ctx, j) = Montar(asaas);
        using var _ = ctx;

        j.ReceberPagamentoOnline = true;
        j.AsaasWalletId = "carteira-que-ja-existia";
        await ctx.SaveChangesAsync();

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        await asaas.DidNotReceive().CriarSubcontaAsync(Arg.Any<DadosDaSubconta>());
        Assert.Equal("carteira-que-ja-existia", ctx.Jogadores.Find(j.Id)!.AsaasWalletId);
    }

    [Fact]
    public async Task Gateway_desligado_nao_oferece_o_caminho()
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(false);

        var (c, ctx, j) = Montar(asaas);
        using var _ = ctx;

        await c.AbrirConta(3000m, new DateTime(1990, 5, 20), "91520-000", "Av. Assis Brasil", "1234", "Sarandi", null);

        await asaas.DidNotReceive().CriarSubcontaAsync(Arg.Any<DadosDaSubconta>());
        Assert.False(ctx.Jogadores.Find(j.Id)!.ReceberPagamentoOnline);
    }
}
