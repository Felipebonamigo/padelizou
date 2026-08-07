using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// A lista de torneios vinha numa ordem só, decrescente, pra todas as seções. Dava dois
// resultados errados na mesma tela: o torneio mais DISTANTE encabeçava "Inscrições Abertas",
// e torneio sem data marcada vinha antes de todo mundo (no decrescente o nulo vem primeiro).
// Quem abre a tela quer saber o que é ESTE fim de semana.
public class OrdemDosTorneiosTests
{
    private static Torneio Torneio(string nome, string status, DateTime? data) => new()
    {
        Nome = nome,
        Codigo = nome,
        Status = status,
        DataInicio = data,
        // Sem aprovação ele nem chega na vitrine — e aí o teste passaria por lista vazia.
        AprovadoEm = DateTime.Now,
    };

    private static async Task<List<Torneio>> SecaoAsync(DbPadelContext ctx, string secao)
    {
        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1).Index());
        return (List<Torneio>)view.ViewData[secao]!;
    }

    [Fact]
    public async Task Inscricoes_abertas_vem_do_mais_proximo_pro_mais_distante()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("Novembro", "Inscrições Abertas", new DateTime(2026, 11, 20)),
            Torneio("Sabado", "Inscrições Abertas", new DateTime(2026, 8, 8)),
            Torneio("Setembro", "Inscrições Abertas", new DateTime(2026, 9, 12)));
        await ctx.SaveChangesAsync();

        var abertos = await SecaoAsync(ctx, "Abertos");

        Assert.Equal(new[] { "Sabado", "Setembro", "Novembro" }, abertos.Select(t => t.Nome));
    }

    [Fact]
    public async Task Torneio_SEM_data_vai_pro_fim_e_nao_pro_topo()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("Sem data", "Inscrições Abertas", null),
            Torneio("Sabado", "Inscrições Abertas", new DateTime(2026, 8, 8)));
        await ctx.SaveChangesAsync();

        var abertos = await SecaoAsync(ctx, "Abertos");

        Assert.Equal(new[] { "Sabado", "Sem data" }, abertos.Select(t => t.Nome));
    }

    [Fact]
    public async Task Em_andamento_tambem_comeca_pelo_mais_proximo()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("Depois", "Fase de Grupos", new DateTime(2026, 8, 22)),
            Torneio("Hoje", "Fase de Grupos", new DateTime(2026, 8, 7)));
        await ctx.SaveChangesAsync();

        var emAndamento = await SecaoAsync(ctx, "EmAndamento");

        Assert.Equal(new[] { "Hoje", "Depois" }, emAndamento.Select(t => t.Nome));
    }

    [Fact]
    public async Task Finalizado_vem_do_mais_RECENTE_pro_mais_antigo()
    {
        // Aqui a ordem se inverte de propósito: histórico se lê de trás pra frente, e o que
        // acabou ontem interessa mais do que o de março.
        using var ctx = TestInfra.NovoContexto();
        ctx.Torneios.AddRange(
            Torneio("Marco", "Finalizado", new DateTime(2026, 3, 1)),
            Torneio("Ontem", "Finalizado", new DateTime(2026, 8, 6)),
            Torneio("Maio", "Finalizado", new DateTime(2026, 5, 10)));
        await ctx.SaveChangesAsync();

        var finalizados = await SecaoAsync(ctx, "Finalizados");

        Assert.Equal(new[] { "Ontem", "Maio", "Marco" }, finalizados.Select(t => t.Nome));
    }
}
