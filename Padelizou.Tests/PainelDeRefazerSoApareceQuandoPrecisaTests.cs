using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — O PAINEL "REFAZER COMO PREVISTO" APARECIA SEM TER O QUE FAZER.
//
// 🗣️ Felipe, com as 7 categorias do 2ª Etapa ER PADEL TOUR já conferidas e o painel em todas
// elas: *"acho que podemos ocultar isso agora que resolveu, não?"*.
//
// 🕳️ A view pedia três coisas — organizador, chave publicada, categoria com grupos — e
// NUNCA perguntava se a chave real já batia com o previsto. O painel era andaime de
// emergência (12/09) e ficou gritando em toda categoria depois que a emergência passou.
//
// ⚠️ OCULTAR DE VEZ TIRARIA A SAÍDA DE EMERGÊNCIA. O que resolve o pedido sem apagar a porta
// é o painel aparecer só quando o clique MUDA alguma coisa — some sozinho agora e volta se
// algo embaralhar de novo.
//
// ⚠️ E A RÉGUA TEM QUE SER A MESMA DA AÇÃO, senão o painel promete o que a ação recusa (ou
// esconde o que ela faria). Por isso `RefazerComoPrevisto.Avaliar` decide pelos dois.
public class PainelDeRefazerSoApareceQuandoPrecisaTests
{
    // 12 duplas → 4 grupos de 3 → 8 classificados, quadro de 8, sem bye. O mesmo cenário do
    // ChaveRespeitaOPrevistoTests, porque é o que reproduz o defeito original.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComAChaveMontadaAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        var deGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .OrderBy(p => p.Id)
            .ToListAsync();

