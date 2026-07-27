using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O organizador escolhe, ao criar o torneio, se o dinheiro corre pelo site ou por fora.
// "Por fora" não pode gerar cobrança nenhuma, mesmo com o Asaas todo configurado.
public class FormaPagamentoTorneioTests
{
    private static PagamentoInscricaoService NovoServico(DbPadelContext ctx, bool asaasConfigurado = true)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(asaasConfigurado);
        return new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>());
    }

    private static Jogador RecebedorApto() => new()
    {
        Nome = "Organizador",
        Cpf = "11144477735",
        ReceberPagamentoOnline = true,
        AsaasWalletId = "carteira-de-verdade",
    };

    [Fact]
    public void Torneio_online_cobra_pelo_site()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "T", Codigo = "T1", PrecoInscricao = 150m, FormaPagamento = "Online" };

        Assert.True(NovoServico(ctx).PodeCobrar(torneio, RecebedorApto()));
    }

    [Fact]
    public void Torneio_por_fora_nunca_cobra_mesmo_com_tudo_configurado()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "T", Codigo = "T1", PrecoInscricao = 150m, FormaPagamento = "Externo" };

        // Recebedor apto, Asaas ligado, preço válido — e ainda assim não cobra, porque a
        // escolha do organizador vem antes da capacidade técnica.
        Assert.False(NovoServico(ctx).PodeCobrar(torneio, RecebedorApto()));
    }

    [Fact]
    public void Torneio_novo_nasce_cobrando_pelo_site()
    {
        var torneio = new Torneio { Nome = "T", Codigo = "T1" };

        Assert.Equal("Online", torneio.FormaPagamento);
        Assert.True(torneio.CobraPeloSite);
    }

    [Fact]
    public void Jogador_paga_exatamente_o_valor_anunciado()
    {
        // A taxa sai de dentro do preço, nunca por cima: o organizador anuncia R$ 75 e o
        // jogador paga R$ 75. Deixou de ser escolha em 27/07/2026.
        Assert.Equal("Descontada", new Torneio { Nome = "T", Codigo = "T1" }.ModoComissao);
    }

    [Fact]
    public void Preco_e_por_pessoa_entao_a_dupla_paga_o_dobro()
    {
        var torneio = new Torneio { Nome = "T", Codigo = "T1", PrecoInscricao = 75m };

        Assert.Equal(150m, torneio.ValorCobrado(inscricaoDeDupla: true));
        Assert.Equal(75m, torneio.ValorCobrado(inscricaoDeDupla: false));
    }

    [Fact]
    public void Americano_conta_uma_pessoa_por_inscricao()
    {
        Assert.Equal(1, new Torneio { Nome = "T", Codigo = "T1", Formato = "Americano" }.PessoasPorInscricao);
        Assert.Equal(2, new Torneio { Nome = "T", Codigo = "T1", Formato = "Padrao" }.PessoasPorInscricao);
    }

    [Fact]
    public void Comissao_do_torneio_por_fora_e_menor_que_a_do_online()
    {
        // Por fora não tem custo de gateway nem risco de chargeback — o serviço é só a
        // organização. Se ficasse igual, ninguém escolheria receber pelo site.
        var taxas = new TaxasExibicao();
        var asaas = new AsaasSettings();

        Assert.True(taxas.ComissaoPercentualExterno < asaas.ComissaoPercentualPorTipo["Torneio"]);
    }

    [Fact]
    public void Cartao_de_credito_e_sinalizado_como_demorado()
    {
        // É o aviso que aparece em vermelho na criação do torneio.
        var taxas = new TaxasExibicao();

        Assert.True(taxas.CreditoAVista.Demora);
        Assert.True(taxas.CreditoAte21x.Demora);
        Assert.False(taxas.Boleto.Demora);
        Assert.False(taxas.Pix.Demora);
        Assert.Equal("1 dia útil", taxas.Boleto.PrazoEscrito);
        Assert.Equal("na hora", taxas.Pix.PrazoEscrito);
    }

    [Fact]
    public void Parcelar_no_credito_custa_mais_caro_a_cada_faixa()
    {
        // O jogador é quem escolhe parcelar, e quem paga a conta é o organizador — por isso
        // as quatro faixas aparecem na tela, não só a taxa "à vista".
        var t = new TaxasExibicao();

        Assert.True(t.CreditoAVista.Percentual < t.CreditoAte6x.Percentual);
        Assert.True(t.CreditoAte6x.Percentual < t.CreditoAte12x.Percentual);
        Assert.True(t.CreditoAte12x.Percentual < t.CreditoAte21x.Percentual);
    }

    [Theory]
    [InlineData(150)]
    [InlineData(80)]
    public void Pix_sempre_sobra_mais_que_credito_parcelado(decimal preco)
    {
        var t = new TaxasExibicao();

        Assert.True(t.Pix.Liquido(preco) > t.CreditoAte21x.Liquido(preco));
    }

    [Fact]
    public void Taxa_escrita_sai_legivel_em_cada_formato()
    {
        var t = new TaxasExibicao();

        Assert.Equal("R$ 0,99", t.Boleto.TaxaEscrita);            // só valor fixo
        Assert.Equal("1,99% + R$ 0,49", t.CreditoAVista.TaxaEscrita); // percentual + fixo
    }
}
