using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O caixa do Padelizou (/Admin/Financeiro): a separação por fora × gateway × despesas, e as
// regras do registro manual. A conta é dinheiro — testa-se com centavos, nunca só redondo.
public class FinanceiroDoPadelizouTests
{
    private static Pagamento Confirmado(string metodo, decimal comissao, decimal valor = 0m) => new()
    {
        Tipo = "AssinaturaProfessor",
        JogadorId = 1,
        Status = "Confirmado",
        MetodoPagamento = metodo,
        Valor = valor == 0m ? comissao : valor,
        Comissao = comissao,
    };

    [Fact]
    public void Soma_separa_por_fora_de_gateway_e_desconta_despesa()
    {
        var pagamentos = new[]
        {
            Confirmado(PixDireto.Metodo, 49.90m),
            Confirmado(FinanceiroDoPadelizou.MetodoManual, 33.33m),
            // Split de torneio: o valor cheio tem repasse, só a comissão é nossa.
            Confirmado("Gateway", comissao: 22.50m, valor: 150.00m),
        };
        var despesas = new[]
        {
            new DespesaRegistrada { Descricao = "Fatura do gateway", Valor = 12.34m, Data = DateTime.Now },
        };

        var resumo = FinanceiroDoPadelizou.Somar(pagamentos, despesas);

        Assert.Equal(83.23m, resumo.RecebidoPorFora);       // 49,90 + 33,33
        Assert.Equal(22.50m, resumo.RecebidoPeloGateway);   // a comissão, nunca os 150
        Assert.Equal(12.34m, resumo.Despesas);
        Assert.Equal(105.73m, resumo.TotalRecebido);
        Assert.Equal(93.39m, resumo.Liquido);
        Assert.Equal(2, resumo.QtdPorFora);
        Assert.Equal(1, resumo.QtdPeloGateway);
    }

    [Fact]
    public void Pendente_e_cancelado_nao_sao_caixa()
    {
        var pagamentos = new[]
        {
            new Pagamento { Tipo = "AssinaturaProfessor", JogadorId = 1, Status = "Pendente",
                MetodoPagamento = PixDireto.Metodo, Valor = 49.90m, Comissao = 49.90m },
            new Pagamento { Tipo = "AssinaturaProfessor", JogadorId = 1, Status = "Cancelado",
                MetodoPagamento = "Gateway", Valor = 49.90m, Comissao = 49.90m },
        };

        var resumo = FinanceiroDoPadelizou.Somar(pagamentos, Array.Empty<DespesaRegistrada>());

        Assert.Equal(0m, resumo.TotalRecebido);
        Assert.Equal(0, resumo.QtdPorFora + resumo.QtdPeloGateway);
    }

    // ── As regras do registro manual ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("AssinaturaProfessor", true)]
    [InlineData("TaxaTorneio", true)]
    [InlineData("TorneioDupla", false)]  // repasse a terceiro: registrar seria receita que mente
    [InlineData("Aula", false)]
    [InlineData(null, false)]
    public void So_o_que_e_nosso_pode_ser_registrado(string? tipo, bool aceita)
    {
        var problema = FinanceiroDoPadelizou.ProblemaParaRegistrar(tipo, 10m);
        Assert.Equal(aceita, problema == null);
    }

    [Fact]
    public void Valor_zerado_ou_negativo_e_recusado()
    {
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaParaRegistrar("AssinaturaProfessor", 0m));
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaParaRegistrar("AssinaturaProfessor", -5m));
    }

    [Fact]
    public void Mensalidade_manual_exige_professor()
    {
        var naoDaAula = new Jogador { Nome = "Zé", Cpf = "11144477735", IsProfessor = false };
        var professor = new Jogador { Nome = "Prof", Cpf = "11144477735", IsProfessor = true };

        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComOPagador("AssinaturaProfessor", null));
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComOPagador("AssinaturaProfessor", naoDaAula));
        Assert.Null(FinanceiroDoPadelizou.ProblemaComOPagador("AssinaturaProfessor", professor));
    }

    [Fact]
    public void Taxa_manual_so_pra_torneio_por_fora_com_taxa_em_aberto()
    {
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComOTorneio(null));

        // Torneio "pelo site": a taxa dele entra no split, registrar à mão duplicaria.
        var peloSite = new Torneio { Nome = "T", Codigo = "T1", FormaPagamento = "OnlinePix" };
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComOTorneio(peloSite));

        var jaPago = new Torneio { Nome = "T", Codigo = "T2", FormaPagamento = "Externo",
            TaxaExternoPagaEm = DateTime.Now };
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComOTorneio(jaPago));

        var emAberto = new Torneio { Nome = "T", Codigo = "T3", FormaPagamento = "Externo" };
        Assert.Null(FinanceiroDoPadelizou.ProblemaComOTorneio(emAberto));
    }

    [Fact]
    public void Despesa_exige_descricao_e_valor()
    {
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComADespesa(null, 10m));
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComADespesa("   ", 10m));
        Assert.NotNull(FinanceiroDoPadelizou.ProblemaComADespesa("Fatura", 0m));
        Assert.Null(FinanceiroDoPadelizou.ProblemaComADespesa("Fatura do gateway", 12.34m));
    }
}
