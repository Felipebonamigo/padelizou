using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUEM FOLGOU A PRIMEIRA RODADA NÃO PODE SUMIR DA PROJEÇÃO.
//
// 🗣️ Felipe, 12/09/2026, com o print de "Meus jogos" no meio do ER: *"aqui tambem nao esta
// aparecendo"* — a lista mandava o vencedor das Oitavas 2 direto pra uma "Semifinal 2" contra o
// vencedor das Oitavas 3. As QUARTAS tinham sumido da tela, e com elas os quatro byes.
//
// 🕳️ UMA FALHA CALADA, em `TorneiosController.ProjetarProximasFasesAsync`. O dicionário de nomes
// era montado só com as duplas que aparecem em partidas de mata-mata JÁ EXISTENTES:
//
//     var nomePorDupla = porCategoria.SelectMany(p => new[] { p.Dupla1, p.Dupla2 })…
//     var byes = byeIds.Select(id => nomePorDupla.TryGetValue(id, out var n) ? n : null)
//                      .Where(n => n != null)          // ← engolia o bye aqui
//
// Um BYE, por definição, **não está na primeira rodada** — é isso que o torna bye. Então ele
// nunca estava no dicionário, e o `.Where(n => n != null)` o descartava sem uma linha de log.
// A projeção rodava com metade dos lados: 4 vencedores em vez de 4 vencedores + 4 byes. O
// resultado não é "uma fase a menos no fim" — é a fase seguinte inteira MAL BATIZADA, porque o
// nome dela sai de quanta gente sobrou (`ChaveamentoMataMata.NomeFase`).
//
// ⚠️ O ROBÔ NUNCA PASSOU POR AQUI, e é por isso que a chave de verdade saiu certa: ele vai por
// `AvancoDaChave.ByesDaCategoriaAsync` com IDs. Quem mentia era só a PREVISÃO — e ela é o que o
// jogador lê pra saber a que horas voltar.
public class ByeNaoSomeDaProjecaoTests
{
    // 8 duplas → grupos de 2, 3 e 3 → 6 classificados num quadro de 8: a abertura tem 2 jogos
    // e 2 byes. É a menor forma que reproduz o defeito — sem bye ele não aparece.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComByesEGruposFechadosAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();
        for (int i = 0; i < grupos.Count; i++)
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), grupos[i], 9, new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);
        }

        return (ctx, torneio, categoria, org.Id);
    }

    private static async Task<List<ProximasFasesDaChave.JogoQueVem>> ProjecaoAsync(
        DbPadelContext ctx, int torneioId, int orgId)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        await controller.Details(torneioId, null, null);
        return controller.ViewBag.ProjecaoCompleta as List<ProximasFasesDaChave.JogoQueVem>
               ?? new List<ProximasFasesDaChave.JogoQueVem>();
    }

    [Fact]
    public async Task A_fase_seguinte_conta_os_byes_e_sai_com_o_nome_certo()
    {
        var (ctx, torneio, categoria, orgId) = await ComByesEGruposFechadosAsync();
        using var _ctx = ctx;

        // A abertura nasceu com 2 jogos; os outros 2 classificados folgaram.
        var abertura = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
            .ToListAsync();
        Assert.Equal(2, abertura.Count);

        var projecao = await ProjecaoAsync(ctx, torneio.Id, orgId);

        // 2 vencedores + 2 byes = 4 → a fase seguinte é SEMIFINAL, com dois jogos.
        // Com os byes engolidos sobravam 2 lados, e a projeção emitia "Final" direto.
        Assert.Equal(2, projecao.Count(j => j.Fase == "Semifinal"));
        Assert.Equal(1, projecao.Count(j => j.Fase == "Final"));
    }

    [Fact]
    public async Task O_bye_aparece_como_LADO_do_jogo_projetado_com_o_nome_dele()
    {
        var (ctx, torneio, categoria, orgId) = await ComByesEGruposFechadosAsync();
        using var _ctx = ctx;

        var byeIds = await AvancoDaChave.ByesDaCategoriaAsync(
            ctx, categoria.Id, TestInfra.SemPontosDoRanking);
        Assert.NotEmpty(byeIds);

        var nomesDosByes = await ctx.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => byeIds.Contains(d.Id))
            .ToListAsync();

        var projecao = await ProjecaoAsync(ctx, torneio.Id, orgId);
        var lados = projecao.SelectMany(j => new[] { j.Lado1.Rotulo, j.Lado2.Rotulo }).ToList();

        // Quem folgou entra na fase seguinte PELO NOME — não como "Vencedor de..." nem sumindo.
        foreach (var bye in nomesDosByes)
        {
            Assert.Contains(bye.NomeDeExibicao, lados);
        }
    }
}
