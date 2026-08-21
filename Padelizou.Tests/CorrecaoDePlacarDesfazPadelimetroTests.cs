using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Achado da análise de sistema de 21/08/2026: corrigir o placar de um jogo FINALIZADO sem
// passar por "Reabrir" primeiro nunca desfazia o Padelímetro daquela partida —
// PadelimetroService.AplicarAsync via a linha do extrato do placar VELHO já existente e não
// fazia nada (guard de idempotência), então o nível dos 4 jogadores ficava calculado pelo
// placar errado até um replay manual. Estes testes cobrem o caso GERAL, fora da Final e fora
// de qualquer campanha — CampanhaNoPadelimetroTests cobre o caso entrelaçado com campanha.
public class CorrecaoDePlacarDesfazPadelimetroTests
{
    private static (Torneio torneio, Categoria categoria, Jogador organizador,
        Dupla a, Dupla b, Partida jogo) Montar(DbPadelContext ctx, int nivel = 600)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio { Nome = "Torneio de Teste", Codigo = "PDZ1", Status = "Fase de Grupos" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "C3M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        Jogador NovoNivelado(int seed) =>
            new() { Nome = $"Jogador {seed}", Cpf = $"7770000{seed:0000}", Padelimetro = nivel, JogosDePadelimetro = 12 };

        var a = new Dupla { Categoria = categoria, Jogador1 = NovoNivelado(1), Jogador2 = NovoNivelado(2) };
        var b = new Dupla { Categoria = categoria, Jogador1 = NovoNivelado(3), Jogador2 = NovoNivelado(4) };
        ctx.Duplas.AddRange(a, b);
        ctx.SaveChanges();

        // A venceu B 6x2 — placar que a correção vai desmentir.
        var jogo = new Partida
        {
            CategoriaId = categoria.Id, TorneioId = torneio.Id, Codigo = "G1", Fase = "Fase de Grupos",
            Dupla1Id = a.Id, Dupla2Id = b.Id, Status = "Finalizada",
            GamesDupla1 = 6, GamesDupla2 = 2, SetsDupla1 = 1, SetsDupla2 = 0, VencedorId = a.Id,
        };
        ctx.Partidas.Add(jogo);
        ctx.SaveChanges();

        return (torneio, categoria, organizador, a, b, jogo);
    }

    [Fact]
    public async Task Corrigir_o_placar_sem_reabrir_desfaz_e_reaplica_o_padelimetro_do_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, org, a, b, jogo) = Montar(ctx);

        await new PadelimetroService(ctx).AplicarAsync(jogo.Id);

        var jogadorA1 = await ctx.Jogadores.FindAsync(a.Jogador1Id);
        var nivelDepoisDoJogoOriginal = jogadorA1!.Padelimetro!.Value;
        Assert.True(nivelDepoisDoJogoOriginal > 600);   // A venceu, subiu.

        // Organizador descobre que o placar real foi o CONTRÁRIO — B venceu 6x2.
        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ControlePlacar(jogo.Id, "Finalizada", 2, 6, null, null);

        var jogoDepois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(b.Id, jogoDepois!.VencedorId);

        // Sem o conserto, A continuaria com o nível de QUEM VENCEU — mesmo tendo perdido.
        // Placar 2x6 (mesma margem, 4 games, de FatorDeGames) com os quatro no mesmo nível
        // (expectativa 0.5): K=20 (estável) × fator 1,4 × 0,5 = 14 de variação. A perdeu:
        // 600 - 14 = 586 — e não os 600 "neutros" que sobrariam se a correção só desfizesse
        // o jogo velho sem aplicar o resultado novo.
        jogadorA1 = await ctx.Jogadores.FindAsync(a.Jogador1Id);
        Assert.Equal(586, jogadorA1!.Padelimetro);

        var jogadorB1 = await ctx.Jogadores.FindAsync(b.Jogador1Id);
        Assert.Equal(614, jogadorB1!.Padelimetro);   // B, que realmente venceu, sobe: 600 + 14.

        // Uma linha de extrato por jogador, não duas — o desfazer removeu a velha antes de
        // reaplicar, não empilhou em cima.
        var linhas = await ctx.HistoricosDePadelimetro.Where(h => h.PartidaId == jogo.Id).CountAsync();
        Assert.Equal(4, linhas);
    }

    [Fact]
    public async Task Corrigir_so_a_margem_sem_trocar_vencedor_tambem_reaplica()
    {
        // O delta depende da MARGEM de games (Padelimetro.FatorDeGames) — apertar o placar
        // sem trocar quem venceu já é motivo pra recalcular.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, org, a, _, jogo) = Montar(ctx);

        await new PadelimetroService(ctx).AplicarAsync(jogo.Id);
        var jogadorA1 = await ctx.Jogadores.FindAsync(a.Jogador1Id);
        var nivelComMargemDe4 = jogadorA1!.Padelimetro!.Value;   // 6x2, margem 4

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.ControlePlacar(jogo.Id, "Finalizada", 6, 5, null, null);   // margem 1, A continua vencendo

        jogadorA1 = await ctx.Jogadores.FindAsync(a.Jogador1Id);
        Assert.True(jogadorA1!.Padelimetro!.Value < nivelComMargemDe4,
            "Vitória apertada tem que valer menos do que a vitória folgada que foi corrigida.");
        Assert.True(jogadorA1.Padelimetro!.Value > 600);   // continua sendo uma vitória, só que menor.
    }

    [Fact]
    public async Task Jogo_que_nunca_entrou_no_padelimetro_nao_gera_custo_nem_erro_na_correcao()
    {
        // Torneio restrito não conta pro Padelímetro — DesfazerPartidaAsync precisa ser
        // um no-op limpo quando não há o que desfazer, não um erro.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org, _, _, jogo) = Montar(ctx);
        torneio.Restrito = true;
        ctx.SaveChanges();

        await new PadelimetroService(ctx).AplicarAsync(jogo.Id);   // no-op: restrito não conta
        Assert.Equal(0, await ctx.HistoricosDePadelimetro.CountAsync());

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        var resultado = await controller.ControlePlacar(jogo.Id, "Finalizada", 2, 6, null, null);

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal(0, await ctx.HistoricosDePadelimetro.CountAsync());
    }
}
