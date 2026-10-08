using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using static Padelizou.Services.ChaveamentoMataMata;

namespace Padelizou.Tests;

// 07/10/2026 — O 1º DO GRUPO A CAÍA NA SEMIFINAL DE BAIXO.
//
// 🗣️ Felipe, com o print da prévia de uma chave de 3 grupos (6 classificados num quadro de 8):
// *"aqui esta errado, deveria ser o 1a em cima"*. A tela mostrava a Semifinal 3 como "Vencedor
// do jogo 1 × 1º do Grupo B" e a Semifinal 4 como "Vencedor do jogo 2 × 1º do Grupo A".
//
// 🕳️ Não era a prévia: era o motor. A rodada depois da primeira é montada sobre a lista
// [vencedores na ordem dos jogos, byes do melhor pro pior], cruzada primeiro × último
// (ParearVencedores). Com os byes nessa ordem, o MELHOR bye sempre cruza com o ÚLTIMO
// vencedor — a semifinal de baixo. A prévia só repetia, fielmente, o que o sábado faria.
//
// A régua de toda chave: o cabeça 1 no topo, o cabeça 2 na outra metade. Os byes entram do
// pior pro melhor (ChaveamentoMataMata.EntrantesDepoisDaPrimeiraRodada), e o melhor cruza com o
// vencedor do jogo 1.
//
// ⚠️ Categoria que já tinha a 1ª rodada criada com a régua antiga trocaria de régua no meio —
// por isso a publicação é num dia sem categoria entre a 1ª rodada e as semis (decisão do
// Felipe, 07/10/2026). Torneio encerrado não muda: o desenho dele sai dos jogos reais.
public class CabecaDeChaveNoTopoDaChaveTests
{
    // ── O print do Felipe ───────────────────────────────────────────────────────────────

    [Fact]
    public void Na_previa_de_3_grupos_o_1o_do_A_esta_na_semifinal_de_cima_e_o_1o_do_B_na_de_baixo()
    {
        var rodadas = ChaveProjetada.MontarCompleta(
            new[] { "Grupo A", "Grupo B", "Grupo C" }, 2, new[] { 3, 3, 3 });

        Assert.Equal("Quartas de Final", rodadas[0].Fase);
        Assert.Equal("Semifinal", rodadas[1].Fase);

        string Lados(ChaveProjetada.JogoProjetado j) => j.Lado1 + " | " + j.Lado2;
        var semi1 = Lados(rodadas[1].Jogos[0]);
        var semi2 = Lados(rodadas[1].Jogos[1]);

        Assert.Contains("1º do Grupo A", semi1);
        Assert.Contains("1º do Grupo B", semi2);
        // E o caminho continua ligado: quem vem pra semi de cima sai do jogo 1.
        Assert.Contains("Vencedor do jogo 1", semi1);
    }

    // ── A régua, em todo tamanho de chave que tem bye ──────────────────────────────────

    // Campanha zerada e grupos de mesmo tamanho: o bye vai pela ordem dos grupos, então o
    // melhor bye é sempre o 1º do Grupo A — e isso deixa a asserção legível.
    public static IEnumerable<object[]> ChavesComBye() => new[]
    {
        new object[] { 3 },   // 6 classificados → quadro 8: 2 jogos + 2 byes
        new object[] { 5 },   // 10 → quadro 16: 2 jogos + 6 byes
        new object[] { 6 },   // 12 → quadro 16: 4 jogos + 4 byes
        new object[] { 7 },   // 14 → quadro 16: 6 jogos + 2 byes
    };

    private static List<Classificado> Classificados(int grupos) =>
        Enumerable.Range(0, grupos)
            .SelectMany(g => new[]
            {
                new Classificado(100 + g, $"Grupo {(char)('A' + g)}", 0, 0, 1, Jogos: 2),
                new Classificado(200 + g, $"Grupo {(char)('A' + g)}", 0, 0, 2, Jogos: 2),
            })
            .ToList();

