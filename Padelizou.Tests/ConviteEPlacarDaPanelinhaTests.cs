using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// 13/08/2026 — TRÊS PEDIDOS DO FELIPE NA TELA DO JOGO DA SEMANA.
//
// O que amarra os três é o convidado: a panelinha de verdade joga com quem apareceu, não com
// quem está cadastrado. Até aqui o sistema só sabia lidar com MEMBRO — convidar era privilégio
// de quem administra, e o placar só oferecia a lista de mensalistas. Na quadra, o resultado da
// noite simplesmente não era lançado.
public class ConviteEPlacarDaPanelinhaTests
{
    private static GruposController Controller(
        DbPadelContext ctx, int euId, ISessaoGrupoService? sessoes = null)
    {
        var controller = new GruposController(
            ctx, sessoes ?? Substitute.For<ISessaoGrupoService>(),
            Substitute.For<IPushNotificationService>(), NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static Jogador NovoJogador(int n, bool aceitaConvites = true) => new()
    {
        Nome = $"Jogador {n}",
        Cpf = $"5550000000{n}",
        Login = $"jog{n}",
        Celular = "51999990000",
        AceitaConvitesJogo = aceitaConvites,
    };

    // Panelinha com 4 membros — o primeiro administra, o segundo é membro comum.
    private static async Task<(GrupoPrivado grupo, List<Jogador> membros)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 4).Select(n => NovoJogador(n)).ToList();
        ctx.Jogadores.AddRange(membros);
        await ctx.SaveChangesAsync();

        var clube = new Clube { Nome = "Arena Teste" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Terças no Paladino",
            CodigoConvite = "TER123",
            AdministradorId = membros[0].Id,
            ClubeId = clube.Id,
            CategoriaPadraoId = 1,
            DiaSemanaFixo = 2,
            HorarioFixo = new TimeSpan(20, 0, 0),
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        foreach (var m in membros)
        {
            ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id, PontuacaoInterna = 0 });
        }
        await ctx.SaveChangesAsync();

