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

// ACEITAR OU RECUSAR QUEM CHAMOU (Felipe, 17/08/2026: "ao usuário clicar, abra uma tela e
// permita aceitar ou não como dupla").
//
// Antes, o aviso "Alguém quer fechar dupla com você" largava a pessoa no PERFIL do candidato,
// e o texto mandava "mande o convite por link da sua inscrição" — o aviso terminava numa
// tarefa manual: voltar pro torneio, gerar link, mandar por WhatsApp, esperar o outro abrir.
// Agora ele cai numa tela com Aceitar e Recusar, e aceitar fecha a dupla na hora.
public class AceitarOuRecusarChamadoTests
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
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // Uma inscrição SOZINHA com dois candidatos que já chamaram.
    private static (DbPadelContext ctx, Dupla solo, Jogador dono, Jogador a, Jogador b) Cenario(
        string statusTorneio = "Inscrições Abertas", decimal? valorInscricao = null)
    {
        var ctx = TestInfra.NovoContexto();
        var dono = new Jogador { Nome = "Dono Silva", Cpf = "66600000011" };
        var a = new Jogador { Nome = "Candidato Um", Cpf = "66600000012" };
        var b = new Jogador { Nome = "Candidato Dois", Cpf = "66600000013" };
        ctx.Jogadores.AddRange(dono, a, b);

        var torneio = new Torneio { Nome = "Copa do Mural", Codigo = "MUR2", Status = statusTorneio };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "CAT5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var solo = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = dono.Id, Jogador2Id = null,
            ValorInscricao = valorInscricao,
        };
        ctx.Duplas.Add(solo);
        ctx.SaveChanges();

        ctx.ChamadosDoMural.AddRange(
            new ChamadoDoMural { DuplaId = solo.Id, CandidatoId = a.Id, CriadoEm = DateTime.Now.AddMinutes(-10) },
            new ChamadoDoMural { DuplaId = solo.Id, CandidatoId = b.Id, CriadoEm = DateTime.Now.AddMinutes(-5) });
        ctx.SaveChanges();

        return (ctx, solo, dono, a, b);
    }

    // ═══════════════════ A REGRA PURA ═══════════════════

    [Fact]
    public void So_o_DONO_de_uma_inscricao_solo_aberta_pode_aceitar()
    {
        var solo = new Dupla { Jogador1Id = 1, Jogador2Id = null };

        Assert.Null(MuralDeParceiros.MotivoParaNaoAceitar(solo, "Inscrições Abertas", 1));

        // Não é minha inscrição.
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoAceitar(solo, "Inscrições Abertas", 2));

        // Dupla que já fechou não escolhe mais ninguém — senão o segundo aceite TROCARIA o
        // parceiro sem ninguém ter pedido isso.
        var fechada = new Dupla { Jogador1Id = 1, Jogador2Id = 3 };
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoAceitar(fechada, "Inscrições Abertas", 1));

        // Inscrições encerradas: o chaveamento já pode ter saído.
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoAceitar(solo, "Chaves em Sorteio", 1));

        var time = new Dupla { Jogador1Id = 1, NomeTime = "Os Fortes" };
        Assert.NotNull(MuralDeParceiros.MotivoParaNaoAceitar(time, "Inscrições Abertas", 1));

        Assert.NotNull(MuralDeParceiros.MotivoParaNaoAceitar(null, "Inscrições Abertas", 1));
    }

    // ═══════════════════ A TELA ═══════════════════

    [Fact]
    public async Task A_tela_lista_quem_chamou_pro_dono_da_inscricao()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _ = ctx;

        var resultado = await Controller(ctx, dono.Id).Chamados(solo.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        var chamados = Assert.IsType<List<ChamadoDoMural>>(view.Model);
        Assert.Equal(2, chamados.Count);
        // Em ordem de chegada — quem chamou primeiro aparece primeiro.
        Assert.Equal(a.Id, chamados[0].CandidatoId);
        Assert.Equal(b.Id, chamados[1].CandidatoId);
        Assert.Null(view.ViewData["MotivoParaNaoAceitar"]);
    }

    // A lista diz quem se interessou por alguém — não é de quem passa na rua.
    [Fact]
    public async Task Quem_nao_e_dono_da_inscricao_nao_ve_a_lista()
    {
        var (ctx, solo, _, a, _) = Cenario();
        using var _1 = ctx;

        Assert.IsType<ForbidResult>(await Controller(ctx, a.Id).Chamados(solo.Id));
    }

    // ⚠️ A TELA NÃO PODE SER ALCANÇÁVEL SÓ PELO AVISO. Quem apagasse a notificação perdia o
    // caminho — e a própria inscrição não dizia que havia gente esperando resposta. Aviso é
    // lembrete; o que existe no sistema precisa estar visível de dentro dele.
    //
    // A tela do torneio recebe a contagem por inscrição (ViewBag.ChamadosPorInscricao) e
    // desenha o botão "N querem jogar com você" na inscrição da própria pessoa.
    [Fact]
    public async Task A_tela_do_torneio_diz_quantos_me_chamaram_na_MINHA_inscricao()
    {
        var (ctx, solo, dono, _, _) = Cenario();
        using var _1 = ctx;

        var torneioId = (await ctx.Categorias.FindAsync(solo.CategoriaId))!.TorneioId;
        var controller = TestInfra.NovoTorneiosController(ctx, dono.Id);

        await controller.Details(torneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var contagem = Assert.IsType<Dictionary<int, int>>(controller.ViewBag.ChamadosPorInscricao);
        Assert.Equal(2, contagem[solo.Id]);
    }

    // A contagem é de QUEM ESTÁ LOGADO. Sem isso, a tela mostraria na inscrição de um jogador
    // que outras pessoas foram chamadas — e o botão levaria a um Forbid.
    [Fact]
    public async Task A_contagem_nao_vaza_pra_quem_nao_e_dono_da_inscricao()
    {
        var (ctx, solo, _, a, _) = Cenario();
        using var _1 = ctx;

        var torneioId = (await ctx.Categorias.FindAsync(solo.CategoriaId))!.TorneioId;
        var controller = TestInfra.NovoTorneiosController(ctx, a.Id);

        await controller.Details(torneioId, timeFiltroId: null, categoriaFiltroIds: null);

        var contagem = Assert.IsType<Dictionary<int, int>>(controller.ViewBag.ChamadosPorInscricao);
        Assert.False(contagem.ContainsKey(solo.Id),
            "a inscrição de outra pessoa apareceu na contagem de quem me chamou");
    }

    // ═══════════════════ ACEITAR ═══════════════════

    [Fact]
    public async Task Aceitar_fecha_a_dupla_e_avisa_os_dois_lados()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).AceitarChamado(solo.Id, a.Id);

        var dupla = await ctx.Duplas.FindAsync(solo.Id);
        Assert.Equal(a.Id, dupla!.Jogador2Id);

        // Quem entrou soube que entrou; o dono, que a dupla mudou.
        await push.Received(1).EnviarParaJogadorAsync(a.Id,
            Arg.Is<string>(t => t.Contains("entrou numa dupla")),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(dono.Id,
            Arg.Is<string>(t => t.Contains("Dupla atualizada")),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ⚠️ A dupla fechou: os outros pedidos não têm mais o que responder. Deixá-los de pé daria
    // ao dono uma lista de gente pra "aceitar" numa vaga que não existe — e o segundo aceite
    // trocaria o parceiro sem ninguém ter pedido.
    [Fact]
    public async Task Aceitar_um_derruba_os_chamados_dos_outros()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _ = ctx;

        Assert.Equal(2, await ctx.ChamadosDoMural.CountAsync(c => c.DuplaId == solo.Id));

        await Controller(ctx, dono.Id).AceitarChamado(solo.Id, a.Id);

        Assert.Empty(await ctx.ChamadosDoMural.Where(c => c.DuplaId == solo.Id).ToListAsync());
    }

    // O preço passa a contar DUAS pessoas quando a dupla fecha — a mesma conta do convite por
    // link, porque os dois caminhos chamam o mesmo FecharDuplaComAsync. Sem isso, dupla
    // fechada pelo mural entraria nos somatórios de dinheiro valendo uma inscrição só.
    [Fact]
    public async Task Aceitar_reconta_o_valor_da_inscricao_como_o_convite_faz()
    {
        var (ctx, solo, dono, a, _) = Cenario(valorInscricao: 100m);
        using var _ = ctx;

        var torneio = await ctx.Torneios.FirstAsync();
        torneio.PrecoInscricao = 100m;
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id).AceitarChamado(solo.Id, a.Id);

        var dupla = await ctx.Duplas.FindAsync(solo.Id);
        Assert.Equal(a.Id, dupla!.Jogador2Id);
        // Não ficou valendo uma inscrição só: a entrada do parceiro mexeu no valor.
        Assert.True(dupla.ValorInscricao > 100m,
            $"valor ficou {dupla.ValorInscricao} — o parceiro que entrou não foi contado");
    }

    // ⚠️ Sem o chamado, aceitar seria uma porta lateral pra colocar QUALQUER pessoa na própria
    // dupla sem que ela tenha se oferecido.
    [Fact]
    public async Task Nao_da_pra_aceitar_quem_nunca_chamou()
    {
        var (ctx, solo, dono, _, _) = Cenario();
        using var _1 = ctx;

        var estranho = new Jogador { Nome = "Estranho", Cpf = "66600000014" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(solo.Id, estranho.Id);

        Assert.Null((await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_e_dono_nao_aceita_ninguem()
    {
        var (ctx, solo, _, a, b) = Cenario();
        using var _1 = ctx;

        // O candidato tentando se auto-aceitar na dupla de outra pessoa.
        var resultado = await Controller(ctx, a.Id).AceitarChamado(solo.Id, b.Id);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Null((await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);
    }

    // Entre abrir a lista e tocar em aceitar, o torneio fecha (ou outro caminho fechou a
    // dupla). A tela some com o botão, mas quem decide é o servidor.
    [Fact]
    public async Task Torneio_que_fechou_no_meio_do_caminho_recusa_o_aceite()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _1 = ctx;

        ctx.Torneios.First().Status = "Chaves em Sorteio";
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(solo.Id, a.Id);

        Assert.Null((await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ═══════════════════ RECUSAR ═══════════════════

    // ⚠️ A REGRA MUDOU EM 17/08/2026, por decisão do Felipe. A primeira versão calava a
    // recusa ("avisar só machuca"); ele decidiu avisar, e o motivo é melhor: quem chamou fica
    // ESPERANDO. Sem resposta, a pessoa não sabe se ainda tem chance e não procura outro
    // parceiro — o silêncio custa a vaga dela num torneio com prazo.
    [Fact]
    public async Task Recusar_apaga_so_aquele_chamado_e_AVISA_quem_chamou()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).RecusarChamado(solo.Id, a.Id);

        var sobraram = await ctx.ChamadosDoMural.Where(c => c.DuplaId == solo.Id).ToListAsync();
        Assert.Single(sobraram);
        Assert.Equal(b.Id, sobraram[0].CandidatoId);

        // Quem foi recusado recebe — e o texto aponta pra saída ("outras inscrições
        // procurando"), que é a única coisa acionável que existe pra quem lê.
        await push.Received(1).EnviarParaJogadorAsync(a.Id,
            MuralDeParceiros.TituloDaRecusa,
            Arg.Is<string>(c => c.Contains("procurando parceiro")),
            Arg.Any<string>(), AlcanceDoAviso.AppSemEmail);

        // ⚠️ E SÓ ELE: quem não foi recusado não pode receber nada.
        await push.DidNotReceive().EnviarParaJogadorAsync(b.Id,
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<AlcanceDoAviso>());

        // Recusar não fecha nem mexe na dupla.
        Assert.Null((await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);
    }

    // Dois toques no mesmo botão (ou duas abas) não podem virar dois avisos: o segundo já não
    // acha o chamado, e sem essa amarra ele mandaria o recado de novo.
    [Fact]
    public async Task Recusar_duas_vezes_avisa_uma_vez_so()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();
        var controller = Controller(ctx, dono.Id, push);

        await controller.RecusarChamado(solo.Id, a.Id);
        await controller.RecusarChamado(solo.Id, a.Id);

        await push.Received(1).EnviarParaJogadorAsync(a.Id,
            MuralDeParceiros.TituloDaRecusa, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<AlcanceDoAviso>());
    }

    // ⚠️ QUEM PERDEU A VAGA TAMBÉM É AVISADO — e com TEXTO PRÓPRIO. Aqui ninguém recusou
    // ninguém: outra pessoa chegou antes. Dizer "recusou" pra quem só perdeu a corrida é falso.
    [Fact]
    public async Task Aceitar_avisa_quem_ficou_de_fora_que_a_vaga_foi_preenchida()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).AceitarChamado(solo.Id, a.Id);

        // O escolhido NÃO recebe "a vaga foi preenchida" — ele recebe o aviso de dupla fechada.
        await push.DidNotReceive().EnviarParaJogadorAsync(a.Id,
            MuralDeParceiros.TituloDaVagaPreenchida, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<AlcanceDoAviso>());

        // Quem ficou de fora recebe, uma vez.
        await push.Received(1).EnviarParaJogadorAsync(b.Id,
            MuralDeParceiros.TituloDaVagaPreenchida,
            Arg.Is<string>(c => c.Contains("outra pessoa")),
            Arg.Any<string>(), AlcanceDoAviso.AppSemEmail);
    }

    [Fact]
    public async Task Quem_nao_e_dono_nao_recusa_nada()
    {
        var (ctx, solo, _, a, _) = Cenario();
        using var _1 = ctx;

        Assert.IsType<ForbidResult>(await Controller(ctx, a.Id).RecusarChamado(solo.Id, a.Id));
        Assert.Equal(2, await ctx.ChamadosDoMural.CountAsync(c => c.DuplaId == solo.Id));
    }
}
