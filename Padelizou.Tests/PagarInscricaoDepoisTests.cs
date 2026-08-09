using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "PAGAR AGORA" de uma inscrição que JÁ existe (08/08/2026).
//
// O par que faltava do torneio que garante a vaga e cobra depois: os dois únicos caminhos que
// criavam cobrança eram os de inscrição, então quem já estava dentro NUNCA conseguia pagar
// pelo app — o torneio cobrava pelo site só no nome (NATA PADEL TOUR).
//
// ⚠️ O que estes testes seguram não é o botão: é o EFEITO da confirmação ser marcar a
// inscrição existente, e nunca criar outra.
public class PagarInscricaoDepoisTests
{
    private static PagamentoInscricaoService NovoServico(DbPadelContext ctx)
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

    private static Pagamento PagamentoDe(DadosPagamentoDeInscricao dados, int pagadorId) => new()
    {
        Tipo = "TorneioPagarDepois",
        JogadorId = pagadorId,
        RecebedorId = pagadorId,
        Valor = 120m,
        Status = "Pendente",
        DadosInscricao = System.Text.Json.JsonSerializer.Serialize(dados),
    };

    [Fact]
    public async Task Confirmar_marca_a_dupla_que_ja_existe_como_paga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        Assert.False(dupla.Pago);

        var pagamento = PagamentoDe(new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), dupla.Jogador1Id);
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        await NovoServico(ctx).EfetivarAsync(pagamento);

        var depois = ctx.Duplas.Find(dupla.Id)!;
        Assert.True(depois.Pago);
        Assert.NotNull(depois.PagoEm);
    }

    [Fact]
    public async Task Confirmar_NAO_cria_uma_inscricao_nova()
    {
        // ⚠️ O teste que importa. No caminho normal o pagamento CRIA a dupla; se o tipo novo
        // caísse no `default` do EfetivarAsync, a pessoa ficaria inscrita DUAS vezes —
        // ocupando duas vagas e aparecendo duas vezes na chave.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        int duplasAntes = ctx.Duplas.Count();

        var pagamento = PagamentoDe(new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), dupla.Jogador1Id);
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        await NovoServico(ctx).EfetivarAsync(pagamento);

        Assert.Equal(duplasAntes, ctx.Duplas.Count());
    }

    [Fact]
    public async Task Confirmar_duas_vezes_nao_muda_nada()
    {
        // O Asaas reenvia o mesmo evento até receber 200, e manda CONFIRMED e RECEIVED pela
        // mesma cobrança. O carimbo do ReferenciaId é o que faz a segunda passada sair fora.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagamento = PagamentoDe(new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), dupla.Jogador1Id);
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        var servico = NovoServico(ctx);
        await servico.EfetivarAsync(pagamento);
        await servico.EfetivarAsync(pagamento);

        Assert.Equal(1, ctx.Duplas.Count(d => d.CategoriaId == categoria.Id));
        Assert.Equal(dupla.Id, ctx.Pagamentos.Find(pagamento.Id)!.ReferenciaId);
    }

    [Fact]
    public async Task Confirmar_marca_a_inscricao_do_americano_individual()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var jogador = TestInfra.NovoJogador(7);
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        var inscricao = new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = jogador.Id };
        ctx.InscricoesAmericanas.Add(inscricao);
        ctx.SaveChanges();

        var pagamento = PagamentoDe(new DadosPagamentoDeInscricao(torneio.Id, null, inscricao.Id), jogador.Id);
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        await NovoServico(ctx).EfetivarAsync(pagamento);

        Assert.True(ctx.InscricoesAmericanas.Find(inscricao.Id)!.Pago);
    }

    [Fact]
    public async Task Estorno_NAO_apaga_a_inscricao_so_desmarca_o_pago()
    {
        // ⚠️ O outro lado da mesma moeda. No caminho normal, o dinheiro de volta APAGA a
        // inscrição — porque lá ela só nasceu por causa do pagamento. Aqui a vaga existia
        // antes e continua existindo: quem toma o dinheiro de volta acerta por fora.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagamento = PagamentoDe(new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), dupla.Jogador1Id);
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        var servico = NovoServico(ctx);
        await servico.EfetivarAsync(pagamento);
        Assert.True(ctx.Duplas.Find(dupla.Id)!.Pago);

        await servico.DesfazerAsync(pagamento);

        var depois = ctx.Duplas.Find(dupla.Id);
        Assert.NotNull(depois);
        Assert.False(depois!.Pago);
        Assert.Null(depois.PagoEm);
    }
}
