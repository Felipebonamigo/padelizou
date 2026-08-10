using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O aviso de assinatura vencendo — a régua E a varredura, no mesmo arquivo porque a régua aqui
// é pequena e o que importa é o percurso: achar o assinante certo, dizer a coisa certa no
// momento certo e não repetir.
//
// ⚠️ O defeito que isto cobre é MUDO: sem aviso, a assinatura vence, a carência passa e a taxa
// das aulas sobe de 3% pra 10% sem uma linha em lugar nenhum. Teste que passa sem testar nada
// aqui é pior que teste nenhum — por isso quase tudo abaixo confere o TEXTO que sai, não só a
// contagem.
public class LembreteDeAssinaturaVencendoTests
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
        LembreteAssinaturaVencendoBackgroundService.VarrerAsync(ctx, push, Cfg, agora);

    // ── Os três momentos ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cinco_dias_antes_avisa_que_vai_vencer()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Assinante(ctx, Vencimento);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(1, await Varrer(ctx, push, AsDez(15)));

        Assert.Equal(LembreteDeAssinaturaVencendo.VaiVencer,
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

        Assert.Equal(LembreteDeAssinaturaVencendo.VaiVencer,
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

        Assert.Equal(LembreteDeAssinaturaVencendo.VenceuNaCarencia,
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

        Assert.Equal(LembreteDeAssinaturaVencendo.TaxaVoltouAoCheio,
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

    // ── Quem NÃO entra ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_esta_no_teste_ou_escolheu_avulso_nao_recebe_nada()
    {
        using var ctx = TestInfra.NovoContexto();

        // Em teste: escolheu Assinante mas nunca pagou — não há vencimento a lembrar, e o
        // fim do teste é outro aviso, com outro texto.
        Assinante(ctx, pagaAte: null);
        // Avulso: não tem assinatura nenhuma.
        Assinante(ctx, Vencimento, plano: PlanoDoProfessor.Avulso);

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
