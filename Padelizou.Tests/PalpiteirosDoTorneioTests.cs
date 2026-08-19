using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// OS PALPITEIROS: quem mais acertou no palpitrômetro do torneio.
//
// O que estes testes guardam, em ordem de importância:
//   · a RÉGUA — cravou 3, perto 2, vencedor 1, errou 0 — e o fato de ela ordenar por PONTOS,
//     com o aproveitamento só desempatando (o caso 9/11 × 8/8, que é a decisão do Felipe);
//   · o palpite ANTIGO, sem placar, continua valendo o ponto do vencedor — é isso que faz o
//     ranking nascer com o histórico inteiro em vez de zerado;
//   · quem estava EM QUADRA não pontua no próprio jogo (e em categoria de TIMES ninguém é
//     excluído, porque ali o Jogador1 é quem cadastrou, não quem joga);
//   · e a conta é feita NA HORA, então placar corrigido acerta o ranking sozinho.
public class PalpiteirosDoTorneioTests
{
    // Um jogo que terminou 6 x 4 pra Dupla 1 (id 1), pra encurtar os testes da régua.
    private static PalpiteConferido Palpite(int escolheu, int? palpitou1 = null, int? palpitou2 = null,
        bool porWo = false) => new()
        {
            DuplaEscolhidaId = escolheu,
            VencedorId = 1,
            PalpitouLado1 = palpitou1,
            PalpitouLado2 = palpitou2,
            PlacarLado1 = 6,
            PlacarLado2 = 4,
            PorWo = porWo,
        };

    // ─────────────────────────── A RÉGUA ───────────────────────────

