using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O MURAL "PROCURO DUPLA": quem está inscrito sem parceiro já aparece na lista pública —
// o chamado é a AÇÃO em cima dela. Ele só manda o recado; quem fecha a dupla é o dono da
// inscrição, pelo convite por link. Um chamado por pessoa por inscrição, preso no banco.
public class MuralDeParceirosTests
{
    private static DuplasController Controller(
        DbPadelContext ctx, int usuarioLogadoId, IPushNotificationService? push = null)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            push ?? Substitute.For<IPushNotificationService>(),
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
        controller.Url = Substitute.For<IUrlHelper>();
        return controller;
    }

    // Um torneio aberto com uma inscrição SEM PARCEIRO (o Sozinho) e um candidato de fora.
    private static (DbPadelContext ctx, Dupla solo, Jogador candidato) Cenario(
        string statusTorneio = "Inscrições Abertas")
    {
        var ctx = TestInfra.NovoContexto();
        var sozinho = new Jogador { Nome = "Sozinho Silva", Cpf = "66600000001" };
        var candidato = new Jogador { Nome = "Candidato Souza", Cpf = "66600000002" };
        ctx.Jogadores.AddRange(sozinho, candidato);

        var torneio = new Torneio { Nome = "Copa do Mural", Codigo = "MUR1", Status = statusTorneio };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "CAT5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var solo = new Dupla { CategoriaId = categoria.Id, Jogador1Id = sozinho.Id, Jogador2Id = null };
        ctx.Duplas.Add(solo);
        ctx.SaveChanges();

        return (ctx, solo, candidato);
    }

    // ─────────────────────────── A REGRA PURA ───────────────────────────

    [Fact]
    public void So_inscricao_solo_de_torneio_aberto_recebe_chamado()
    {
        var aberta = new Dupla { Jogador1Id = 1, Jogador2Id = null };

        Assert.Null(MuralDeParceiros.MotivoParaNaoChamar(aberta, "Inscrições Abertas", 2));

        // A própria inscrição não se chama.
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoChamar(aberta, "Inscrições Abertas", 1));

        // Dupla completa não procura ninguém.
        var completa = new Dupla { Jogador1Id = 1, Jogador2Id = 3 };
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoChamar(completa, "Inscrições Abertas", 2));

        // Inscrições fechadas fecham o mural — chamar viraria conversa sem saída.
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoChamar(aberta, "Chaves em Sorteio", 2));

        // Linha de time não tem parceiro.
        var time = new Dupla { Jogador1Id = 1, NomeTime = "Os Fortes" };
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoChamar(time, "Inscrições Abertas", 2));

        Assert.NotNull(MuralDeParceiros.MotivoParaNaoChamar(null, "Inscrições Abertas", 2));
    }

    // ─────────────────────────── O CAMINHO INTEIRO ───────────────────────────

    [Fact]
    public async Task O_chamado_grava_avisa_o_dono_e_nao_repete()
    {
        var (ctx, solo, candidato) = Cenario();
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();
        var controller = Controller(ctx, candidato.Id, push);

        await controller.ChamarParaDupla(solo.Id);

        var chamado = Assert.Single(ctx.ChamadosDoMural);
        Assert.Equal(solo.Id, chamado.DuplaId);
        Assert.Equal(candidato.Id, chamado.CandidatoId);

        // O aviso foi pro DONO da inscrição, com o nome de quem chamou no corpo.
        await push.Received(1).EnviarParaJogadorAsync(
            solo.Jogador1Id,
            MuralDeParceiros.TituloDoAviso,
            Arg.Is<string>(corpo => corpo.Contains("Candidato Souza")),
            Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);

        // Chamar de novo NÃO duplica nem re-avisa — o recado já está lá.
        await controller.ChamarParaDupla(solo.Id);
        Assert.Single(ctx.ChamadosDoMural);
        await push.Received(1).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Chamar_a_propria_inscricao_ou_torneio_fechado_recusa_sem_gravar()
    {
        var (ctx, solo, candidato) = Cenario();
        using var _ = ctx;

        // O dono tentando chamar a si mesmo.
        var euMesmo = Controller(ctx, solo.Jogador1Id);
        await euMesmo.ChamarParaDupla(solo.Id);
        Assert.NotNull(euMesmo.TempData["Erro"]);
        Assert.Empty(ctx.ChamadosDoMural);

        // Inscrições fechadas depois que a página ficou aberta no celular.
        ctx.Torneios.First().Status = "Chaves em Sorteio";
        await ctx.SaveChangesAsync();

        var atrasado = Controller(ctx, candidato.Id);
        await atrasado.ChamarParaDupla(solo.Id);
        Assert.NotNull(atrasado.TempData["Erro"]);
        Assert.Empty(ctx.ChamadosDoMural);
    }
}
