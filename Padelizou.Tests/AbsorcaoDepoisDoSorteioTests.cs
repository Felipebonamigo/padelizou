using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Xunit;

namespace Padelizou.Tests;

// JUNTAR DUAS INSCRIÇÕES SOZINHAS QUANDO AS DUAS JÁ ESTÃO NA CHAVE.
//
// Fechar dupla com quem também está inscrito sozinho na mesma categoria ABSORVE a inscrição
// dele: as duas viram uma, e a linha que sobra é apagada (FecharDuplaComAsync). Isso sempre
// funcionou porque as duas inscrições sozinhas ficavam FORA do sorteio — não existia jogo
// apontando pra nenhuma das duas.
//
// 💥 Desde 09/09/2026 elas ENTRAM na chave, cada uma com os jogos dela. Absorver ali significa
// apagar uma Dupla que tem Partida apontando pra ela: `Partida.Dupla1Id/Dupla2Id` são NOT NULL,
// então o DELETE bate na FK e o jogador leva um 500 ao tocar em "Aceitar". E se passasse,
// seria pior que o erro — sumiria com jogos que já estavam marcados no grupo de alguém.
//
// A saída não é escolher sozinho qual das duas vagas morre: é recusar e explicar. Quem resolve
// é o organizador, que enxerga a grade inteira.
public class AbsorcaoDepoisDoSorteioTests
{
    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static async Task<(DbPadelContext ctx, Torneio torneio, Dupla doDono, Dupla doCandidato, Jogador dono, Jogador candidato)>
        DoisSozinhosNaChaveAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Fase de Grupos");

        var dono = new Jogador { Nome = "Paulo Prass", Cpf = "11144477735" };
        var candidato = new Jogador { Nome = "Joao Silva", Cpf = "22233344456" };
        ctx.Jogadores.AddRange(dono, candidato);
        await ctx.SaveChangesAsync();

        var doDono = new Dupla { CategoriaId = categoria.Id, Jogador1Id = dono.Id, Jogador2Id = null };
        var doCandidato = new Dupla { CategoriaId = categoria.Id, Jogador1Id = candidato.Id, Jogador2Id = null };
        ctx.Duplas.AddRange(doDono, doCandidato);
        await ctx.SaveChangesAsync();

        // As duas já estão na chave, cada uma com o jogo dela contra a dupla fechada.
        var fechada = await ctx.Duplas.FirstAsync(d => d.Jogador2Id != null);
        ctx.Partidas.AddRange(
            new Partida
            {
                TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P1",
                Dupla1Id = doDono.Id, Dupla2Id = fechada.Id, Status = "Agendada",
            },
            new Partida
            {
                TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "P2",
                Dupla1Id = doCandidato.Id, Dupla2Id = fechada.Id, Status = "Agendada",
            });
        await ctx.SaveChangesAsync();

        ctx.ChamadosDoMural.Add(new ChamadoDoMural { DuplaId = doDono.Id, CandidatoId = candidato.Id });
        await ctx.SaveChangesAsync();

        return (ctx, torneio, doDono, doCandidato, dono, candidato);
    }

    [Fact]
    public async Task Nao_absorve_inscricao_que_ja_tem_jogo_marcado()
    {
        var (ctx, _, doDono, doCandidato, dono, candidato) = await DoisSozinhosNaChaveAsync();
        using var _1 = ctx;

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(doDono.Id, candidato.Id);

        // Nada mudou, e ninguém levou 500: as duas inscrições continuam de pé, com os jogos delas.
        Assert.Null((await ctx.Duplas.FindAsync(doDono.Id))!.Jogador2Id);
        Assert.NotNull(await ctx.Duplas.FindAsync(doCandidato.Id));
        Assert.Equal(2, await ctx.Partidas.CountAsync());
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Absorve_normalmente_quando_a_inscricao_do_candidato_ainda_nao_tem_jogo()
    {
        // O caso comum, e ele NÃO pode ter sido quebrado pela trava acima: dois sozinhos
        // fechando dupla antes de a chave sair continua sendo o produto do mural.
        var (ctx, _, doDono, doCandidato, dono, candidato) = await DoisSozinhosNaChaveAsync();
        using var _1 = ctx;

        // A inscrição do candidato ainda não entrou na chave.
        ctx.Partidas.Remove(await ctx.Partidas.FirstAsync(p => p.Dupla1Id == doCandidato.Id));
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(doDono.Id, candidato.Id);

        Assert.Equal(candidato.Id, (await ctx.Duplas.FindAsync(doDono.Id))!.Jogador2Id);
        Assert.Null(await ctx.Duplas.FindAsync(doCandidato.Id));   // absorvida
    }
}
