using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Os avisos do plano do professor — a régua E a varredura, no mesmo arquivo porque o que
// importa é o percurso: achar o professor certo, dizer a coisa certa no momento certo e não
// repetir.
//
// ⚠️ O defeito que isto cobre é MUDO, nos DOIS mundos: a taxa por aula sobe de 3% pra 10%
// sozinha quando o teste de 15 dias acaba sem escolha, e de novo quando a assinatura vence e a
// carência passa. Teste que passa sem testar nada aqui é pior que teste nenhum — por isso
// quase tudo abaixo confere o TEXTO que sai, não só a contagem.
public class AvisosDoPlanoDoProfessorTests
{
    private static readonly PlanoProfessorSettings Cfg = new(); // 3% × 10%, carência de 7 dias

    // Dez da manhã: hora civilizada, que é o único horário em que o varredor fala.
    private static DateTime AsDez(int dia) => new(2026, 9, dia, 10, 0, 0);

    private static readonly DateTime Vencimento = new(2026, 9, 20);

    private static Jogador Assinante(DbPadelContext ctx, DateTime? pagaAte, string? plano = null)
    {
        var professor = new Jogador
        {
            Nome = "Prof Teste",
            Cpf = "11144477735",
            IsProfessor = true,
            PlanoProfessor = plano ?? PlanoDoProfessor.Assinante,
            AssinaturaProfessorPagaAte = pagaAte,
        };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();
        return professor;
    }

    // Professor no TESTE: o relógio começou e ninguém pagou nada. `plano` nulo é quem ainda não
    // escolheu — o caso mais comum, e o que cai no Avulso calado quando o prazo passa.
    // O teste padrão do sistema tem 15 dias, então começar em 05/09 faz ele acabar em 20/09 —
    // a mesma data do `Vencimento`, pra as duas famílias de teste lerem o mesmo calendário.
    private static Jogador NoTeste(DbPadelContext ctx, string? plano = null)
    {
        var professor = new Jogador
        {
            Nome = "Prof em Teste",
            Cpf = "11144477735",
            IsProfessor = true,
            PlanoProfessor = plano,
            TesteProfessorInicio = new DateTime(2026, 9, 5),
            AssinaturaProfessorPagaAte = null,
        };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();
        return professor;
    }

