using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O QUE ACONTECE NO FIM DE UM JOGO NÃO PODE DEPENDER DE POR ONDE O PLACAR FOI LANÇADO.
//
// O organizador encerra partida por dois caminhos, e os dois são usados no mesmo torneio:
//   • Mesa de Controle / botão do card  → TorneiosController.FinalizarPartida
//   • Controle de Placar em tela cheia  → PartidasController.ControlePlacar
//
// Auditando isso em 06/08/2026 — depois de já ter unificado o robô de chaveamento — as duas
// telas AINDA divergiam em três pontos, cada um invisível de um lado:
//
//   1. a tela cheia não movia o PADELÍMETRO (o nível dos 4 jogadores não mudava e o extrato
//      do perfil ficava sem a linha daquele jogo);
//   2. a Mesa não gerava a FINAL DO AMERICANO (o torneio acabava as rodadas e ficava parado
//      esperando uma final que nenhum robô ia criar);
//   3. a Mesa não disparava o "SEU JOGO É O PRÓXIMO" — justamente a tela do dia de torneio
//      era a que deixava o próximo par sem aviso.
//
// Cada teste aqui roda o MESMO cenário pelas duas telas. É a única forma de garantia que
// funciona neste projeto: os defeitos vieram todos de uma segunda cópia que ninguém exercita.
public class EncerramentoIgualNasDuasTelasTests
{
    // Como o jogo vai ser encerrado. Os testes rodam os dois valores.
    public enum Tela { Mesa, TelaCheia }

