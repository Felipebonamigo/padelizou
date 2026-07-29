using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Services;

namespace Padelizou.Tests;

// CalcularRateio é o coração financeiro: decide quanto o jogador paga, quanto o
// recebedor leva e quanto fica de comissão. Qualquer erro aqui é dinheiro errado.
public class RateioComissaoTests
{
    private static AsaasService NovoServico(decimal comissaoMinima = 4m, string modoPadrao = "Somada")
    {
        var settings = new AsaasSettings
        {
            ComissaoPercentualPorTipo = new() { ["Torneio"] = 15m, ["Aula"] = 10m, ["Jogo"] = 10m },
            ComissaoPercentualPadrao = 10m,
            ComissaoMinima = comissaoMinima,
            ModoComissaoPadrao = modoPadrao,
        };
        return new AsaasService(new HttpClient(), Options.Create(settings), NullLogger<AsaasService>.Instance);
    }

    [Theory]
    [InlineData("Torneio", 100, 15)] // 15%
    [InlineData("Aula", 100, 10)]    // 10%
    [InlineData("Jogo", 100, 10)]    // 10%
    public void Comissao_por_tipo_de_operacao(string tipo, decimal preco, decimal comissaoEsperada)
    {
        var r = NovoServico().CalcularRateio(preco, tipo);
        Assert.Equal(comissaoEsperada, r.Comissao);
    }

    [Fact]
    public void Modo_Somada_jogador_paga_a_taxa_e_recebedor_leva_o_preco_cheio()
    {
        var r = NovoServico().CalcularRateio(100m, "Torneio", "Somada");
        Assert.Equal(115m, r.ValorTotal);      // jogador paga preço + taxa
        Assert.Equal(100m, r.ValorRepasse);    // dono recebe cheio
        Assert.Equal(15m, r.Comissao);
    }

    [Fact]
    public void Modo_Descontada_jogador_paga_o_preco_e_a_taxa_sai_do_repasse()
    {
        var r = NovoServico().CalcularRateio(100m, "Aula", "Descontada");
        Assert.Equal(100m, r.ValorTotal);
        Assert.Equal(90m, r.ValorRepasse);
        Assert.Equal(10m, r.Comissao);
    }

    [Fact]
    public void Comissao_minima_vale_quando_o_percentual_ficaria_abaixo()
    {
        // 15% de R$20 = R$3, abaixo do piso de R$4 do torneio → cobra R$4.
        var r = NovoServico().CalcularRateio(20m, "Torneio", "Somada");
        Assert.Equal(4m, r.Comissao);
        Assert.Equal(24m, r.ValorTotal);
    }

    [Fact]
    public void Piso_e_por_tipo_de_operacao_e_nao_transforma_3_por_cento_em_ficcao()
    {
        // O bug que o piso único de R$ 4 causaria: 3% de uma aula de R$ 100 são R$ 3 — com
        // piso global o professor assinante pagaria R$ 4 (4%) achando que pagava 3%. O piso
        // da aula é R$ 1 (só cobre o custo fixo do Pix), então os R$ 3 valem de verdade.
        var r = NovoServico().CalcularRateio(100m, "Aula", "Somada", percentual: 3m);
        Assert.Equal(3m, r.Comissao);

        // E o piso da aula ainda segura cobrança de centavos: 3% de R$ 20 = R$ 0,60 → R$ 1.
        var barata = NovoServico().CalcularRateio(20m, "Aula", "Somada", percentual: 3m);
        Assert.Equal(1m, barata.Comissao);
    }

    [Fact]
    public void Modo_Descontada_nunca_deixa_repasse_negativo()
    {
        // Preço menor que a comissão mínima: comissão vira o próprio preço, repasse zera.
        var r = NovoServico().CalcularRateio(3m, "Torneio", "Descontada");
        Assert.Equal(3m, r.Comissao);
        Assert.Equal(0m, r.ValorRepasse);
        Assert.True(r.ValorRepasse >= 0);
    }

    [Fact]
    public void Tipo_desconhecido_usa_percentual_padrao()
    {
        var r = NovoServico().CalcularRateio(100m, "Assinatura");
        Assert.Equal(10m, r.Comissao); // ComissaoPercentualPadrao
    }

    [Fact]
    public void Tipo_com_caixa_diferente_ainda_acha_o_percentual()
    {
        var r = NovoServico().CalcularRateio(100m, "torneio");
        Assert.Equal(15m, r.Comissao);
    }

    [Fact]
    public void Modo_invalido_ou_vazio_cai_no_padrao_da_configuracao()
    {
        var r = NovoServico(modoPadrao: "Descontada").CalcularRateio(100m, "Aula", null);
        Assert.Equal(100m, r.ValorTotal);
        Assert.Equal(90m, r.ValorRepasse);
    }

    [Fact]
    public void Centavos_sao_arredondados_para_duas_casas()
    {
        // 15% de R$ 33,33 = R$ 4,9995 → R$ 5,00
        var r = NovoServico().CalcularRateio(33.33m, "Torneio", "Somada");
        Assert.Equal(5.00m, r.Comissao);
        Assert.Equal(38.33m, r.ValorTotal);
    }
}
