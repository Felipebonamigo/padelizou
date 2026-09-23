using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "ESSE Q ESTA VENCIDO O MES DO PAGAMENTO MAS AQUI AINDA CONSTA, NAO TA ERRADO?" (Felipe,
// 22/09/2026, com o print do Jonatas: **Pago até 18/09/2026** e selo VERDE "Assinante em dia").
//
// ⚠️ A RÉGUA ESTAVA CERTA E A TELA ESTAVA MENTINDO — que é a mentira de rótulo do `build-373`
// de novo. Os 7 dias de `DiasDeCarencia` seguram mesmo as condições de assinante depois do
// vencimento (existem pra não punir quem esqueceu o boleto no feriado), então em 22/09 o
// Jonatas É "em dia" pra quem cobra. Só que o selo verde fica colado na data 18/09, e a única
// explicação da tela mora em cinza no RODAPÉ da página — a uma rolagem de distância do que o
// olho pega primeiro.
//
// ⚠️ E ISTO NÃO É ENFEITE: é a tela em que o Felipe vai ler QUEM ESTÁ PRESTES A SER BLOQUEADO
// quando o item 2 subir. Um verde que quer dizer "vence em 3 dias" é exatamente a informação
// que ela não pode esconder.
//
// ⚠️ NÃO NASCEU `Situacao` NOVA de propósito. Acrescentar um valor ao enum arrastaria
// CondicoesDeAssinante, CobrancaDaAula, RotuloDaSituacao e os baldes do resumo — pra cobrança,
// carência É "em dia", e separar lá é como a carência viraria desconto. A pergunta daqui é
// outra ("isto vai cair em breve?"), e ela é de TELA.
public class CarenciaNaoSeDisfarcaDeEmDiaTests
{
    private static readonly PlanoProfessorSettings Cfg = new();   // DiasDeCarencia = 7
    private static readonly DateTime Agora = new(2026, 09, 22, 10, 0, 0);

    private static Jogador Assinante(DateTime? pagaAte) => new()
    {
        Nome = "Jonatas", Cpf = "88800000000", IsProfessor = true,
        PlanoProfessor = PlanoDoProfessor.Assinante,
        AssinaturaProfessorPagaAte = pagaAte,
    };

    [Fact]
    public void O_caso_do_print_continua_sendo_em_dia_e_agora_diz_ate_quando()
    {
        var jonatas = Assinante(new DateTime(2026, 09, 18));

        // O que não pode mudar: pra cobrança ele segue em dia, e a taxa dele segue a menor.
        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia,
            PlanoDoProfessor.SituacaoDe(jonatas, Agora, Cfg));
        Assert.True(PlanoDoProfessor.CondicoesDeAssinante(jonatas, Agora, Cfg));

        // O que passa a existir: a data em que esse verde acaba. 18/09 + 7 = 25/09.
        Assert.Equal(new DateTime(2026, 09, 25), PlanoDoProfessor.CarenciaAte(jonatas, Agora, Cfg));
    }

    [Fact]
    public void Quem_esta_em_dia_de_verdade_nao_aparece_em_carencia()
    {
        // Mensalidade ainda válida: não há nada pra avisar, e inventar uma data aqui encheria
        // a tela de aviso pra quem está certo — o jeito mais rápido de ninguém mais ler nenhum.
        var emDia = Assinante(new DateTime(2026, 10, 18));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia,
            PlanoDoProfessor.SituacaoDe(emDia, Agora, Cfg));
        Assert.Null(PlanoDoProfessor.CarenciaAte(emDia, Agora, Cfg));
    }

    [Fact]
    public void Passada_a_carencia_ele_ja_e_atraso_e_nao_carencia()
    {
        // 10/09 + 7 = 17/09, e hoje é 22/09: a carência ACABOU. Devolver data aqui faria a tela
        // prometer uma proteção que já não existe.
        var atrasado = Assinante(new DateTime(2026, 09, 10));

        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmAtraso,
            PlanoDoProfessor.SituacaoDe(atrasado, Agora, Cfg));
        Assert.Null(PlanoDoProfessor.CarenciaAte(atrasado, Agora, Cfg));
    }

    [Fact]
    public void No_dia_exato_do_vencimento_ainda_nao_e_carencia()
    {
        // ⚠️ "Pago até 22/09" vale o dia 22 INTEIRO — a mesma régua de dia de calendário que a
        // cortesia usa. Chamar isso de carência faria o selo mudar de cor no meio do dia em que
        // o professor ainda está pagando em dia.
        var hoje = Assinante(new DateTime(2026, 09, 22));

        Assert.Null(PlanoDoProfessor.CarenciaAte(hoje, Agora, Cfg));
    }

    [Fact]
    public void Quem_nunca_pagou_nao_tem_carencia_nenhuma()
    {
        // Em teste, sem pagamento: `AssinaturaProfessorPagaAte` é nulo e o `!.Value` de um
        // desenho descuidado estouraria aqui — dentro da montagem da tela do admin, derrubando
        // a página inteira por causa de uma linha.
        var novato = new Jogador
        {
            Nome = "Novata", Cpf = "88800000001", IsProfessor = true,
            TesteProfessorInicio = Agora.AddDays(-3),
        };

        Assert.Equal(PlanoDoProfessor.Situacao.EmTeste,
            PlanoDoProfessor.SituacaoDe(novato, Agora, Cfg));
        Assert.Null(PlanoDoProfessor.CarenciaAte(novato, Agora, Cfg));
    }

    [Fact]
    public void A_linha_do_admin_carrega_a_data_da_carencia()
    {
        var jonatas = Assinante(new DateTime(2026, 09, 18));

        var linhas = ProfessoresNoAdmin.Montar(
            new[] { jonatas }, Array.Empty<Pagamento>(), Array.Empty<padelizou.Models.Aula>(), Agora, Cfg);

        var linha = Assert.Single(linhas);
        Assert.Equal("Assinante em dia", linha.Situacao);
        Assert.Equal(new DateTime(2026, 09, 25), linha.CarenciaAte);
    }

    [Fact]
    public void A_tela_do_admin_mostra_a_carencia_no_lugar_do_verde_liso()
    {
        var tela = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Admin", "Professores.cshtml"));

        // ⚠️ O selo, e não só o rodapé. A explicação já existia lá embaixo em cinza e não
        // impediu a pergunta — o que o olho pega é a cor colada na data.
        Assert.Contains("CarenciaAte", tela);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
