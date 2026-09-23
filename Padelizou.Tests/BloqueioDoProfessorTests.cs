using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A RÉGUA DO BLOQUEIO (item 2 do desenho aprovado em 22/09/2026). Quem não sustenta o plano
// para de agendar; a visualização do que já está marcado continua.
//
// ⚠️ "PODE AGENDAR" JÁ TINHA NOME: `PlanoDoProfessor.CondicoesDeAssinante` (em teste, assinante
// em dia ou cortesia). Com o Avulso fora de cartaz ela virou a definição inteira, e o bloqueio
// é o complemento dela MAIS UM PRAZO. Escrever uma segunda régua aqui seria a cópia que um dia
// discorda da primeira — o defeito que já custou a Mesa de Controle em 31/07.
//
// ⚠️ A DATA DE ESTREIA É A TRAVA QUE IMPEDE O DEPLOY DE BLOQUEAR A BASE INTEIRA NUM SEGUNDO.
// Sem ela, subir isto bloquearia, no primeiro tique, todo professor dos baldes "Avulsos" e "Sem
// escolha" do /Admin/Professores — gente que nunca foi avisada de nada. É o mesmo raciocínio do
// `JanelaDoAvisoDeQueda`: vencimento velho não vira cobrança nova. Nula = bloqueio DORMENTE, e
// é assim que este código sobe em produção sem mudar o comportamento de ninguém.
public class BloqueioDoProfessorTests
{
    private static readonly DateTime Agora = new(2026, 09, 22, 12, 0, 0);

    // Estreia bem no passado: tira a trava do caminho pra poder testar a régua em si.
    private static PlanoProfessorSettings Cfg(DateTime? estreia = null) => new()
    {
        BloqueioAPartirDe = estreia ?? new DateTime(2026, 01, 01),
    };

    private static Jogador Professor(
        string? plano = null, DateTime? testeInicio = null,
        DateTime? pagaAte = null, DateTime? cortesiaAte = null, DateTime? cortesiaEm = null) =>
        new()
        {
            Nome = "Prof", Cpf = "88800000000", IsProfessor = true,
            PlanoProfessor = plano,
            TesteProfessorInicio = testeInicio,
            AssinaturaProfessorPagaAte = pagaAte,
            CortesiaProfessorAte = cortesiaAte,
            CortesiaConcedidaEm = cortesiaEm,
        };

    // ── A INVARIANTE QUE VALE MAIS QUE TODAS AS OUTRAS ────────────────────────────────────

    [Fact]
    public void Quem_tem_condicoes_de_assinante_NUNCA_esta_bloqueado()
    {
        var cfg = Cfg();

        // ⚠️ Bloquear quem está pagando é o pior desfecho possível deste desenho: ele perde a
        // agenda no dia em que honrou o combinado. Por isso a checagem é feita nos três jeitos
        // de ter direito, e não só no mais comum.
        var emTeste = Professor(testeInicio: Agora.AddDays(-3));
        var emDia = Professor(PlanoDoProfessor.Assinante, pagaAte: Agora.AddDays(20));
        var cortesia = Professor(cortesiaAte: Agora.AddDays(5), cortesiaEm: Agora.AddDays(-60),
                                 pagaAte: Agora.AddYears(-1));   // mensalidade podre por baixo

        foreach (var p in new[] { emTeste, emDia, cortesia })
        {
            Assert.True(PlanoDoProfessor.CondicoesDeAssinante(p, Agora, cfg));
            Assert.False(BloqueioDoProfessor.EstaBloqueado(p, Agora, cfg));
        }
    }

    // ── Quem nunca pagou: o teste acaba e a agenda fecha ──────────────────────────────────

