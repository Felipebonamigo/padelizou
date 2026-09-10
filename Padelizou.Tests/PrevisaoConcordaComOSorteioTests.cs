using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// A PREVISÃO DA GRADE PROMETE O QUE O SORTEIO FAZ?
//
// O painel "Como essa grade vai ficar" diz com todas as letras que ali *"os números são REAIS —
// as duplas já estão inscritas —, então não é estimativa"*. Dele saem quantos jogos, a hora do
// último, quantos dias de quadra, o alerta de estourar o `DataFim` e — por outro caminho — a
// conta de quantas quadras alugar (TorneiosController.Planejamento).
//
// 💥 O DEFEITO QUE ESTE ARQUIVO TRAVA (achado em revisão adversarial, 09/09/2026):
// `MontarPrevisaoDaGrade` tinha a régua do sorteio COPIADA À MÃO (`d.Jogador2Id != null`), e por
// isso o grep por `ForaDoSorteio` não a alcançou quando a régua mudou. Quando a inscrição sem
// parceiro passou a ENTRAR na chave, a previsão continuou contando só as duplas fechadas: 8
// fechadas + 3 sozinhas prometiam a grade de 8 e o sorteio fazia a de 11 — menos grupos, menos
// jogos, menos quadra alugada. O mesmo estrago de "o organizador aluga de menos" que a sessão
// anterior tinha acabado de consertar noutro lugar.
//
// ⚠️ E ERRAVA NOS DOIS SENTIDOS: a linha dos grupos não filtrava `EmListaDeEspera`, então
// contava dupla fechada que o sorteio deixa de fora. Num cenário com uma sozinha E uma na
// espera os dois erros se cancelavam — que é exatamente por que a suíte ficou verde. Por isso
// o cenário deste teste tem DUAS sozinhas e UMA na espera: números que não se cancelam.
public class PrevisaoConcordaComOSorteioTests
{
    private static PrevisaoGradeVM? PrevisaoDe(DbPadelContext ctx, Torneio torneio, Jogador organizador)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        controller.Details(torneio.Id, timeFiltroId: null, categoriaFiltroIds: null).GetAwaiter().GetResult();
        return controller.ViewBag.PrevisaoGrade as PrevisaoGradeVM;
    }

    private static Jogador NovoSolo(DbPadelContext ctx, Categoria categoria, int n)
    {
        var jogador = new Jogador { Nome = $"Solo {n:00}", Cpf = $"7770000{n:04}" };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogador.Id, Jogador2Id = null });
        ctx.SaveChanges();
        return jogador;
    }

    private static void NovaEspera(DbPadelContext ctx, Categoria categoria, int n)
    {
        var a = new Jogador { Nome = $"Espera {n:00}A", Cpf = $"7780000{n:04}" };
        var b = new Jogador { Nome = $"Espera {n:00}B", Cpf = $"7790000{n:04}" };
        ctx.Jogadores.AddRange(a, b);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, EmListaDeEspera = true,
        });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task A_previsao_conta_as_MESMAS_duplas_que_o_sorteio_leva()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        NovoSolo(ctx, categoria, 1);
        NovoSolo(ctx, categoria, 2);
        NovaEspera(ctx, categoria, 1);

        // A previsão que o organizador lê ANTES de apertar o botão.
        var prev = PrevisaoDe(ctx, torneio, org);
        Assert.NotNull(prev);

        // E o que o sorteio faz de verdade.
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        int duplasSorteadas = await ctx.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && d.Grupo != null);

        // 4 fechadas + 2 sozinhas = 6 entram; a da lista de espera fica fora.
        Assert.Equal(6, duplasSorteadas);
        Assert.Equal(duplasSorteadas, prev!.Duplas);
    }

    [Fact]
    public async Task A_previsao_conta_os_MESMOS_jogos_que_o_sorteio_gera()
    {
        // O número que vira quadra alugada. Se a contagem de duplas erra, este erra atrás dela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        NovoSolo(ctx, categoria, 1);
        NovoSolo(ctx, categoria, 2);
        NovaEspera(ctx, categoria, 1);

        var prev = PrevisaoDe(ctx, torneio, org);
        Assert.NotNull(prev);

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        int jogosDeGrupo = await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id
            && p.Fase != null && p.Fase.StartsWith("Grupo"));

        Assert.Equal(jogosDeGrupo, prev!.JogosDeGrupo);
    }
}
