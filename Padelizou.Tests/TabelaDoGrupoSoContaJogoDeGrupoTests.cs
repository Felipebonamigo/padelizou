using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — A TABELA DO GRUPO CONTAVA JOGO DE MATA-MATA.
//
// 🗣️ Felipe, na virada do primeiro dia do 2ª Etapa ER PADEL TOUR: *"Verifique o que falta"* —
// e o que faltava estava no card dos grupos. No Grupo B da 4ª Masculina, de DUAS duplas (um
// jogo só), a linha do Felipe Berg / Bruno Bergamashi dizia **J=2, 1V, 1D, +3**, e com isso
// subia pra 1º. Mesma coisa na 5ª Masculina (J=3 num grupo de 3, que tem 2 jogos por dupla) e
// na 6ª Feminina, onde a ordem também virou.
//
// 🕳️ `TorneiosController.Details` carrega `partidasFinalizadas` SEM FILTRO DE FASE:
//
//     .Where(p => p.TorneioId == id && p.Status == "Finalizada")
//
// e a contabilidade grupo a grupo filtra só por dupla. Enquanto a categoria estava nos grupos
// não havia o que somar errado; assim que a chave abriu, cada vitória de mata-mata entrou na
// linha do grupo — em J, em V, no saldo de games, e portanto na ORDEM, que é ordenada por esses
// mesmos números. A chave em si nunca leu isso (o robô recalcula a classificação a partir das
// partidas de grupo), então o estrago era só na tela — a mais visitada do site, e a que o
// jogador usa pra saber se passou.
//
// ⚠️ A MESMA LISTA ALIMENTA O MVP, algumas linhas acima, e lá TODOS os jogos são desejados (a
// votação abre 7 dias depois do último jogo do torneio, mata-mata incluído). Por isso o filtro
// entra na contabilidade dos grupos, e não na consulta.
public class TabelaDoGrupoSoContaJogoDeGrupoTests
{
    // 8 duplas → grupos de 2, 3 e 3 → 6 classificados num quadro de 8. É a menor forma que tem
    // ao mesmo tempo um grupo de 2 (onde "J=2" é impossível e denuncia o defeito sozinho) e
    // mata-mata de verdade pra sujar a conta.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComOsGruposFechadosAsync()
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

        var daFase = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .OrderBy(p => p.Id)
            .ToListAsync();