    [Fact]
    public void Teste_vencido_sem_nunca_ter_pago_bloqueia_no_dia_seguinte_as_10h()
    {
        // Teste começou em 01/09 → acaba 16/09 (15 dias). Nunca pagou, nunca teve cortesia.
        var novato = Professor(testeInicio: new DateTime(2026, 09, 01, 14, 30, 0));

        // ⚠️ NO DIA SEGUINTE, E NÃO NA HORA EXATA DO FIM. O teste acaba às 14h30 de 16/09;
        // bloquear às 10h daquele mesmo dia fecharia a agenda de alguém que AINDA ESTÁ em
        // teste — `CondicoesDeAssinante` ainda diz sim às 10h. O dia seguinte é o primeiro
        // instante em que as duas coisas concordam.
        Assert.Equal(new DateTime(2026, 09, 17, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(novato, Cfg()));
        Assert.True(BloqueioDoProfessor.EstaBloqueado(novato, Agora, Cfg()));
    }

    [Fact]
    public void Dentro_do_teste_nao_bloqueia_nem_no_ultimo_dia()
    {
        var novato = Professor(testeInicio: Agora.AddDays(-15).AddHours(2));   // acaba daqui a 2h

        Assert.True(PlanoDoProfessor.EmTeste(novato, Agora, Cfg()));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(novato, Agora, Cfg()));
    }