    [Theory]
    [MemberData(nameof(ChavesComBye))]
    public void O_melhor_bye_cruza_com_o_vencedor_do_jogo_1_e_o_segundo_fica_na_outra_metade(int grupos)
    {
        var (_, jogos, byes) = MontarPrimeiraFase(Classificados(grupos));
        Assert.True(byes.Count >= 2, $"{grupos} grupos deveriam ter pelo menos 2 byes.");
        Assert.Equal(100, byes[0]);   // o 1º do Grupo A
        Assert.Equal(101, byes[1]);   // o 1º do Grupo B

        // Vencedor de cada jogo representado pelo mandante — a posição na lista é o que conta.
        var vencedores = jogos.Select(j => j.Dupla1Id).ToList();
        var entrantes = EntrantesDepoisDaPrimeiraRodada(vencedores, byes);
        var proximaRodada = ParearVencedores(entrantes);

        // O primeiro jogo da rodada seguinte é o topo do quadro: o jogo 0 de cada rodada
        // recebe sempre o vencedor do jogo 0 da anterior (primeiro × último).
        var topo = proximaRodada[0];
        Assert.True(topo.Dupla1Id == vencedores[0] && topo.Dupla2Id == byes[0],
            $"{grupos} grupos: o topo saiu {topo.Dupla1Id} × {topo.Dupla2Id}, e devia ser o vencedor do jogo 1 × o 1º do Grupo A.");

        int vagas = entrantes.Count;
        Assert.NotEqual(
            LadoDaVaga(vagas, entrantes.IndexOf(byes[0])),
            LadoDaVaga(vagas, entrantes.IndexOf(byes[1])));
    }

    // A promessa antiga continua de pé com a régua nova: dois do mesmo grupo só na final.
    [Theory]
    [MemberData(nameof(ChavesComBye))]
    public void Os_dois_classificados_de_um_grupo_continuam_em_metades_opostas(int grupos)
    {
        var classificados = Classificados(grupos);
        var (_, jogos, byes) = MontarPrimeiraFase(classificados);
        var grupoDe = classificados.ToDictionary(c => c.DuplaId, c => c.Grupo);

        int vagas = jogos.Count + byes.Count;
        var metade = new Dictionary<int, int>();
        for (int k = 0; k < jogos.Count; k++)
        {
            metade[jogos[k].Dupla1Id] = LadoDaVaga(vagas, k);
            metade[jogos[k].Dupla2Id] = LadoDaVaga(vagas, k);
        }
        var posicaoDoBye = EntrantesDepoisDaPrimeiraRodada(jogos.Select(_ => -1), byes);
        foreach (var bye in byes) metade[bye] = LadoDaVaga(vagas, posicaoDoBye.IndexOf(bye));

        foreach (var doGrupo in classificados.GroupBy(c => c.Grupo))
            Assert.True(doGrupo.Select(c => metade[c.DuplaId]).Distinct().Count() == 2,
                $"{grupos} grupos: os dois do {doGrupo.Key} caíram na mesma metade.");
    }

    // ── O sábado de verdade: grupos jogados, quartas jogadas, semifinal criada pelo robô ──

    [Fact]
    public async Task Na_chave_de_verdade_a_semifinal_1_e_do_melhor_bye()
    {
        var ctx = TestInfra.NovoContexto();
        // 8 duplas → grupos de 2, 3 e 3 → 6 classificados num quadro de 8.
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id);

        async Task JogarAsync(Func<Partida, bool> quais)
        {
            var abertos = (await ctx.Partidas
                    .Where(p => p.CategoriaId == categoria.Id && p.Status != "Finalizada")
                    .OrderBy(p => p.Id).ToListAsync())
                .Where(quais).ToList();
            foreach (var jogo in abertos)
                await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id), jogo, 6, 2);
        }

        await JogarAsync(p => FasesTorneio.EhFaseDeGrupos(p.Fase));
        await JogarAsync(p => p.Fase == "Quartas de Final");

        var byes = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id, TestInfra.SemPontosDoRanking);
        Assert.Equal(2, byes.Count);

        var semis = ReservasDeHorario.NaOrdemDaFase(
            await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync(), "Semifinal");
        Assert.Equal(2, semis.Count);

        Assert.Contains(byes[0], new[] { semis[0].Dupla1Id, semis[0].Dupla2Id });
        Assert.Contains(byes[1], new[] { semis[1].Dupla1Id, semis[1].Dupla2Id });
    }
}
