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

namespace Padelizou.Tests;

// 21/08/2026 — TIRAR A PRÓPRIA CANDIDATURA, pedido do Felipe: "se ele se candidatou para jogar,
// e se arrependeu, ele pode remover essa solicitação".
//
// Até aqui o chamado do mural era de mão única: o candidato mandava e só o DONO da inscrição
// conseguia fazer a linha sumir (aceitando ou recusando). Quem se arrependeu ficava numa lista
// alheia sem nenhum botão.
//
// O que está protegido aqui: (1) a autorização mora na cláusula WHERE, não numa comparação
// depois — buscar por duplaId e conferir o dono em seguida criaria um oráculo de "quem chamou
// quem"; (2) o botão alcança a inscrição que FECHOU por outro caminho, senão a linha vira
// zumbi invisível pros dois lados.
public class TirarChamadoDoMuralTests
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

    // Uma inscrição SOZINHA com dois candidatos que já chamaram.
    private static (DbPadelContext ctx, Dupla solo, Jogador dono, Jogador a, Jogador b) Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var dono = new Jogador { Nome = "Dono Silva", Cpf = "55500000011" };
        var a = new Jogador { Nome = "Candidato Um", Cpf = "55500000012" };
        var b = new Jogador { Nome = "Candidato Dois", Cpf = "55500000013" };
        ctx.Jogadores.AddRange(dono, a, b);

        var torneio = new Torneio { Nome = "Copa do Mural", Codigo = "TIR1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "CAT5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var solo = new Dupla { CategoriaId = categoria.Id, Jogador1Id = dono.Id, Jogador2Id = null };
        ctx.Duplas.Add(solo);
        ctx.SaveChanges();

        ctx.ChamadosDoMural.AddRange(
            new ChamadoDoMural { DuplaId = solo.Id, CandidatoId = a.Id, CriadoEm = DateTime.Now.AddMinutes(-10) },
            new ChamadoDoMural { DuplaId = solo.Id, CandidatoId = b.Id, CriadoEm = DateTime.Now.AddMinutes(-5) });
        ctx.SaveChanges();

        return (ctx, solo, dono, a, b);
    }

    [Fact]
    public async Task Quem_se_arrependeu_tira_o_proprio_pedido()
    {
        var (ctx, solo, _, a, b) = Cenario();
        using var _ctx = ctx;

        await Controller(ctx, a.Id).TirarChamado(solo.Id, torneioId: null);

        // O dele sumiu; o do outro candidato NÃO.
        Assert.False(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == a.Id));
        Assert.True(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == b.Id));
    }

    // ⚠️ A AUTORIZAÇÃO É O `WHERE`. Sem isso, um POST montado à mão apagaria o pedido de outra
    // pessoa — e, pior, a resposta diria se aquele pedido existia: um oráculo de "quem chamou
    // quem", que é o dado que a tela de Chamados protege com Forbid.
    [Fact]
    public async Task Ninguem_tira_o_pedido_de_outra_pessoa()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _ctx = ctx;

        // O DONO da inscrição tentando apagar o pedido do candidato A por esta porta.
        await Controller(ctx, dono.Id).TirarChamado(solo.Id, torneioId: null);

        Assert.True(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == a.Id));
    }

    [Fact]
    public async Task Tirar_duas_vezes_nao_estoura_e_avisa_que_ja_nao_estava()
    {
        var (ctx, solo, _, a, _) = Cenario();
        using var _ctx = ctx;

        var controller = Controller(ctx, a.Id);
        await controller.TirarChamado(solo.Id, torneioId: null);

        var segundoClique = Controller(ctx, a.Id);
        await segundoClique.TirarChamado(solo.Id, torneioId: null);

        Assert.Equal(MuralDeParceiros.PedidoJaNaoEstava, segundoClique.TempData["Sucesso"]);
    }

    // ⚠️ O FURO QUE ISTO FECHA: a dupla pode fechar por OUTRO caminho (troca de parceiro), que
    // não varre os chamados. Se o botão exigisse `Jogador2Id == null`, a linha ficaria lá pra
    // sempre — invisível pro dono (a tela dele filtra inscrição sem parceiro) e intocável pelo
    // candidato.
    [Fact]
    public async Task Da_pra_tirar_o_pedido_de_uma_inscricao_que_ja_fechou_por_outro_caminho()
    {
        var (ctx, solo, _, a, b) = Cenario();
        using var _ctx = ctx;

        solo.Jogador2Id = b.Id;   // fechou por fora, sem passar pelo mural
        await ctx.SaveChangesAsync();

        await Controller(ctx, a.Id).TirarChamado(solo.Id, torneioId: null);

        Assert.False(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == a.Id));
    }

    // Depois de tirar, pode chamar de novo — o índice único é anti-spam, não punição.
    [Fact]
    public async Task Depois_de_tirar_da_pra_chamar_de_novo()
    {
        var (ctx, solo, _, a, _) = Cenario();
        using var _ctx = ctx;

        await Controller(ctx, a.Id).TirarChamado(solo.Id, torneioId: null);
        await Controller(ctx, a.Id).ChamarParaDupla(solo.Id);

        Assert.True(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == a.Id));
    }

    // ⚠️ O TESTE QUE HOJE PASSARIA e só falha se a régua nova estiver errada: quem já tem dupla
    // FECHADA na categoria não consegue chamar nem por POST montado à mão. A tela esconde o
    // botão; a trava mora no servidor.
    [Fact]
    public async Task Quem_ja_tem_dupla_na_categoria_NAO_chama_nem_por_POST_montado_a_mao()
    {
        var (ctx, solo, _, a, b) = Cenario();
        using var _ctx = ctx;

        // O candidato A já fechou dupla com B na MESMA categoria, por outra inscrição.
        ctx.Duplas.Add(new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = b.Id });
        await ctx.SaveChangesAsync();

        // E tira o chamado que ele já tinha, pra o teste medir a régua nova e não o "já chamou".
        await Controller(ctx, a.Id).TirarChamado(solo.Id, torneioId: null);

        var controller = Controller(ctx, a.Id);
        await controller.ChamarParaDupla(solo.Id);

        Assert.False(await ctx.ChamadosDoMural.AnyAsync(c => c.DuplaId == solo.Id && c.CandidatoId == a.Id));
        Assert.NotNull(controller.TempData["Erro"]);
    }
}
