using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using System.Text.Json;

namespace Padelizou.Tests;

// Os dois pagamentos que são receita direta do Padelizou: a taxa do torneio "por fora"
// (destrava as chaves) e a mensalidade do professor assinante (estende a vigência).
// O caminho é o do webhook — EfetivarAsync — e o Asaas REENVIA eventos, então cada um
// precisa ser idempotente.
public class EfetivarPagamentosDaPlataformaTests
{
    private static PagamentoInscricaoService Servico(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));
    }

    [Fact]
    public async Task Taxa_do_externo_confirmada_libera_as_chaves_e_reentrega_nao_muda_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "Copa", Codigo = "C1", FormaPagamento = "Externo", PrecoInscricao = 100m };
        var pagador = new Jogador { Nome = "Org", Cpf = "11144477735" };
        ctx.Torneios.Add(torneio);
        ctx.Jogadores.Add(pagador);
        await ctx.SaveChangesAsync();

        var pagamento = new Pagamento
        {
            Tipo = "TaxaTorneio",
            TorneioId = torneio.Id,
            JogadorId = pagador.Id,
            Valor = 120m,
            Comissao = 120m,
            DadosInscricao = JsonSerializer.Serialize(new DadosTaxaExterno(torneio.Id)),
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await Servico(ctx).EfetivarAsync(pagamento);

        Assert.NotNull(torneio.TaxaExternoPagaEm);
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
        Assert.Equal(torneio.Id, pagamento.ReferenciaId);

        // Reentrega do mesmo evento: nada muda (a data original fica).
        var dataOriginal = torneio.TaxaExternoPagaEm;
        await Servico(ctx).EfetivarAsync(pagamento);
        Assert.Equal(dataOriginal, torneio.TaxaExternoPagaEm);
    }

    [Fact]
    public async Task Assinatura_confirmada_estende_um_mes_e_reentrega_nao_soma_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = new Jogador
        {
            Nome = "Prof",
            Cpf = "11144477735",
            IsProfessor = true,
            PlanoProfessor = PlanoDoProfessor.Assinante,
        };
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var pagamento = new Pagamento
        {
            Tipo = "AssinaturaProfessor",
            JogadorId = professor.Id,
            Valor = 49.90m,
            Comissao = 49.90m,
            DadosInscricao = JsonSerializer.Serialize(new DadosAssinaturaProfessor(professor.Id)),
        };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var antes = DateTime.Now;
        await Servico(ctx).EfetivarAsync(pagamento);

        Assert.NotNull(professor.AssinaturaProfessorPagaAte);
        Assert.True(professor.AssinaturaProfessorPagaAte >= antes.AddMonths(1).AddMinutes(-1));
        Assert.Equal(professor.Id, pagamento.ReferenciaId);

        // Reentrega: um pagamento só não pode virar dois meses.
        var vigencia = professor.AssinaturaProfessorPagaAte;
        await Servico(ctx).EfetivarAsync(pagamento);
        Assert.Equal(vigencia, professor.AssinaturaProfessorPagaAte);
    }
}