        for (int i = 0; i < deGrupo.Count; i++)
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), deGrupo[i], 9, new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);
        }

        return (ctx, torneio, categoria, org.Id);
    }

    private static async Task<bool> PainelApareceAsync(DbPadelContext ctx, int torneioId, int categoriaId, int orgId)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        await controller.Details(torneioId, null, null);

        var mapa = controller.ViewBag.RefazerPrevistoPorCategoria as Dictionary<int, bool>;
        Assert.NotNull(mapa);
        return mapa!.TryGetValue(categoriaId, out var aparece) && aparece;
    }

    // Recria o estado do ER: a chave montada pela campanha, sem obedecer ao cruzamento previsto.
    private static async Task EmbaralharAsync(DbPadelContext ctx, int categoriaId)
    {
        var categoria = await ctx.Categorias
            .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
            .FirstAsync(c => c.Id == categoriaId);

        var duplas = categoria.GruposTorneio.SelectMany(g => g.Duplas).ToList();
        var finalizadas = (await ctx.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Status == "Finalizada")
                .ToListAsync())
            .Where(p => FasesTorneio.EhFaseDeGrupos(p.Fase))
            .ToList();

        int passam = ClassificacaoDeGrupos.VagasPorGrupo(categoria);
        var pontos = await ClassificacaoDeGrupos.PontosSePrecisarAsync(
            duplas, finalizadas, TestInfra.SemPontosDoRanking);
        var classificados = ClassificacaoDeGrupos.Calcular(duplas, finalizadas, pontos, passam);
        var (_, confrontos, _) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, passam);

        var mataMata = (await ctx.Partidas
                .Where(p => p.CategoriaId == categoriaId)
                .OrderBy(p => p.Id)
                .ToListAsync())
            .Where(p => !FasesTorneio.EhFaseDeGrupos(p.Fase))
            .ToList();

        for (int i = 0; i < mataMata.Count && i < confrontos.Count; i++)
        {
            mataMata[i].Dupla1Id = confrontos[i].Dupla1Id;
            mataMata[i].Dupla2Id = confrontos[i].Dupla2Id;
        }

        // O desenho congelado é o que a chave passaria a desobedecer.
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Chave_igual_ao_previsto_nao_mostra_o_painel()
    {
        var (ctx, torneio, categoria, orgId) = await ComAChaveMontadaAsync();
        using var _ctx = ctx;

        // Este é o estado das 7 categorias do ER depois do congelamento: a chave nasceu
        // obedecendo ao previsto, então o botão não tem o que fazer.
        Assert.False(await PainelApareceAsync(ctx, torneio.Id, categoria.Id, orgId));
    }

    [Fact]
    public async Task Chave_embaralhada_mostra_o_painel()
    {
        var (ctx, torneio, categoria, orgId) = await ComAChaveMontadaAsync();
        using var _ctx = ctx;

        await EmbaralharAsync(ctx, categoria.Id);

        // A saída de emergência não some: volta sozinha no instante em que há divergência.
        Assert.True(await PainelApareceAsync(ctx, torneio.Id, categoria.Id, orgId));
    }

    [Fact]
    public async Task Jogo_do_mata_mata_ja_comecado_nao_mostra_o_painel()
    {
        var (ctx, torneio, categoria, orgId) = await ComAChaveMontadaAsync();
        using var _ctx = ctx;

        await EmbaralharAsync(ctx, categoria.Id);

        var primeiro = (await ctx.Partidas
                .Where(p => p.CategoriaId == categoria.Id)
                .OrderBy(p => p.Id)
                .ToListAsync())
            .First(p => !FasesTorneio.EhFaseDeGrupos(p.Fase));
        primeiro.Status = "AoVivo";
        primeiro.HorarioInicioReal = DateTime.Now;
        await ctx.SaveChangesAsync();

        // A ação recusa este caso (tudo-ou-nada: a bola que já rolou fixa o resto da chave).
        // Oferecer o botão aqui é prometer o que ele não entrega.
        Assert.False(await PainelApareceAsync(ctx, torneio.Id, categoria.Id, orgId));
    }

    // ── A RÉGUA, DIRETO ────────────────────────────────────────────────────────────────

    [Fact]
    public void Sem_desenho_nao_ha_painel()
    {
        var avaliacao = RefazerComoPrevisto.Avaliar(
            desenho: null, desenhoCongelado: false,
            deGrupo: new List<Partida>(), doMataMata: new List<Partida>(),
            classificados: new List<ChaveamentoMataMata.Classificado>());

        Assert.Equal(RefazerComoPrevisto.Estado.SemDesenho, avaliacao.Estado);
        Assert.False(avaliacao.ValeMostrarOPainel);
    }

    [Fact]
    public void Mata_mata_que_ainda_nao_existe_com_o_desenho_ja_congelado_nao_tem_o_que_fazer()
    {
        var desenho = CruzamentoDoMataMata.Padrao(
            new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" }, 2, new[] { 3, 3, 3, 3 })!;

        var avaliacao = RefazerComoPrevisto.Avaliar(
            desenho, desenhoCongelado: true,
            deGrupo: new List<Partida>(), doMataMata: new List<Partida>(),
            classificados: new List<ChaveamentoMataMata.Classificado>());

        // O robô congela sozinho desde 12/09. Com o desenho já gravado, clicar não muda nada.
        Assert.Equal(RefazerComoPrevisto.Estado.NadaAFazer, avaliacao.Estado);
        Assert.False(avaliacao.ValeMostrarOPainel);
    }

    [Fact]
    public void Mata_mata_que_ainda_nao_existe_e_desenho_solto_ainda_vale_congelar()
    {
        var desenho = CruzamentoDoMataMata.Padrao(
            new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" }, 2, new[] { 3, 3, 3, 3 })!;

        var avaliacao = RefazerComoPrevisto.Avaliar(
            desenho, desenhoCongelado: false,
            deGrupo: new List<Partida>(), doMataMata: new List<Partida>(),
            classificados: new List<ChaveamentoMataMata.Classificado>());

        // Torneio aprovado antes do congelamento existir: aqui o botão tem trabalho de verdade.
        Assert.Equal(RefazerComoPrevisto.Estado.SoCongelar, avaliacao.Estado);
        Assert.True(avaliacao.ValeMostrarOPainel);
    }
}
