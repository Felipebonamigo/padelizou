using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DESFAZER O SORTEIO DEVOLVE O FIADO DA TAXA (09/09/2026).
//
// 🗣️ Felipe, num print do Painel de Controle com o torneio de volta em "Inscrições Abertas":
// *"aqui está dizendo que já sorteou a chave, mas a gente voltou, deveria ter sumido aquela
// mensagem"*.
//
// ⚠️ O AVISO ERA A PONTA. No torneio "por fora" o dinheiro nunca passa pelo sistema, e a trava
// do sorteio é o mecanismo de cobrança inteiro (Services/TaxaDoTorneioExterno). O organizador
// pega FIADO pra sortear — `TaxaExternoAdiadaEm` carimba, a chave destrava, o torneio passa a
// dever. `DesfazerSorteio` apagava as partidas e devolvia o status, mas NÃO o carimbo. Daí duas
// coisas, e a segunda é dinheiro:
//
//   1. o aviso cobrava por uma chave que já tinha sido devolvida;
//   2. `ChavesLiberadas` responde `true` enquanto existir o carimbo — então a trava ficava
//      desligada PRA SEMPRE. Dava pra desfazer, reabrir inscrições, entrar mais 20 duplas e
//      sortear de novo sem a taxa ser apresentada nenhuma vez. E o valor não é congelado em
//      lugar nenhum (`TaxaDoTorneioExterno.Valor` calcula na hora), então a dívida registrada
//      era a de um torneio menor do que o que ia acontecer.
//
// Desenho aprovado pelo Felipe: quem devolve as chaves devolve a dívida. O carimbo volta a
// nulo, a trava volta, e no sorteio seguinte ele escolhe de novo — sobre a lista nova.
//
// ⚠️ TAXA JÁ PAGA NUNCA É MEXIDA, e negociada também não: pagar é dinheiro que entrou, negociar
// é o Padelizou tendo aberto mão. Só o FIADO — a promessa em aberto — é que volta atrás.
public class FiadoVoltaComOSorteioDesfeitoTests
{
    private static async Task<(Torneio torneio, Jogador organizador)> ComChaveSorteadaNoFiadoAsync(
        DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        torneio.FormaPagamento = "Externo";
        torneio.PrecoInscricao = 150m;
        torneio.Status = AprovacaoDeChaves.Pendente;
        torneio.TaxaExternoAdiadaEm = new DateTime(2026, 9, 9, 8, 9, 0);

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = "JG01",
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
        });

        await ctx.SaveChangesAsync();
        return (torneio, organizador);
    }

    [Fact]
    public async Task Desfazer_o_sorteio_apaga_o_fiado_e_a_trava_volta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await ComChaveSorteadaNoFiadoAsync(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);

        await controller.DesfazerSorteio(torneio.Id);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);

        // O aviso do print sai daqui: sem carimbo, não há dívida a mostrar.
        Assert.Null(gravado!.TaxaExternoAdiadaEm);
        Assert.False(TaxaDoTorneioExterno.EstaDevendo(gravado));

        // ⚠️ E ESTA É A PARTE QUE VALE DINHEIRO: a trava do sorteio volta. Sem ela, o próximo
        // sorteio sairia de graça, com a lista já maior.
        Assert.False(TaxaDoTorneioExterno.ChavesLiberadas(gravado));
    }

    [Fact]
    public async Task Taxa_ja_paga_nao_e_mexida_por_desfazer_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await ComChaveSorteadaNoFiadoAsync(ctx);

        var quandoPagou = new DateTime(2026, 9, 9, 9, 0, 0);
        torneio.TaxaExternoPagaEm = quandoPagou;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.DesfazerSorteio(torneio.Id);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);

        // Dinheiro que entrou não volta atrás por causa de um sorteio refeito — e a chave
        // continua liberada, porque ela foi PAGA, não fiada.
        Assert.Equal(quandoPagou, gravado!.TaxaExternoPagaEm);
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(gravado));

        // ⚠️ E O CARIMBO DO FIADO CONTINUA LÁ. É ele que conta a história — "pegou fiado e
        // depois pagou" — e é por ele que o /Admin/Financeiro ordena os devedores. Apagar aqui
        // não mudaria nada na trava (a taxa está paga) e passaria despercebido: sumiria só o
        // registro de que houve fiado.
        Assert.Equal(new DateTime(2026, 9, 9, 8, 9, 0), gravado.TaxaExternoAdiadaEm);
    }

    [Fact]
    public async Task Cortesia_registrada_nao_e_desfeita()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await ComChaveSorteadaNoFiadoAsync(ctx);

        var quandoNegociou = new DateTime(2026, 9, 9, 9, 30, 0);
        torneio.TaxaExternoNegociadaEm = quandoNegociou;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.DesfazerSorteio(torneio.Id);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);

        // O Padelizou abriu mão; desfazer o sorteio não ressuscita a cobrança.
        Assert.Equal(quandoNegociou, gravado!.TaxaExternoNegociadaEm);
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(gravado));
        Assert.Equal(new DateTime(2026, 9, 9, 8, 9, 0), gravado.TaxaExternoAdiadaEm);
    }

    // A guarda que impede o conserto de virar "apaga o carimbo de qualquer torneio": o
    // Desfazer continua recusando quem já tem jogo rolando, e aí nada é mexido.
    [Fact]
    public async Task Com_jogo_em_andamento_nada_e_desfeito_nem_a_taxa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, organizador) = await ComChaveSorteadaNoFiadoAsync(ctx);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        jogo.Status = "Em Andamento";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: organizador.Id);
        await controller.DesfazerSorteio(torneio.Id);

        var gravado = await ctx.Torneios.FindAsync(torneio.Id);

        Assert.Equal(new DateTime(2026, 9, 9, 8, 9, 0), gravado!.TaxaExternoAdiadaEm);
        Assert.Equal(AprovacaoDeChaves.Pendente, gravado.Status);
    }
}
