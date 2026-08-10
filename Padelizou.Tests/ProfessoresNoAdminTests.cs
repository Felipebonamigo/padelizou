using Padelizou.Models;
using Padelizou.Services;
using Aula = padelizou.Models.Aula;

namespace Padelizou.Tests;

// A tela /Admin/Professores.
//
// ⚠️ Quase todo número dela é uma DEFINIÇÃO, não um dado guardado: "ativo" não existe no banco,
// "já pagou" é consulta em outra tabela, "em atraso" é regra do plano. Definição errada aqui
// não quebra nada — só devolve um número plausível e errado, que é o pior tipo, porque vai ser
// lido pra decidir preço.
public class ProfessoresNoAdminTests
{
    private static readonly PlanoProfessorSettings Cfg = new();      // 49,90 · carência 7 dias
    private static readonly DateTime Agora = new(2026, 9, 20, 10, 0, 0);

    private static Jogador Professor(int id, string nome, string? plano = null,
        DateTime? pagaAte = null, DateTime? testeInicio = null) =>
        new()
        {
            Id = id,
            Nome = nome,
            Cpf = "11144477735",
            IsProfessor = true,
            PlanoProfessor = plano,
            AssinaturaProfessorPagaAte = pagaAte,
            TesteProfessorInicio = testeInicio,
            CriadoEm = new DateTime(2026, 7, 1),
        };

