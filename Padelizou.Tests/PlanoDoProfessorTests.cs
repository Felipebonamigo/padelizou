using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O plano do professor decide quanto CADA aula paga de taxa — errar aqui é cobrar errado em
// silêncio, do professor ou do aluno. As datas são tudo: teste de 15 dias, mensalidade de
// 1 mês, carência de 7 dias.
public class PlanoDoProfessorTests
{
    private static readonly PlanoProfessorSettings Cfg = new(); // 49,90 + 3%/6% vs 10%, 15d teste, 7d carência
    private static readonly DateTime Agora = new(2026, 07, 29, 12, 0, 0);

    private static Jogador Professor(string? plano = null, DateTime? testeInicio = null, DateTime? pagaAte = null) =>
        new()
        {
            Nome = "Prof Teste",
            Cpf = "00000000000",
            IsProfessor = true,
            PlanoProfessor = plano,
            TesteProfessorInicio = testeInicio,
            AssinaturaProfessorPagaAte = pagaAte,
        };

    [Fact]
    public void Os_15_dias_de_teste_dao_condicoes_de_assinante_sem_mensalidade()
    {
        var prof = Professor(testeInicio: Agora.AddDays(-10));

        Assert.Equal(PlanoDoProfessor.Situacao.EmTeste, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));

        // Aula no Pix durante o teste: 3%, cobrança travada em Pix.
        var pix = PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaPix, Agora, Cfg);
        Assert.Equal("PIX", pix.BillingType);
        Assert.Equal(3m, pix.Percentual);
    }

    [Fact]
    public void Teste_vencido_sem_escolha_vira_avulso()
    {
        var prof = Professor(testeInicio: Agora.AddDays(-16));

        Assert.Equal(PlanoDoProfessor.Situacao.TesteVencidoSemEscolha, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));

        // E a aula volta pra taxa cheia com a forma aberta — sem escolha não há travamento.
        var c = PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaPix, Agora, Cfg);
        Assert.Equal("UNDEFINED", c.BillingType);
        Assert.Equal(10m, c.Percentual);
    }

    [Fact]
    public void Assinante_em_dia_paga_3_no_pix_e_6_no_cartao()
    {
        var prof = Professor(PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(20));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));

        Assert.Equal(3m, PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaPix, Agora, Cfg).Percentual);
        Assert.Equal(3m, PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaBoleto, Agora, Cfg).Percentual);

        var cartao = PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaCartao, Agora, Cfg);
        Assert.Equal("CREDIT_CARD", cartao.BillingType);
        Assert.Equal(6m, cartao.Percentual);
    }

    [Fact]
    public void Escolha_ausente_do_aluno_cai_na_taxa_de_cartao_com_forma_aberta()
    {
        // Formulário velho em cache: errar pra cá nunca cobra do professor a menos.
        var prof = Professor(PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(20));

        var c = PlanoDoProfessor.CobrancaDaAula(prof, null, Agora, Cfg);
        Assert.Equal("UNDEFINED", c.BillingType);
        Assert.Equal(6m, c.Percentual);
    }

    [Fact]
    public void Atraso_dentro_da_carencia_ainda_e_em_dia_depois_vira_atraso()
    {
        // Venceu há 5 dias (carência é 7): segue com as condições.
        var naCarencia = Professor(PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(-5));
        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia, PlanoDoProfessor.SituacaoDe(naCarencia, Agora, Cfg));

        // Venceu há 8: caiu. E a aula volta pra 10% na hora — a trava é automática, não
        // depende de ninguém lembrar de "desligar" o assinante.
        var atrasado = Professor(PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(-8));
        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmAtraso, PlanoDoProfessor.SituacaoDe(atrasado, Agora, Cfg));
        Assert.Equal(10m, PlanoDoProfessor.CobrancaDaAula(atrasado, CobrancaDoTorneio.EscolhaPix, Agora, Cfg).Percentual);
    }

    [Fact]
    public void Assinou_durante_o_teste_e_o_teste_segura_ate_o_fim()
    {
        // Assinou no dia 3 do teste e ainda não pagou nada: não está "em atraso" — o teste
        // cobre até o 15º dia.
        var prof = Professor(PlanoDoProfessor.Assinante, testeInicio: Agora.AddDays(-3));
        Assert.Equal(PlanoDoProfessor.Situacao.EmTeste, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));
    }

    [Fact]
    public void Mensalidade_estende_do_fim_da_vigencia_ou_de_hoje_o_que_for_maior()
    {
        // Pagou adiantado: soma no fim da vigência.
        Assert.Equal(Agora.AddDays(10).AddMonths(1),
            PlanoDoProfessor.NovaDataPagaAte(Agora.AddDays(10), Agora));

        // Pagou atrasado: conta de hoje — atraso não vira crédito.
        Assert.Equal(Agora.AddMonths(1),
            PlanoDoProfessor.NovaDataPagaAte(Agora.AddDays(-10), Agora));

        // Primeira mensalidade.
        Assert.Equal(Agora.AddMonths(1), PlanoDoProfessor.NovaDataPagaAte(null, Agora));
    }

    [Fact]
    public void Avulso_explicito_paga_taxa_cheia_mesmo_com_teste_correndo()
    {
        // Escolheu Avulso no dia 2: a escolha dele vale mais que o teste.
        var prof = Professor(PlanoDoProfessor.Avulso, testeInicio: Agora.AddDays(-2));
        Assert.Equal(PlanoDoProfessor.Situacao.Avulso, PlanoDoProfessor.SituacaoDe(prof, Agora, Cfg));
        Assert.Equal(10m, PlanoDoProfessor.CobrancaDaAula(prof, CobrancaDoTorneio.EscolhaPix, Agora, Cfg).Percentual);
    }
}
