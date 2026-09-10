using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — GERENCIAR › EDITAR APAGAVA AS QUADRAS DO TORNEIO, calado, em TODO "Salvar".
//
// Achado no ensaio do torneio do Er numa app de verdade (Kestrel + Postgres + navegador):
// salvar só "Precisa terminar até" apagou Arena 2–5 e Radar 1–2 (com a janela do Radar),
// renomeou Arena 1 pra "Quadra A" e deixou QuantidadeQuadras = 1. O Details voltou dizendo
// "sucesso". No Er de produção, qualquer "Salvar" no Editar (preço, recado, link do grupo,
// capa…) destruiria as sedes cadastradas no planejador — sem aviso.
//
// 🕳️ A guarda `nomesQuadras != null || quantidadeQuadras > 0` (09/09, quando o planejador
// virou o lugar único de quadra) supunha que o campo ausente chega NULO. Não chega: pra
// parâmetro de topo de tipo coleção, o model binder do MVC entrega uma coleção VAZIA quando o
// formulário não manda o campo. A guarda ficava sempre verdadeira, e `Math.Max(1, 0)`
// reconciliava o torneio pra UMA quadra.
//
// ⚠️ Teste de controller chama a ação direto e pula o binder — por isso o teste MANDA o array
// vazio, que é o que o binder entrega de verdade. Com `null` ele passaria e não provaria nada.
// A mesma armadilha já tinha nome aqui: "teste de controller não vê isso, porque ele chama a
// ação direto e pula o model binding" (Details.cshtml, na caixa do MVP).
public class EditarNaoApagaAsQuadrasTests
{
    private static async Task<(Torneio torneio, Jogador org, Clube radar)> MontarComSedesAsync(DbPadelContext ctx)
    {
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var radar = new Clube { Nome = "Radar" };
        ctx.Clubes.Add(radar);
        await ctx.SaveChangesAsync();

        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Arena 1" },
            new Quadra { TorneioId = torneio.Id, Nome = "Arena 2" },
            new Quadra
            {
                TorneioId = torneio.Id, Nome = "Radar 1", ClubeId = radar.Id,
                DisponivelDe = new DateTime(2026, 9, 12, 8, 0, 0), DisponivelAte = new DateTime(2026, 9, 12, 12, 10, 0),
            });
        torneio.QuantidadeQuadras = 3;
        await ctx.SaveChangesAsync();
        return (torneio, org, radar);
    }

    private static Task EditarAsync(DbPadelContext ctx, Torneio torneio, int quemEdita,
        int quantidadeQuadras, string[]? nomesQuadras, DateTime? dataFim = null) =>
        TestInfra.NovoTorneiosController(ctx, quemEdita).Editar(
            id: torneio.Id, nome: torneio.Nome, localTorneio: null,
            dataInicio: torneio.DataInicio,
            precoInscricao: torneio.PrecoInscricao, clubeId: torneio.ClubeId,
            quantidadeQuadras: quantidadeQuadras, nomesQuadras: nomesQuadras,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            dataFim: dataFim, dataFimInformada: dataFim != null);

    // O formulário de gestão não manda mais nome nem quantidade de quadra. O que chega no
    // controller é quantidade 0 e a lista VAZIA (não nula) — e nada disso pode tocar em quadra.
    [Fact]
    public async Task Salvar_sem_mandar_quadra_nao_toca_nas_quadras()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, radar) = await MontarComSedesAsync(ctx);

        await EditarAsync(ctx, torneio, org.Id,
            quantidadeQuadras: 0, nomesQuadras: Array.Empty<string>(),
            dataFim: new DateTime(2026, 9, 13));

        var quadras = await ctx.Quadras.Where(q => q.TorneioId == torneio.Id).OrderBy(q => q.Id).ToListAsync();
        Assert.Equal(new[] { "Arena 1", "Arena 2", "Radar 1" }, quadras.Select(q => q.Nome).ToArray());

        var doRadar = quadras.Single(q => q.Nome == "Radar 1");
        Assert.Equal(radar.Id, doRadar.ClubeId);
        Assert.Equal(new DateTime(2026, 9, 12, 8, 0, 0), doRadar.DisponivelDe);
        Assert.Equal(new DateTime(2026, 9, 12, 12, 10, 0), doRadar.DisponivelAte);

        var salvo = await ctx.Torneios.FindAsync(torneio.Id);
        Assert.Equal(3, salvo!.QuantidadeQuadras);
        // E o que o organizador pediu de verdade foi gravado.
        Assert.Equal(new DateTime(2026, 9, 13), salvo.DataFim);
    }

    // Uma aba aberta antes do planejador ainda manda quantidade e nomes — e aí a reconciliação
    // por posição continua valendo, como sempre valeu. É o outro lado da guarda: ela não pode
    // virar "nunca mais mexe em quadra".
    [Fact]
    public async Task Aba_antiga_que_manda_quantidade_e_nomes_ainda_reconcilia()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, org, _) = await MontarComSedesAsync(ctx);

        await EditarAsync(ctx, torneio, org.Id,
            quantidadeQuadras: 2, nomesQuadras: new[] { "Central", "Lateral" });

        var quadras = await ctx.Quadras.Where(q => q.TorneioId == torneio.Id).OrderBy(q => q.Id).ToListAsync();
        Assert.Equal(new[] { "Central", "Lateral" }, quadras.Select(q => q.Nome).ToArray());
        Assert.Equal(2, (await ctx.Torneios.FindAsync(torneio.Id))!.QuantidadeQuadras);
    }
}
