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

    // ---- 2. FINAL DO AMERICANO ----

    [Theory]
    [InlineData(Tela.Mesa)]
    [InlineData(Tela.TelaCheia)]
    public async Task A_final_do_americano_nasce_pelas_duas_telas(Tela tela)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Em Andamento");
        var rodada = JogoEntreAsDuasDuplas(ctx, torneio, categoria, "Americano 1");

        await FinalizarAsync(ctx, tela, rodada, org, Substitute.For<IPushNotificationService>());

        // Acabaram as rodadas → o robô cruza os 4 melhores (1º+4º × 2º+3º) e marca a final.
        var final = await ctx.Partidas.SingleOrDefaultAsync(p => p.CategoriaId == categoria.Id && p.Fase == "Final");
        Assert.NotNull(final);
        Assert.False(string.IsNullOrEmpty(final!.Codigo));   // NOT NULL no banco
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
