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

    private static async Task<List<Torneio>> SeletorAsync(DbPadelContext ctx)
    {
        var controller = new JogadoresController(
            ctx, new EstatisticasService(ctx), Substitute.For<IRankingRsService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        // O dublê precisa devolver o VM vazio, e não o `null` que o NSubstitute dá de graça:
        // a tela lê as DUAS listas do Americano (individual e duplas) e nenhuma pode faltar.
        var americano = Substitute.For<IRankingAmericanoService>();
        americano.ListarAsync(Arg.Any<HashSet<int>?>())
            .Returns(new RankingAmericanoVM(new(), new()));

        var view = Assert.IsType<ViewResult>(await controller.Ranking(
            clubeId: null, torneioId: null, cidade: null, estado: null, periodo: null,
            padelimetro: new PadelimetroService(ctx),
            rankingAmericano: americano));

        return (List<Torneio>)view.ViewData["TorneiosList"]!;
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
}
