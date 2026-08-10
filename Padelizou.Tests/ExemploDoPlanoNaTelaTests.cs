using Padelizou.Services;

namespace Padelizou.Tests;

// O card "Na prática" da tela do plano do professor.
//
// ⚠️ Estes testes existem por causa de um card que passou meses dizendo o número certo com o
// DONO errado: "o Avulso paga R$ 200,00 de taxa" — sem dizer quem paga. No modo padrão
// ("Somada") quem paga é o ALUNO, e o professor recebe o valor cheio; aqueles R$ 200 nunca
// saíram do bolso dele. Quem lia achava que a mensalidade economizava R$ 90 do próprio
// dinheiro. Economizava do dinheiro do aluno.
public class ExemploDoPlanoNaTelaTests
{
    private static readonly PlanoProfessorSettings Plano = new();  // 49,90 · 3% × 10%
    private const string Padrao = "Somada";                        // AsaasSettings.ModoComissaoPadrao

    [Fact]
    public void No_modo_padrao_quem_paga_a_taxa_e_o_ALUNO()
    {
        var conta = ExemploDoPlanoNaTela.Montar(modoDoProfessor: null, Padrao, Plano);

        Assert.True(conta.TaxaSaiDoAluno);

        // Aula de R$ 100: o aluno paga 110 no Avulso e 103 no Assinante.
        Assert.Equal(110m, conta.AlunoPagaAvulso);
        Assert.Equal(103m, conta.AlunoPagaAssinante);

        // ⚠️ E do bolso do PROFESSOR não sai taxa nenhuma — só a mensalidade. É exatamente o
        // número que o card antigo errava: ele dizia R$ 200 de custo no Avulso.
        Assert.Equal(0m, conta.CustoMensalAvulso);
        Assert.Equal(49.90m, conta.CustoMensalAssinante);
    }

    [Fact]
    public void No_modo_descontada_a_taxa_sai_da_parte_do_professor()
    {
        var conta = ExemploDoPlanoNaTela.Montar("Descontada", Padrao, Plano);

        Assert.False(conta.TaxaSaiDoAluno);

        // O aluno paga o anunciado nos dois planos — o preço não muda pra ele.
        Assert.Equal(100m, conta.AlunoPagaAvulso);
        Assert.Equal(100m, conta.AlunoPagaAssinante);

        // 20 aulas de R$ 100: 10% = R$ 200 no Avulso; 49,90 + 3% = R$ 109,90 no Assinante.
        // É a única leitura em que a conta antiga estava certa.
        Assert.Equal(200m, conta.CustoMensalAvulso);
        Assert.Equal(109.90m, conta.CustoMensalAssinante);
    }

    [Fact]
    public void A_escolha_do_professor_vence_o_padrao_do_gateway()
    {
        // Ele escolheu "Somada" numa instalação cujo padrão é "Descontada": vale a dele.
        var dele = ExemploDoPlanoNaTela.Montar("Somada", "Descontada", Plano);
        Assert.True(dele.TaxaSaiDoAluno);

        // E quem nunca escolheu segue o padrão — seja ele qual for.
        var semEscolha = ExemploDoPlanoNaTela.Montar(null, "Descontada", Plano);
        Assert.False(semEscolha.TaxaSaiDoAluno);

        // Vazio é o mesmo que não ter escolhido: o campo é texto livre no banco.
        Assert.False(ExemploDoPlanoNaTela.Montar("   ", "Descontada", Plano).TaxaSaiDoAluno);

        // Caixa diferente não pode virar outro modo — "descontada" é "Descontada".
        Assert.False(ExemploDoPlanoNaTela.Montar("descontada", Padrao, Plano).TaxaSaiDoAluno);
    }

    [Fact]
    public void Os_numeros_seguem_a_tabela_de_precos_e_nao_ficam_chumbados()
    {
        // Preço de tabela muda por configuração; o card não pode continuar prometendo o antigo.
        var outra = new PlanoProfessorSettings
        {
            MensalidadeAssinante = 59.90m,
            PercentualAssinantePix = 4m,
            PercentualAvulso = 12m,
        };

        var somada = ExemploDoPlanoNaTela.Montar(null, Padrao, outra);
        Assert.Equal(112m, somada.AlunoPagaAvulso);
        Assert.Equal(104m, somada.AlunoPagaAssinante);
        Assert.Equal(59.90m, somada.CustoMensalAssinante);

        var descontada = ExemploDoPlanoNaTela.Montar("Descontada", Padrao, outra);
        Assert.Equal(240m, descontada.CustoMensalAvulso);              // 20 × 12
        Assert.Equal(59.90m + 80m, descontada.CustoMensalAssinante);   // 59,90 + 20 × 4
    }
}
