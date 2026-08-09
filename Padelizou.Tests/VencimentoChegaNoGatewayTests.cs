using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// OS DOIS RELÓGIOS DA COBRANÇA (09/08/2026).
//
// ⚠️ ESTE ARQUIVO GUARDA UMA LIGAÇÃO, NÃO UMA CONTA. `VencimentoDaCobranca` já sabia calcular
// o prazo certo e `PrazoParaPagar` já sabia até quando o torneio aceita pagamento — o defeito
// era que um nunca chegava ao outro: o prazo do torneio esticava só o NOSSO relógio
// (`Pagamento.ExpiraEm`) e o vencimento que ia pro gateway continuava sendo hoje+1.
//
// O estrago: a tela prometia "Pague até 21/09" e a fatura vencia no dia seguinte. Vencida, o
// Asaas manda PAYMENT_OVERDUE e o webhook grava "Cancelado" aqui. Pior, silenciosamente: o
// lembrete de "sua cobrança está pra vencer" só procura pagamento com status "Pendente", então
// ele nunca sairia — a cobrança já estaria cancelada 40 dias antes das 6h finais.
//
// Teste de unidade no ajudante não pegaria isso: os dois lados estavam certos sozinhos.
public class VencimentoChegaNoGatewayTests
{
    private static (PagamentoInscricaoService servico, IAsaasService asaas) Novo(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.CalcularRateio(default, default!, default, default)
            .ReturnsForAnyArgs(ci => new RateioComissao((decimal)ci[0]!, (decimal)ci[0]!, 0m));
        asaas.ObterOuCriarClienteAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult<string?>("cli_1"));
        asaas.CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!)
            .ReturnsForAnyArgs(_ => Task.FromResult<CobrancaAsaas?>(
                new CobrancaAsaas("pay_1", "https://fatura/x")));

        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));

        return (servico, asaas);
    }

    private static Jogador Apto() => new()
    {
        Nome = "Organizador", Cpf = "11144477735",
        ReceberPagamentoOnline = true, AsaasWalletId = "carteira",
    };

    // O que o gateway realmente recebeu: vencimento (arg 4) e forma (arg 6).
    private static (DateTime vencimento, string billingType) OQueFoiProGateway(IAsaasService asaas)
    {
        ICall chamada = asaas.ReceivedCalls()
            .Single(c => c.GetMethodInfo().Name == nameof(IAsaasService.CriarCobrancaAsync));

        var argumentos = chamada.GetArguments();
        return ((DateTime)argumentos[4]!, (string)argumentos[6]!);
    }

    [Fact]
    public async Task O_prazo_do_torneio_chega_ao_vencimento_da_fatura()
    {
        // Datas relativas de propósito: com "21/09/2026" cravado, este teste passaria a mentir
        // sozinho depois daquele dia.
        var fecha = DateTime.Today.AddDays(45);

        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 125m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        torneio.PagamentoObrigatorioNaInscricao = false;
        torneio.PrevisaoEncerramentoInscricoes = fecha;
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;
        var (servico, asaas) = Novo(ctx);

        await servico.IniciarCobrancaDeInscricaoAsync(
            torneio, Apto(), pagador, inscricaoDeDupla: true, impedimentos: 0,
            new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), CobrancaDoTorneio.EscolhaPix);

        var (vencimento, _) = OQueFoiProGateway(asaas);

        Assert.Equal(fecha, vencimento);
    }

    [Fact]
    public async Task O_nosso_relogio_e_o_do_gateway_marcam_o_MESMO_dia()
    {
        // A regra que faltava, dita inteira: não basta o vencimento ser longo — ele tem que
        // bater com o `ExpiraEm`, senão volta a existir um prazo curto escondido atrás do
        // prazo anunciado.
        var fecha = DateTime.Today.AddDays(45);

        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 125m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        torneio.PagamentoObrigatorioNaInscricao = false;
        torneio.PrevisaoEncerramentoInscricoes = fecha;
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;
        var (servico, asaas) = Novo(ctx);

        await servico.IniciarCobrancaDeInscricaoAsync(
            torneio, Apto(), pagador, inscricaoDeDupla: true, impedimentos: 0,
            new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), CobrancaDoTorneio.EscolhaBoleto);

        var (vencimento, _) = OQueFoiProGateway(asaas);
        var gravado = ctx.Pagamentos.Single(p => p.Tipo == "TorneioPagarDepois");

        Assert.Equal(vencimento.Date, gravado.ExpiraEm!.Value.Date);
    }

    [Fact]
    public async Task Quem_exige_pagamento_na_inscricao_segue_com_o_prazo_curto()
    {
        // Aqui o prazo curto é o CERTO: a vaga só existe com o dinheiro, e não pode ficar
        // pendurada a noite toda em quem abandonou o checkout.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 125m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        torneio.PagamentoObrigatorioNaInscricao = true;
        torneio.PrevisaoEncerramentoInscricoes = DateTime.Today.AddDays(45);
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;
        var (servico, asaas) = Novo(ctx);

        await servico.IniciarCobrancaTorneioAsync(
            torneio, Apto(), pagador, "TorneioDupla",
            new DadosInscricaoTorneio(torneio.Id, categoria.Id, pagador.Id, null, false, false, false, false),
            CobrancaDoTorneio.EscolhaPix);

        var (vencimento, billingType) = OQueFoiProGateway(asaas);

        Assert.Equal(VencimentoDaCobranca.Para(billingType, DateTime.Today), vencimento);
        Assert.True(vencimento < DateTime.Today.AddDays(4));
    }
}
