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

// 20/08/2026 — A VAGA DO CONVIDADO SEM NOME, pedido do Felipe.
//
// Desde 13/08 o convidado já entrava no placar, mas só se JÁ TIVESSE CONTA e fosse convidado pela
// tela Convidar. Quem lança o jogo está em pé ao lado da quadra às 22h, e o primo do Rafael que
// tapou o buraco não tem conta nenhuma — sem esta vaga o resultado da noite não é lançado.
//
// O que está sendo protegido aqui NÃO é a tela, são duas coisas: o fail-closed do POST (campo
// vazio continua sendo recusado, e não vira convidado) e o ranking (o anônimo não pontua, os
// outros três saem certos).
public class ConvidadoSemNomeNaPanelinhaTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
    {
        var controller = new GruposController(
            ctx, Substitute.For<ISessaoGrupoService>(),
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

    private static readonly DateTime DiaDoJogo = new(2026, 8, 18);

    // Panelinha com 4 membros — o primeiro administra.
    private static async Task<(GrupoPrivado grupo, List<Jogador> membros)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 4)
            .Select(n => new Jogador { Nome = $"Jogador {n}", Cpf = $"3330000000{n}", Login = $"conv{n}" })
            .ToList();
        ctx.Jogadores.AddRange(membros);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Terças no OK",
            CodigoConvite = "CVD123",
            AdministradorId = membros[0].Id,
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        foreach (var m in membros)
        {
            ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id });
        }
        await ctx.SaveChangesAsync();

        return (grupo, membros);
    }

    private static async Task<int> PontosAsync(DbPadelContext ctx, int jogadorId) =>
        await ctx.JogadoresGrupo.Where(jg => jg.JogadorId == jogadorId)
            .Select(jg => jg.PontuacaoInterna).FirstAsync();

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // ⭐ O TESTE QUE JÁ PASSAVA ANTES DESTA LEVA, e que só continua passando se o convidado for
    // um sentinela EXPLÍCITO.
    //
    // ⚠️ Zero é o que o model binder produz quando o campo do formulário chega vazio ou com
    // lixo, e ele SEMPRE foi recusado pelo porteiro. Se os parâmetros do POST tivessem virado
    // `int?` pra representar o convidado, "veio um convidado" e "o formulário chegou truncado"
    // passariam a ser o MESMO POST, byte a byte — e a única coisa separando os dois seria o
    // `disabled` de um botão em JavaScript. O -1 existe pra manter esta linha vermelha.
    // ═══════════════════════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Campo_de_jogador_VAZIO_continua_sendo_RECUSADO_e_NAO_vira_convidado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[0].Id, 0,               // 0 = o hidden chegou vazio
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        Assert.False(await ctx.JogosSemanais.AnyAsync());
    }

    [Fact]
    public async Task Convidado_sem_nome_entra_no_placar_e_a_vaga_grava_NULO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[0].Id, ConvidadoNoJogo.NoFio,
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        var jogo = await ctx.JogosSemanais.SingleAsync();

        // ⚠️ NULO, e não -1. O sentinela é do formulário e morre na tradução do controller —
        // gravá-lo violaria a FK, e `int` cabe em `int?` sem o compilador dizer nada.
        Assert.Null(jogo.Dupla1Jogador2Id);
        Assert.Equal(membros[0].Id, jogo.Dupla1Jogador1Id);
    }

    [Fact]
    public async Task Convidado_NAO_pontua_e_os_outros_TRES_saem_certos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[0].Id, ConvidadoNoJogo.NoFio,   // dupla 1 venceu
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        Assert.Equal(3, await PontosAsync(ctx, membros[0].Id));
        Assert.Equal(1, await PontosAsync(ctx, membros[1].Id));
        Assert.Equal(1, await PontosAsync(ctx, membros[2].Id));

        // Quem não jogou continua zerado — carimba que os 3 pontos do convidado não escorreram
        // pra ninguém por engano.
        Assert.Equal(0, await PontosAsync(ctx, membros[3].Id));
    }

    // ⚠️ O `?? 0` QUE ESTE TESTE PROÍBE. Zero é chave válida de dicionário: os pontos de todos os
    // convidados de todas as panelinhas cairiam no mesmo balde. Não vaza pro ranking (ninguém tem
    // JogadorGrupo.JogadorId = 0), mas envenena qualquer leitura futura de `Totais` — e a soma
    // continua "batendo" com ela mesma, então ninguém acha a causa.
    [Fact]
    public void A_conta_de_pontos_IGNORA_a_vaga_do_convidado_em_vez_de_somar_na_chave_zero()
    {
        var jogo = new JogoSemanal
        {
            Dupla1Jogador1Id = 7,
            Dupla1Jogador2Id = null,
            Dupla2Jogador1Id = null,
            Dupla2Jogador2Id = 9,
            VencedorLado = 1,
        };

        var totais = PontuacaoDaPanelinha.Totais(new[] { jogo });

        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, totais[7]);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorDerrota, totais[9]);
        Assert.False(totais.ContainsKey(0));
        Assert.Equal(2, totais.Count);
    }

    [Fact]
    public async Task Jogo_com_TRES_convidados_e_RECUSADO_e_a_data_volta_no_formulario()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        var resultado = await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[0].Id, ConvidadoNoJogo.NoFio,
            ConvidadoNoJogo.NoFio, ConvidadoNoJogo.NoFio,
            1, 6, 3, clubeId: null, voltarParaSemana: DiaDoJogo);

        Assert.False(await ctx.JogosSemanais.AnyAsync());
        Assert.NotNull(controller.TempData["Erro"]);

        // A recusa devolve o formulário na MESMA data — mandar pro "hoje" faria a pessoa perder
        // o dia que ela estava lançando junto com o erro.
        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(DiaDoJogo, redirect.RouteValues!["data"]);
    }

    // ⚠️ A SEGUNDA CÓPIA DA RÉGUA. Ensinar o teto só ao registrar faria o Corrigir recusar
    // exatamente o jogo que o Registrar acabou de aceitar — o bug histórico nº 1 deste projeto.
    [Fact]
    public async Task O_MESMO_limite_de_convidados_vale_no_CORRIGIR()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var jogo = new JogoSemanal
        {
            GrupoId = grupo.Id, DataJogo = DiaDoJogo,
            Dupla1Jogador1Id = membros[0].Id, Dupla1Jogador2Id = membros[1].Id,
            Dupla2Jogador1Id = membros[2].Id, Dupla2Jogador2Id = membros[3].Id,
            VencedorLado = 1, RegistradoPorId = membros[0].Id,
        };
        ctx.JogosSemanais.Add(jogo);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, membros[0].Id);
        await controller.EditarJogo(
            jogo.Id, DiaDoJogo,
            membros[0].Id, ConvidadoNoJogo.NoFio,
            ConvidadoNoJogo.NoFio, ConvidadoNoJogo.NoFio,
            1, 6, 3, clubeId: null);

        var depois = await ctx.JogosSemanais.SingleAsync();
        Assert.Equal(membros[1].Id, depois.Dupla1Jogador2Id);   // nada mudou
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ⚠️ O CAMINHO DE VOLTA PRECISA FUNCIONAR. Corrigir só o placar de um jogo que TEM convidado
    // não pode trocar ninguém: o nulo tem que atravessar o GET e o POST intacto. Se o porteiro
    // recusasse o próprio nulo, um jogo com convidado ficaria impossível de corrigir pra sempre.
    [Fact]
    public async Task Corrigir_um_jogo_QUE_TEM_convidado_nao_troca_ninguem_CALADO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var jogo = new JogoSemanal
        {
            GrupoId = grupo.Id, DataJogo = DiaDoJogo,
            Dupla1Jogador1Id = membros[0].Id, Dupla1Jogador2Id = null,
            Dupla2Jogador1Id = membros[1].Id, Dupla2Jogador2Id = membros[2].Id,
            VencedorLado = 1, GamesDupla1 = 6, GamesDupla2 = 4, RegistradoPorId = membros[0].Id,
        };
        ctx.JogosSemanais.Add(jogo);
        await ctx.SaveChangesAsync();

        // Reenvia os quatro como a tela faz — a vaga vazia volta como o sentinela.
        var controller = Controller(ctx, membros[0].Id);
        await controller.EditarJogo(
            jogo.Id, DiaDoJogo,
            ConvidadoNoJogo.ParaOFio(jogo.Dupla1Jogador1Id), ConvidadoNoJogo.ParaOFio(jogo.Dupla1Jogador2Id),
            ConvidadoNoJogo.ParaOFio(jogo.Dupla2Jogador1Id), ConvidadoNoJogo.ParaOFio(jogo.Dupla2Jogador2Id),
            1, 6, 2, clubeId: null);

        var depois = await ctx.JogosSemanais.SingleAsync();
        Assert.Equal(2, depois.GamesDupla2);                 // o placar corrigiu
        Assert.Null(depois.Dupla1Jogador2Id);                // e a vaga continua vaga
        Assert.Equal(membros[0].Id, depois.Dupla1Jogador1Id);
    }

    [Fact]
    public async Task Corrigir_trocando_o_convidado_por_um_MEMBRO_devolve_o_ponto_pra_ele()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[0].Id, ConvidadoNoJogo.NoFio,
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        Assert.Equal(0, await PontosAsync(ctx, membros[3].Id));

        // "Ah, era o Jogador 4 que tinha jogado."
        var jogo = await ctx.JogosSemanais.SingleAsync();
        await Controller(ctx, membros[0].Id).EditarJogo(
            jogo.Id, DiaDoJogo,
            membros[0].Id, membros[3].Id,
            membros[1].Id, membros[2].Id,
            1, 6, 3, clubeId: null);

        // A vaga deixou de ser vaga: agora tem dono, e o dono recebeu o ponto da vitória.
        Assert.Equal(membros[3].Id, (await ctx.JogosSemanais.SingleAsync()).Dupla1Jogador2Id);
        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, await PontosAsync(ctx, membros[3].Id));
    }

    // ⚠️ O SENTIDO CONTRÁRIO, e ele é DESTRUTIVO: trocar um membro por "Convidado" apaga uma
    // pessoa identificada do jogo e tira os pontos dela sem aviso. Não existe auditoria de
    // JogoSemanal. O que segura isso é o caminho de volta continuar aberto — e quem registrou
    // continua sendo a âncora do botão "Corrigir" mesmo tendo saído da quadra.
    [Fact]
    public async Task Corrigir_trocando_um_MEMBRO_por_convidado_TIRA_o_ponto_dele_e_QUEM_REGISTROU_ainda_desfaz()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        // Quem registra é o membro 0, que NÃO está na quadra.
        var controller = Controller(ctx, membros[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[1].Id, membros[2].Id,
            membros[3].Id, ConvidadoNoJogo.NoFio,
            1, 6, 3, clubeId: null);

        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, await PontosAsync(ctx, membros[2].Id));

        var jogo = await ctx.JogosSemanais.SingleAsync();
        await Controller(ctx, membros[0].Id).EditarJogo(
            jogo.Id, DiaDoJogo,
            membros[1].Id, ConvidadoNoJogo.NoFio,
            membros[3].Id, ConvidadoNoJogo.NoFio,
            1, 6, 3, clubeId: null);

        Assert.Equal(0, await PontosAsync(ctx, membros[2].Id));
        Assert.Null((await ctx.JogosSemanais.SingleAsync()).Dupla1Jogador2Id);

        // E dá pra desfazer: quem registrou continua com a chave, mesmo sem ter jogado.
        await Controller(ctx, membros[0].Id).EditarJogo(
            jogo.Id, DiaDoJogo,
            membros[1].Id, membros[2].Id,
            membros[3].Id, ConvidadoNoJogo.NoFio,
            1, 6, 3, clubeId: null);

        Assert.Equal(PontuacaoDaPanelinha.PontosPorVitoria, await PontosAsync(ctx, membros[2].Id));
    }

    // ⚠️ NÃO É OPCIONAL, e é o teste mais fácil de não escrever: o sorteio de duplas evita
    // repetir parceria olhando as 8 semanas anteriores. Com `?? 0`, toda dupla que teve convidado
    // viraria o par (0, Fulano) — e dois convidados, o par (0, 0). O motor passaria a fugir de
    // parcerias que nunca existiram, e o efeito é INVISÍVEL: é um sorteio, ninguém prova na tela
    // que ele saiu errado.
    [Fact]
    public void Historico_de_parcerias_IGNORA_a_dupla_que_teve_convidado()
    {
        // A mesma conta que GruposController.SortearDuplas faz — se ela mudar lá e não aqui,
        // este teste vira decoração. Ele existe pra carimbar a REGRA: par com nulo não conta.
        var jogos = new[]
        {
            new JogoSemanal { Dupla1Jogador1Id = 1, Dupla1Jogador2Id = 2, Dupla2Jogador1Id = 3, Dupla2Jogador2Id = null },
            new JogoSemanal { Dupla1Jogador1Id = null, Dupla1Jogador2Id = null, Dupla2Jogador1Id = 1, Dupla2Jogador2Id = 2 },
        };

        var vezesJuntos = new Dictionary<(int, int), int>();
        foreach (var j in jogos)
        {
            foreach (var (a, b) in new[]
            {
                (j.Dupla1Jogador1Id, j.Dupla1Jogador2Id),
                (j.Dupla2Jogador1Id, j.Dupla2Jogador2Id),
            })
            {
                if (a == null || b == null) continue;
                var par = SorteioDeDuplas.Chave(a.Value, b.Value);
                vezesJuntos[par] = vezesJuntos.GetValueOrDefault(par) + 1;
            }
        }

        Assert.Equal(2, vezesJuntos[SorteioDeDuplas.Chave(1, 2)]);   // essa dupla existiu 2x
        Assert.DoesNotContain((0, 0), vezesJuntos.Keys);
        Assert.DoesNotContain((0, 3), vezesJuntos.Keys);
        Assert.Single(vezesJuntos);
    }

    // ⚠️ NAVEGAÇÃO NULA COM ID PREENCHIDO NÃO É CONVIDADO — é `.Include()` esquecido, e tem que
    // estourar. Um `?? "Convidado"` solto no lugar disto transformaria todo Include esquecido
    // daqui pra frente em texto normal, imprimindo a palavra por cima do nome de um membro real.
    [Fact]
    public void Navegacao_nula_com_id_preenchido_EXPLODE_em_vez_de_virar_Convidado()
    {
        Assert.Equal(ConvidadoNoJogo.Rotulo, ConvidadoNoJogo.NomeNaTela(null, null));
        Assert.Throws<InvalidOperationException>(() => ConvidadoNoJogo.NomeNaTela(7, null));

        var jogador = new Jogador { Id = 7, Nome = "Fulano de Tal", Cpf = "33300000099", Login = "ful" };
        Assert.Equal(jogador.ComoChamar, ConvidadoNoJogo.NomeNaTela(7, jogador));
    }

    [Fact]
    public async Task Quem_jogou_ao_lado_de_convidado_continua_podendo_CORRIGIR_o_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        // Registra o membro 0; quem vai corrigir é o membro 2, que jogou mas não registrou.
        await Controller(ctx, membros[0].Id).RegistrarJogo(
            grupo.Id, DiaDoJogo,
            membros[1].Id, ConvidadoNoJogo.NoFio,
            membros[2].Id, membros[3].Id,
            1, 6, 3, clubeId: null);

        var jogo = await ctx.JogosSemanais.SingleAsync();
        var resultado = await Controller(ctx, membros[2].Id).EditarJogo(jogo.Id);

        Assert.IsType<ViewResult>(resultado);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // GUARDAS DE ARQUIVO — o que teste de controller não alcança.
    //
    // ⚠️ As duas telas abaixo falham 100% no NAVEGADOR: uma option com `value=""` faria o
    // `required` bloquear o submit, e um botão que só existe num dos ramos do JS some justamente
    // na panelinha grande. Nenhum teste de C# enxerga isso — a guarda é ler o arquivo.
    // Mesmo molde de CadastroDeLocalPorAjaxTests.
    // ═══════════════════════════════════════════════════════════════════════════════════════

    // Mesmo achador de CadastroDeLocalPorAjaxTests — os testes rodam a partir de bin/.
    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }

    [Theory]
    [InlineData("dupla1Jogador1Id")]
    [InlineData("dupla1Jogador2Id")]
    [InlineData("dupla2Jogador1Id")]
    [InlineData("dupla2Jogador2Id")]
    public void A_tela_de_corrigir_oferece_o_Convidado_e_NAO_bloqueia_o_submit(string campo)
    {
        var view = LerDaWeb("Views", "Grupos", "EditarJogo.cshtml");

        // Recorta SÓ o select deste jogador. A tela tem outros selects (clube, vencedor) e o do
        // clube tem um `<option value="">-- Não informar --</option>` legítimo — uma asserção na
        // tela inteira acusaria ele e não provaria nada sobre estes quatro.
        var inicio = view.IndexOf($"<select name=\"{campo}\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"Não achei o select de {campo} na tela.");
        var fim = view.IndexOf("</select>", inicio, StringComparison.Ordinal);
        var bloco = view[inicio..fim];

        // Sem a opção, um id nulo não casa com nada, o navegador seleciona o PRIMEIRO NOME da
        // lista, e quem abriu "Corrigir" só pra arrumar um 6x4 troca quem jogou, calado.
        Assert.Contains("vagaDeConvidado", bloco);

        // ⚠️ `value=""` faria o navegador tratar a opção como "não escolhi": com `required`, um
        // jogo com convidado NUNCA MAIS poderia ser corrigido. Falha 100% no navegador — nenhum
        // teste de controller enxerga.
        Assert.DoesNotContain("<option value=\"\"", bloco);
        Assert.Contains("required", bloco);
    }

    [Fact]
    public void A_tela_de_registrar_oferece_o_Convidado_nos_DOIS_jeitos_de_escolher()
    {
        var js = LerDaWeb("wwwroot", "js", "registrar-jogo-da-panelinha.js");

        Assert.Contains("botaoDeConvidado", js);

        // ⚠️ O teto vem do servidor. Um número escrito à mão aqui seria a segunda cópia da régua
        // — e as duas cópias discordariam no dia em que uma mudasse.
        Assert.Contains("convidado.maximo", js);
        Assert.Contains("convidado.noFio", js);
        Assert.DoesNotContain("totalDeConvidados() < 2", js);

        // ⚠️ O `|| ''` de antes: pseudo-id 0 seria falsy e viraria campo vazio.
        Assert.DoesNotContain("escolhidos[t][0] || ''", js);

        var view = LerDaWeb("Views", "Grupos", "RegistrarJogo.cshtml");
        Assert.Contains("ConvidadoNoJogo.MaximoPorJogo", view);
        Assert.Contains("ConvidadoNoJogo.NoFio", view);
    }
}