        return (grupo, membros);
    }

    private static async Task<SessaoGrupo> SessaoAsync(DbPadelContext ctx, GrupoPrivado grupo)
    {
        var sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = new DateTime(2026, 8, 18, 20, 0, 0) };
        ctx.SessoesGrupo.Add(sessao);
        await ctx.SaveChangesAsync();
        return await ctx.SessoesGrupo.Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
            .FirstAsync(s => s.Id == sessao.Id);
    }

    private static ISessaoGrupoService SempreEsta(SessaoGrupo sessao)
    {
        var fake = Substitute.For<ISessaoGrupoService>();
        fake.ObterOuCriarSessaoAsync(Arg.Any<GrupoPrivado>(), Arg.Any<DateTime?>()).Returns(sessao);
        return fake;
    }

    private static async Task<Jogador> ConvidarAsync(DbPadelContext ctx, SessaoGrupo sessao, Jogador quem)
    {
        ctx.ConfirmacoesSessao.Add(new ConfirmacaoSessao
        {
            SessaoId = sessao.Id, JogadorId = quem.Id, Status = "Pendente", Avulso = true,
        });
        await ctx.SaveChangesAsync();
        return quem;
    }

    // ===================== QUEM CONVIDA =====================

    // ⚠️ ERA SÓ DE QUEM ADMINISTRA. Faltar um pro quarteto às 19h de terça é problema de quem
    // vai jogar, e esperar o administrador aparecer é exatamente como o jogo não acontece.
    // É a mesma régua que o `ConvidarParaGrupo` já usava logo acima no controller.
    [Fact]
    public async Task Membro_comum_convida_pro_jogo_da_semana()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));
        var resultado = await controller.Convidar(grupo.Id, sessao.DataHora, busca: null);

        Assert.IsType<ViewResult>(resultado);
    }

    [Fact]
    public async Task Quem_nao_e_da_panelinha_nao_convida_ninguem_pra_ela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var estranho = NovoJogador(9);
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, estranho.Id, SempreEsta(sessao));
        var resultado = await controller.Convidar(grupo.Id, sessao.DataHora, busca: null);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(resultado).ActionName);
    }

    // ===================== A BUSCA =====================

    [Fact]
    public async Task Busca_pelo_CPF_INTEIRO_acha_quem_os_filtros_deixaram_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        // Este não passa pelos filtros de sugestão: joga em OUTRO clube.
        var deOutroClube = NovoJogador(7);
        ctx.Jogadores.Add(deOutroClube);
        await ctx.SaveChangesAsync();
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = deOutroClube.Id, ClubeId = 999 });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));

        var semBusca = (List<Jogador>)((ViewResult)await controller.Convidar(grupo.Id, sessao.DataHora, null)).Model!;
        Assert.DoesNotContain(semBusca, j => j.Id == deOutroClube.Id);

        var comBusca = (List<Jogador>)((ViewResult)await controller.Convidar(
            grupo.Id, sessao.DataHora, deOutroClube.Cpf)).Model!;
        Assert.Contains(comBusca, j => j.Id == deOutroClube.Id);
    }

    // ⚠️ O TESTE QUE MAIS IMPORTA DESTE ARQUIVO. Uma busca "começa com" transformaria a tela
    // num caça-níquel de CPF: digitar 555 e ir vendo nomes de gente real aparecer é varredura
    // da base inteira, uma tecla por vez. A busca é EXATA de propósito — só acha quem já sabe
    // o número inteiro.
    [Theory]
    [InlineData("555")]
    [InlineData("5550000000")]
    [InlineData("jog")]
    public async Task Pedaco_de_CPF_ou_de_login_NAO_acha_ninguem(string pedaco)
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var deOutroClube = NovoJogador(7);
        ctx.Jogadores.Add(deOutroClube);
        await ctx.SaveChangesAsync();
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = deOutroClube.Id, ClubeId = 999 });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));
        var achados = (List<Jogador>)((ViewResult)await controller.Convidar(
            grupo.Id, sessao.DataHora, pedaco)).Model!;

        Assert.DoesNotContain(achados, j => j.Id == deOutroClube.Id);
    }

    [Fact]
    public async Task Quem_desligou_convites_nao_e_achado_nem_pelo_CPF_exato()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var naoQuerConvite = NovoJogador(8, aceitaConvites: false);
        ctx.Jogadores.Add(naoQuerConvite);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));
        var achados = (List<Jogador>)((ViewResult)await controller.Convidar(
            grupo.Id, sessao.DataHora, naoQuerConvite.Cpf)).Model!;

        Assert.DoesNotContain(achados, j => j.Id == naoQuerConvite.Id);
    }

    // A lista some da vista; o POST não. Sem a trava no servidor, um formulário montado à mão
    // convidaria justamente quem desligou os convites.
    [Fact]
    public async Task O_POST_tambem_recusa_convidar_quem_desligou_convites()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var naoQuerConvite = NovoJogador(8, aceitaConvites: false);
        ctx.Jogadores.Add(naoQuerConvite);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));
        await controller.ConvidarJogador(sessao.Id, naoQuerConvite.Id);

        Assert.False(await ctx.ConfirmacoesSessao
            .AnyAsync(c => c.SessaoId == sessao.Id && c.JogadorId == naoQuerConvite.Id));
    }

    // O convite JÁ ESTÁ GRAVADO quando o WhatsApp entra em cena — quem foi achado pelo CPF
    // pode não ter celular aqui, e um wa.me com número vazio abre conversa com ninguém: parece
    // que o recado foi, e não foi.
    [Fact]
    public async Task Convidado_SEM_celular_entra_na_lista_do_mesmo_jeito()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var semZap = NovoJogador(6);
        semZap.Celular = null;
        ctx.Jogadores.Add(semZap);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[1].Id, SempreEsta(sessao));
        await controller.ConvidarJogador(sessao.Id, semZap.Id);

        Assert.True(await ctx.ConfirmacoesSessao
            .AnyAsync(c => c.SessaoId == sessao.Id && c.JogadorId == semZap.Id && c.Avulso));
        Assert.Null(controller.TempData["WhatsAppLink"]);
    }

    // ===================== O PLACAR COM CONVIDADO =====================

    [Fact]
    public async Task Convidado_aparece_no_placar_mesmo_SEM_ter_aceitado_o_convite()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var visita = NovoJogador(5);
        ctx.Jogadores.Add(visita);
        await ctx.SaveChangesAsync();
        await ConvidarAsync(ctx, sessao, visita);

        // Segue "Pendente" — não aceitou nada.
        Assert.Equal("Pendente", await ctx.ConfirmacoesSessao
            .Where(c => c.JogadorId == visita.Id).Select(c => c.Status).FirstAsync());

        var controller = Controller(ctx, membros[1].Id);
        var view = Assert.IsType<ViewResult>(await controller.RegistrarJogo(grupo.Id, data: null));
        var oferecidos = (List<Jogador>)view.ViewData["Membros"]!;

        Assert.Contains(oferecidos, j => j.Id == visita.Id);
        Assert.Contains(visita.Id, (HashSet<int>)view.ViewData["Convidados"]!);
    }

    [Fact]
    public async Task Placar_com_convidado_e_gravado_e_os_pontos_dos_MEMBROS_saem_certos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo);

        var visita = NovoJogador(5);
        ctx.Jogadores.Add(visita);
        await ctx.SaveChangesAsync();
        await ConvidarAsync(ctx, sessao, visita);

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 18),
            membros[0].Id, visita.Id,      // dupla 1: um membro e o convidado — venceram
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        var jogo = await ctx.JogosSemanais.SingleAsync();
        Assert.Equal(visita.Id, jogo.Dupla1Jogador2Id);

        // Vitória vale 3, derrota vale 1 (mesma régua do resto da panelinha).
        Assert.Equal(3, await ctx.JogadoresGrupo
            .Where(jg => jg.JogadorId == membros[0].Id).Select(jg => jg.PontuacaoInterna).FirstAsync());
        Assert.Equal(1, await ctx.JogadoresGrupo
            .Where(jg => jg.JogadorId == membros[1].Id).Select(jg => jg.PontuacaoInterna).FirstAsync());

        // ⚠️ E O CONVIDADO NÃO ENTRA NO RANKING DO GRUPO — de propósito, não por esquecimento.
        // Ele não tem linha em JogadorGrupo, e quem tapa buraco numa noite não pode passar na
        // frente de mensalista no ranking interno.
        Assert.False(await ctx.JogadoresGrupo.AnyAsync(jg => jg.JogadorId == visita.Id));
    }

    // A lista da tela não é a trava: ela some de vista, o POST não.
    [Fact]
    public async Task Jogador_de_fora_da_panelinha_NAO_entra_no_placar_por_POST_montado_a_mao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var estranho = NovoJogador(9);
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 18),
            membros[0].Id, estranho.Id,
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        Assert.False(await ctx.JogosSemanais.AnyAsync());
    }

    // Corrigir um placar velho não pode ser recusado porque um dos quatro saiu da panelinha
    // desde então — senão o jogo fica com o erro preso pra sempre.
    [Fact]
    public async Task Corrigir_placar_antigo_continua_valendo_com_quem_ja_estava_no_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var jogo = new JogoSemanal
        {
            GrupoId = grupo.Id, DataJogo = new DateTime(2026, 7, 1),
            Dupla1Jogador1Id = membros[0].Id, Dupla1Jogador2Id = membros[1].Id,
            Dupla2Jogador1Id = membros[2].Id, Dupla2Jogador2Id = membros[3].Id,
            GamesDupla1 = 6, GamesDupla2 = 4, RegistradoPorId = membros[0].Id,
        };
        ctx.JogosSemanais.Add(jogo);
        await ctx.SaveChangesAsync();

        // Um dos quatro sai da panelinha depois do jogo.
        var saiu = await ctx.JogadoresGrupo.FirstAsync(jg => jg.JogadorId == membros[3].Id);
        ctx.JogadoresGrupo.Remove(saiu);
        await ctx.SaveChangesAsync();

        // ⚠️ `ParaOFio` porque o formulário fala em `int` e o banco em `int?` desde 20/08/2026 (a
        // vaga do convidado sem nome — ver Services/ConvidadoNoJogo). Reenviar os ids do jogo é
        // exatamente o que a tela faz ao corrigir só o placar, e é essa tradução que ela aplica.
        var controller = Controller(ctx, membros[0].Id);
        await controller.EditarJogo(
            jogo.Id, jogo.DataJogo,
            ConvidadoNoJogo.ParaOFio(jogo.Dupla1Jogador1Id), ConvidadoNoJogo.ParaOFio(jogo.Dupla1Jogador2Id),
            ConvidadoNoJogo.ParaOFio(jogo.Dupla2Jogador1Id), ConvidadoNoJogo.ParaOFio(jogo.Dupla2Jogador2Id),
            1, 6, 2, clubeId: null);

        Assert.Equal(2, (await ctx.JogosSemanais.SingleAsync()).GamesDupla2);
    }

    // ===================== O BOTÃO DE PLACAR NA TELA DA SEMANA =====================

    // ⚠️ NÃO É COSMÉTICO. O ranking DA SEMANA é fatiado por data (`> início && <= fim`): abrir
    // o formulário sempre em "hoje" fazia quem lança na quarta o jogo de terça gravar o dia
    // errado — e um dia a mais joga a partida pra semana seguinte, onde ela some do quadro sem
    // erro nenhum.
    [Fact]
    public async Task Vindo_da_tela_da_semana_o_formulario_abre_na_data_DAQUELE_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var oJogoDaSemana = new DateTime(2026, 8, 18, 20, 0, 0);

        var controller = Controller(ctx, membros[0].Id);
        var view = Assert.IsType<ViewResult>(await controller.RegistrarJogo(grupo.Id, oJogoDaSemana));

        Assert.Equal(oJogoDaSemana.Date, view.ViewData["DataSugerida"]);
        Assert.Equal(oJogoDaSemana, view.ViewData["VoltarParaSemana"]);
    }

    [Fact]
    public async Task Vindo_do_ranking_geral_continua_abrindo_em_hoje()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        var view = Assert.IsType<ViewResult>(await controller.RegistrarJogo(grupo.Id, data: null));

        Assert.Equal(DateTime.Today, view.ViewData["DataSugerida"]);
        Assert.Null(view.ViewData["VoltarParaSemana"]);
    }

    // Quem clicou na tela da semana quer VER o ranking daquela semana mudar. Cuspir a pessoa no
    // ranking geral esconde justamente o efeito do que ela acabou de fazer.
    [Fact]
    public async Task Depois_de_salvar_a_pessoa_volta_pra_semana_de_onde_veio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var aSemana = new DateTime(2026, 8, 18, 20, 0, 0);

        var controller = Controller(ctx, membros[0].Id);
        var vindoDaSemana = await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 18),
            membros[0].Id, membros[1].Id, membros[2].Id, membros[3].Id,
            1, 6, 3, clubeId: null, voltarParaSemana: aSemana);

        var destino = Assert.IsType<RedirectToActionResult>(vindoDaSemana);
        Assert.Equal("Semana", destino.ActionName);
        Assert.Equal(grupo.Id, destino.RouteValues!["grupoId"]);

        // E quem veio do ranking geral continua voltando pra lá.
        var vindoDoRankingGeral = await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 18),
            membros[0].Id, membros[1].Id, membros[2].Id, membros[3].Id,
            1, 6, 4, clubeId: null);

        Assert.Equal("Detalhes", Assert.IsType<RedirectToActionResult>(vindoDoRankingGeral).ActionName);
    }

    // A recusa não pode custar a data: mandar de volta pro "hoje" faria a pessoa perder o que
    // estava lançando junto com o erro.
    [Fact]
    public async Task Ao_recusar_o_jogador_de_fora_o_formulario_volta_com_a_data_que_estava()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var aSemana = new DateTime(2026, 8, 18, 20, 0, 0);

        var estranho = NovoJogador(9);
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[0].Id);
        var recusa = await controller.RegistrarJogo(
            grupo.Id, new DateTime(2026, 8, 18),
            membros[0].Id, estranho.Id, membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null, voltarParaSemana: aSemana);

        var destino = Assert.IsType<RedirectToActionResult>(recusa);
        Assert.Equal("RegistrarJogo", destino.ActionName);
        Assert.Equal(aSemana, destino.RouteValues!["data"]);
        Assert.False(await ctx.JogosSemanais.AnyAsync());
    }

    // ===================== A SEMANA ATUAL =====================

    // O botão do meio não manda data nenhuma: quem responde "qual é a semana corrente" é o
    // controller. Duas respostas pra mesma pergunta divergem sozinhas na virada do dia do jogo.
    [Fact]
    public void A_semana_atual_cai_sempre_no_dia_e_hora_fixos_do_grupo()
    {
        var terca = 2;
        var vinteHoras = new TimeSpan(20, 0, 0);

        var quando = SessaoGrupoService.ProximaOcorrencia(terca, vinteHoras);

        Assert.Equal(DayOfWeek.Tuesday, quando.DayOfWeek);
        Assert.Equal(vinteHoras, quando.TimeOfDay);
        Assert.True(quando >= DateTime.Now, "A semana atual nunca é um jogo que já passou.");
        Assert.True(quando < DateTime.Now.AddDays(8), "E nunca pula uma semana inteira.");
    }

    // 🔒 O CPF NÃO PODE ENCOSTAR NESTA PÁGINA. O filtro instantâneo roda no navegador, então
    // tudo que ele compara precisa estar NO HTML — e cobrir CPF ali significaria mandar o CPF
    // de todo mundo pro navegador de qualquer um. Por isso o filtro vê só nome e login, e a
    // busca por CPF acontece no servidor.
    [Fact]
    public void A_tela_de_convite_nao_carrega_CPF_de_ninguem()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a pasta Views.");

        var tela = File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Views", "Grupos", "Convidar.cshtml"));

        Assert.DoesNotContain(".Cpf", tela);
        Assert.DoesNotContain("data-cpf", tela);
    }
}