    private static async Task FinalizarAsync(DbPadelContext ctx, Tela tela, Partida partida,
        Jogador organizador, IPushNotificationService push)
    {
        partida.GamesDupla1 = 6;
        partida.GamesDupla2 = 2;
        await ctx.SaveChangesAsync();

        if (tela == Tela.Mesa)
            await TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push)
                .FinalizarPartida(partida.Id);
        else
            await TestInfra.NovoPartidasController(ctx, organizador.Id, push: push)
                .ControlePlacar(partida.Id, "Finalizada", 6, 2, partida.NomeQuadra, null);
    }

    // ---- 1. PADELÍMETRO ----

    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task O_padelimetro_anda_pelas_duas_telas(Tela tela)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var partida = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Final");

        await FinalizarAsync(ctx, tela, partida, org, Substitute.For<IPushNotificationService>());

        // Os 4 jogadores entram no Padelímetro: a linha de entrada (PartidaId nulo) e a do
        // jogo. Sem o gancho, o extrato ficava vazio e o perfil dizia "Ranking: 0 pts".
        Assert.Equal(4, ctx.HistoricosDePadelimetro.Count(h => h.PartidaId == partida.Id));
        Assert.Equal(4, ctx.Jogadores.Count(j => j.Padelimetro != null));
    }

    // ---- 2. O DESFECHO DO AMERICANO ----
    //
    // ⚠️ Este teste já exigiu o CONTRÁRIO: que o robô montasse uma "Final" cruzando os 4
    // melhores. Isso saiu em 06/08/2026, quando rodar o formato inteiro no navegador mostrou
    // que aquela final dava ao torneio DOIS campeões — a tabela coroava o líder em games e a
    // conquista do perfil coroava quem vencesse a final (a líder com 56 games ficou sem
    // título; o 2º colocado, com 53, levou).
    //
    // A regra (Felipe): vence quem fez mais games, e só há partida extra se DOIS OU MAIS
    // empatarem na liderança. A intenção do teste continua a mesma — o desfecho não pode
    // depender de por qual tela o placar foi lançado.
    //
    // Cenário: 4 jogadores, um jogo só de 6x2. Os dois vencedores terminam empatados em 6
    // games — é empate na liderança, e este torneio não previu desempate (MontarTorneio
    // deixa a opção desligada). Então: ninguém é coroado pelo sistema, nenhuma partida nova
    // nasce, e o título fica com o organizador.
    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task O_americano_nao_inventa_final_pelas_duas_telas(Tela tela)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var rodada = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Americano 1");

        await FinalizarAsync(ctx, tela, rodada, org, Substitute.For<IPushNotificationService>());

        // O defeito: uma "Final" que ninguém pediu.
        Assert.Null(await ctx.Partidas.SingleOrDefaultAsync(
            p => p.CategoriaId == categoria.Id && p.Fase == "Final"));

        // E nada de coroar alguém no meio de um empate.
        Assert.False(await ctx.Duplas.AnyAsync(
            d => d.CategoriaId == categoria.Id && d.UltimaFase == "Campeao"));

        // As rodadas acabaram: o torneio encerra do mesmo jeito pelas duas telas.
        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // O caminho NORMAL do Americano: alguém fez mais games que todo mundo e leva o título sem
    // jogar mais nada. Dois jogos com os parceiros trocados, como manda o formato — é o que
    // desempata os quatro e produz um líder isolado.
    //
    // ⚠️ O campeão do Americano é UMA PESSOA. O carimbo vai numa linha SEM parceiro: coroar a
    // dupla de uma rodada daria o título também a quem calhou de jogar junto naquele jogo.
    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task Lider_em_games_e_coroado_pelas_duas_telas(Tela tela)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");

        // Materializa antes de achatar: o provedor do EF não traduz a projeção pra array.
        var jogadores = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)
            .OrderBy(d => d.Id)
            .ToList()
            .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id!.Value })
            .ToList();
        var (a, b, c, d4) = (jogadores[0], jogadores[1], jogadores[2], jogadores[3]);

        // Rodada 1: (a,b) 6 x 2 (c,d)   → a e b com 6; c e d com 2.
        var r1 = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Americano 1");

        // Rodada 2: (a,c) 6 x 2 (b,d)   → a fecha com 12, b com 8, c com 8, d com 4.
        var dupla1R2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a, Jogador2Id = c };
        var dupla2R2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = b, Jogador2Id = d4 };
        ctx.Duplas.AddRange(dupla1R2, dupla2R2);
        await ctx.SaveChangesAsync();
        var r2 = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id,
            Dupla1Id = dupla1R2.Id, Dupla2Id = dupla2R2.Id,
            Fase = "Americano 2", Status = "Agendada", Codigo = "AM2TST",
        };
        ctx.Partidas.Add(r2);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await FinalizarAsync(ctx, tela, r1, org, push);
        await FinalizarAsync(ctx, tela, r2, org, push);

        // Líder isolado: 12 games contra 8, 8 e 4. Nenhuma partida extra.
        Assert.Null(await ctx.Partidas.SingleOrDefaultAsync(
            p => p.CategoriaId == categoria.Id && p.Fase == "Final"));

        var carimbo = await ctx.Duplas.SingleOrDefaultAsync(
            x => x.CategoriaId == categoria.Id && x.UltimaFase == "Campeao");
        Assert.NotNull(carimbo);
        Assert.Equal(a, carimbo!.Jogador1Id);
        // SEM parceiro: senão o título vaza pra quem jogou junto na última rodada.
        Assert.Null(carimbo.Jogador2Id);

        Assert.Equal("Finalizado", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    // ---- 3. "SEU JOGO É O PRÓXIMO" ----

    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task O_proximo_par_da_quadra_e_avisado_pelas_duas_telas(Tela tela)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Em Andamento");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        var agora = new DateTime(2026, 8, 8, 20, 0, 0);
        var terminando = NovoJogo(ctx, torneio, categoria, duplas[0], duplas[1], "Grupo A", agora, "Quadra 1");
        // O próximo está AGENDADO na mesma quadra — é essa a regra do aviso
        // (AvisosDoDiaDeJogo.ProximaAposTerminar): quem sabe que a quadra vagou é o jogo que
        // acabou de terminar NELA.
        var seguinte = NovoJogo(ctx, torneio, categoria, duplas[2], duplas[3], "Grupo A",
            agora.AddMinutes(30), "Quadra 1", status: "Agendada");
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        await FinalizarAsync(ctx, tela, terminando, org, push);

        // Os QUATRO jogadores do jogo seguinte recebem a chamada — as duas duplas, não só uma.
        await push.Received(4).EnviarParaJogadorAsync(
            Arg.Any<int>(), "Seu jogo é o próximo!", Arg.Any<string>(), Arg.Any<string?>(),
            // ⚠️ O único aviso do sistema que vale WhatsApp: a pessoa está no clube, o jogo é
            // agora, e ela não vai abrir e-mail.
            AlcanceDoAviso.AppEWhatsApp);

        // E fica carimbado, pra não avisar duas vezes se alguém corrigir o placar depois.
        Assert.NotNull((await ctx.Partidas.FindAsync(seguinte.Id))!.AvisoProximoEnviadoEm);
    }

    // ---- 4. A HORA EM QUE O JOGO ACABOU ----

    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task A_hora_do_fim_fica_carimbada_pelas_duas_telas(Tela tela)
    {
        // ⚠️ Só a tela cheia carimbava (Felipe, 08/08/2026: "a ordem das finalizadas tem que
        // ser por qual terminou por último vem primeiro"). Encerrando pelo botão do card AO
        // VIVO — a tela do dia do torneio — `HorarioFimReal` ficava NULO, e a lista de
        // Finalizadas, que ordena por ele, caía no desempate: num torneio por ordem de
        // liberação sobrava o Id, ou seja, a ordem do SORTEIO. O jogo que acabou agora
        // aparecia no meio da lista.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var partida = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Final");

        var antes = DateTime.Now.AddSeconds(-1);
        await FinalizarAsync(ctx, tela, partida, org, Substitute.For<IPushNotificationService>());

        var fim = (await ctx.Partidas.FindAsync(partida.Id))!.HorarioFimReal;
        Assert.NotNull(fim);
        Assert.InRange(fim!.Value, antes, DateTime.Now.AddSeconds(1));
    }

    [Fact]
    public async Task Corrigir_o_placar_de_um_jogo_antigo_NAO_reescreve_a_hora_do_fim()
    {
        // Senão um jogo de ontem, corrigido hoje, pularia pro topo das Finalizadas — e o
        // organizador perderia de vista o que acabou de sair da quadra.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var partida = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Final");

        var ontem = new DateTime(2026, 8, 7, 21, 30, 0);
        partida.Status = "Finalizada";
        partida.GamesDupla1 = 6;
        partida.GamesDupla2 = 2;
        partida.VencedorId = partida.Dupla1Id;
        partida.HorarioFimReal = ontem;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoPartidasController(ctx, org.Id)
            .ControlePlacar(partida.Id, "Finalizada", 6, 3, partida.NomeQuadra, null);

        Assert.Equal(ontem, (await ctx.Partidas.FindAsync(partida.Id))!.HorarioFimReal);
    }

    // ---- montagem ----

    private static Partida JogoEntreAsDuasDuplas(DbPadelContext ctx, Torneio torneio, Categoria categoria, string fase)
    {
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        var partida = NovoJogo(ctx, torneio, categoria, duplas[0], duplas[1], fase,
            new DateTime(2026, 8, 8, 20, 0, 0), "Quadra 1");
        ctx.SaveChanges();
        return partida;
    }

    private static Partida NovoJogo(DbPadelContext ctx, Torneio torneio, Categoria categoria,
        Dupla d1, Dupla d2, string fase, DateTime horario, string quadra, string status = "AoVivo")
    {
        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Fase = fase,
            Status = status,
            NomeQuadra = quadra,
            HorarioPrevisto = horario,
            Codigo = Guid.NewGuid().ToString()[..6].ToUpper(),
        };
        ctx.Partidas.Add(partida);
        return partida;
    }
}