    [Fact]
    public void Quem_nunca_abriu_o_plano_nao_tem_relogio_e_nao_bloqueia()
    {
        // ⚠️ Sem `TesteProfessorInicio` o relógio nunca começou — não dá pra bloquear alguém
        // por um prazo que nunca correu. E um `!.Value` descuidado aqui estouraria dentro do
        // filtro, derrubando toda requisição de professor novo.
        var ninguem = Professor();

        Assert.Null(BloqueioDoProfessor.BloqueiaEm(ninguem, Cfg()));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(ninguem, Agora, Cfg()));
    }

    // ── Quem já pagou: os 10 dias de prazo ────────────────────────────────────────────────

    [Fact]
    public void Entre_o_fim_da_carencia_e_o_decimo_dia_ele_paga_10_por_cento_e_AINDA_agenda()
    {
        // ⚠️ O PRAZO DO BLOQUEIO É OUTRO, E MAIOR, QUE O DA CARÊNCIA — e esta é a janela que
        // prova os dois eixos separados. Venceu em 14/09: a carência (7) acabou em 21/09, então
        // hoje (22/09) ele JÁ paga taxa cheia; o bloqueio (10) só cai em 24/09. Sobram três
        // dias em que ele custa 10% e continua marcando aula.
        //
        // Colar os dois números faria a porta bater no mesmo instante em que a taxa sobe — e o
        // Felipe pediu explicitamente um prazo pra quem já foi cliente: "coloca o aviso de 10
        // dias, se ele nao pagar, bloqueia tambem".
        var atrasado = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2026, 09, 14));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmAtraso,
            PlanoDoProfessor.SituacaoDe(atrasado, Agora, Cfg()));
        Assert.False(PlanoDoProfessor.CondicoesDeAssinante(atrasado, Agora, Cfg()));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(atrasado, Agora, Cfg()));
        Assert.Equal(new DateTime(2026, 09, 24, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(atrasado, Cfg()));
    }

    [Fact]
    public void Dentro_da_carencia_ele_nem_chega_perto_do_bloqueio()
    {
        // Venceu em 17/09, carência até 24/09: hoje ele ainda é "Assinante em dia" e a
        // invariante do topo já o protege — o bloqueio nem precisa ser consultado.
        var naCarencia = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2026, 09, 17));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia,
            PlanoDoProfessor.SituacaoDe(naCarencia, Agora, Cfg()));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(naCarencia, Agora, Cfg()));
    }

    [Fact]
    public void Assinatura_vencida_ha_mais_de_dez_dias_bloqueia()
    {
        var largado = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2026, 09, 10));

        Assert.Equal(new DateTime(2026, 09, 20, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(largado, Cfg()));
        Assert.True(BloqueioDoProfessor.EstaBloqueado(largado, Agora, Cfg()));
    }

    [Fact]
    public void Quem_teve_cortesia_ganha_o_mesmo_prazo_de_quem_pagou()
    {
        // Permuta que acabou em 15/09: ele foi cliente de verdade, e porta na cara no dia
        // seguinte seria o pior jeito de tratar quem pagou com trabalho.
        var permuta = Professor(cortesiaAte: new DateTime(2026, 09, 15),
                                cortesiaEm: new DateTime(2026, 03, 01),
                                testeInicio: new DateTime(2026, 02, 01));

        Assert.False(BloqueioDoProfessor.EstaBloqueado(permuta, Agora, Cfg()));
        Assert.Equal(new DateTime(2026, 09, 25, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(permuta, Cfg()));
    }

    [Fact]
    public void Vale_o_direito_que_durou_mais_e_nao_o_ultimo_campo_lido()
    {
        // Mensalidade até 30/09 e cortesia que acabou em 10/09: quem manda é a data MAIOR.
        // Olhar só a cortesia bloquearia quem tem mensalidade paga e viva.
        var os_dois = Professor(PlanoDoProfessor.Assinante,
                                pagaAte: new DateTime(2026, 09, 30),
                                cortesiaAte: new DateTime(2026, 09, 10),
                                cortesiaEm: new DateTime(2026, 08, 01));

        Assert.Equal(new DateTime(2026, 10, 10, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(os_dois, Cfg()));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(os_dois, Agora, Cfg()));
    }

    // ── A trava de estreia ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_data_de_estreia_o_bloqueio_esta_DORMENTE()
    {
        var largado = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2025, 01, 01));
        var dormente = new PlanoProfessorSettings { BloqueioAPartirDe = null };

        // ⚠️ É ASSIM QUE ISTO SOBE EM PRODUÇÃO: código no ar, comportamento inalterado. Ligar
        // o bloqueio vira uma linha de configuração, depois de os avisos do item 3 terem rodado.
        Assert.Null(BloqueioDoProfessor.BloqueiaEm(largado, dormente));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(largado, Agora, dormente));
    }

    [Fact]
    public void Ninguem_bloqueia_antes_da_estreia_por_mais_velho_que_seja_o_vencimento()
    {
        // O Avulso legado e o "teste vencido" de meses atrás: pela régua pura os dois já
        // estariam bloqueados há muito tempo. A estreia é o que dá aviso a eles.
        var avulsoLegado = Professor(PlanoDoProfessor.Avulso, testeInicio: new DateTime(2026, 01, 10));
        var semEscolha = Professor(testeInicio: new DateTime(2026, 02, 10));

        var estreia = new DateTime(2026, 10, 06);
        var cfg = Cfg(estreia);

        foreach (var p in new[] { avulsoLegado, semEscolha })
        {
            Assert.Equal(estreia.AddHours(10), BloqueioDoProfessor.BloqueiaEm(p, cfg));
            Assert.False(BloqueioDoProfessor.EstaBloqueado(p, Agora, cfg));
            Assert.True(BloqueioDoProfessor.EstaBloqueado(p, estreia.AddHours(10), cfg));
        }
    }

    [Fact]
    public void A_estreia_e_PISO_e_nao_teto()
    {
        // Quem vence DEPOIS da estreia segue o próprio relógio — a trava não antecipa ninguém
        // nem empurra todo mundo pro mesmo dia.
        var vencePosEstreia = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2026, 11, 20));

        Assert.Equal(new DateTime(2026, 11, 30, 10, 0, 0),
            BloqueioDoProfessor.BloqueiaEm(vencePosEstreia, Cfg(new DateTime(2026, 10, 06))));
    }

    // ── A hora ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_bloqueio_cai_numa_hora_que_deixa_o_aviso_de_1_hora_ser_entregue()
    {
        // ⚠️ ISTO NÃO É ESTÉTICA. O Felipe pediu aviso "1 hora antes", e o varredor só manda
        // em HORA CIVILIZADA (9h–21h, LembreteDeInscricaoNaoPaga). Bloqueio às 10h põe o aviso
        // de 1 hora exatamente às 9h, que é `PrimeiraHora`. Qualquer hora menor que 10 torna
        // esse aviso impossível de entregar, e o item 3 nasceria quebrado.
        Assert.Equal(LembreteDeInscricaoNaoPaga.PrimeiraHora + 1, BloqueioDoProfessor.HoraDoBloqueio);

        var largado = Professor(PlanoDoProfessor.Assinante, pagaAte: new DateTime(2026, 09, 10));
        Assert.Equal(BloqueioDoProfessor.HoraDoBloqueio, BloqueioDoProfessor.BloqueiaEm(largado, Cfg())!.Value.Hour);
    }
}
