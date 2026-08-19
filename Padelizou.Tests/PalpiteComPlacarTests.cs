using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O PALPITE COM PLACAR: além de quem vence, quanto a quanto.
//
// O que estes testes guardam:
//   · a LISTA de placares possíveis sai do FORMATO do jogo — com o desempate do padel (7x5) e
//     sem placar que nenhuma partida termina;
//   · quem recusa placar impossível é o SERVIÇO, não a tela;
//   · o placar e o voto nunca se contradizem — e trocar de opinião APAGA o placar velho;
//   · em jogo de 2+ sets o palpite é em SETS, porque ali os games são do set em andamento;
//   · e cravar o placar vale 3 no ranking, de ponta a ponta.
public class PalpiteComPlacarTests
{
    private static FormatoDaPartida.Formato Ate(int games, int sets = 1) =>
        new(sets, games, ContagemDeGamesDoTorneio.Ate);

    private static FormatoDaPartida.Formato Soma(int games) =>
        new(1, games, ContagemDeGamesDoTorneio.Soma);

    private static string Placar(PlacaresPossiveis.Placar p) => $"{p.Vencedor}x{p.Perdedor}";

    // ─────────────────────────── OS PLACARES POSSÍVEIS ───────────────────────────

    [Fact]
    public void Jogo_ate_6_termina_de_6x0_a_6x5_e_o_desempate_vai_a_7x5()
    {
        // ⚠️ O 7x5 é o "vencer por dois" do padel: empatou em 5x5, joga-se o desempate. É a
        // mesma paridade de FormatoDaPartida.TetoDeGames — limite PAR estende.
        Assert.Equal(
            new[] { "6x0", "6x1", "6x2", "6x3", "6x4", "6x5", "7x5" },
            PlacaresPossiveis.Do(Ate(6)).Select(Placar));
    }

    [Fact]
    public void Jogo_ate_9_NAO_estende()
    {
        // Limite ÍMPAR já embute o desempate no próprio número: 8x8 se resolve no tie-break, e
        // o 9º game É ele. Um "10x8" aqui seria um game que ninguém joga.
        var placares = PlacaresPossiveis.Do(Ate(9)).Select(Placar).ToList();

        Assert.Equal("9x0", placares.First());
        Assert.Equal("9x8", placares.Last());
        Assert.DoesNotContain("10x8", placares);
    }

    [Fact]
    public void Na_SOMA_o_placar_e_a_divisao_do_bolo_e_o_empate_fica_de_fora()
    {
        // Soma de 8: joga-se 8 games e vence quem fizer mais.
        // ⚠️ 4x4 não entra — o sistema recusa finalizar partida empatada, então palpitar empate
        // seria palpitar um final que ele próprio não deixa gravar.
        Assert.Equal(
            new[] { "8x0", "7x1", "6x2", "5x3" },
            PlacaresPossiveis.Do(Soma(8)).Select(Placar));
    }

    [Fact]
    public void Formato_de_dois_sets_palpita_SETS_e_nao_games()
    {
        var formato = Ate(6, sets: 2);

        Assert.True(PlacaresPossiveis.EmSets(formato));
        Assert.Equal(new[] { "2x0", "2x1" }, PlacaresPossiveis.Do(formato).Select(Placar));

        // Um set só continua sendo pergunta de games.
        Assert.False(PlacaresPossiveis.EmSets(Ate(6)));
    }

    [Fact]
    public void Todo_placar_oferecido_e_um_placar_que_a_partida_aceita_encerrar()
    {
        // ⚠️ ESTE é o teste que amarra as duas réguas. A lista de fichas é um RECORTE do que
        // FormatoDaPartida já considera encerrável — se um dia ela oferecer algo que a Mesa
        // recusaria, a pessoa palpitaria um placar que o jogo dela não pode terminar.
        foreach (int games in new[] { 4, 6, 9 })
        {
            var formato = Ate(games);
            foreach (var p in PlacaresPossiveis.Do(formato))
                Assert.True(FormatoDaPartida.PodeEncerrar(formato, p.Vencedor, p.Perdedor),
                    $"até {games}: {Placar(p)}");
        }

        foreach (int games in new[] { 7, 8 })
        {
            var formato = Soma(games);
            foreach (var p in PlacaresPossiveis.Do(formato))
                Assert.True(FormatoDaPartida.PodeEncerrar(formato, p.Vencedor, p.Perdedor),
                    $"soma de {games}: {Placar(p)}");
        }
    }

    [Fact]
    public void Empate_nunca_e_placar_aceito()
    {
        Assert.False(PlacaresPossiveis.Aceita(Ate(6), 5, 5));
        Assert.False(PlacaresPossiveis.Aceita(Soma(8), 4, 4));
        Assert.Null(PlacaresPossiveis.LadoVencedor(3, 3));
    }