        for (int i = 0; i < daFase.Count; i++)
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), daFase[i], 9, new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);
        }

        return (ctx, torneio, categoria, org.Id);
    }

    // Joga o mata-mata inteiro, rodada a rodada (o robô só cria a próxima quando a anterior
    // fecha), com um roteiro: `sempreVence` ganha 9x0 todas as que disputa, `semprePerde` leva
    // 0x9 em todas. É assim que o teste produz o flagrante — a dupla que ficou em 2º no grupo
    // vira a que mais venceu no torneio, e a 1ª do grupo cai logo.
    private static async Task JogarOMataMataAsync(
        DbPadelContext ctx, int orgId, int categoriaId, int sempreVence, int semprePerde)
    {
        for (int rodada = 0; rodada < 6; rodada++)
        {
            var prontas = (await ctx.Partidas
                    .Where(p => p.CategoriaId == categoriaId && p.Status != "Finalizada"
                                && p.Dupla1Id != 0 && p.Dupla2Id != 0)
                    .OrderBy(p => p.Id)
                    .ToListAsync())
                .Where(p => !FasesTorneio.EhFaseDeGrupos(p.Fase))
                .ToList();

            if (prontas.Count == 0) return;

            foreach (var jogo in prontas)
            {
                bool dupla1Ganha =
                    jogo.Dupla1Id == sempreVence || jogo.Dupla2Id == semprePerde
                    || (jogo.Dupla2Id != sempreVence && jogo.Dupla1Id != semprePerde);

                await TestInfra.FinalizarComPlacarAsync(
                    ctx, TestInfra.NovoTorneiosController(ctx, orgId), jogo,
                    dupla1Ganha ? 9 : 0, dupla1Ganha ? 0 : 9);
            }
        }
    }

    private static async Task<List<GrupoTorneio>> GruposNaTelaAsync(DbPadelContext ctx, int torneioId, int orgId)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        var view = (ViewResult)await controller.Details(torneioId, null, null);
        var torneio = (Torneio)view.Model!;
        return torneio.Categorias.SelectMany(c => c.GruposTorneio).ToList();
    }

    // Monta o cenário completo e devolve o grupo de 2 duplas já com a chave inteira jogada,
    // mais quem ganhou e quem perdeu o único jogo daquele grupo.
    private static async Task<(DbPadelContext Ctx, int TorneioId, int OrgId, int CampeaoDoGrupo, int LanternaDoGrupo)>
        ComAChaveInteiraJogadaAsync()
    {
        var (ctx, torneio, categoria, orgId) = await ComOsGruposFechadosAsync();

        var grupoDeDois = (await ctx.Categorias
                .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
                .SingleAsync(c => c.Id == categoria.Id))
            .GruposTorneio.Single(g => g.Duplas.Count == 2);

        var idsDoGrupo = grupoDeDois.Duplas.Select(d => d.Id).ToList();
        var jogoDoGrupo = await ctx.Partidas.SingleAsync(p =>
            p.CategoriaId == categoria.Id
            && idsDoGrupo.Contains(p.Dupla1Id) && idsDoGrupo.Contains(p.Dupla2Id));

        int campeao = jogoDoGrupo.VencedorId!.Value;
        int lanterna = idsDoGrupo.Single(id => id != campeao);

        await JogarOMataMataAsync(ctx, orgId, categoria.Id, sempreVence: lanterna, semprePerde: campeao);

        return (ctx, torneio.Id, orgId, campeao, lanterna);
    }

    [Fact]
    public async Task Num_grupo_de_duas_duplas_ninguem_pode_ter_jogado_dois_jogos()
    {
        var (ctx, torneioId, orgId, campeao, lanterna) = await ComAChaveInteiraJogadaAsync();
        using var _ctx = ctx;

        var grupoDeDois = (await GruposNaTelaAsync(ctx, torneioId, orgId))
            .Single(g => g.Duplas.Count == 2);

        // Um grupo de 2 duplas tem UM jogo. Qualquer número acima de 1 aqui só pode ter vindo
        // do mata-mata.
        Assert.All(grupoDeDois.Duplas, d => Assert.Equal(1, d.Jogos));
    }

    [Fact]
    public async Task Quem_perdeu_o_jogo_do_grupo_nao_ganha_vitoria_por_vencer_no_mata_mata()
    {
        var (ctx, torneioId, orgId, campeao, lanterna) = await ComAChaveInteiraJogadaAsync();
        using var _ctx = ctx;

        var grupoDeDois = (await GruposNaTelaAsync(ctx, torneioId, orgId))
            .Single(g => g.Duplas.Count == 2);

        var linhaDaLanterna = grupoDeDois.Duplas.Single(d => d.Id == lanterna);

        Assert.Equal(0, linhaDaLanterna.Vitorias);
        Assert.Equal(1, linhaDaLanterna.Derrotas);
        // Perdeu 0x9 o único jogo do grupo. O 9x0 que deu nas Quartas não é saldo de grupo.
        Assert.Equal(-9, linhaDaLanterna.SaldoGames);
    }

    [Fact]
    public async Task A_ordem_do_grupo_nao_vira_de_cabeca_para_baixo_por_causa_da_chave()
    {
        var (ctx, torneioId, orgId, campeao, lanterna) = await ComAChaveInteiraJogadaAsync();
        using var _ctx = ctx;

        var grupoDeDois = (await GruposNaTelaAsync(ctx, torneioId, orgId))
            .Single(g => g.Duplas.Count == 2);

        // Era exatamente isto na tela do Felipe: a 2ª do grupo aparecendo em 1º porque venceu
        // no mata-mata. Quem ganhou o jogo do grupo é a 1ª do grupo, e ponto.
        Assert.Equal(campeao, grupoDeDois.Duplas.First().Id);
        Assert.Equal(lanterna, grupoDeDois.Duplas.Last().Id);
    }

    [Fact]
    public async Task Em_todo_grupo_o_J_bate_com_o_numero_de_jogos_de_grupo_daquela_dupla()
    {
        var (ctx, torneioId, orgId, _, _) = await ComAChaveInteiraJogadaAsync();
        using var _ctx = ctx;

        var deGrupo = (await ctx.Partidas.Where(p => p.Status == "Finalizada").ToListAsync())
            .Where(p => FasesTorneio.EhFaseDeGrupos(p.Fase))
            .ToList();

        foreach (var grupo in await GruposNaTelaAsync(ctx, torneioId, orgId))
        {
            foreach (var dupla in grupo.Duplas)
            {
                int esperado = deGrupo.Count(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id);
                Assert.Equal(esperado, dupla.Jogos);
            }
        }
    }
}