    [Fact]
    public void Cravar_o_placar_vale_tres_chegar_perto_dois_e_acertar_so_o_vencedor_um()
    {
        // A escada inteira, no mesmo jogo 6 x 4.
        Assert.Equal(3, PontosDoPalpite.De(Palpite(escolheu: 1, 6, 4)));   // cravou
        Assert.Equal(2, PontosDoPalpite.De(Palpite(escolheu: 1, 6, 3)));   // errou um game
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, 6, 0)));   // só o vencedor
        Assert.Equal(0, PontosDoPalpite.De(Palpite(escolheu: 2, 4, 6)));   // errou o vencedor
    }

    [Fact]
    public void Errar_o_vencedor_nao_vale_NADA_por_mais_perto_que_o_placar_esteja()
    {
        // ⚠️ Escolheu a dupla 2 e escreveu o placar exato do jogo. Não pontua: o palpitrômetro
        // pergunta QUEM VENCE, e o placar é a segunda pergunta — quem erra a primeira errou.
        Assert.Equal(0, PontosDoPalpite.De(Palpite(escolheu: 2, 6, 4)));
    }

    [Fact]
    public void Palpite_SEM_placar_continua_valendo_o_ponto_do_vencedor()
    {
        // ⚠️ É ISTO que faz o ranking nascer com o HISTÓRICO INTEIRO. Todo palpite gravado antes
        // de o placar entrar na tela chega sem os dois números; se isso valesse zero, o ranking
        // começaria vazio no dia da publicação e todo torneio já jogado sumiria dele.
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1)));

        // Meio palpite de placar é palpite nenhum — não dá pra comparar um lado só.
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, palpitou1: 6)));
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, palpitou2: 4)));
    }

    [Fact]
    public void Errar_um_game_de_CADA_lado_ainda_e_chegar_perto()
    {
        // ⚠️ A folga é POR LADO, não pela SOMA das duas diferenças. Este teste é o que segura
        // isso: pela soma, 7 x 5 contra um 6 x 4 real "erra por 2" e cairia pra 1 ponto — e no
        // formato de SOMA (joga-se N games e acabou) TODO chute vizinho mexe nos dois números
        // ao mesmo tempo, o que mataria a faixa do meio inteira, caladinha, só naquele formato.
        Assert.Equal(2, PontosDoPalpite.De(Palpite(escolheu: 1, 7, 5)));
        Assert.Equal(2, PontosDoPalpite.De(Palpite(escolheu: 1, 5, 3)));
    }

    [Fact]
    public void Errar_dois_games_de_um_lado_ja_nao_e_chegar_perto()
    {
        // 6 x 2 num jogo que terminou 6 x 4: acertou o vencedor e nada mais.
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, 6, 2)));
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, 8, 4)));
    }

    [Fact]
    public void W_O_nao_tem_placar_pra_cravar()
    {
        // ⚠️ O jogo encerrado por não comparecimento é gravado com o placar convencional da
        // fase — um 6 x 4 que ninguém jogou. Quem acertou o vencedor leva o ponto; a "cravada"
        // seria sorteada entre quem chutou o placar mais comum da tabela.
        Assert.Equal(1, PontosDoPalpite.De(Palpite(escolheu: 1, 6, 4, porWo: true)));

        // E errar o vencedor de um W.O. continua valendo zero, como em qualquer jogo.
        Assert.Equal(0, PontosDoPalpite.De(Palpite(escolheu: 2, 4, 6, porWo: true)));
    }

    [Fact]
    public void Jogo_sem_vencedor_nao_pontua_ninguem()
    {
        var semVencedor = new PalpiteConferido { DuplaEscolhidaId = 1, VencedorId = null };
        Assert.Equal(0, PontosDoPalpite.De(semVencedor));
    }

    [Fact]
    public void Jogo_sem_placar_gravado_ainda_paga_o_acerto_do_vencedor()
    {
        // Jogo antigo finalizado com vencedor e sem games (acontece na base): comparar placar é
        // impossível, e o acerto do vencedor não pode sumir por causa disso.
        var semPlacar = new PalpiteConferido
        {
            DuplaEscolhidaId = 1,
            VencedorId = 1,
            PalpitouLado1 = 6,
            PalpitouLado2 = 4,
        };
        Assert.Equal(1, PontosDoPalpite.De(semPlacar));
    }

    // ─────────────────────────── A CLASSIFICAÇÃO ───────────────────────────

    private static PalpiteiroNoRanking Linha(int id, string nome, int pontos, int acertos, int palpites) =>
        new() { JogadorId = id, Nome = nome, Pontos = pontos, Acertos = acertos, Palpites = palpites };

    [Fact]
    public void Quem_acerta_9_de_11_fica_na_frente_de_quem_acerta_8_de_8()
    {
        // ⚠️ ESTE É O TESTE DA DECISÃO. Se ele inverter, a régua mudou de "soma pontos" pra
        // "média de acertos" — e aí o piso mínimo de palpites tem que voltar junto, senão quem
        // acertou 1 de 1 lidera com 100% pra sempre.
        var classificacao = RankingDePalpiteiros.Classificar(new[]
        {
            Linha(1, "Oito de Oito", pontos: 8, acertos: 8, palpites: 8),
            Linha(2, "Nove de Onze", pontos: 9, acertos: 9, palpites: 11),
        });

        Assert.Equal("Nove de Onze", classificacao[0].Nome);
        Assert.Equal(100, classificacao[1].Aproveitamento);
    }

    [Fact]
    public void O_aproveitamento_SO_desempata()
    {
        var classificacao = RankingDePalpiteiros.Classificar(new[]
        {
            Linha(1, "Chutou Muito", pontos: 3, acertos: 3, palpites: 6),   // 50%
            Linha(2, "Escolheu Bem", pontos: 3, acertos: 3, palpites: 3),   // 100%
        });

        Assert.Equal("Escolheu Bem", classificacao[0].Nome);
        Assert.Equal(1, classificacao[0].Posicao);
        Assert.Equal(2, classificacao[1].Posicao);
    }

    [Fact]
    public void Empate_divide_a_POSICAO()
    {
        // 1, 2, 2, 4 — e não 1, 2, 3, 4. Numerar em sequência poria dois desempenhos idênticos
        // em lugares diferentes por causa da ordem alfabética, que é a primeira pergunta a
        // voltar: "por que ele está na minha frente se empatamos?".
        var classificacao = RankingDePalpiteiros.Classificar(new[]
        {
            Linha(1, "Ana", pontos: 5, acertos: 5, palpites: 5),
            Linha(2, "Bruno", pontos: 3, acertos: 3, palpites: 3),
            Linha(3, "Carla", pontos: 3, acertos: 3, palpites: 3),
            Linha(4, "Diego", pontos: 1, acertos: 1, palpites: 2),
        });

        Assert.Equal(new[] { 1, 2, 2, 4 }, classificacao.Select(l => l.Posicao));
    }

    [Fact]
    public void A_ordem_e_TOTAL_e_nao_muda_entre_duas_visitas()
    {
        // Ordenação parcial faz a lista trocar de ordem entre dois carregamentos da MESMA
        // página — ninguém reporta isso como defeito, só desconfia da tela.
        var entrada = new[]
        {
            Linha(9, "Ana", pontos: 2, acertos: 2, palpites: 2),
            Linha(2, "Ana", pontos: 2, acertos: 2, palpites: 2),
            Linha(7, "Bruno", pontos: 2, acertos: 2, palpites: 2),
        };

        var primeira = RankingDePalpiteiros.Classificar(entrada);
        var segunda = RankingDePalpiteiros.Classificar(entrada.Reverse().ToArray());

        Assert.Equal(primeira.Select(l => l.JogadorId), segunda.Select(l => l.JogadorId));
        Assert.Equal(new[] { 2, 9, 7 }, primeira.Select(l => l.JogadorId));
    }

    // ─────────────────────────── O QUE VEM DO BANCO ───────────────────────────

    // Um torneio finalizado com UMA partida terminada entre as duas duplas.
    private static async Task<(Torneio torneio, Categoria categoria, Partida partida)>
        MontarJogoTerminadoAsync(DbPadelContext ctx, int games1 = 6, int games2 = 4)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            VencedorId = games1 > games2 ? duplas[0].Id : duplas[1].Id,
            GamesDupla1 = games1,
            GamesDupla2 = games2,
            Status = "Finalizada",
            Fase = "Final",
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (torneio, categoria, partida);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static async Task PalpitarAsync(DbPadelContext ctx, Partida partida, int jogadorId, int duplaId)
    {
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id,
            JogadorId = jogadorId,
            DuplaEscolhidaId = duplaId,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Quem_estava_EM_QUADRA_nao_pontua_no_proprio_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        // O vencedor palpitou em si mesmo, e um torcedor palpitou no mesmo lado.
        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor da Arquibancada", "55500000001");
        await PalpitarAsync(ctx, partida, duplas[0].Jogador1Id, duplas[0].Id);
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[0].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        // ⚠️ Os quatro em quadra são os únicos que podem MUDAR o resultado do próprio palpite.
        // O voto continua gravado (e conta na barra do palpitrômetro); só não vale ponto.
        Assert.Single(ranking!.Linhas);
        Assert.Equal(torcedor.Id, ranking.Linhas[0].JogadorId);
        Assert.Equal(1, ranking.Linhas[0].Pontos);
        Assert.Equal(1, ranking.JogosApurados);
    }

    [Fact]
    public async Task Em_categoria_de_TIMES_ninguem_e_excluido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);

        // ⚠️ Na linha de TIME o Jogador1 é quem CADASTROU o time, não quem entra em quadra —
        // excluí-lo tiraria o acerto de quem talvez nem tenha jogado.
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        duplas[0].NomeTime = "Time da Casa";
        duplas[1].NomeTime = "Time Visitante";
        await ctx.SaveChangesAsync();

        await PalpitarAsync(ctx, partida, duplas[0].Jogador1Id, duplas[0].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        Assert.Single(ranking!.Linhas);
        Assert.Equal(1, ranking.Linhas[0].Pontos);
    }

    [Fact]
    public async Task Jogo_que_ainda_NAO_terminou_fica_fora_da_conta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);

        // ⚠️ A Mesa grava o placar game a game: um jogo no ar já tem 4 x 1 no banco. Apurar por
        // ele diria quem está GANHANDO, não quem ganhou — e o ranking mudaria a cada refresh.
        partida.Status = "Em Andamento";
        partida.VencedorId = null;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor Ansioso", "55500000002");
        await PalpitarAsync(ctx, partida, torcedor.Id, partida.Dupla1Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        Assert.Empty(ranking!.Linhas);
        Assert.False(ranking.TemRanking);
    }

    [Fact]
    public async Task O_placar_corrigido_pelo_organizador_acerta_o_ranking_na_visita_seguinte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor Fiel", "55500000003");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[1].Id);

        // Como o jogo está gravado, ele errou.
        var antes = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);
        Assert.Equal(0, antes!.Linhas[0].Pontos);

        // O organizador conserta o placar: quem venceu era o outro lado.
        partida.GamesDupla1 = 4;
        partida.GamesDupla2 = 6;
        partida.VencedorId = duplas[1].Id;
        await ctx.SaveChangesAsync();

        // ⚠️ Nenhum ponto é GRAVADO — a conta é refeita a cada visita. Ponto gravado ficaria
        // congelado no placar errado, e ninguém descobriria.
        var depois = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);
        Assert.Equal(1, depois!.Linhas[0].Pontos);
    }

    [Fact]
    public async Task Torneio_sem_palpite_nenhum_nao_tem_ranking()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarJogoTerminadoAsync(ctx);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        // A página devolve 404 com isto — link que só sabe decepcionar não vale a pena existir.
        Assert.False(ranking!.TemRanking);
        Assert.Equal(0, ranking.JogosApurados);
    }

    [Fact]
    public async Task Torneio_que_nao_existe_devolve_nulo()
    {
        using var ctx = TestInfra.NovoContexto();
        Assert.Null(await RankingDePalpiteiros.DoTorneioAsync(ctx, torneioId: 4242, olhandoId: null));
    }

    [Fact]
    public async Task Enquanto_ninguem_palpita_PLACAR_a_tela_nao_fala_em_cravar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor Antigo", "55500000004");
        await PalpitarAsync(ctx, partida, torcedor.Id, partida.Dupla1Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        // ⚠️ A pergunta é feita AO DADO, nunca a um interruptor: torneio jogado antes de o
        // placar existir no palpitrômetro tem zero aqui pra sempre, e mostrar a ele uma régua
        // de "cravou 3" explicaria um jeito de pontuar que ninguém daquele torneio teve.
        Assert.Equal(0, ranking!.PalpitesComPlacar);
        Assert.Equal(0, ranking.Linhas[0].Cravadas);
    }
}