    [Fact]
    public void O_placar_e_aceito_nos_DOIS_lados()
    {
        // 6x4 e 4x6 são o mesmo jogo visto de cada lado — os dois valem, e é o LadoVencedor que
        // diz de quem é a vitória.
        Assert.True(PlacaresPossiveis.Aceita(Ate(6), 6, 4));
        Assert.True(PlacaresPossiveis.Aceita(Ate(6), 4, 6));
        Assert.Equal(1, PlacaresPossiveis.LadoVencedor(6, 4));
        Assert.Equal(2, PlacaresPossiveis.LadoVencedor(4, 6));
    }

    // ─────────────────────────── A RÉGUA, EM SETS ───────────────────────────

    [Fact]
    public void Em_SETS_nao_existe_chegar_perto()
    {
        // Melhor de 3 termina 2x0 ou 2x1. Com a folga de um game valendo aqui, errar seria
        // matematicamente impossível: todo mundo levaria 2 ou 3, e a faixa de baixo sumiria do
        // formato inteiro. Ou se crava, ou vale o ponto do vencedor.
        var errouOPlacar = new PalpiteConferido
        {
            DuplaEscolhidaId = 1,
            VencedorId = 1,
            PalpitouLado1 = 2, PalpitouLado2 = 0,
            PlacarLado1 = 2, PlacarLado2 = 1,
            EmSets = true,
        };
        Assert.Equal(PontosDoPalpite.SoOVencedor, PontosDoPalpite.De(errouOPlacar));

        Assert.Equal(PontosDoPalpite.Cravou,
            PontosDoPalpite.De(errouOPlacar with { PalpitouLado2 = 1 }));
    }

    // ─────────────────────────── O SERVIÇO ───────────────────────────

