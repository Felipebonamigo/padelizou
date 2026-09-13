using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// Nem todo torneio quer lista de chamada: num interno de 8 duplas o organizador conhece todo
// mundo de vista, e a tela vira mais um botão pra ignorar. O interruptor esconde o botão — e
// o servidor recusa junto, porque link antigo e histórico do navegador continuam abrindo a
// tela mesmo sem botão nenhum.
public class CheckInOpcionalTests
{
    private static async Task<(Torneio torneio, Jogador organizador)> MontarAsync(DbPadelContext ctx, bool usaCheckIn)
    {
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Chaves em Sorteio");
        torneio.UsaCheckIn = usaCheckIn;
        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    [Fact]
    public void Torneio_novo_nasce_com_check_in_desligado()
    {
        // Ligar é escolha de quem faz torneio grande. Nascer ligado dava ao organizador uma
        // tela que ele não pediu — e que os inscritos veem e cobram. Os torneios que já
        // estavam no ar continuam com o check-in: a migração gravou `true` neles.
        Assert.False(new Torneio { Nome = "X", Codigo = "X1" }.UsaCheckIn);
    }

    // ⚠️ AQUI VIVIAM DOIS TESTES DA TELA "Check-in do dia" — que ela abria com a chamada ligada,
    // e que recusava com ela desligada. A tela saiu em 13/09/2026 (🗣️ *"acho que esse checkin
    // aqui em cima tb nao precisa mais"*): a chamada acontece na bolinha da linha do jogo.
    //
    // As duas verdades continuam guardadas, do lado que sobrou — o POST: o
    // `Desligado_nao_da_pra_marcar_presenca_por_POST_feito_a_mao` e o
    // `Ligado_a_presenca_e_gravada_e_da_pra_desfazer`, logo abaixo. O interruptor nunca foi da
    // tela; era da gravação.

    [Fact]
    public async Task Desligado_nao_da_pra_marcar_presenca_por_POST_feito_a_mao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: false);
        var duplas = await ctx.Duplas.Where(d => d.Categoria.TorneioId == torneio.Id).Take(2).ToListAsync();
        var dupla = duplas[0];
        var categoria = await ctx.Categorias.FirstAsync(c => c.Id == dupla.CategoriaId);
        var jogo = TestInfra.NovoJogo(ctx, categoria, duplas[0], duplas[1]);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);

        Assert.IsType<RedirectToActionResult>(resultado);
        // A presença é linha em PresencaNoJogo desde 12/09/2026 — desligado, ela não nasce.
        Assert.Empty(ctx.Presencas);
    }

    [Fact]
    public async Task Ligado_a_presenca_e_gravada_e_da_pra_desfazer()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await MontarAsync(ctx, usaCheckIn: true);
        var duplas = await ctx.Duplas.Where(d => d.Categoria.TorneioId == torneio.Id).Take(2).ToListAsync();
        var dupla = duplas[0];
        var categoria = await ctx.Categorias.FirstAsync(c => c.Id == dupla.CategoriaId);
        var jogo = TestInfra.NovoJogo(ctx, categoria, duplas[0], duplas[1]);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: true);
        Assert.Single(ctx.Presencas);

        await controller.MarcarCheckIn(dupla.Jogador1Id, jogo.Id, presente: false);
        Assert.Empty(ctx.Presencas);
    }

    [Fact]
    public async Task Quem_nao_organiza_continua_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = await MontarAsync(ctx, usaCheckIn: true);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "22255588896" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var duplas = await ctx.Duplas.Where(d => d.Categoria.TorneioId == torneio.Id).Take(2).ToListAsync();
        var categoria = await ctx.Categorias.FirstAsync(c => c.Id == duplas[0].CategoriaId);
        var jogo = TestInfra.NovoJogo(ctx, categoria, duplas[0], duplas[1]);

        // Era o GET da tela; virou o POST que grava, que é o que sobrou de porta.
        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, estranho.Id)
                .MarcarCheckIn(duplas[0].Jogador1Id, jogo.Id, presente: true));
    }
}
