using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// Salvar o placar de TODAS as quadras de uma vez, direto na lista de Ao Vivo.
//
// Com 5 quadras rodando, marcar um game era: abrir o Controle de Placar, mexer, salvar,
// voltar, achar o próximo card. Cinco vezes, a cada game, a noite inteira.
public class PlacarEmLoteTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, List<Partida> aoVivo, Jogador org)>
        ComJogosNoArAsync(int quantos = 3, int gamesDaFase = 9)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 5;
        torneio.GamesFaseGrupos = gamesDaFase;
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        var aoVivo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Take(quantos).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        return (ctx, torneio, aoVivo, org);
    }

    [Fact]
    public async Task Organizador_salva_o_placar_de_varias_quadras_num_POST_so()
    {
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(3);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id,
            aoVivo.Select(p => p.Id).ToArray(),
            new[] { 4, 2, 6 },
            new[] { 1, 5, 6 });

        var depois = await ctx.Partidas.Where(p => aoVivo.Select(x => x.Id).Contains(p.Id))
            .OrderBy(p => p.Id).ToListAsync();

        Assert.Equal(new int?[] { 4, 2, 6 }, depois.Select(p => p.GamesDupla1).ToArray());
        Assert.Equal(new int?[] { 1, 5, 6 }, depois.Select(p => p.GamesDupla2).ToArray());
        Assert.All(depois, p => Assert.Equal("AoVivo", p.Status));   // salvar não finaliza nada
    }

    [Fact]
    public async Task Jogador_comum_nao_altera_placar_nenhum()
    {
        // A tela nem desenha os campos pra quem não organiza, mas a rota é o que de fato
        // protege — oferecer um campo que o servidor vai recusar é desleixo que o usuário paga.
        var (ctx, torneio, aoVivo, _) = await ComJogosNoArAsync(2);
        using var _1 = ctx;

        var xereta = new Jogador { Nome = "Xereta", Cpf = "99900000094" };
        ctx.Jogadores.Add(xereta);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, xereta.Id);
        var resposta = await controller.SalvarPlacaresAoVivo(
            torneio.Id, aoVivo.Select(p => p.Id).ToArray(), new[] { 9, 9 }, new[] { 0, 0 });

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resposta);
        var depois = await ctx.Partidas.Where(p => aoVivo.Select(x => x.Id).Contains(p.Id)).ToListAsync();
        Assert.All(depois, p => Assert.True((p.GamesDupla1 ?? 0) == 0 && (p.GamesDupla2 ?? 0) == 0));
    }

    [Fact]
    public async Task Jogo_que_saiu_do_ar_no_meio_do_caminho_nao_e_sobrescrito()
    {
        // Duas pessoas na Mesa: uma finaliza enquanto a outra ainda tem a lista velha aberta.
        // Sobrescrever aqui é o que faz resultado sumir sem ninguém entender.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        var finalizado = aoVivo[0];
        finalizado.Status = "Finalizada";
        finalizado.GamesDupla1 = 9;
        finalizado.GamesDupla2 = 3;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, aoVivo.Select(p => p.Id).ToArray(), new[] { 1, 5 }, new[] { 1, 2 });

        var intocado = await ctx.Partidas.FindAsync(finalizado.Id);
        Assert.Equal(9, intocado!.GamesDupla1);
        Assert.Equal(3, intocado.GamesDupla2);

        var mexido = await ctx.Partidas.FindAsync(aoVivo[1].Id);
        Assert.Equal(5, mexido!.GamesDupla1);
    }

    [Fact]
    public async Task Placar_nao_passa_do_teto_da_FASE()
    {
        // Torneio até 4 games: digitar 9 seria um placar que aquele jogo não pode ter.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1, gamesDaFase: 4);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id }, new[] { 9 }, new[] { 0 });

        var depois = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.True(depois!.GamesDupla1 <= 5,
            $"placar {depois.GamesDupla1} passou do teto de um jogo até 4 games");
    }

    [Fact]
    public async Task Jogo_de_outro_torneio_nao_entra_de_carona()
    {
        // O id do torneio vem do formulário: sem o filtro, um POST forjado alteraria o placar
        // de um torneio que não é o de quem está logado.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var deOutro = new Partida
        {
            TorneioId = 999, CategoriaId = aoVivo[0].CategoriaId, Codigo = "OUTRO",
            Status = "AoVivo", Fase = "Grupo A",
            Dupla1Id = aoVivo[0].Dupla1Id, Dupla2Id = aoVivo[0].Dupla2Id,
        };
        ctx.Partidas.Add(deOutro);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { deOutro.Id }, new[] { 7 }, new[] { 0 });

        Assert.Null((await ctx.Partidas.FindAsync(deOutro.Id))!.GamesDupla1);
    }
}