    // Um torneio com um jogo AGENDADO (palpitável) na fase de grupos, no formato pedido.
    private static async Task<(Torneio torneio, Partida partida, List<Dupla> duplas)>
        MontarJogoAgendadoAsync(DbPadelContext ctx, int gamesDosGrupos = 6, int setsDosGrupos = 1,
            string contagem = ContagemDeGamesDoTorneio.Ate)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        torneio.GamesFaseGrupos = gamesDosGrupos;
        torneio.SetsFaseGrupos = setsDosGrupos;
        torneio.ContagemDeGames = contagem;

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = FasesTorneio.FaseDeGrupos,
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (torneio, partida, duplas);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string cpf)
    {
        var torcedor = new Jogador { Nome = "Torcedor", Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    [Fact]
    public async Task Palpitar_um_placar_que_NAO_fecha_o_jogo_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx, gamesDosGrupos: 6);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000001");
        var servico = new PalpiteService(ctx);

        // ⚠️ Num jogo até 6, "9 x 4" não é placar de coisa nenhuma. Quem recusa é o SERVIÇO: a
        // tela oferece fichas, mas um POST montado à mão não passa por view nenhuma.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 9, 4));

        Assert.Contains("9 x 4", erro.Message);
        Assert.Empty(ctx.PalpitesPartida.ToList());
    }

    [Fact]
    public async Task Placar_que_aponta_a_OUTRA_dupla_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000002");
        var servico = new PalpiteService(ctx);

        // Votou na Dupla 1 e escreveu um placar em que ela PERDE. São duas respostas que se
        // contradizem, e escolher uma pela pessoa seria o sistema decidindo o que ela quis.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 4, 6));

        Assert.Empty(ctx.PalpitesPartida.ToList());
    }

    [Fact]
    public async Task Meio_placar_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000003");
        var servico = new PalpiteService(ctx);

        // Completar o lado que faltou com zero inventaria um palpite que ninguém deu; ignorar
        // em silêncio faria a tela dizer "registrado" pra um placar que não foi gravado.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 6, null));
    }

    [Fact]
    public async Task Palpite_SEM_placar_continua_sendo_o_caminho_de_sempre()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000004");
        var servico = new PalpiteService(ctx);

        var resumo = await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id);

        var gravado = ctx.PalpitesPartida.Single();
        Assert.Equal(duplas[0].Id, gravado.DuplaEscolhidaId);

        // ⚠️ NULO, nunca zero: "não palpitou o placar" e "palpitou 0x0" são coisas diferentes,
        // e a segunda é um placar que não existe.
        Assert.Null(gravado.GamesDupla1);
        Assert.Null(gravado.GamesDupla2);
        Assert.False(resumo.PalpiteiOPlacar);
    }

    [Fact]
    public async Task O_placar_palpitado_volta_no_resumo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx, gamesDosGrupos: 6);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000005");
        var servico = new PalpiteService(ctx);

        var resumo = await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 7, 5);

        Assert.True(resumo.PalpiteiOPlacar);
        Assert.Equal(7, resumo.MeuPlacarLado1);
        Assert.Equal(5, resumo.MeuPlacarLado2);
        Assert.False(resumo.PlacarEmSets);

        var gravado = ctx.PalpitesPartida.Single();
        Assert.Equal(7, gravado.GamesDupla1);
        Assert.Null(gravado.SetsDupla1);
    }

    [Fact]
    public async Task Trocar_de_opiniao_sem_dizer_o_placar_APAGA_o_placar_anterior()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000006");
        var servico = new PalpiteService(ctx);

        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 6, 2);

        // Mudou de ideia: agora acha que a outra dupla vence, e não disse o placar.
        await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[1].Id);

        // ⚠️ Sem apagar, a linha ficaria com "vence a Dupla 2" e "6 x 2" (que aponta a Dupla 1)
        // gravados juntos — e o ranking leria isso como palpite de placar, contando CONTRA a
        // própria pessoa um placar que ela abandonou.
        var gravado = ctx.PalpitesPartida.Single();
        Assert.Equal(duplas[1].Id, gravado.DuplaEscolhidaId);
        Assert.Null(gravado.GamesDupla1);
        Assert.Null(gravado.GamesDupla2);
    }

    [Fact]
    public async Task Num_jogo_de_DOIS_SETS_o_palpite_e_gravado_em_sets()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx, gamesDosGrupos: 6, setsDosGrupos: 2);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000007");
        var servico = new PalpiteService(ctx);

        var resumo = await servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 2, 1);

        // ⚠️ Nas colunas de SETS, não nas de games: ali `Partida.GamesDupla1` guarda os games do
        // set em andamento, e um palpite de games seria conferido contra um pedaço do jogo.
        var gravado = ctx.PalpitesPartida.Single();
        Assert.Equal(2, gravado.SetsDupla1);
        Assert.Equal(1, gravado.SetsDupla2);
        Assert.Null(gravado.GamesDupla1);
        Assert.True(resumo.PlacarEmSets);

        // E "6 x 4" não é placar de jogo de dois sets.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 6, 4));
    }

    [Fact]
    public async Task Jogo_que_ja_comecou_nao_aceita_palpite_de_placar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, partida, duplas) = await MontarJogoAgendadoAsync(ctx);
        var torcedor = await NovoTorcedorAsync(ctx, "55510000008");
        var servico = new PalpiteService(ctx);

        partida.Status = "Em Andamento";
        await ctx.SaveChangesAsync();

        // A trava é a mesma de sempre — palpitar com a bola rolando é palpitar sabendo.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.RegistrarVotoAsync(partida.Id, torcedor.Id, duplas[0].Id, 6, 4));
    }

    // ─────────────────────────── DE PONTA A PONTA ───────────────────────────

    [Fact]
    public async Task Cravar_o_placar_vale_3_no_ranking_do_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, partida, duplas) = await MontarJogoAgendadoAsync(ctx, gamesDosGrupos: 6);
        torneio.Status = "Finalizado";

        var cravou = await NovoTorcedorAsync(ctx, "55510000009");
        var chegouPerto = await NovoTorcedorAsync(ctx, "55510000010");
        var soOVencedor = await NovoTorcedorAsync(ctx, "55510000011");
        var servico = new PalpiteService(ctx);

        await servico.RegistrarVotoAsync(partida.Id, cravou.Id, duplas[0].Id, 6, 4);
        await servico.RegistrarVotoAsync(partida.Id, chegouPerto.Id, duplas[0].Id, 6, 3);
        await servico.RegistrarVotoAsync(partida.Id, soOVencedor.Id, duplas[0].Id);

        // O jogo aconteceu: 6 x 4 pra Dupla 1.
        partida.GamesDupla1 = 6;
        partida.GamesDupla2 = 4;
        partida.VencedorId = duplas[0].Id;
        partida.Status = "Finalizada";
        await ctx.SaveChangesAsync();

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        Assert.Equal(new[] { 3, 2, 1 }, ranking!.Linhas.Select(l => l.Pontos));
        Assert.Equal(cravou.Id, ranking.Linhas[0].JogadorId);
        Assert.Equal(1, ranking.Linhas[0].Cravadas);
        Assert.Equal(0, ranking.Linhas[1].Cravadas);

        // Os três acertaram o vencedor — o que separa os pontos é só o placar.
        Assert.All(ranking.Linhas, l => Assert.Equal(1, l.Acertos));

        // E agora a tela PODE falar em cravar placar: houve palpite com placar neste torneio.
        Assert.Equal(2, ranking.PalpitesComPlacar);
    }
}
