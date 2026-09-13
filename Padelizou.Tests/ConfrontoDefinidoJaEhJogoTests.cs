using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 13/09/2026 — CONFRONTO DEFINIDO TEM QUE SER JOGO, MESMO QUE O ANTERIOR NÃO ESTEJA.
//
// 🗣️ Felipe, 12/09: *"eu preciso que todo jogo com confronto definido ja seja possivel palpitar
// e começar se preciso, tratar ele como um jogo pronto para iniciar"*. E de novo em 13/09, com
// o print da Semifinal 2 da 4ª Masculina às 13:00: *"O jogo ja está definido e nao esta
// aparecendo de novo"*.
//
// 🕳️ O FLAGRANTE, no 2ª Etapa ER PADEL TOUR: as Quartas 2 e 3 tinham terminado (9x8 e 4x9), o
// que define a Semifinal 2 inteira — Felipe/Guilherme × Lucas/Alexandre. Mas a Semifinal 1
// depende do jogo 8, que estava AO VIVO em 0 x 0. O laço do robô PARAVA no primeiro confronto
// que não dava pra montar:
//
//     for (int i = jaCriados; i < vagas.Count / 2; i++)
//         if (vagas[i] is not int lado1 || vagas[^(i+1)] is not int lado2) break;   // ← aqui
//
// e a Semifinal 2 não nascia. Sem Partida não há palpite, não há iniciar, não há nada.
//
// ⚠️ O `break` NÃO ERA DESCUIDO — era a única saída correta enquanto o NÚMERO DO JOGO DENTRO
// DA FASE fosse deduzido da ordem de criação (`ReservasDeHorario.NumeroNaFase`, por Id). Criar
// a Semifinal 2 antes da 1 a transformaria em "Semifinal 1", e SETE pontos leem esse número:
// o pareamento da fase seguinte, o desenho do quadro, a projeção ("Vencedor Semifinal 2"), o
// card da chave, a numeração do "Meus jogos", a comparação do painel "Refazer como previsto" e
// as reservas de horário do organizador.
//
// ✅ A SAÍDA É GRAVAR O NÚMERO em vez de deduzi-lo. `Partida.NumeroNaFase` é anulável e **nulo
// = deduz pelo Id, letra por letra como antes** — nenhum jogo existente muda de número.
public class ConfrontoDefinidoJaEhJogoTests
{
    // 12 duplas → 4 grupos de 3 → 8 classificados, quadro de 8 SEM BYE: Quartas com 4 jogos.
    // É a forma do ER, e a menor que tem uma semifinal capaz de ficar meio definida.
    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Categoria Categoria, int OrgId)>
        ComAsQuartasMontadasAsync()
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

    // Os jogos de uma fase, na ORDEM DO QUADRO (a numeração oficial), e não por Id.
    private static async Task<List<Partida>> DaFaseAsync(DbPadelContext ctx, int categoriaId, string fase)
    {
        var todas = await ctx.Partidas.Where(p => p.CategoriaId == categoriaId).ToListAsync();
        var numero = ReservasDeHorario.NumeroNaFase(todas);
        return todas.Where(p => p.Fase == fase).OrderBy(p => numero[p.Id]).ToList();
    }

    private static async Task<List<Partida>> QuartasAsync(DbPadelContext ctx, int categoriaId) =>
        await DaFaseAsync(ctx, categoriaId, "Quartas de Final");

    // Encerra a Quarta de número `numero` (1..4) do quadro.
    private static async Task VencerQuartaAsync(
        DbPadelContext ctx, int orgId, int categoriaId, int numero, int games1, int games2)
    {
        var quartas = await QuartasAsync(ctx, categoriaId);
        await TestInfra.FinalizarComPlacarAsync(
            ctx, TestInfra.NovoTorneiosController(ctx, orgId), quartas[numero - 1], games1, games2);
    }

    [Fact]
    public async Task A_semifinal_2_nasce_com_as_quartas_2_e_3_decididas_mesmo_com_a_1_em_aberto()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        Assert.Equal(4, (await QuartasAsync(ctx, categoria.Id)).Count);

        // O pareamento cruza a vaga i com a n-1-i: a Semifinal 2 é vencedor(Quartas 2) ×
        // vencedor(Quartas 3). É exatamente o estado da 4ª Masculina no ER.
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);

        var semis = await DaFaseAsync(ctx, categoria.Id, "Semifinal");

        // Antes: zero. O laço parava na Semifinal 1, que depende das Quartas 1 e 4.
        Assert.Single(semis);
    }

    [Fact]
    public async Task E_ela_e_a_SEMIFINAL_2_e_nao_a_1()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);

        var todas = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        var semi = todas.Single(p => p.Fase == "Semifinal");

        // ⚠️ O CORAÇÃO DA MUDANÇA. Sem o número gravado, o único jogo da fase seria "Semifinal
        // 1" por ser o primeiro Id — e a projeção, o quadro, o card, o "Meus jogos" e a reserva
        // de horário do organizador passariam todos a apontar pro jogo errado.
        Assert.Equal(2, ReservasDeHorario.NumeroNaFase(todas)[semi.Id]);
    }

    [Fact]
    public async Task Os_dois_lados_dela_sao_os_vencedores_das_quartas_2_e_3()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);

        var quartas = await QuartasAsync(ctx, categoria.Id);
        var semi = (await DaFaseAsync(ctx, categoria.Id, "Semifinal")).Single();

        Assert.Equal(
            new[] { quartas[1].VencedorId!.Value, quartas[2].VencedorId!.Value }.OrderBy(x => x).ToArray(),
            new[] { semi.Dupla1Id, semi.Dupla2Id }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task A_semifinal_1_nasce_depois_e_o_quadro_le_1_depois_2()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        // Fora de ordem de propósito: a 2 primeiro, a 1 depois.
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 1, 9, 2);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 4, 5, 9);

        var semis = await DaFaseAsync(ctx, categoria.Id, "Semifinal");
        Assert.Equal(2, semis.Count);

        var quartas = await QuartasAsync(ctx, categoria.Id);

        // Semifinal 1 = vencedor(Quartas 1) × vencedor(Quartas 4); Semifinal 2 = 2 × 3.
        Assert.Equal(
            new[] { quartas[0].VencedorId!.Value, quartas[3].VencedorId!.Value }.OrderBy(x => x).ToArray(),
            new[] { semis[0].Dupla1Id, semis[0].Dupla2Id }.OrderBy(x => x).ToArray());
        Assert.Equal(
            new[] { quartas[1].VencedorId!.Value, quartas[2].VencedorId!.Value }.OrderBy(x => x).ToArray(),
            new[] { semis[1].Dupla1Id, semis[1].Dupla2Id }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task A_final_pareia_certo_depois_de_uma_fase_criada_fora_de_ordem()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 1, 9, 2);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 4, 5, 9);

        var semis = await DaFaseAsync(ctx, categoria.Id, "Semifinal");
        foreach (var semi in semis)
        {
            await TestInfra.FinalizarComPlacarAsync(
                ctx, TestInfra.NovoTorneiosController(ctx, orgId), semi, 9, 3);
        }

        var final = (await DaFaseAsync(ctx, categoria.Id, "Final")).Single();

        // ⚠️ ESTE É O TESTE QUE PEGA O ESTRAGO SILENCIOSO. Se a fase fosse lida por Id, a
        // Semifinal 2 (criada primeiro) seria a "vaga 1" do pareamento e a Final sairia entre
        // as duplas erradas — sem erro nenhum, só com o jogo errado na tela.
        Assert.Equal(
            new[] { semis[0].VencedorId!.Value, semis[1].VencedorId!.Value }.OrderBy(x => x).ToArray(),
            new[] { final.Dupla1Id, final.Dupla2Id }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Duas_partidas_nao_podem_ser_a_mesma_semifinal_2()
    {
        using var ctx = TestInfra.NovoContexto();

        var indice = ctx.Model.FindEntityType(typeof(Partida))!
            .GetIndexes()
            .FirstOrDefault(i => i.IsUnique
                && i.Properties.Select(p => p.Name).SequenceEqual(
                       new[] { "CategoriaId", "Fase", "NumeroNaFase" }));

        // ⚠️ O GATE OLHA O MODELO PORQUE O EF InMemory NÃO APLICA ÍNDICE ÚNICO — a suíte
        // inteira gravaria duas "Semifinal 2" sem reclamar. Quem impede de verdade é o
        // Postgres; o que dá pra travar aqui é a DECLARAÇÃO não sumir num refactor.
        //
        // É a trava contra a corrida que o contador de antes deixava passar: ele era lido
        // ANTES do INSERT, então dois encerramentos simultâneos criavam a mesma semifinal duas
        // vezes. (Nulo não conflita no Postgres, então o acervo inteiro convive com ele.)
        Assert.NotNull(indice);
    }

    [Fact]
    public async Task A_projecao_cita_o_numero_certo_da_semifinal_que_falta()
    {
        var (ctx, torneio, categoria, orgId) = await ComAsQuartasMontadasAsync();
        using var _ctx = ctx;

        await VencerQuartaAsync(ctx, orgId, categoria.Id, 2, 9, 8);
        await VencerQuartaAsync(ctx, orgId, categoria.Id, 3, 4, 9);

        var controller = TestInfra.NovoTorneiosController(ctx, orgId);
        await controller.Details(torneio.Id, null, null);
        var projecao = controller.ViewBag.ProjecaoCompleta as List<ProximasFasesDaChave.JogoQueVem>
                       ?? new List<ProximasFasesDaChave.JogoQueVem>();

        // A Semifinal 2 já é real; a projeção não pode continuar prometendo uma "Semifinal 2"
        // por vir, nem rebatizar a que falta de "Semifinal 2".
        var semisProjetadas = projecao.Where(j => j.Fase == "Semifinal").ToList();
        Assert.Single(semisProjetadas);
        Assert.Equal(1, semisProjetadas[0].Numero);
    }
}
