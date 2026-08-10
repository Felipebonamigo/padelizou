using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O plano anual: R$ 499,90 compram 12 meses, contra R$ 49,90 que compram 1. O ciclo viaja no
// JSON da cobrança (DadosAssinaturaProfessor) — não há coluna nova —, e é por isso que o
// primeiro teste daqui é o da cobrança ANTIGA, que não tem ciclo nenhum gravado.
public class PlanoAnualDoProfessorTests
{
    private static readonly PlanoProfessorSettings Cfg = new(); // 49,90/mês, 499,90/ano
    private static readonly DateTime Agora = new(2026, 08, 10, 12, 0, 0);

    private static PagamentoInscricaoService Servico(DbPadelContext ctx, IPushNotificationService? push = null)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            push ?? Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(Cfg));
    }

    private static Jogador NovoProfessor() =>
        new() { Nome = "Prof", Cpf = "11144477735", IsProfessor = true, PlanoProfessor = PlanoDoProfessor.Assinante };

    // ── A regra pura ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Anual_compra_12_meses_e_mensal_compra_1()
    {
        Assert.Equal(Agora.AddMonths(12),
            PlanoDoProfessor.NovaDataPagaAte(null, Agora, PlanoDoProfessor.CicloAnual));
        Assert.Equal(Agora.AddMonths(1),
            PlanoDoProfessor.NovaDataPagaAte(null, Agora, PlanoDoProfessor.CicloMensal));

        Assert.Equal(499.90m, PlanoDoProfessor.ValorDo(PlanoDoProfessor.CicloAnual, Cfg));
        Assert.Equal(49.90m, PlanoDoProfessor.ValorDo(PlanoDoProfessor.CicloMensal, Cfg));
    }

    [Fact]
    public void Cobranca_sem_ciclo_gravado_vale_um_mes()
    {
        // ⚠️ O teste que protege o dia do deploy: toda cobrança de assinatura em aberto no
        // banco foi criada antes do anual existir e tem só `{"ProfessorId":N}` no JSON.
        // Se o null virasse "anual", elas dariam 12 meses de graça na confirmação.
        Assert.Equal(1, PlanoDoProfessor.MesesDo(null));
        Assert.Equal(Agora.AddMonths(1), PlanoDoProfessor.NovaDataPagaAte(null, Agora, null));

        // E lixo na entrada também é mensal — nunca o ciclo caro.
        Assert.Equal(1, PlanoDoProfessor.MesesDo("anual"));  // caixa errada não é o nosso "Anual"
        Assert.Equal(PlanoDoProfessor.CicloMensal, PlanoDoProfessor.CicloValido("qualquer coisa"));
        Assert.Equal(PlanoDoProfessor.CicloMensal, PlanoDoProfessor.CicloValido(null));
        Assert.Equal(PlanoDoProfessor.CicloAnual, PlanoDoProfessor.CicloValido(PlanoDoProfessor.CicloAnual));
    }

    [Fact]
    public void Anual_estende_do_fim_da_vigencia_quem_ja_esta_em_dia()
    {
        // Assinante em dia até daqui a 10 dias compra o anual: os 12 meses somam no FIM, não
        // engolem os 10 dias que ele já tinha pago.
        var pagaAte = Agora.AddDays(10);
        Assert.Equal(pagaAte.AddMonths(12),
            PlanoDoProfessor.NovaDataPagaAte(pagaAte, Agora, PlanoDoProfessor.CicloAnual));

        // Atrasado, conta de hoje — atraso não vira crédito, igual ao mensal.
        Assert.Equal(Agora.AddMonths(12),
            PlanoDoProfessor.NovaDataPagaAte(Agora.AddDays(-30), Agora, PlanoDoProfessor.CicloAnual));
    }

    [Fact]
    public void O_anual_economiza_quase_duas_mensalidades()
    {
        // 12 × 49,90 = 598,80 contra 499,90. O número vai pra tela, então errar aqui é
        // anunciar desconto que não existe.
        Assert.Equal(98.90m, PlanoDoProfessor.EconomiaDoAnual(Cfg));
    }

    // ── O caminho de verdade: cobrança nasce, é confirmada, credita ───────────────────────

    [Fact]
    public async Task Cobranca_anual_nasce_com_499_90_e_credita_12_meses()
    {
        using var ctx = TestInfra.NovoContexto();
        await ChavePixDoPadelizou.GravarAsync(ctx, "12345678000199", "Padelizou", "Porto Alegre", null);
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var servico = Servico(ctx);
        var cobranca = await servico.IniciarPixDiretoAssinaturaAsync(professor, PlanoDoProfessor.CicloAnual);

        Assert.NotNull(cobranca);
        Assert.Equal(499.90m, cobranca!.Valor);
        Assert.Equal(499.90m, cobranca.Comissao);   // é receita nossa inteira, sem repasse
        Assert.Null(cobranca.RecebedorId);

        cobranca.Status = "Confirmado";
        await servico.EfetivarAsync(cobranca);

        var atualizado = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.NotNull(atualizado!.AssinaturaProfessorPagaAte);
        // Doze meses a partir de hoje: a data tem que passar de 11 meses, o que uma
        // mensalidade jamais faria.
        Assert.True(atualizado.AssinaturaProfessorPagaAte > DateTime.Now.AddMonths(11));
        Assert.True(atualizado.AssinaturaProfessorPagaAte <= DateTime.Now.AddMonths(12).AddMinutes(1));
    }

    [Fact]
    public async Task Cobranca_mensal_continua_creditando_um_mes_so()
    {
        using var ctx = TestInfra.NovoContexto();
        await ChavePixDoPadelizou.GravarAsync(ctx, "12345678000199", "Padelizou", "Porto Alegre", null);
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var servico = Servico(ctx);
        var cobranca = await servico.IniciarPixDiretoAssinaturaAsync(professor, PlanoDoProfessor.CicloMensal);

        Assert.Equal(49.90m, cobranca!.Valor);

        cobranca.Status = "Confirmado";
        await servico.EfetivarAsync(cobranca);

        var atualizado = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.True(atualizado!.AssinaturaProfessorPagaAte <= DateTime.Now.AddMonths(1).AddMinutes(1));
        Assert.True(atualizado.AssinaturaProfessorPagaAte > DateTime.Now.AddDays(27));
    }

    [Fact]
    public async Task Cobranca_gravada_antes_do_anual_existir_ainda_credita_um_mes()
    {
        // O JSON literal que está no banco de produção hoje, sem ciclo nenhum. Este é o teste
        // que prova que o deploy não quebra (nem presenteia) quem tem cobrança em aberto.
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var antiga = new Pagamento
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = professor.Id,
            Valor = 49.90m,
            Comissao = 49.90m,
            MetodoPagamento = PixDireto.Metodo,
            DadosInscricao = $"{{\"ProfessorId\":{professor.Id}}}",
            Status = "Confirmado",
        };
        ctx.Pagamentos.Add(antiga);
        await ctx.SaveChangesAsync();

        await Servico(ctx).EfetivarAsync(antiga);

        var atualizado = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.NotNull(atualizado!.AssinaturaProfessorPagaAte);
        Assert.True(atualizado.AssinaturaProfessorPagaAte <= DateTime.Now.AddMonths(1).AddMinutes(1));
    }

    // ── A trava das duas cobranças abertas ────────────────────────────────────────────────

    [Fact]
    public async Task Com_uma_cobranca_aberta_o_outro_ciclo_nao_gera_a_segunda()
    {
        using var ctx = TestInfra.NovoContexto();
        await ChavePixDoPadelizou.GravarAsync(ctx, "12345678000199", "Padelizou", "Porto Alegre", null);
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPlanoProfessorController(ctx, professor.Id, Servico(ctx), Cfg);

        await controller.PagarMensalidade(PlanoDoProfessor.CicloMensal);
        Assert.Single(ctx.Pagamentos);

        // Mudou de ideia e pediu o anual com a mensal em aberto: NÃO nasce uma segunda
        // cobrança — pagar as duas é o erro que ele não desfaz sozinho.
        await controller.PagarMensalidade(PlanoDoProfessor.CicloAnual);
        Assert.Single(ctx.Pagamentos);
        Assert.Equal(49.90m, ctx.Pagamentos.Single().Valor);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Segundo_clique_no_mesmo_ciclo_volta_pra_mesma_cobranca_sem_reclamar()
    {
        using var ctx = TestInfra.NovoContexto();
        await ChavePixDoPadelizou.GravarAsync(ctx, "12345678000199", "Padelizou", "Porto Alegre", null);
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPlanoProfessorController(ctx, professor.Id, Servico(ctx), Cfg);

        await controller.PagarMensalidade(PlanoDoProfessor.CicloAnual);
        await controller.PagarMensalidade(PlanoDoProfessor.CicloAnual);

        Assert.Single(ctx.Pagamentos);
        Assert.Equal(499.90m, ctx.Pagamentos.Single().Valor);
        // Clicar duas vezes no mesmo botão não é erro nenhum — não pode virar recado vermelho.
        Assert.Null(controller.TempData["Erro"]);
    }
}
