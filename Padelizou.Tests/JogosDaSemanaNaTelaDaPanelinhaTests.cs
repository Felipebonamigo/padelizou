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

// 26/08/2026 — OS JOGOS DA SEMANA NA TELA DA PANELINHA, pedido do Felipe num print da tela:
// "nessa tela, exiba os jogos q ocorreram na semana".
//
// A tela JÁ carregava esses jogos pra somar o "Ranking da semana" e os descartava — o ranking
// dizia quem pontuou sem dizer de onde os pontos vieram. O que este arquivo protege:
//
//   • A JANELA. A lista e o ranking logo abaixo dela saem da MESMA consulta, e é isso que faz
//     os dois poderem ser conferidos um contra o outro a olho. Uma janela própria pra lista
//     seria uma quarta cópia da definição de semana (já são três em produção).
//   • OS `.Include()`. Sem eles a tela INTEIRA cai — `ConvidadoNoJogo.NomeNaTela` estoura de
//     propósito quando a navegação vem nula, e a suíte não renderiza Razor: tirar um Include
//     passa verde aqui e derruba a página em produção.
//   • O CERCADO DO GRUPO. Panelinha é grupo privado; jogo de um grupo não pode aparecer na
//     tela de outro, e quem não é membro nem convidado não chega na tela.
public class JogosDaSemanaNaTelaDaPanelinhaTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
    {
        var controller = new GruposController(
            ctx, new SessaoGrupoService(ctx),
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

    private static async Task<(GrupoPrivado grupo, List<Jogador> membros, Clube clube)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"Semanal {i}", Cpf = $"5550000000{i}", Login = $"semjog{i}" })
            .ToList();
        ctx.Jogadores.AddRange(membros);

        var clube = new Clube { Nome = "OK Padel" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Terças no OK", CodigoConvite = "SEMJOG", AdministradorId = membros[0].Id,
            DiaSemanaFixo = 2, HorarioFixo = new TimeSpan(19, 0, 0),
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.AddRange(membros.Select(m =>
            new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id, PontuacaoInterna = 0 }));
        await ctx.SaveChangesAsync();

        return (grupo, membros, clube);
    }

    // A sessão daquela semana, criada como a tela cria. Devolve a data pra ancorar os jogos.
    private static async Task<SessaoGrupo> SessaoAsync(DbPadelContext ctx, GrupoPrivado grupo, int euId)
    {
        var resultado = await Controller(ctx, euId).Semana(grupo.Id, null);
        var view = Assert.IsType<ViewResult>(resultado);
        return Assert.IsType<SessaoGrupo>(view.Model);
    }

    private static JogoSemanal Jogo(DbPadelContext ctx, GrupoPrivado grupo, List<Jogador> membros,
        DateTime quando, Clube? clube = null, int vencedorLado = 1,
        int? games1 = 6, int? games2 = 3, bool convidadoNaVaga4 = false)
    {
        var jogo = new JogoSemanal
        {
            GrupoId = grupo.Id,
            DataJogo = quando.Date,
            ClubeId = clube?.Id,
            Dupla1Jogador1Id = membros[0].Id,
            Dupla1Jogador2Id = membros[1].Id,
            Dupla2Jogador1Id = membros[2].Id,
            Dupla2Jogador2Id = convidadoNaVaga4 ? null : membros[3].Id,
            GamesDupla1 = games1,
            GamesDupla2 = games2,
            VencedorLado = vencedorLado,
            RegistradoPorId = membros[0].Id,
        };
        ctx.JogosSemanais.Add(jogo);
        ctx.SaveChanges();
        return jogo;
    }

    private static List<JogoSemanal> ListaDaTela(ViewResult view) =>
        Assert.IsType<List<JogoSemanal>>(view.ViewData["JogosDaSemana"]);

    // O tracker é limpo antes de abrir a tela: em produção cada request nasce com um DbContext
    // vazio, e o teste fica mais perto disso. Não confunda com trava de `.Include()` — não é
    // (ver o comentário de `Os_jogos_vem_com_os_quatro_jogadores...`, e a trava de verdade em
    // `IncludesDaConsultaDosJogosDaSemanaTests` no fim do arquivo).
    private static async Task<ViewResult> AbrirTelaAsync(DbPadelContext ctx, int euId, int grupoId, DateTime? data)
    {
        ctx.ChangeTracker.Clear();
        return Assert.IsType<ViewResult>(await Controller(ctx, euId).Semana(grupoId, data));
    }

    // ── A lista chega na tela ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tela_da_semana_entrega_os_jogos_daquela_semana()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        var jogo = Jogo(ctx, grupo, membros, sessao.DataHora, clube);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);

        Assert.Equal(jogo.Id, Assert.Single(ListaDaTela(view)).Id);
    }

    // ── A janela ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_janela_da_lista_e_a_MESMA_do_ranking_da_semana()
    {
        // As duas bordas de uma vez: o dia da sessão ANTERIOR está fora (início exclusivo — ele
        // pertence à semana passada) e o dia DESTA sessão está dentro (fim inclusivo). Trocar
        // `>` por `>=` na consulta, ou ancorar em DateTime.Today em vez da data da sessão,
        // quebra aqui — e faria a lista discordar do ranking impresso logo abaixo dela.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        var fim = sessao.DataHora.Date;
        var foraPorUmDia = Jogo(ctx, grupo, membros, fim.AddDays(-7), clube);
        var dentroNaBordaDeBaixo = Jogo(ctx, grupo, membros, fim.AddDays(-6), clube);
        var dentroNaBordaDeCima = Jogo(ctx, grupo, membros, fim, clube);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);
        var lista = ListaDaTela(view);

        Assert.Equal(2, lista.Count);
        Assert.DoesNotContain(lista, j => j.Id == foraPorUmDia.Id);
        Assert.Contains(lista, j => j.Id == dentroNaBordaDeBaixo.Id);
        Assert.Contains(lista, j => j.Id == dentroNaBordaDeCima.Id);

        // E a prova de que é a MESMA janela: o ranking impresso ao lado saiu desses 2 jogos.
        // Com 2 vitórias da dupla 1, os dois vencedores têm mais pontos que os dois perdedores.
        var ranking = Assert.IsType<List<RankingMesItem>>(view.ViewData["RankingSemana"]);
        Assert.All(ranking, item => Assert.True(item.Pontos > 0));
        Assert.Contains(ranking, item => item.Jogador.Id == membros[0].Id);
    }

    // ── Os Include ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Os_jogos_vem_com_os_quatro_jogadores_e_o_clube_carregados()
    {
        // Trava o CONTRATO que a view consome: pra cada jogo da lista, `NomeNaTela` (exatamente
        // o que o Razor chama) devolve um nome nas quatro vagas e o clube veio junto.
        //
        // ⚠️ ELE NÃO TRAVA OS `.Include()`, e isso foi MEDIDO em vez de suposto: apagando um
        // `.Include()` do controller, este teste continuou VERDE. Dois motivos empilhados —
        // (1) o EF InMemory preenche navegação por *fixup* a partir do ChangeTracker, e
        // (2) a própria action carrega os quatro jogadores ANTES desta consulta, no
        //     `ranking` (`JogadoresGrupo.Include(jg => jg.Jogador)`), então limpar o tracker
        //     antes de chamar a tela também não resolve — ela o repopula sozinha.
        // Nenhum teste com InMemory consegue provar Include aqui. Quem trava é o teste de
        // FONTE em `IncludesDaConsultaDosJogosDaSemanaTests`, no fim deste arquivo.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        Jogo(ctx, grupo, membros, sessao.DataHora, clube);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);
        var jogo = Assert.Single(ListaDaTela(view));

        Assert.Equal(membros[0].ComoChamar, ConvidadoNoJogo.NomeNaTela(jogo.Dupla1Jogador1Id, jogo.Dupla1Jogador1));
        Assert.Equal(membros[1].ComoChamar, ConvidadoNoJogo.NomeNaTela(jogo.Dupla1Jogador2Id, jogo.Dupla1Jogador2));
        Assert.Equal(membros[2].ComoChamar, ConvidadoNoJogo.NomeNaTela(jogo.Dupla2Jogador1Id, jogo.Dupla2Jogador1));
        Assert.Equal(membros[3].ComoChamar, ConvidadoNoJogo.NomeNaTela(jogo.Dupla2Jogador2Id, jogo.Dupla2Jogador2));
        Assert.NotNull(jogo.Clube);
    }

    [Fact]
    public async Task Jogo_com_convidado_sem_nome_continua_na_lista()
    {
        // A vaga do convidado sem nome (id nulo) é NORMAL desde 20/08/2026. As quatro navegações
        // são opcionais justamente pro `.Include()` virar LEFT JOIN — obrigatórias, o jogo com
        // convidado SUMIRIA da lista sem erro nenhum.
        //
        // ⚠️ Alcance honesto: o EF InMemory não faz JOIN de verdade, então isto trava a
        // INTENÇÃO. A regressão de INNER JOIN só apareceria no Postgres.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        var jogo = Jogo(ctx, grupo, membros, sessao.DataHora, clube, convidadoNaVaga4: true);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);
        var naTela = Assert.Single(ListaDaTela(view));

        Assert.Equal(jogo.Id, naTela.Id);
        Assert.Equal(ConvidadoNoJogo.Rotulo, ConvidadoNoJogo.NomeNaTela(naTela.Dupla2Jogador2Id, naTela.Dupla2Jogador2));
    }

    // ── A ordem ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Os_jogos_vem_do_mais_recente_pro_mais_antigo_com_o_Id_desempatando()
    {
        // DataJogo é data pura e a panelinha lança vários jogos na MESMA noite — sem o
        // desempate por Id a ordem muda de um F5 pro outro.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        var primeiro = Jogo(ctx, grupo, membros, sessao.DataHora, clube);
        var segundo = Jogo(ctx, grupo, membros, sessao.DataHora, clube);
        var terceiro = Jogo(ctx, grupo, membros, sessao.DataHora, clube);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);

        Assert.Equal(
            new[] { terceiro.Id, segundo.Id, primeiro.Id },
            ListaDaTela(view).Select(j => j.Id).ToArray());
    }

    // ── O cercado do grupo ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_lista_nao_vaza_jogo_de_outro_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);

        var outroGrupo = new GrupoPrivado
        {
            Nome = "Quintas no Hangar", CodigoConvite = "SEMJOG2", AdministradorId = membros[0].Id,
            DiaSemanaFixo = 4, HorarioFixo = new TimeSpan(20, 0, 0),
        };
        ctx.GruposPrivados.Add(outroGrupo);
        await ctx.SaveChangesAsync();

        var doOutro = Jogo(ctx, outroGrupo, membros, sessao.DataHora, clube);
        var oMeu = Jogo(ctx, grupo, membros, sessao.DataHora, clube);

        var view = await AbrirTelaAsync(ctx, membros[0].Id, grupo.Id, sessao.DataHora);
        var lista = ListaDaTela(view);

        Assert.Equal(oMeu.Id, Assert.Single(lista).Id);
        Assert.DoesNotContain(lista, j => j.Id == doOutro.Id);
    }

    [Fact]
    public async Task Quem_nao_e_membro_nem_convidado_da_sessao_nao_ve_a_lista()
    {
        // O portão que já existe (a tela redireciona quem não é membro nem tem convite pra
        // sessão) tem que continuar valendo DEPOIS da lista de jogos existir: mover a consulta
        // pra antes dele entregaria os jogos de um grupo privado a qualquer conta logada.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros, clube) = await MontarAsync(ctx);
        var sessao = await SessaoAsync(ctx, grupo, membros[0].Id);
        Jogo(ctx, grupo, membros, sessao.DataHora, clube);

        var estranho = new Jogador { Nome = "De Fora", Cpf = "55500000099", Login = "defora" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        var resultado = await Controller(ctx, estranho.Id).Semana(grupo.Id, sessao.DataHora);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Index", redirect.ActionName);
    }
}

