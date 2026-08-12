using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O que o hub de ranking (/Jogadores/Ranking) entrega pras abas que não saem de resultado de
// torneio de chave.
//
// ⚠️ Duas coisas se guardam aqui, e as duas quebram calado:
//
// 1. A aba DESAFIOS não pode existir pra quem ainda não enxerga o módulo. Esta página é PÚBLICA
//    — é a porta mais larga do sistema, e um módulo em construção vazando por ela apareceria
//    pra qualquer visitante antes de alguém reparar.
//
// 2. Os troféus de AMERICANO obedecem ao seletor de período; o ranking de Americano, não. A
//    assimetria é de propósito (ranking é o acumulado da campanha, troféu é "no período"), e é
//    exatamente o tipo de coisa que alguém "conserta" um dia igualando os dois.
public class AbasDoRankingTests
{
    // O dublê devolve VMs distintos por chamada pra que os testes consigam dizer QUAL lista foi
    // parar em qual propriedade — com um objeto só, "reaproveitou" e "consultou de novo" ficariam
    // indistinguíveis.
    private static IRankingAmericanoService AmericanoDublado(
        RankingAmericanoVM acumulado, RankingAmericanoVM? recortado = null)
    {
        var servico = Substitute.For<IRankingAmericanoService>();
        servico.ListarAsync(Arg.Any<HashSet<int>?>(), Arg.Any<DateTime?>(), null)
            .Returns(acumulado);
        servico.ListarAsync(Arg.Any<HashSet<int>?>(), Arg.Any<DateTime?>(), Arg.Is<DateTime?>(d => d != null))
            .Returns(recortado ?? acumulado);
        return servico;
    }

    private static async Task<RankingHubVM> HubAsync(DbPadelContext ctx, int? logadoComo = null,
        bool desafiosHabilitados = false, string? periodo = null,
        IRankingAmericanoService? americano = null)
    {
        var controller = TestInfra.NovoJogadoresController(ctx, logadoComo);

        var view = Assert.IsType<ViewResult>(await controller.Ranking(
            clubeId: null, torneioId: null, cidade: null, estado: null, periodo: periodo,
            padelimetro: new PadelimetroService(ctx),
            rankingAmericano: americano ?? AmericanoDublado(new RankingAmericanoVM(new(), new())),
            portaDosDesafios: TestInfra.PortaDosDesafiosDe(ctx, desafiosHabilitados),
            telaDeDesafios: new TelaDoRankingDeDesafios(ctx)));

        return Assert.IsType<RankingHubVM>(view.Model);
    }

    private static async Task<Jogador> JogadorAsync(DbPadelContext ctx, bool admin = false)
    {
        var jogador = TestInfra.NovoJogador(admin ? 99 : 1);
        jogador.IsAdminGeral = admin;
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();
        return jogador;
    }

    // ── A aba de Desafios ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Visitante_deslogado_nao_recebe_a_aba_de_desafios()
    {
        // O hub responde pra quem não fez login — é a tela que o Google indexa. Sem a lista, a
        // aba não é desenhada E nenhuma consulta do módulo é paga por quem não a veria.
        using var ctx = TestInfra.NovoContexto();

        var hub = await HubAsync(ctx, logadoComo: null, desafiosHabilitados: true);

        Assert.Null(hub.Desafios);
    }

    [Fact]
    public async Task Em_construcao_so_o_admin_recebe_a_aba_de_desafios()
    {
        // A MESMA régua do menu e do /Desafios — PortaDosDesafios. Se a aba tivesse um `if`
        // próprio, ela seria a segunda cópia, e a que continuaria abrindo o módulo no dia em que
        // a régua de verdade mudasse.
        using var ctx = TestInfra.NovoContexto();
        var comum = await JogadorAsync(ctx);
        var admin = await JogadorAsync(ctx, admin: true);

        Assert.Null((await HubAsync(ctx, comum.Id)).Desafios);
        Assert.NotNull((await HubAsync(ctx, admin.Id)).Desafios);
    }

    [Fact]
    public async Task Com_o_modulo_aberto_qualquer_jogador_recebe_a_aba_de_desafios()
    {
        using var ctx = TestInfra.NovoContexto();
        var comum = await JogadorAsync(ctx);

        var hub = await HubAsync(ctx, comum.Id, desafiosHabilitados: true);

        Assert.NotNull(hub.Desafios);
        // A faixa amarela de "em construção" é o oposto da porta: módulo aberto, faixa some.
        Assert.False(hub.Desafios!.EmConstrucao);
        // A tela destaca "essa linha é você", e pra isso ela precisa saber quem está lendo.
        Assert.Equal(comum.Id, hub.Desafios.MeuId);
    }

    // ── Os troféus de Americano ───────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_periodo_os_trofeus_de_Americano_reaproveitam_a_lista_do_ranking()
    {
        // Em "sempre" as duas listas SÃO a mesma coisa, e por isso não há segunda consulta.
        // Trocar isto por uma chamada nova faria o servidor remontar o Americano inteiro em toda
        // visita a uma tela pública — trabalho puro pra chegar no mesmo resultado.
        using var ctx = TestInfra.NovoContexto();

        var hub = await HubAsync(ctx);

        Assert.Same(hub.AmericanoIndividual, hub.TrofeusAmericanoIndividual);
        Assert.Same(hub.AmericanoDuplas, hub.TrofeusAmericanoDuplas);
        Assert.Null(hub.PeriodoDe);
    }

    [Fact]
    public async Task Com_periodo_os_trofeus_de_Americano_sao_recortados_e_o_ranking_nao()
    {
        // "Este ano" recorta os TROFÉUS. O ranking ao lado segue sendo o acumulado — é o que um
        // ranking de campanha é, e igualá-lo ao recorte apagaria a campanha do ano passado da aba
        // que existe pra mostrá-la.
        using var ctx = TestInfra.NovoContexto();

        var acumulado = new RankingAmericanoVM(new(), new());
        var doAno = new RankingAmericanoVM(new(), new());

        var hub = await HubAsync(ctx, periodo: "ano", americano: AmericanoDublado(acumulado, doAno));

        Assert.Equal(new DateTime(DateTime.Now.Year, 1, 1), hub.PeriodoDe);
        Assert.Same(acumulado.Individual, hub.AmericanoIndividual);
        Assert.Same(doAno.Individual, hub.TrofeusAmericanoIndividual);
        Assert.Same(doAno.Duplas, hub.TrofeusAmericanoDuplas);
    }
}
