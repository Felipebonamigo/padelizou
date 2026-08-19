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

    // ═══════════════════ ACEITAR QUEM TAMBÉM ESTÁ INSCRITO SOZINHO ═══════════════════
    //
    // ⚠️ ESTE É O CASO COMUM DO MURAL, e era o único que NÃO FUNCIONAVA (relato do Gabriel,
    // 19/08/2026): os dois estão na mesma lista de "procurando parceiro" da mesma categoria —
    // é por isso que um chamou o outro — e aceitar devolvia "Fulano já está inscrito nesta
    // categoria, sozinho... Marque \"juntar com a inscrição que já existe\" e confirme de
    // novo", numa tela que não tem caixa nenhuma pra marcar. Não havia como aceitar ninguém.
    //
    // O aceite É a resposta à pergunta de juntar: a inscrição sozinha dele sai, esta fica.
    [Fact]
    public async Task Aceitar_quem_tambem_esta_inscrito_sozinho_JUNTA_as_duas_inscricoes()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _1 = ctx;

        var soloDoCandidato = new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null };
        ctx.Duplas.Add(soloDoCandidato);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(solo.Id, a.Id);

        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(a.Id, (await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);

        // E ninguém fica inscrito duas vezes na categoria: a inscrição sozinha dele saiu.
        Assert.Null(await ctx.Duplas.FindAsync(soloDoCandidato.Id));
        Assert.Single(await ctx.Duplas.Where(d => d.CategoriaId == solo.CategoriaId).ToListAsync());
    }

    // A tela precisa dizer o efeito ANTES do clique: aceitar APAGA a inscrição do outro, e
    // isso não pode ser descoberto depois.
    [Fact]
    public async Task A_tela_marca_quem_tem_inscricao_sozinha_pra_juntar()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _1 = ctx;

        ctx.Duplas.Add(new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null });
        await ctx.SaveChangesAsync();

        var view = Assert.IsType<ViewResult>(await Controller(ctx, dono.Id).Chamados(solo.Id));
        var comSolo = Assert.IsType<HashSet<int>>(view.ViewData["CandidatosComInscricaoSolo"]);

        Assert.Contains(a.Id, comSolo);
        Assert.DoesNotContain(b.Id, comSolo);
    }

    // Juntar não é "aceitar qualquer um": quem já tem DUPLA FECHADA nesta categoria continua
    // recusado — aí a pessoa está mesmo tentando jogar duas vezes na mesma categoria.
    [Fact]
    public async Task Quem_ja_tem_dupla_fechada_na_categoria_continua_recusado()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _1 = ctx;

        ctx.Duplas.Add(new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = b.Id });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(solo.Id, a.Id);

        Assert.Null((await ctx.Duplas.FindAsync(solo.Id))!.Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ⚠️ JUNTAR NÃO É "SEGUNDA INSCRIÇÃO". A inscrição sozinha dele some no mesmo movimento —
    // dar o desconto de quem joga duas categorias por causa de uma linha que deixou de existir
    // faria a dupla entrar nos somatórios de dinheiro valendo menos do que ela custa.
    [Fact]
    public async Task Juntar_nao_da_o_desconto_de_segunda_inscricao()
    {
        var (ctx, solo, dono, a, _) = Cenario(valorInscricao: 100m);
        using var _1 = ctx;

        var torneio = await ctx.Torneios.FirstAsync();
        torneio.PrecoInscricao = 100m;
        torneio.PrecoSegundaInscricao = 50m;
        torneio.PermiteMultiplasCategorias = true;
        ctx.Duplas.Add(new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id).AceitarChamado(solo.Id, a.Id);

        var dupla = await ctx.Duplas.FindAsync(solo.Id);
        Assert.Equal(200m, dupla!.ValorInscricao);
    }

    // "A VAGA É A MESMA, NINGUÉM PERDE LUGAR" vale também pra FILA: se a inscrição que sai
    // tinha vaga confirmada e a que fica está na lista de espera, quem sobrevive é a vaga
    // confirmada — senão juntar apagaria uma vaga da categoria e ainda deixaria a dupla
    // esperando na fila por ela.
    [Fact]
    public async Task Juntar_com_inscricao_confirmada_tira_a_dupla_da_lista_de_espera()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _1 = ctx;

        solo.EmListaDeEspera = true;
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null, EmListaDeEspera = false,
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id).AceitarChamado(solo.Id, a.Id);

        Assert.False((await ctx.Duplas.FindAsync(solo.Id))!.EmListaDeEspera);
    }

    // Quem chamou a inscrição QUE SAIU também estava esperando resposta — e a linha dele morre
    // junto com ela. Sem aviso, a pessoa segue esperando e não procura outro parceiro: o mesmo
    // silêncio que já custou vaga na inscrição que fica.
    [Fact]
    public async Task Quem_chamou_a_inscricao_que_saiu_e_avisado_com_o_nome_dela()
    {
        var (ctx, solo, dono, a, b) = Cenario();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        var soloDoCandidato = new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null };
        ctx.Duplas.Add(soloDoCandidato);
        await ctx.SaveChangesAsync();

        // O "b" tinha chamado OS DOIS: a inscrição do dono e a do candidato.
        ctx.ChamadosDoMural.Add(new ChamadoDoMural
        {
            DuplaId = soloDoCandidato.Id, CandidatoId = b.Id, CriadoEm = DateTime.Now.AddMinutes(-1),
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id, push).AceitarChamado(solo.Id, a.Id);

        // Um aviso pela inscrição do dono e outro pela do candidato, cada um com o nome de
        // quem fechou ali — quem chamou o candidato nunca ouviu falar do dono.
        await push.Received(1).EnviarParaJogadorAsync(b.Id,
            MuralDeParceiros.TituloDaVagaPreenchida,
            Arg.Is<string>(c => c.Contains(dono.ComoChamar)),
            Arg.Any<string>(), AlcanceDoAviso.AppSemEmail);
        await push.Received(1).EnviarParaJogadorAsync(b.Id,
            MuralDeParceiros.TituloDaVagaPreenchida,
            Arg.Is<string>(c => c.Contains(a.ComoChamar)),
            Arg.Any<string>(), AlcanceDoAviso.AppSemEmail);

        // E o chamado que morreu com a inscrição não fica de pé.
        Assert.Empty(await ctx.ChamadosDoMural.Where(c => c.DuplaId == soloDoCandidato.Id).ToListAsync());
    }

    // ⚠️ O DONO da inscrição que fica não recebe "a vaga foi preenchida" quando ele mesmo tinha
    // chamado a inscrição que saiu — ele É a outra pessoa do texto.
    [Fact]
    public async Task O_dono_nao_recebe_aviso_de_vaga_preenchida_da_inscricao_que_ele_absorveu()
    {
        var (ctx, solo, dono, a, _) = Cenario();
        using var _1 = ctx;
        var push = Substitute.For<IPushNotificationService>();

        var soloDoCandidato = new Dupla { CategoriaId = solo.CategoriaId, Jogador1Id = a.Id, Jogador2Id = null };
        ctx.Duplas.Add(soloDoCandidato);
        await ctx.SaveChangesAsync();

        ctx.ChamadosDoMural.Add(new ChamadoDoMural
        {
            DuplaId = soloDoCandidato.Id, CandidatoId = dono.Id, CriadoEm = DateTime.Now.AddMinutes(-1),
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id, push).AceitarChamado(solo.Id, a.Id);

        await push.DidNotReceive().EnviarParaJogadorAsync(dono.Id,
            MuralDeParceiros.TituloDaVagaPreenchida, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<AlcanceDoAviso>());
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
