using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 14/09/2026 — O HORÁRIO QUE A PRÉVIA MOSTRA É COMPROMISSO.
//
// 🗣️ Felipe, depois do 2ª Etapa ER PADEL TOUR: *"é muito importante que o chaveamento pré
// definido seja seguido, por que o pessoal se baseia nisso para se programar, o chaveamento
// fixo, os horarios fixos"* · *"ele é obrigatoriamente obrigado a respeitar os horarios das
// quadras dos sorteios, pq o pessoal se programa para jogar por esses horarios mesmo com o
// checkin"*.
//
// 🕳️ A PRÉVIA E O ROBÔ ERAM DOIS MOTORES SOLTOS. A tela projetava a Semifinal pras 11:20; o
// robô, ao criar o jogo, reencaixava do zero com as entradas daquele instante — e o horário que
// o jogador leu virava outro assim que o jogo passava a existir. No ER isso pôs semifinais em
// SÁBADO 13:00, antes das próprias quartas.
//
// ✅ Agora o robô PERGUNTA À PRÓPRIA PRÉVIA onde cada jogo foi prometido (a projeção mora nele
// desde o commit anterior) e põe o jogo ali. O encaixe continua existindo como saída pra quem a
// prévia não prometeu nada.
//
// ⚠️ E O "DESLIZA INTEIRO" É DE GRAÇA, que foi a escolha do Felipe pro caso de atraso: a
// projeção é recalculada a partir do estado atual, então quando o torneio atrasa ela empurra a
// cadeia toda pra frente mantendo os intervalos — e o jogo nasce no horário empurrado, não num
// slot vago qualquer lá atrás.
public class OHorarioDoSorteioEPromessaTests
{
    // ⚠️ DUAS CATEGORIAS, E ISSO É O TESTE. Com UMA só, a prévia e o encaixe chegam ao mesmo
    // resultado por coincidência — a primeira versão deste arquivo passou de primeira, o que não
    // prova nada (Regra 1 do CLAUDE.md). A divergência aparece quando duas categorias disputam a
    // mesma grade: a prévia guarda o horário das eliminatórias FUTURAS de ambas, e o encaixe do
    // robô só enxerga jogo REAL. Foi assim que o 2ª Etapa ER PADEL TOUR terminou com três jogos
    // num horário de duas quadras.
    //
    // A segunda categoria fecha os grupos DEPOIS da primeira, então quando a Semifinal da
    // primeira nasce, a da segunda ainda é só promessa — e é justamente a promessa dela que o
    // encaixe atropelava.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Primeira, Categoria Segunda, int OrgId)>
        ComDuasCategoriasAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, primeira, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 50;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });

        var segunda = new Categoria { Nome = "4ª Categoria Masculina", Codigo = "CAT4M", Torneio = torneio };
        ctx.Categorias.Add(segunda);
        await ctx.SaveChangesAsync();

        for (int i = 0; i < 12; i++)
        {
            var j1 = TestInfra.NovoJogador(100 + i * 2);
            var j2 = TestInfra.NovoJogador(101 + i * 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = segunda, Jogador1 = j1, Jogador2 = j2 });
        }
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        // Só os grupos da PRIMEIRA fecham. Os da segunda ficam em aberto — a chave dela segue
        // sendo promessa, com horário reservado na grade.
        var deGrupo = await ctx.Partidas.Where(p => p.CategoriaId == primeira.Id)
            .OrderBy(p => p.Id).ToListAsync();
        for (int i = 0; i < deGrupo.Count; i++)
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, org.Id), deGrupo[i], 9,
                new[] { 0, 7, 2, 5, 1, 6 }[i % 6]);

        return (ctx, torneio, primeira, segunda, org.Id);
    }

    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComAsQuartasMontadasAsync()
    {
        var (ctx, torneio, primeira, _, orgId) = await ComDuasCategoriasAsync();
        return (ctx, torneio, primeira, orgId);
    }

    // O que a PRÉVIA promete agora, por (fase, número).
    private static async Task<Dictionary<(string Fase, int Numero), DateTime?>> PrometidoAsync(
        DbPadelContext ctx, int torneioId, int categoriaId)
    {
        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneioId).ToListAsync();
        var projecao = await new RoboDoChaveamento(ctx, TestInfra.EstatisticasFalsas()).ProjetarProximasFasesAsync(torneioId, todas);

        return projecao.Jogos
            .Where(j => j.CategoriaId == categoriaId)
            .ToDictionary(j => (j.Fase, j.Numero), j => j.Horario);
    }

    private static async Task<List<Partida>> DaFaseAsync(DbPadelContext ctx, int categoriaId, string fase)
    {
        var todas = await ctx.Partidas.Where(p => p.CategoriaId == categoriaId).ToListAsync();
        return ReservasDeHorario.NaOrdemDaFase(todas, fase);
    }

    // ⚠️ O ACHADO QUE BLOQUEIA A PROMESSA, e o motivo de ela não estar implementada ainda.
    //
    // A projeção NÃO É ESTÁVEL: com o torneio ANDANDO NO HORÁRIO, sem atraso nenhum, ela promete
    // um horário enquanto a Semifinal é só promessa e OUTRO depois que as Quartas viram
    // resultado. Medido aqui: 14:40 antes, 15:30 depois.
    //
    // Isso derruba o desenho mais simples — "o robô pergunta à prévia onde prometeu e põe ali" —,
    // porque não existe UMA resposta: depende de quando se pergunta. A promessa que o Felipe quer
    // (*"os horarios das quadras dos sorteios"*) é a do SORTEIO, e ela precisa ser GRAVADA no
    // momento em que a chave é aprovada, não recalculada na hora do nascimento.
    //
    // Este teste existe pra que esse fato não se perca, e pra falhar no dia em que alguém tornar
    // a projeção estável — aí o desenho simples volta a ser possível.
    [Fact]
    public async Task A_projecao_muda_sozinha_quando_a_fase_anterior_vira_resultado()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        var antes = (await PrometidoAsync(ctx, torneio.Id, categoria.Id))[("Semifinal", 1)];

        foreach (var q in await DaFaseAsync(ctx, categoria.Id, "Quartas de Final"))
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, orgId), q, 9, 3);

        var semi1 = (await DaFaseAsync(ctx, categoria.Id, "Semifinal"))[0];

        // O torneio não atrasou — os jogos foram encerrados sem mexer em horário nenhum.
        Assert.NotEqual(antes, semi1.HorarioPrevisto);
    }

    [Fact]
    public async Task Nenhum_jogo_nasce_antes_do_que_o_torneio_ja_jogou()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        var quartas = await DaFaseAsync(ctx, categoria.Id, "Quartas de Final");
        foreach (var q in quartas)
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, orgId), q, 9, 3);

        var todas = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var jaJogou = ReservasDeHorario.RelogioDoTorneio(todas);
        Assert.NotNull(jaJogou);

        // O estrago do ER, em uma linha: semifinal marcada ANTES das próprias quartas.
        foreach (var semi in await DaFaseAsync(ctx, categoria.Id, "Semifinal"))
            Assert.True(semi.HorarioPrevisto >= jaJogou,
                $"Semifinal marcada {semi.HorarioPrevisto:dd/MM HH:mm}, e o torneio já passou de {jaJogou:dd/MM HH:mm}");
    }
}