// A TRAVA DOS `.Include()` — e ela é de FONTE por necessidade medida, não por preguiça.
//
// A consulta precisa das 5 navegações: a view lê os nomes por `ConvidadoNoJogo.NomeNaTela`, que
// ESTOURA quando o id está preenchido e a navegação veio nula. Sem elas, a tela da panelinha
// inteira cai — não só o card.
//
// ⚠️ POR QUE NÃO DÁ PRA TRAVAR ISSO COM TESTE DE COMPORTAMENTO: com EF InMemory, apagar um
// `.Include()` do controller deixa a suíte VERDE. Medido, não suposto. O provider preenche
// navegação por *fixup* a partir do ChangeTracker, e a própria action `Semana` carrega os
// quatro jogadores ANTES desta consulta (no `ranking`, via `JogadoresGrupo.Include(jg =>
// jg.Jogador)`) — então nem limpar o tracker antes da chamada resolve: ela o repopula sozinha.
// A alternativa a este teste de fonte era não travar nada.
//
// Ele é grosseiro de propósito: confere que os 5 `.Include(j => ...)` aparecem no trecho da
// consulta de `JogosSemanais`. Não prova que a query roda — prova que ninguém apagou uma linha
// cujo efeito só aparece renderizando Razor em produção.
public class IncludesDaConsultaDosJogosDaSemanaTests
{
    [Theory]
    [InlineData("Dupla1Jogador1")]
    [InlineData("Dupla1Jogador2")]
    [InlineData("Dupla2Jogador1")]
    [InlineData("Dupla2Jogador2")]
    [InlineData("Clube")]
    public void A_consulta_dos_jogos_da_semana_carrega_a_navegacao(string navegacao)
    {
        var trecho = TrechoDaConsulta();
        Assert.Contains($".Include(j => j.{navegacao})", trecho);
    }

