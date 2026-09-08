using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// SORTEAR AGORA, PAGAR DEPOIS (08/09/2026).
//
// Pedido do Felipe: *"do lado de ver e pagar a taxa, tenha o botão de gerar chaves mesmo
// assim, porque a taxa pode ser paga depois"*.
//
// ⚠️ O QUE ESTAVA EM JOGO. No torneio "por fora" o dinheiro nunca passa pelo sistema — não há
// split, não há webhook, não há nada que cobre os 5% sozinho. A trava do sorteio ERA o
// mecanismo de cobrança inteiro (ver Services/TaxaDoTorneioExterno). Tirá-la devolveria o
// Externo à condição de "na prática grátis", que foi exatamente a avaliação corrigida em
// 09/08/2026.
//
// A saída escolhida (desenho aprovado pelo Felipe em 08/09) não tira a trava: transforma
// bloqueio em DÍVIDA REGISTRADA. O organizador destrava as chaves sozinho, e o torneio passa a
// constar como devendo na lista que o admin já usa pra dar baixa.
//
// ⚠️ COLUNA PRÓPRIA, e não reaproveitar TaxaExternoNegociadaEm. Aquela é o Padelizou ABRINDO
// MÃO da cobrança (só o raiz assina, ver RegistrarNegociacaoTaxa); esta é o organizador
// pegando fiado. Misturar as duas apagaria a diferença entre "eu perdoei" e "ele deve" — que é
// justamente o que a lista precisa mostrar.
public class TaxaExternoAdiadaTests
{
    private static (Torneio torneio, Jogador organizador) TorneioPorFora(DbPadelContext ctx)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.FormaPagamento = "Externo";
        torneio.PrecoInscricao = 150m;
        ctx.SaveChanges();
        return (torneio, organizador);
    }

    [Fact]
    public void A_trava_continua_de_pe_pra_quem_nao_adiou()
    {
        // O teste que impede o conserto de virar remoção: sem carimbo nenhum, a taxa BLOQUEIA.
        var torneio = new Torneio { FormaPagamento = "Externo", PrecoInscricao = 150m };

        Assert.True(TaxaDoTorneioExterno.SeAplica(torneio));
        Assert.False(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
    }

    [Fact]
    public void Adiar_libera_as_chaves()
    {
        var torneio = new Torneio
        {
            FormaPagamento = "Externo",
            PrecoInscricao = 150m,
            TaxaExternoAdiadaEm = DateTime.Now,
        };

        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
    }

    [Fact]
    public void Adiada_e_diferente_de_paga_e_de_negociada()
    {
        // ⚠️ A distinção que faz a lista de cobrança valer alguma coisa. Adiado é DEVENDO;
        // pago e negociado são casos encerrados.
        var adiado = new Torneio { FormaPagamento = "Externo", PrecoInscricao = 150m, TaxaExternoAdiadaEm = DateTime.Now };

        Assert.True(TaxaDoTorneioExterno.EstaDevendo(adiado));
        Assert.False(TaxaDoTorneioExterno.EstaDevendo(
            new Torneio { FormaPagamento = "Externo", PrecoInscricao = 150m, TaxaExternoPagaEm = DateTime.Now }));
        Assert.False(TaxaDoTorneioExterno.EstaDevendo(
            new Torneio { FormaPagamento = "Externo", PrecoInscricao = 150m, TaxaExternoNegociadaEm = DateTime.Now }));

        // Quem nem chegou a adiar não está devendo: está parado antes do sorteio.
        Assert.False(TaxaDoTorneioExterno.EstaDevendo(
            new Torneio { FormaPagamento = "Externo", PrecoInscricao = 150m }));
    }

    [Fact]
    public async Task O_organizador_adia_e_o_sorteio_passa_a_sair()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = TorneioPorFora(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        // Antes de adiar, o sorteio esbarra na taxa e nem cria jogo.
        await controller.GerarChaves(torneio.Id);
        Assert.Empty(ctx.Partidas);

        await controller.AdiarTaxaExterno(torneio.Id);

        var depois = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.NotNull(depois!.TaxaExternoAdiadaEm);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);
        Assert.NotEmpty(ctx.Partidas);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_pega_fiado()
    {
        // Mesma régua do GerarChaves: é dívida em nome de quem ficou com o dinheiro das
        // inscrições, então quem não organiza não assina por ele.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = TorneioPorFora(ctx);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "99900000055" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, estranho.Id).AdiarTaxaExterno(torneio.Id);

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.TaxaExternoAdiadaEm);
    }

    [Fact]
    public async Task Adiar_duas_vezes_nao_mexe_na_data()
    {
        // Botão de painel é clicado duas vezes. A data é a do PRIMEIRO fiado — ela é o "desde
        // quando" da lista de cobrança, e reescrevê-la zeraria a idade da dívida.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = TorneioPorFora(ctx);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).AdiarTaxaExterno(torneio.Id);
        var primeira = (await ctx.Torneios.FindAsync(torneio.Id))!.TaxaExternoAdiadaEm;

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).AdiarTaxaExterno(torneio.Id);

        Assert.Equal(primeira, (await ctx.Torneios.FindAsync(torneio.Id))!.TaxaExternoAdiadaEm);
    }

    [Fact]
    public async Task O_admin_e_avisado_de_que_alguem_pegou_fiado()
    {
        // Sem isto a dívida só existe pra quem abrir o financeiro. O aviso é o que faz o
        // Padelizou saber, no dia, que tem taxa pra cobrar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = TorneioPorFora(ctx);

        var admin = new Jogador { Nome = "Felipe", Cpf = "99900000097", IsAdminRaiz = true };
        ctx.Jogadores.Add(admin);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push).AdiarTaxaExterno(torneio.Id);

        await push.Received().EnviarParaJogadorAsync(admin.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Pagar_depois_tira_o_torneio_da_lista_de_devedores()
    {
        // O ciclo fecha onde já fechava: TaxaExternoPagaEm continua sendo o que quita.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = TorneioPorFora(ctx);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).AdiarTaxaExterno(torneio.Id);
        var devendo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.True(TaxaDoTorneioExterno.EstaDevendo(devendo!));

        devendo!.TaxaExternoPagaEm = DateTime.Now;
        await ctx.SaveChangesAsync();

        Assert.False(TaxaDoTorneioExterno.EstaDevendo(devendo));
    }
}