    // O texto que saiu na última chamada — é o que prova que o aviso diz a coisa certa.
    private static string CorpoEnviado(IPushNotificationService push)
    {
        var chamada = push.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync));
        return (string)chamada.GetArguments()[2]!;
    }

    private static string TituloEnviado(IPushNotificationService push)
    {
        var chamada = push.ReceivedCalls()
            .Last(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync));
        return (string)chamada.GetArguments()[1]!;
    }

    private static Task<int> Varrer(DbPadelContext ctx, IPushNotificationService push, DateTime agora) =>
        AvisosDoPlanoDoProfessorBackgroundService.VarrerAsync(ctx, push, Cfg, agora);

    // ── Os três momentos ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cinco_dias_antes_avisa_que_vai_vencer()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(1, await Varrer(ctx, push, AsDez(15)));

        Assert.Equal(AvisosDoPlanoDoProfessor.VaiVencer,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        var corpo = CorpoEnviado(push);
        Assert.Contains("vence em 5 dias", corpo);
        Assert.Contains("20/09", corpo);
        // As duas porcentagens saem da configuração — a frase tem que contar as DUAS, senão
        // "renove" não diz o que se perde.
        Assert.Contains("3%", corpo);
        Assert.Contains("10%", corpo);
    }

    [Fact]
    public async Task Seis_dias_antes_ainda_e_cedo_demais()
    {
        using var ctx = TestInfra.NovoContexto();
        Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await Varrer(ctx, push, AsDez(14)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task No_dia_do_vencimento_ainda_e_VAI_vencer_e_nao_venceu()
    {
        // ⚠️ No dia, a assinatura VALE. Dizer "venceu" faria a pessoa achar que já perdeu a
        // taxa menor — e quem paga hoje não perdeu nada.
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        await Varrer(ctx, push, AsDez(20));

        Assert.Equal(AvisosDoPlanoDoProfessor.VaiVencer,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);
        Assert.Contains("vence hoje", CorpoEnviado(push));
    }

    [Fact]
    public async Task Na_carencia_avisa_que_a_taxa_menor_ainda_vale_e_ate_quando()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        // Venceu dia 20, carência de 7 dias: no dia 23 ele ainda paga 3%.
        await Varrer(ctx, push, AsDez(23));

        Assert.Equal(AvisosDoPlanoDoProfessor.VenceuNaCarencia,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        var corpo = CorpoEnviado(push);
        Assert.Contains("venceu em 20/09", corpo);
        Assert.Contains("ainda vale até 27/09", corpo);
    }

    [Fact]
    public async Task Quando_a_carencia_acaba_avisa_que_a_taxa_subiu()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        // Carência acaba dia 27; dia 28 a aula já custa 10%. É o aviso que este serviço
        // existe pra dar: sem ele, essa subida acontece sem uma linha em lugar nenhum.
        await Varrer(ctx, push, AsDez(28));

        Assert.Equal(AvisosDoPlanoDoProfessor.TaxaVoltouAoCheio,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        Assert.Contains("voltou ao cheio", TituloEnviado(push));
        var corpo = CorpoEnviado(push);
        Assert.Contains("voltou pros 10%", corpo);
        Assert.Contains("volta na hora", corpo);   // e como desfazer
    }

    // ── As travas contra virar spam ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_segunda_varredura_da_hora_seguinte_nao_avisa_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        await Varrer(ctx, push, AsDez(15));
        push.ClearReceivedCalls();

        Assert.Equal(0, await Varrer(ctx, push, AsDez(15).AddHours(1)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Vencimento_VELHO_nao_avisa_ninguem()
    {
        // ⚠️ O teste do dia do deploy. Quem largou a mensalidade meses atrás está tecnicamente
        // vencido; disparar "sua taxa subiu" pra todos eles seria uma rajada sobre algo que
        // aconteceu em maio — e a cota de e-mail já estourou duas vezes por rajada.
        using var ctx = TestInfra.NovoContexto();
        Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        // Carência acabou em 27/09; a janela do aviso são 15 dias, então em 13/10 já é assunto
        // encerrado.
        Assert.Equal(0, await Varrer(ctx, push, new DateTime(2026, 10, 13, 10, 0, 0)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Quem_ja_caiu_recebe_SO_o_ultimo_estagio_nao_os_tres_seguidos()
    {
        // O serviço subiu (ou voltou do ar) com a assinatura já caída: os três estágios estão
        // vencidos ao mesmo tempo. Mandar os três num tick é como se perde a permissão de
        // notificação de alguém.
        using var ctx = TestInfra.NovoContexto();
        Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(1, await Varrer(ctx, push, AsDez(29)));

        await push.Received(1).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        Assert.Contains("voltou pros 10%", CorpoEnviado(push));
    }

    [Fact]
    public async Task De_madrugada_nao_sai_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await Varrer(ctx, push, new DateTime(2026, 9, 15, 3, 0, 0)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    // ── O outro mundo: o teste de 15 dias ─────────────────────────────────────────────────

    [Fact]
    public async Task Cinco_dias_antes_do_fim_do_teste_avisa_que_ha_uma_escolha_a_fazer()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = NoTeste(ctx);   // nunca escolheu plano nenhum
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(1, await Varrer(ctx, push, AsDez(15)));

        Assert.Equal(AvisosDoPlanoDoProfessor.TesteAcabando,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        var corpo = CorpoEnviado(push);
        Assert.Contains("acaba em 5 dias", corpo);
        Assert.Contains("20/09", corpo);
        Assert.Contains("sem escolher um plano", corpo);
        Assert.Contains("10%", corpo);
    }

    [Fact]
    public async Task Quem_escolheu_assinante_e_nao_pagou_ouve_falar_da_MENSALIDADE_nao_da_escolha()
    {
        // ⚠️ Duas situações bem diferentes caem no mesmo estágio. Esta pessoa **já escolheu**:
        // mandar "escolha um plano" a faria procurar na tela um botão que não é o dela — o que
        // falta é a primeira mensalidade.
        using var ctx = TestInfra.NovoContexto();
        NoTeste(ctx, PlanoDoProfessor.Assinante);
        var push = Substitute.For<IPushNotificationService>();

        await Varrer(ctx, push, AsDez(15));

        var corpo = CorpoEnviado(push);
        Assert.Contains("primeira mensalidade ainda não entrou", corpo);
        Assert.DoesNotContain("escolher um plano", corpo);
    }

    [Fact]
    public async Task Quando_o_teste_acaba_o_aviso_diz_em_que_plano_a_pessoa_caiu()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = NoTeste(ctx);
        var push = Substitute.For<IPushNotificationService>();

        // Teste acaba em 20/09; no dia 21 já é taxa cheia. ⚠️ Sem carência: essa tolerância de
        // 7 dias é da assinatura, não do teste.
        await Varrer(ctx, push, AsDez(21));

        Assert.Equal(AvisosDoPlanoDoProfessor.TesteAcabou,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        var corpo = CorpoEnviado(push);
        Assert.Contains("você está no Avulso", corpo);
        Assert.Contains("Vire Assinante", corpo);
    }

    [Fact]
    public async Task No_ultimo_dia_o_teste_ainda_esta_ACABANDO_e_nao_acabou()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = NoTeste(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await Varrer(ctx, push, AsDez(20));

        Assert.Equal(AvisosDoPlanoDoProfessor.TesteAcabando,
            ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);
        Assert.Contains("acaba hoje", CorpoEnviado(push));
    }

    [Fact]
    public async Task Teste_que_acabou_ha_muito_tempo_nao_avisa_ninguem()
    {
        // A mesma janela do outro mundo, pelo mesmo motivo: no dia do deploy, todo professor
        // que abandonou o teste meses atrás está tecnicamente vencido.
        using var ctx = TestInfra.NovoContexto();
        NoTeste(ctx);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await Varrer(ctx, push, new DateTime(2026, 10, 6, 10, 0, 0)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Pagar_durante_o_teste_passa_a_bola_pro_mundo_da_ASSINATURA()
    {
        // ⚠️ A costura entre os dois mundos: enquanto `PagaAte` é nulo manda o teste; a partir
        // do primeiro pagamento manda a assinatura. Com os dois falando ao mesmo tempo, o
        // professor levaria dois avisos no mesmo tick.
        using var ctx = TestInfra.NovoContexto();
        var prof = NoTeste(ctx, PlanoDoProfessor.Assinante);
        var push = Substitute.For<IPushNotificationService>();

        // Ele paga: a vigência vai pra bem longe do fim do teste.
        prof.AssinaturaProfessorPagaAte = new DateTime(2026, 10, 20);
        await ctx.SaveChangesAsync();

        // O teste acabaria em 20/09 — e no dia 21 ninguém é avisado de nada, porque quem manda
        // agora é a assinatura, que só vence em outubro.
        Assert.Equal(0, await Varrer(ctx, push, AsDez(21)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);

        // E cinco dias antes do vencimento DELA o aviso volta, já com o texto da assinatura.
        Assert.Equal(1, await Varrer(ctx, push, new DateTime(2026, 10, 15, 10, 0, 0)));
        Assert.Contains("Seu plano Assinante vence em 5 dias", CorpoEnviado(push));
    }

    // ── Quem NÃO entra ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_escolheu_AVULSO_nao_recebe_nada_nos_dois_mundos()
    {
        using var ctx = TestInfra.NovoContexto();

        // ⚠️ O teste acabando não tira nada de quem já decidiu pagar a taxa cheia. Avisá-lo
        // seria cutucar quem resolveu — e cada aviso a mais é um motivo pra desligar o canal.
        NoTeste(ctx, PlanoDoProfessor.Avulso);
        Assinante(ctx, Vencimento, plano: PlanoDoProfessor.Avulso);

        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await Varrer(ctx, push, AsDez(15)));
        Assert.Equal(0, await Varrer(ctx, push, AsDez(21)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Quem_nunca_abriu_a_tela_do_plano_nao_tem_relogio_correndo()
    {
        // Sem `TesteProfessorInicio` não há teste: o relógio só começa na primeira visita —
        // e avisar quem nunca soube que existe um plano é falar de um prazo que não correu.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Nome = "Prof Novo", Cpf = "11144477735", IsProfessor = true,
        });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await Varrer(ctx, push, AsDez(15)));
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    // ── O ciclo recomeça quando o dinheiro entra ──────────────────────────────────────────

    [Fact]
    public async Task Pagar_zera_o_ciclo_e_o_vencimento_seguinte_avisa_de_novo()
    {
        // ⚠️ O teste que impede o "avisado uma vez na vida". Sem o zeramento no EfetivarAsync,
        // o estágio 1 ficaria gravado pra sempre e o vencimento do mês seguinte passaria calado.
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        await Varrer(ctx, push, AsDez(15));
        Assert.NotNull(ctx.Jogadores.Single(j => j.Id == prof.Id).UltimoLembreteDeAssinatura);

        // Ele renova. O pagamento passa pelo caminho de verdade.
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()), Options.Create(Cfg));

        var pagamento = new Pagamento
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = prof.Id,
            Valor = 49.90m,
            Comissao = 49.90m,
            MetodoPagamento = PixDireto.Metodo,
            DadosInscricao = $"{{\"ProfessorId\":{prof.Id},\"Ciclo\":\"Mensal\"}}",
            Status = "Confirmado",
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();
        await servico.EfetivarAsync(pagamento);

        var renovado = ctx.Jogadores.Single(j => j.Id == prof.Id);
        Assert.Null(renovado.UltimoLembreteDeAssinatura);

        // E cinco dias antes do NOVO vencimento ele é avisado outra vez.
        var novoVencimento = renovado.AssinaturaProfessorPagaAte!.Value;
        push.ClearReceivedCalls();

        Assert.Equal(1, await Varrer(ctx, push,
            novoVencimento.Date.AddDays(-5).AddHours(10)));
    }
}