    [Fact]
    public void A_consulta_dos_jogos_da_semana_desempata_por_Id()
    {
        // DataJogo é data pura e a panelinha lança vários jogos na mesma noite: sem isto a
        // ordem da lista muda de um F5 pro outro.
        Assert.Contains(".ThenByDescending(j => j.Id)", TrechoDaConsulta());
    }

    // O pedaço do controller que vai da atribuição de `jogosDaSemana` até o `.ToListAsync()`
    // dela. Recortar, e não buscar no arquivo inteiro: `.Include(j => j.Clube)` também está na
    // consulta de `jogosRecentes` (Detalhes), e a busca solta passaria verde com a NOSSA
    // apagada. A âncora é o nome da variável, não `_context.JogosSemanais` — esse aparece 10
    // vezes neste controller, e `IndexOf` pegaria a primeira, que é de outra tela.
    private static string TrechoDaConsulta()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Controllers", "GruposController.cs"));

        var inicio = fonte.IndexOf("var jogosDaSemana = await _context.JogosSemanais", StringComparison.Ordinal);
        Assert.True(inicio >= 0,
            "Não achei `var jogosDaSemana = await _context.JogosSemanais` no GruposController — "
            + "a consulta da tela da semana foi renomeada ou movida, e esta trava parou de olhar pra ela.");

