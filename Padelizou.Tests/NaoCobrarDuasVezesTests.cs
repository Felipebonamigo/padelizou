using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// NÃO COBRAR DUAS VEZES A MESMA INSCRIÇÃO (Felipe, 08/08/2026: "cuide para não cobrar 2 vezes
// a mesma pessoa, para que não fique pagamentos pendentes").
//
// O "pagar agora" é o tipo de botão que se toca de novo quando a página demora. Sem guarda,
// cada toque criava uma cobrança nova: a pessoa pagaria uma e as outras ficariam penduradas
// como pendentes — na conta do organizador, no painel financeiro e no e-mail de cobrança do
// meio de pagamento.
public class NaoCobrarDuasVezesTests
{
    private static (PagamentoInscricaoService servico, IAsaasService asaas) Novo(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        // Sem isto o dublê devolve null e o serviço estoura ao ler o rateio — o preço inteiro
        // vai pro organizador, que é o suficiente pra estes testes (aqui o que importa é
        // QUANTAS cobranças nascem, não o split).
        asaas.CalcularRateio(default, default!, default, default)
            .ReturnsForAnyArgs(ci => new RateioComissao((decimal)ci[0]!, (decimal)ci[0]!, 0m));
        asaas.ObterOuCriarClienteAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult<string?>("cli_1"));
        asaas.CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!)
            .ReturnsForAnyArgs(_ => Task.FromResult<CobrancaAsaas?>(
                new CobrancaAsaas("pay_" + Guid.NewGuid().ToString("N")[..6], "https://fatura/x")));

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

    [Fact]
    public async Task Dois_cliques_no_pagar_agora_geram_UMA_cobranca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 120m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.SoPix;
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;
        var (servico, _) = Novo(ctx);
        var dados = new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null);

        var primeira = await servico.IniciarCobrancaDeInscricaoAsync(
            torneio, Apto(), pagador, inscricaoDeDupla: true, impedimentos: 0, dados);
        var segunda = await servico.IniciarCobrancaDeInscricaoAsync(
            torneio, Apto(), pagador, inscricaoDeDupla: true, impedimentos: 0, dados);

        // Mesma fatura, e uma linha só de pagamento.
        Assert.Equal(primeira, segunda);
        Assert.Equal(1, ctx.Pagamentos.Count(p => p.Tipo == "TorneioPagarDepois"));
    }

    [Fact]
    public async Task Inscricoes_DIFERENTES_da_mesma_pessoa_tem_cobrancas_diferentes()
    {
        // ⚠️ O caso que quebraria uma guarda preguiçosa. Com múltiplas categorias ligadas, a
        // mesma pessoa tem duas inscrições no MESMO torneio: casar por jogador+torneio
        // devolveria a fatura da OUTRA categoria, e ela pagaria a errada.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 120m;
        torneio.PermiteMultiplasCategorias = true;
        var outra = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(outra);
        ctx.SaveChanges();

        var primeiraDupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var segundaDupla = new Dupla
        {
            CategoriaId = outra.Id,
            Jogador1Id = primeiraDupla.Jogador1Id,
            Jogador2Id = primeiraDupla.Jogador2Id,
            UltimaFase = "",
        };
        ctx.Duplas.Add(segundaDupla);
        ctx.SaveChanges();

        var pagador = ctx.Jogadores.Find(primeiraDupla.Jogador1Id)!;
        var (servico, _) = Novo(ctx);

        await servico.IniciarCobrancaDeInscricaoAsync(torneio, Apto(), pagador,
            inscricaoDeDupla: true, impedimentos: 0,
            new DadosPagamentoDeInscricao(torneio.Id, primeiraDupla.Id, null));

        await servico.IniciarCobrancaDeInscricaoAsync(torneio, Apto(), pagador,
            inscricaoDeDupla: true, impedimentos: 0,
            new DadosPagamentoDeInscricao(torneio.Id, segundaDupla.Id, null));

        // Duas inscrições, duas cobranças — cada uma da sua categoria.
        Assert.Equal(2, ctx.Pagamentos.Count(p => p.Tipo == "TorneioPagarDepois"));
    }

    [Fact]
    public async Task Cobranca_ja_paga_nao_bloqueia_uma_nova()
    {
        // A guarda vale só pra PENDENTE. Uma cobrança já confirmada não pode impedir que o
        // sistema cobre de novo num caso legítimo (ex: estorno e nova inscrição).
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 120m;
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;
        var (servico, _) = Novo(ctx);
        var dados = new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null);

        await servico.IniciarCobrancaDeInscricaoAsync(torneio, Apto(), pagador, true, 0, dados);

        var primeira = ctx.Pagamentos.First(p => p.Tipo == "TorneioPagarDepois");
        primeira.Status = "Confirmado";
        ctx.SaveChanges();

        await servico.IniciarCobrancaDeInscricaoAsync(torneio, Apto(), pagador, true, 0, dados);

        Assert.Equal(2, ctx.Pagamentos.Count(p => p.Tipo == "TorneioPagarDepois"));
    }
}