    private static Pagamento Assinatura(int jogadorId, decimal valor, DateTime confirmadoEm) =>
        new()
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = jogadorId,
            Valor = valor,
            Comissao = valor,
            Status = "Confirmado",
            ConfirmadoEm = confirmadoEm,
            CriadoEm = confirmadoEm,
        };

    private static Aula UmaAula(int professorId, DateTime quando, string status = "Realizada") =>
        new() { ProfessorId = professorId, DataHora = quando, Status = status };

    // ── "Ativo" ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ativo_olha_pros_dois_lados_da_janela()
    {
        // ⚠️ A janela é simétrica de propósito. Só olhando pro passado, quem já fechou a agenda
        // do mês que vem apareceria como sumido; só pro futuro, quem trabalhou o mês inteiro e
        // ainda não remarcou também.
        var professores = new[]
        {
            Professor(1, "Deu aula semana passada"),
            Professor(2, "Tem aula semana que vem"),
            Professor(3, "Ultima aula ha 3 meses"),
            Professor(4, "Nunca deu aula"),
        };

        var aulas = new[]
        {
            UmaAula(1, Agora.AddDays(-7)),
            UmaAula(2, Agora.AddDays(7), "Confirmada"),
            UmaAula(3, Agora.AddDays(-90)),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, Array.Empty<Pagamento>(), aulas, Agora, Cfg);
        var resumo = ProfessoresNoAdmin.Resumir(linhas, Array.Empty<Padelizou.ViewModels.MesDeAssinatura>(), Agora);

        Assert.Equal(4, resumo.Cadastrados);
        Assert.Equal(2, resumo.Ativos);

        // E o total de aulas conta a vida inteira, não só a janela — as duas colunas respondem
        // perguntas diferentes.
        Assert.Equal(1, linhas.Single(p => p.Id == 3).AulasNoTotal);
        Assert.Equal(0, linhas.Single(p => p.Id == 3).AulasNosUltimos30Dias);
    }

    [Fact]
    public void Aula_cancelada_ou_recusada_nao_e_movimento()
    {
        var professores = new[] { Professor(1, "So cancelou") };
        var aulas = new[]
        {
            UmaAula(1, Agora.AddDays(-2), "Cancelada"),
            UmaAula(1, Agora.AddDays(-1), "Recusada"),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, Array.Empty<Pagamento>(), aulas, Agora, Cfg);

        Assert.Equal(0, linhas.Single().AulasNoTotal);
        Assert.Equal(0, linhas.Single().AulasNosUltimos30Dias);
        Assert.Null(linhas.Single().UltimaAulaEm);
    }

    // ── Dinheiro ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ja_pagou_alguma_vez_conta_QUEM_PAROU_de_pagar_tambem()
    {
        // ⚠️ É uma pergunta diferente de "é assinante hoje", e é justamente o cliente perdido
        // que não aparece em nenhuma outra contagem da tela.
        var professores = new[]
        {
            Professor(1, "Pagou e parou", PlanoDoProfessor.Avulso),
            Professor(2, "Assinante em dia", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(10)),
            Professor(3, "Nunca pagou"),
        };

        var pagos = new[]
        {
            Assinatura(1, 49.90m, new DateTime(2026, 7, 10)),
            Assinatura(1, 49.90m, new DateTime(2026, 8, 10)),
            Assinatura(2, 499.90m, new DateTime(2026, 9, 5)),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, pagos, Array.Empty<Aula>(), Agora, Cfg);
        var meses = ProfessoresNoAdmin.PorMes(pagos);
        var resumo = ProfessoresNoAdmin.Resumir(linhas, meses, Agora);

        Assert.Equal(2, resumo.JaPagaramAlgumaVez);
        Assert.Equal(1, resumo.AssinantesEmDia);

        // O total por professor soma tudo que ele já trouxe.
        Assert.Equal(99.80m, linhas.Single(p => p.Id == 1).TotalPago);
        Assert.Equal(2, linhas.Single(p => p.Id == 1).QuantidadeDePagamentos);
        Assert.Equal(new DateTime(2026, 8, 10), linhas.Single(p => p.Id == 1).UltimoPagamentoEm);

        Assert.Equal(599.70m, resumo.ReceitaTotal);
    }

    [Fact]
    public void Receita_do_mes_e_a_do_MES_e_nao_o_historico_de_quem_pagou_nele()
    {
        // ⚠️ O erro que este teste guarda: somar o `TotalPago` de quem pagou este mês traria o
        // histórico inteiro dele junto, e setembro apareceria com o dinheiro de julho dentro.
        var professores = new[] { Professor(1, "Paga todo mes", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(10)) };
        var pagos = new[]
        {
            Assinatura(1, 49.90m, new DateTime(2026, 7, 10)),
            Assinatura(1, 49.90m, new DateTime(2026, 8, 10)),
            Assinatura(1, 49.90m, new DateTime(2026, 9, 10)),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, pagos, Array.Empty<Aula>(), Agora, Cfg);
        var resumo = ProfessoresNoAdmin.Resumir(linhas, ProfessoresNoAdmin.PorMes(pagos), Agora);

        Assert.Equal(149.70m, resumo.ReceitaTotal);
        Assert.Equal(49.90m, resumo.ReceitaNoMes);   // e não 149,70
    }

    [Fact]
    public void Por_mes_conta_PESSOAS_distintas_e_nao_cobrancas()
    {
        // Duas mensalidades do mesmo professor no mesmo mês são um cliente, não dois — senão a
        // linha do mês diria que a base dobrou quando alguém só pagou atrasado e em dia junto.
        var pagos = new[]
        {
            Assinatura(1, 49.90m, new DateTime(2026, 9, 3)),
            Assinatura(1, 49.90m, new DateTime(2026, 9, 28)),
            Assinatura(2, 49.90m, new DateTime(2026, 9, 15)),
            Assinatura(3, 49.90m, new DateTime(2026, 8, 15)),
        };

        var meses = ProfessoresNoAdmin.PorMes(pagos);

        var setembro = meses.Single(m => m.Ano == 2026 && m.Mes == 9);
        Assert.Equal(2, setembro.Professores);
        Assert.Equal(3, setembro.Cobrancas);
        Assert.Equal(149.70m, setembro.Valor);

        // Mais recente primeiro: a tela é lida de cima.
        Assert.Equal(9, meses.First().Mes);
    }

    [Fact]
    public void O_anual_cai_inteiro_no_mes_em_que_foi_pago()
    {
        // Caixa, não competência — a mesma régua do /Admin/Financeiro. A tela avisa disso;
        // este teste garante que ela não está mentindo.
        var pagos = new[] { Assinatura(1, 499.90m, new DateTime(2026, 9, 5)) };

        var meses = ProfessoresNoAdmin.PorMes(pagos);

        Assert.Single(meses);
        Assert.Equal(499.90m, meses[0].Valor);
    }

    // ── Situação ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_situacao_vem_da_MESMA_regua_que_cobra_a_aula()
    {
        // Assinante com mensalidade vencida há 10 dias (carência é 7) já paga a taxa cheia —
        // a tela tem que dizer "em atraso", não "assinante", senão o admin acha que tem um
        // cliente que na prática não tem.
        var professores = new[]
        {
            Professor(1, "Em dia", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(10)),
            Professor(2, "Venceu ha 10 dias", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(-10)),
            Professor(3, "Na carencia", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(-3)),
            Professor(4, "Em teste", testeInicio: Agora.AddDays(-3)),
            Professor(5, "Avulso", PlanoDoProfessor.Avulso),
            Professor(6, "Teste vencido", testeInicio: Agora.AddDays(-40)),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, Array.Empty<Pagamento>(), Array.Empty<Aula>(), Agora, Cfg);
        var resumo = ProfessoresNoAdmin.Resumir(linhas, Array.Empty<Padelizou.ViewModels.MesDeAssinatura>(), Agora);

        Assert.Equal("Assinante em dia", linhas.Single(p => p.Id == 1).Situacao);
        Assert.Equal("Assinante em atraso", linhas.Single(p => p.Id == 2).Situacao);
        Assert.Equal("Assinante em dia", linhas.Single(p => p.Id == 3).Situacao);   // carência
        Assert.Equal("Em teste", linhas.Single(p => p.Id == 4).Situacao);
        Assert.Equal("Avulso", linhas.Single(p => p.Id == 5).Situacao);
        Assert.Equal("Teste vencido, sem escolha", linhas.Single(p => p.Id == 6).Situacao);

        Assert.Equal(2, resumo.AssinantesEmDia);
        Assert.Equal(1, resumo.AssinantesEmAtraso);
        Assert.Equal(1, resumo.EmTeste);
        Assert.Equal(1, resumo.Avulsos);
        Assert.Equal(1, resumo.SemEscolha);
    }

    [Fact]
    public void Quem_NUNCA_abriu_o_plano_nao_e_o_mesmo_que_desistiu_dele()
    {
        // ⚠️ Pra cobrar a aula os dois são iguais (taxa cheia), e é por isso que
        // `SituacaoDe` devolve TesteVencidoSemEscolha pros dois. Aqui a diferença é o funil:
        // um viu o plano e não escolheu, o outro nunca chegou na tela — e só o segundo se
        // resolve mostrando o plano pra ele. Sem esta separação, uma base de gente que nunca
        // abriu a tela aparece como uma fila de desistentes.
        var professores = new[]
        {
            Professor(1, "Nunca abriu"),                                    // sem relógio
            Professor(2, "Abriu e nao escolheu", testeInicio: Agora.AddDays(-40)),
        };

        var linhas = ProfessoresNoAdmin.Montar(professores, Array.Empty<Pagamento>(), Array.Empty<Aula>(), Agora, Cfg);
        var resumo = ProfessoresNoAdmin.Resumir(linhas, Array.Empty<Padelizou.ViewModels.MesDeAssinatura>(), Agora);

        Assert.Equal(ProfessoresNoAdmin.NuncaAbriuOPlano, linhas.Single(p => p.Id == 1).Situacao);
        Assert.Equal("Teste vencido, sem escolha", linhas.Single(p => p.Id == 2).Situacao);

        Assert.Equal(1, resumo.NuncaAbriramOPlano);
        Assert.Equal(1, resumo.SemEscolha);

        // ⚠️ E quem o admin pagou à mão sem ele nunca ter aberto a tela NÃO cai aqui: tem
        // assinatura, então é assinante — o rótulo é sobre não ter chegado ao plano.
        var pagoPeloAdmin = new[] { Professor(3, "Pago a mao", PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(20)) };
        var so = ProfessoresNoAdmin.Montar(pagoPeloAdmin, Array.Empty<Pagamento>(), Array.Empty<Aula>(), Agora, Cfg).Single();
        Assert.Equal("Assinante em dia", so.Situacao);
    }

    [Fact]
    public void A_ordem_poe_na_frente_quem_sustenta_a_conta()
    {
        var professores = new[]
        {
            Professor(1, "Aaa nunca pagou"),
            Professor(2, "Zzz pagou muito"),
            Professor(3, "Mmm so da aula"),
        };

        var pagos = new[] { Assinatura(2, 499.90m, new DateTime(2026, 9, 5)) };
        var aulas = new[] { UmaAula(3, Agora.AddDays(-1)) };

        var linhas = ProfessoresNoAdmin.Montar(professores, pagos, aulas, Agora, Cfg);

        // Quem paga, depois quem move aula, depois o resto — ordem alfabética pura enterraria
        // o assinante no meio de quem só criou conta.
        Assert.Equal(new[] { 2, 3, 1 }, linhas.Select(p => p.Id).ToArray());
    }
}