        var fim = fonte.IndexOf(".ToListAsync()", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim da consulta de jogosDaSemana.");

        return fonte[inicio..fim];
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}

// A consulta dos jogos da semana ganhou 5 `.Include()` em cima de um `Where` que já usava
// `.Date` — e o EF InMemory NÃO valida SQL: uma consulta que o Postgres recusa passa lisa pela
// suíte inteira e só estoura em produção (a lição de 19/08/2026).
//
// ⚠️ HONESTIDADE SOBRE ESTE TESTE: ele não foi escrito antes da correção nem visto falhar. Não
// dá — ele compila a consulta em vez de exercitar comportamento, então nasce verde por natureza.
// É rede de segurança contra uma tradução impossível, não TDD.
public class TraducaoDaConsultaDosJogosDaSemanaTests
{
    [Fact]
    public void A_consulta_dos_jogos_da_semana_com_os_Includes_vira_SQL()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=lugar-nenhum;Database=padelizou;Username=x;Password=y")
            .Options;
        using var ctx = new DbPadelContext(options);

        var fimSemana = new DateTime(2026, 8, 25);
        var inicioSemana = fimSemana.AddDays(-7);

        var consulta = ctx.JogosSemanais
            .Include(j => j.Dupla1Jogador1)
            .Include(j => j.Dupla1Jogador2)
            .Include(j => j.Dupla2Jogador1)
            .Include(j => j.Dupla2Jogador2)
            .Include(j => j.Clube)
            .Where(j => j.GrupoId == 1 && j.DataJogo.Date > inicioSemana && j.DataJogo.Date <= fimSemana)
            .OrderByDescending(j => j.DataJogo)
            .ThenByDescending(j => j.Id);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }
}
