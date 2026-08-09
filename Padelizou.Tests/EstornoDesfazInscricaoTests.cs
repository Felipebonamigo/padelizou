using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O estorno mexia SÓ no dinheiro: a dupla continuava inscrita, continuava marcada como paga e
// a lista de espera não andava. Enquanto ninguém arrumasse na mão, o torneio tinha uma vaga
// ocupada por quem já havia recebido o dinheiro de volta.
public class EstornoDesfazInscricaoTests
{
    private static PagamentoInscricaoService Servico(DbPadelContext ctx, IPushNotificationService? push = null)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            push ?? Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));
    }

    private static async Task<(Torneio torneio, Categoria categoria)> TorneioComCategoriaAsync(DbPadelContext ctx)
    {
        var torneio = new Torneio { Nome = "Copa Teste", Codigo = "C1", Status = "Inscrições Abertas", PrecoInscricao = 100m };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { Nome = "2ª Categoria Masculina", Codigo = "CAT1", TorneioId = torneio.Id };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        return (torneio, categoria);
    }

    private static Jogador Jogador(DbPadelContext ctx, string nome, string cpf)
    {
        var j = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    private static Pagamento PagamentoDe(Dupla dupla, int torneioId, int categoriaId, int jogador1, int? jogador2) =>
        new()
        {
            Tipo = "TorneioDupla",
            Status = "Estornado",
            Valor = 200m,
            ReferenciaId = dupla.Id,
            DadosInscricao = JsonSerializer.Serialize(new DadosInscricaoTorneio(
                torneioId, categoriaId, jogador1, jogador2, false, false, false, false)),
        };

    [Fact]
    public async Task Estorno_apaga_a_inscricao_da_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria) = await TorneioComCategoriaAsync(ctx);
        var a = Jogador(ctx, "Ana", "11144477735");
        var b = Jogador(ctx, "Bia", "22255588896");

        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, Codigo = "D1", Pago = true };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var pagamento = PagamentoDe(dupla, torneio.Id, categoria.Id, a.Id, b.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        Assert.True(await Servico(ctx).DesfazerAsync(pagamento));
        Assert.Empty(await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync());
    }

    [Fact]
    public async Task A_vaga_que_abriu_chama_a_proxima_da_fila()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria) = await TorneioComCategoriaAsync(ctx);
        var a = Jogador(ctx, "Ana", "11144477735");
        var b = Jogador(ctx, "Bia", "22255588896");
        var c = Jogador(ctx, "Caio", "33366699957");
        var d = Jogador(ctx, "Duda", "44477700018");

        var confirmada = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, Codigo = "D1", Pago = true };
        var naEspera = new Dupla { CategoriaId = categoria.Id, Jogador1Id = c.Id, Jogador2Id = d.Id, Codigo = "D2", EmListaDeEspera = true };
        ctx.Duplas.AddRange(confirmada, naEspera);
        await ctx.SaveChangesAsync();

        var pagamento = PagamentoDe(confirmada, torneio.Id, categoria.Id, a.Id, b.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await Servico(ctx, push).DesfazerAsync(pagamento);

        var promovida = await ctx.Duplas.FirstAsync(x => x.Id == naEspera.Id);
        Assert.False(promovida.EmListaDeEspera);

        // Ser promovido em silêncio é o mesmo que não ser promovido: quem entra na lista de
        // espera justamente não fica olhando a página.
        // O canal fica em Arg.Any de propósito: o que importa aqui é que o aviso saiu, não por
        // onde. Prender o valor faz este teste quebrar toda vez que alguém mexer no alcance.
        await push.Received().EnviarParaJogadorAsync(c.Id,
            Arg.Is<string>(t => t.Contains("vaga")), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_estava_na_espera_nao_promove_ninguem_ao_sair()
    {
        // A vaga dele já não era uma vaga: promover aqui furaria o limite da categoria.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria) = await TorneioComCategoriaAsync(ctx);
        var a = Jogador(ctx, "Ana", "11144477735");
        var b = Jogador(ctx, "Bia", "22255588896");
        var c = Jogador(ctx, "Caio", "33366699957");
        var d = Jogador(ctx, "Duda", "44477700018");

        var saindo = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, Codigo = "D1", EmListaDeEspera = true, Pago = true };
        var atras = new Dupla { CategoriaId = categoria.Id, Jogador1Id = c.Id, Jogador2Id = d.Id, Codigo = "D2", EmListaDeEspera = true };
        ctx.Duplas.AddRange(saindo, atras);
        await ctx.SaveChangesAsync();

        var pagamento = PagamentoDe(saindo, torneio.Id, categoria.Id, a.Id, b.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        await Servico(ctx).DesfazerAsync(pagamento);

        var continua = await ctx.Duplas.FirstAsync(x => x.Id == atras.Id);
        Assert.True(continua.EmListaDeEspera);
    }

    [Fact]
    public async Task Reenvio_do_webhook_nao_apaga_outra_coisa()
    {
        // O Asaas reenvia o mesmo evento até receber 200. Na segunda passada a inscrição já
        // não existe — e o id dela não pode acabar apagando a inscrição de outra pessoa.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria) = await TorneioComCategoriaAsync(ctx);
        var a = Jogador(ctx, "Ana", "11144477735");
        var b = Jogador(ctx, "Bia", "22255588896");

        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, Codigo = "D1", Pago = true };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        var pagamento = PagamentoDe(dupla, torneio.Id, categoria.Id, a.Id, b.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        Assert.True(await Servico(ctx).DesfazerAsync(pagamento));
        Assert.False(await Servico(ctx).DesfazerAsync(pagamento));
    }

    [Fact]
    public async Task Pagamento_que_nunca_virou_inscricao_nao_desfaz_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var pagamento = new Pagamento { Tipo = "TorneioDupla", Status = "Estornado", Valor = 100m, ReferenciaId = null };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        Assert.False(await Servico(ctx).DesfazerAsync(pagamento));
    }

    [Theory]
    [InlineData("Aula")]
    [InlineData("Jogo")]
    [InlineData("TaxaTorneio")]
    [InlineData("AssinaturaProfessor")]
    public async Task Outros_tipos_nao_sao_desfeitos_no_chute(string tipo)
    {
        // Cada um tem regra própria (vaga de aula, quadra reservada, chave destravada, mês de
        // assinatura). Desfazer errado destrói dado — estes ficam registrados no log.
        using var ctx = TestInfra.NovoContexto();
        var pagamento = new Pagamento { Tipo = tipo, Status = "Estornado", Valor = 100m, ReferenciaId = 1 };
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        Assert.False(await Servico(ctx).DesfazerAsync(pagamento));
    }
}
