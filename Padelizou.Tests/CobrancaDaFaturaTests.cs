using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Cobrar a conta do mês pelo app: a cobrança nasce na conta do Padelizou e o split manda a
// fatia do professor direto pra carteira dele, como em todo o resto do sistema.
//
// O que é diferente aqui: QUEM PAGA PODE NÃO TER CONTA NO PADELIZOU. É o caso normal — a mãe
// que paga a aula do filho não usa o app.
public class CobrancaDaFaturaTests
{
    private const string Carteira = "carteira-do-professor";
    private const string CpfDaMae = "52998224725";

    private static (DbPadelContext ctx, IAsaasService asaas, PagamentoInscricaoService servico, Jogador professor)
        Montar(bool comCarteira = true, string? plano = PlanoDoProfessor.Assinante)
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador
        {
            Nome = "Joao", Login = "joaop", Cpf = "99900000041", IsProfessor = true,
            AsaasWalletId = comCarteira ? Carteira : null,
            ReceberPagamentoOnline = comCarteira,
            PlanoProfessor = plano,
            AssinaturaProfessorPagaAte = DateTime.Today.AddMonths(1),
        };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.ObterOuCriarClienteAsync(default!, default!, default, default).ReturnsForAnyArgs("cli_1");
        asaas.CalcularRateio(default, default!, default, default)
            .ReturnsForAnyArgs(ci => new RateioComissao(ci.ArgAt<decimal>(0), ci.ArgAt<decimal>(0) * 0.94m,
                ci.ArgAt<decimal>(0) * 0.06m));
        asaas.CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!)
            .ReturnsForAnyArgs(new CobrancaAsaas("pay_1", "https://asaas/fatura/1"));

        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));

        return (ctx, asaas, servico, professor);
    }

    private static FaturaDoAluno Fatura(DbPadelContext ctx, Jogador professor, string? cpf = CpfDaMae,
        decimal valor = 880m)
    {
        var fatura = new FaturaDoAluno
        {
            ProfessorId = professor.Id, NomeAvulso = "Pedrinho",
            Ano = 2026, Mes = 4, Valor = valor, QuantidadeAulas = 8,
            PagadorNome = "Marcia", PagadorCpf = cpf, PagadorCelular = "51988881111",
            FechadaEm = DateTime.Now, Vencimento = new DateTime(2026, 5, 10),
            Status = FechamentoDoMes.Aberta,
        };
        ctx.FaturasDeAlunos.Add(fatura);
        ctx.SaveChanges();
        return fatura;
    }

    // ─── Emitir ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_cobranca_nasce_ligada_a_fatura_com_split_pro_professor()
    {
        var (ctx, asaas, servico, professor) = Montar();
        using var _ = ctx;
        var fatura = Fatura(ctx, professor);

        var link = await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);

        Assert.NotNull(link);
        var pagamento = await ctx.Pagamentos.SingleAsync();
        Assert.Equal("FaturaDeAula", pagamento.Tipo);
        Assert.Equal(professor.Id, pagamento.RecebedorId);
        Assert.Equal(880m, pagamento.Valor);
        Assert.Equal(pagamento.Id, (await ctx.FaturasDeAlunos.SingleAsync()).PagamentoId);

        // O split vai pra carteira DELE, e o vencimento é o que foi combinado no fechamento —
        // não "amanhã", que quebraria o combinado com quem paga.
        await asaas.Received(1).CriarCobrancaAsync(
            Arg.Any<string>(), Arg.Any<RateioComissao>(), Arg.Any<string>(), Arg.Any<string>(),
            new DateTime(2026, 5, 10), Carteira, Arg.Any<string>());
    }

    [Fact]
    public async Task Quem_paga_e_nao_tem_conta_entra_como_pre_cadastro_pelo_CPF()
    {
        // A mãe que paga a aula do filho não usa o app, e `Pagamento.JogadorId` exige um
        // Jogador. Mesmo caminho do inscrito que o organizador coloca no torneio.
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;
        var fatura = Fatura(ctx, professor);

        await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);

        var pagador = await ctx.Jogadores.SingleAsync(j => j.Cpf == CpfDaMae);
        Assert.Equal("Marcia", pagador.Nome);
        Assert.True(pagador.EhPreCadastro);
        Assert.Equal(pagador.Id, (await ctx.Pagamentos.SingleAsync()).JogadorId);
    }

    [Fact]
    public async Task CPF_que_ja_tem_conta_NAO_vira_uma_segunda()
    {
        // É o que impede duas contas da mesma pessoa quando o pai paga por dois filhos, em
        // professores diferentes.
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;

        var jaExiste = new Jogador { Nome = "Marcia Silva", Login = "marcia", Cpf = CpfDaMae };
        ctx.Jogadores.Add(jaExiste);
        await ctx.SaveChangesAsync();

        await servico.IniciarCobrancaDaFaturaAsync(Fatura(ctx, professor), professor);

        Assert.Equal(1, await ctx.Jogadores.CountAsync(j => j.Cpf == CpfDaMae));
        Assert.Equal(jaExiste.Id, (await ctx.Pagamentos.SingleAsync()).JogadorId);
        // E o nome dela não é reescrito pelo apelido que o professor anotou.
        Assert.Equal("Marcia Silva", (await ctx.Jogadores.SingleAsync(j => j.Cpf == CpfDaMae)).Nome);
    }

    [Fact]
    public async Task Sem_CPF_do_pagador_nao_emite_nada()
    {
        // O gateway recusa cliente sem documento. Parar aqui deixa o professor acertar por
        // fora, em vez de um erro sem explicação.
        var (ctx, asaas, servico, professor) = Montar();
        using var _ = ctx;

        Assert.Null(await servico.IniciarCobrancaDaFaturaAsync(Fatura(ctx, professor, cpf: null), professor));
        Assert.Empty(await ctx.Pagamentos.ToListAsync());
        await asaas.DidNotReceiveWithAnyArgs().CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!);
    }

    [Fact]
    public async Task CPF_com_digito_errado_nao_vira_pre_cadastro()
    {
        // "11111111111" tem 11 dígitos e passa por qualquer filtro que só conte números — mas
        // é CPF inválido. Sem conferir o dígito verificador, nasceria uma conta fantasma com
        // documento falso no banco, e a cobrança só morreria lá no gateway.
        var (ctx, asaas, servico, professor) = Montar();
        using var _ = ctx;

        Assert.Null(await servico.IniciarCobrancaDaFaturaAsync(
            Fatura(ctx, professor, cpf: "11111111111"), professor));

        Assert.Empty(await ctx.Pagamentos.ToListAsync());
        Assert.Empty(await ctx.Jogadores.Where(j => j.Cpf == "11111111111").ToListAsync());
        await asaas.DidNotReceiveWithAnyArgs().CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!);
    }

    [Fact]
    public async Task Professor_sem_conta_de_recebimento_nao_emite()
    {
        var (ctx, _, servico, professor) = Montar(comCarteira: false);
        using var _d = ctx;

        Assert.Null(await servico.IniciarCobrancaDaFaturaAsync(Fatura(ctx, professor), professor));
        Assert.Empty(await ctx.Pagamentos.ToListAsync());
    }

    [Fact]
    public async Task Emitir_duas_vezes_devolve_a_MESMA_cobranca()
    {
        // Duas faturas vivas no gateway pro mesmo mês é o aluno pagando a que achar primeiro
        // — e o professor recebendo uma só.
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;
        var fatura = Fatura(ctx, professor);

        var primeiro = await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);
        var segundo = await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);

        Assert.Equal(primeiro, segundo);
        Assert.Equal(1, await ctx.Pagamentos.CountAsync());
    }

    // ─── O dinheiro entrando ──────────────────────────────────────────────────────────

    [Fact]
    public async Task O_webhook_quita_a_conta_do_mes()
    {
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;
        var fatura = Fatura(ctx, professor);

        await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);
        var pagamento = await ctx.Pagamentos.SingleAsync();

        await servico.EfetivarAsync(pagamento);

        var depois = await ctx.FaturasDeAlunos.SingleAsync();
        Assert.Equal(FechamentoDoMes.Paga, depois.Status);
        Assert.NotNull(depois.PagaEm);
    }

    [Fact]
    public async Task O_webhook_repetido_nao_quita_duas_vezes()
    {
        // O Asaas reenvia o mesmo evento até receber 200.
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;
        var fatura = Fatura(ctx, professor);

        await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);
        var pagamento = await ctx.Pagamentos.SingleAsync();

        await servico.EfetivarAsync(pagamento);
        var quitadaEm = (await ctx.FaturasDeAlunos.SingleAsync()).PagaEm;

        await servico.EfetivarAsync(pagamento);

        Assert.Equal(quitadaEm, (await ctx.FaturasDeAlunos.SingleAsync()).PagaEm);
        Assert.Equal(fatura.Id, pagamento.ReferenciaId);
    }

    [Fact]
    public async Task Conta_apagada_entre_a_cobranca_e_a_confirmacao_nao_derruba_o_webhook()
    {
        // O dinheiro entrou e não há conta pra quitar. Fica no log pra devolução à mão —
        // recriar uma conta cancelada seria pior.
        var (ctx, _, servico, professor) = Montar();
        using var _d = ctx;
        var fatura = Fatura(ctx, professor);

        await servico.IniciarCobrancaDaFaturaAsync(fatura, professor);
        var pagamento = await ctx.Pagamentos.SingleAsync();

        fatura.PagamentoId = null;
        ctx.FaturasDeAlunos.Remove(fatura);
        await ctx.SaveChangesAsync();

        await servico.EfetivarAsync(pagamento);

        Assert.Null(pagamento.ReferenciaId);
    }

    // ─── A taxa ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_taxa_e_a_do_plano_do_professor()
    {
        // A mesma régua da aula avulsa: assinante em dia paga a taxa menor, avulso paga a
        // cheia. Sem isso a conta do mês teria uma taxa própria, e duas tabelas discordando
        // é como o professor descobre que pagou mais do que combinou.
        var (ctx, asaas, servico, professor) = Montar(plano: PlanoDoProfessor.Assinante);
        using var _ = ctx;

        await servico.IniciarCobrancaDaFaturaAsync(Fatura(ctx, professor), professor);

        var cfg = new PlanoProfessorSettings();
        asaas.Received(1).CalcularRateio(880m, "Aula", Arg.Any<string?>(), cfg.PercentualAssinanteCartao);
    }

    [Fact]
    public async Task Professor_avulso_paga_a_taxa_cheia()
    {
        var (ctx, asaas, servico, professor) = Montar(plano: PlanoDoProfessor.Avulso);
        using var _ = ctx;
        professor.AssinaturaProfessorPagaAte = null;
        professor.TesteProfessorInicio = null;
        await ctx.SaveChangesAsync();

        await servico.IniciarCobrancaDaFaturaAsync(Fatura(ctx, professor), professor);

        var cfg = new PlanoProfessorSettings();
        asaas.Received(1).CalcularRateio(880m, "Aula", Arg.Any<string?>(), cfg.PercentualAvulso);
    }
}
