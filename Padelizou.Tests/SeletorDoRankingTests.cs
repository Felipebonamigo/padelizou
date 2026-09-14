using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O seletor "ver ranking de um torneio…" da tela pública listava TUDO que existe na tabela,
// sem filtro nenhum: um torneio de teste CANCELADO apareceu ali pra qualquer visitante (visto
// em produção em 07/08/2026). É a mesma régua da vitrine — quem não aparece na lista de
// torneios não pode aparecer aqui.
public class SeletorDoRankingTests
{
    private static Torneio Torneio(string nome, DateTime? aprovadoEm, bool oculto = false,
        string status = "Finalizado") => new()
    {
        Nome = nome,
        Codigo = nome,
        Status = status,
        AprovadoEm = aprovadoEm,
        Oculto = oculto,
        DataInicio = new DateTime(2026, 8, 1),
    };

    private static async Task<List<Torneio>> SeletorAsync(DbPadelContext ctx) =>
        (List<Torneio>)(await RankingAsync(ctx, torneioId: null)).ViewData["TorneiosList"]!;

    private static async Task<ViewResult> RankingAsync(DbPadelContext ctx, int? torneioId)
    {
        var controller = TestInfra.NovoJogadoresController(ctx);

        // O dublê precisa devolver o VM vazio, e não o `null` que o NSubstitute dá de graça:
        // a tela lê as DUAS listas do Americano (individual e duplas) e nenhuma pode faltar.
        //
        // ⚠️ Os matchers cobrem os TRÊS argumentos de propósito. A tela pede a lista mais de uma
        // vez (o recorte do movimento e o dos troféus por período), e um dublê preso ao caso de
        // um argumento só devolveria nulo justamente na segunda chamada.
        var americano = Substitute.For<IRankingAmericanoService>();
        americano.ListarAsync(Arg.Any<HashSet<int>?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(new RankingAmericanoVM(new(), new()));

        return Assert.IsType<ViewResult>(await controller.Ranking(
            clubeId: null, torneioId: torneioId, cidade: null, estado: null, periodo: null,
            padelimetro: new PadelimetroService(ctx),
            rankingAmericano: americano,
            portaDosDesafios: TestInfra.PortaDosDesafiosDe(ctx),
            telaDeDesafios: new TelaDoRankingDeDesafios(ctx)));
    }

    [Fact]
    public async Task Torneio_CANCELADO_nao_aparece_no_seletor()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("De verdade", aprovadoEm: DateTime.Now),
            Torneio("teste", aprovadoEm: DateTime.Now, status: CancelamentoDoTorneio.Status));
        await ctx.SaveChangesAsync();

        var seletor = await SeletorAsync(ctx);

        Assert.Equal(new[] { "De verdade" }, seletor.Select(t => t.Nome));
    }

    [Fact]
    public async Task Torneio_esperando_aprovacao_ou_OCULTO_tambem_ficam_de_fora()
    {
        // Aparecer aqui é aparecer em público. Se a aprovação segura o torneio na listagem,
        // deixá-lo escapar por um <select> devolveria de graça o que a trava tirou.
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("Aprovado", aprovadoEm: DateTime.Now),
            Torneio("Esperando o OK", aprovadoEm: null),
            Torneio("Escondido", aprovadoEm: DateTime.Now, oculto: true));
        await ctx.SaveChangesAsync();

        var seletor = await SeletorAsync(ctx);

        Assert.Equal(new[] { "Aprovado" }, seletor.Select(t => t.Nome));
    }

    // 🕳️ O seletor era só metade da porta. A lista escondia o torneio, mas o `?torneioId=` da
    // URL não perguntava nada: quem digitasse o número de um torneio oculto, cancelado ou
    // esperando aprovação recebia o NOME e o RANKING dele na tela — exatamente o que o filtro
    // do seletor existe pra impedir. Mesma página pública, mesma régua.
    private static async Task<Torneio> ComRankingAsync(DbPadelContext ctx, Torneio torneio)
    {
        var categoria = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        var j1 = new Jogador { Nome = "J1", Cpf = "88800000001" };
        var j2 = new Jogador { Nome = "J2", Cpf = "88800000002" };
        ctx.AddRange(torneio, categoria, j1, j2);
        ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2, UltimaFase = "Campeao" });
        await ctx.SaveChangesAsync();
        return torneio;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Torneio_OCULTO_ou_esperando_aprovacao_pedido_pela_URL_nao_devolve_nome_nem_ranking(bool oculto)
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = await ComRankingAsync(ctx, oculto
            ? Torneio("Escondido", aprovadoEm: DateTime.Now, oculto: true)
            : Torneio("Esperando o OK", aprovadoEm: null));

        // Sem linha nenhuma pra vazar, "o ranking veio vazio" passaria antes E depois da correção.
        Assert.NotEmpty(await new EstatisticasService(ctx).ObterRankingDoTorneioAsync(torneio.Id));

        var hub = Assert.IsType<Padelizou.ViewModels.RankingHubVM>((await RankingAsync(ctx, torneio.Id)).Model);

        Assert.Null(hub.TorneioSelecionadoId);
        Assert.Null(hub.TorneioSelecionadoNome);
        Assert.Empty(hub.RankingTorneio);
    }

    [Fact]
    public async Task Torneio_CANCELADO_pedido_pela_URL_nao_devolve_o_nome()
    {
        // As linhas do cancelado já saem vazias do próprio serviço (evento que não aconteceu não
        // pontua, e ranking só lista quem pontuou). O que vazava aqui era o NOME, no título
        // "Ranking do torneio: …" — o torneio de teste de 07/08 voltava pela URL.
        using var ctx = TestInfra.NovoContexto();
        var torneio = await ComRankingAsync(ctx,
            Torneio("teste", aprovadoEm: DateTime.Now, status: CancelamentoDoTorneio.Status));

        var hub = Assert.IsType<Padelizou.ViewModels.RankingHubVM>((await RankingAsync(ctx, torneio.Id)).Model);

        Assert.Null(hub.TorneioSelecionadoId);
        Assert.Null(hub.TorneioSelecionadoNome);
    }

    [Fact]
    public async Task Torneio_da_vitrine_pedido_pela_URL_continua_com_nome_e_ranking()
    {
        // A trava não pode virar "a URL nunca mais abre torneio": é por ela que o próprio
        // seletor navega quando alguém escolhe uma opção.
        using var ctx = TestInfra.NovoContexto();
        var torneio = await ComRankingAsync(ctx, Torneio("De verdade", aprovadoEm: DateTime.Now));

        var hub = Assert.IsType<Padelizou.ViewModels.RankingHubVM>((await RankingAsync(ctx, torneio.Id)).Model);

        Assert.Equal(torneio.Id, hub.TorneioSelecionadoId);
        Assert.Equal("De verdade", hub.TorneioSelecionadoNome);
        Assert.NotEmpty(hub.RankingTorneio);
    }
}
