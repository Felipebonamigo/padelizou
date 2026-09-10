using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O ORGANIZADOR TROCA O PARCEIRO DEPOIS DO SORTEIO; O JOGADOR, NÃO.
//
// 🗣️ Felipe, 10/09/2026, olhando o card da 3ª do Er: *"troque o parceiro do paulo prass
// (er guex) pelo 03761230010 cpf Arthur Prass"*. A chave do Er já estava sorteada, e TROCAR
// estava preso em `Status == "Inscrições Abertas"` — o único caminho que restava era remover a
// inscrição e refazê-la, o que perde a vaga na chave, o pagamento e o lugar na grade.
//
// ⚠️ A JANELA É A MESMA DO DEFINIR (`JanelaDoParceiro`), e o motivo é estrutural: `Partida`
// guarda `Dupla1Id`/`Dupla2Id` e NÃO guarda quem jogou. Quem jogou é lido da composição ATUAL
// da dupla — ranking, MVP, Padelímetro e estatística, todos. Trocar depois do primeiro jogo
// daria ao parceiro novo os games que outra pessoa jogou, sem uma linha de histórico dizendo o
// contrário. Por isso o teto é `jaComecouAJogar`, e não o fim do torneio.
//
// ⚠️ É O MESMO PAR DE `AlteracaoDeImpedimento`: o jogador anda pelo STATUS (trocar continua
// preso em "Inscrições Abertas"), o organizador anda pela GRADE — porque é ele quem enxerga a
// grade inteira e quem responde por ela.
public class TrocaDeParceiroPeloOrganizadorTests
{
    private static Torneio TorneioCom(string status, string formato = FormatoDoTorneio.Padrao) =>
        new() { Id = 1, Nome = "Er Padel Open", Codigo = "ER1", Status = status, Formato = formato };

    private static Dupla Fechada() =>
        new() { Id = 1, Codigo = "D1", Jogador1Id = 10, Jogador2Id = 11 };

    private static Dupla Sozinha() =>
        new() { Id = 1, Codigo = "D1", Jogador1Id = 10, Jogador2Id = null };

    // ── A RÉGUA ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Com_a_chave_ja_sorteada_o_organizador_ainda_troca()
    {
        // O caso do Paulo: a chave do Er já saiu, ninguém jogou ainda, e o parceiro mudou.
        Assert.Null(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Fase de Grupos"), jaComecouAJogar: false));
    }

    [Fact]
    public void Com_as_inscricoes_ainda_abertas_o_organizador_troca()
    {
        Assert.Null(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Inscrições Abertas"), jaComecouAJogar: false));
    }

    [Fact]
    public void Depois_que_a_bola_rolou_para_ESSA_dupla_nem_o_organizador_troca()
    {
        // O teto que protege o histórico: a partir daqui a troca reescreveria quem jogou.
        var motivo = JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Fase de Grupos"), jaComecouAJogar: true);

        Assert.NotNull(motivo);
        Assert.Contains("já entrou em quadra", motivo);
    }

    [Fact]
    public void Torneio_finalizado_nao_aceita_troca()
    {
        // Mesmo teto do definir: o W.O. é lançado à mão, então a partida de quem não apareceu
        // fica "Agendada" pra sempre e o `jaComecouAJogar` sozinho não fecharia a janela.
        var motivo = JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Finalizado"), jaComecouAJogar: false);

        Assert.NotNull(motivo);
        Assert.Contains("já terminou", motivo);
    }

    [Fact]
    public void Torneio_cancelado_nao_aceita_troca()
    {
        Assert.NotNull(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Cancelado"), jaComecouAJogar: false));
    }

    [Fact]
    public void Time_nao_tem_parceiro_pra_trocar()
    {
        // O time é gravado como Dupla com Jogador1Id = ORGANIZADOR: sem esta recusa, a troca
        // penduraria um jogador na linha do time, calada.
        var time = new Dupla { Id = 2, Codigo = "T1", Jogador1Id = 99, Jogador2Id = null, NomeTime = "Er Padel" };

        Assert.NotNull(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            time, TorneioCom("Fase de Grupos"), jaComecouAJogar: false));
    }

    [Fact]
    public void No_americano_individual_nao_ha_parceiro_pra_trocar()
    {
        // Lá a linha de Dupla é pareamento de rodada ou o carimbo de campeão — nunca inscrição.
        Assert.NotNull(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Fase de Grupos", FormatoDoTorneio.Americano), jaComecouAJogar: false));
    }

    [Fact]
    public void No_americano_de_duplas_a_vaga_e_real_e_a_troca_vale()
    {
        // Ali a inscrição É a dupla — a mesma distinção que JanelaDoParceiro já fazia no definir.
        Assert.Null(JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Fechada(), TorneioCom("Fase de Grupos", FormatoDoTorneio.AmericanoDeDuplas), jaComecouAJogar: false));
    }

    [Fact]
    public void Dupla_sem_parceiro_e_caso_de_DEFINIR_e_a_regua_diz_isso()
    {
        // Duas perguntas diferentes: aqui não há quem trocar. Quem responde é MotivoParaNaoDefinir.
        var motivo = JanelaDoParceiro.MotivoParaOrganizadorNaoTrocar(
            Sozinha(), TorneioCom("Fase de Grupos"), jaComecouAJogar: false);

        Assert.NotNull(motivo);
        Assert.Contains("sem parceiro", motivo);
    }

    // ── O CAMINHO DE VERDADE, PELO CONTROLLER ─────────────────────────────────────────────

    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador organizador,
        Jogador paulo, Jogador erGuex, Jogador arthur, Dupla dupla)> MontarAsync(string status)
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Er Padel Open", Codigo = "ER1", Status = status };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "3ª Masculina", Codigo = "C3M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var organizador = new Jogador { Nome = "Organizador", Cpf = "11144477735" };
        var paulo = new Jogador { Nome = "Paulo Prass", Cpf = "22255588846" };
        var erGuex = new Jogador { Nome = "Er Guex", Cpf = "33366699957" };
        var arthur = new Jogador { Nome = "Arthur Prass", Cpf = "44477788827" };
        ctx.Jogadores.AddRange(organizador, paulo, erGuex, arthur);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = organizador.Id, NivelAcesso = "Criador",
        });

        var dupla = new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = paulo.Id, Jogador2Id = erGuex.Id,
            Codigo = "DPP", Pago = true,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, cat, organizador, paulo, erGuex, arthur, dupla);
    }

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx, new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    [Fact]
    public async Task O_organizador_troca_o_parceiro_com_a_chave_ja_sorteada()
    {
        var (ctx, _t, _c, organizador, _paulo, erGuex, arthur, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, arthur.Cpf, null);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Equal(arthur.Id, depois.Jogador2Id);
        Assert.NotEqual(erGuex.Id, depois.Jogador2Id);
        Assert.Null(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task A_troca_do_organizador_nao_mexe_no_dinheiro_da_inscricao()
    {
        // A dupla já era de DUAS pessoas antes e continua sendo depois: o valor congelado não
        // muda, e o "Pago" que o Felipe vê no card segue de pé. Trocar não é vender uma vaga.
        var (ctx, _t, _c, organizador, _paulo, _er, arthur, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;
        dupla.ValorInscricao = 240m;
        await ctx.SaveChangesAsync();

        await Controller(ctx, organizador.Id).TrocarParceiro(dupla.Id, arthur.Cpf, null);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        // A troca aconteceu de verdade — senão o resto deste teste passaria de graça, medindo
        // o valor de uma troca que foi recusada.
        Assert.Equal(arthur.Id, depois.Jogador2Id);
        Assert.Equal(240m, depois.ValorInscricao);
        Assert.True(depois.Pago);
    }

    [Fact]
    public async Task Depois_que_a_dupla_jogou_o_organizador_nao_troca_mais()
    {
        // Aqui a recusa protege o histórico: o jogo já foi, e quem jogou foi o Er Guex.
        var (ctx, _t, cat, organizador, _paulo, erGuex, arthur, dupla) =
            await MontarAsync("Fase de Grupos");
        using var _ = ctx;

        var adversaria = new Dupla { CategoriaId = cat.Id, Jogador1Id = organizador.Id, Codigo = "DADV" };
        ctx.Duplas.Add(adversaria);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, Dupla1Id = dupla.Id, Dupla2Id = adversaria.Id,
            Codigo = "P1", Status = "Finalizada",
        });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, arthur.Cpf, null);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Equal(erGuex.Id, depois.Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task O_jogador_comum_continua_sem_trocar_depois_do_sorteio()
    {
        // A régua do jogador NÃO se mexeu: depois do sorteio ele fala com o organizador. Sem
        // isto, a janela nova do organizador teria vazado pra quem está na chave.
        var (ctx, _t, _c, _org, paulo, erGuex, arthur, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, paulo.Id);
        await controller.TrocarParceiro(dupla.Id, arthur.Cpf, null);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Equal(erGuex.Id, depois.Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_e_da_dupla_nem_organiza_continua_sem_entrar()
    {
        // Regra 0: a janela nova não pode ter afrouxado o gate. O `arthur` aqui é estranho à
        // dupla — e é justamente quem teria interesse em se pendurar nela.
        var (ctx, _t, _c, _org, _paulo, erGuex, arthur, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var resultado = await Controller(ctx, arthur.Id).TrocarParceiro(dupla.Id, arthur.Cpf, null);

        Assert.IsType<ForbidResult>(resultado);
        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Equal(erGuex.Id, depois.Jogador2Id);
    }

    // ── A TELA ────────────────────────────────────────────────────────────────────────────
    //
    // Teste de FONTE, mesmo motivo de AcoesDaMinhaInscricaoTests: a suíte não renderiza Razor, e
    // é justamente na tela que este defeito mora. Servidor que aceita com botão escondido é a
    // mudança sem caminho — foi o que aconteceu em 09/09/2026 com a cópia do próprio jogador.

    private static string Tela()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (Directory.Exists(Path.Combine(tentativa, "Views")))
                return File.ReadAllText(Path.Combine(tentativa, "Views", "Torneios", "Details.cshtml"));
            pasta = Directory.GetParent(pasta)?.FullName;
        }

        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir do bin.");
    }

    [Fact]
    public void O_painel_do_organizador_le_a_janela_de_trocar_e_nao_o_status()
    {
        var tela = Tela();

        Assert.Contains("MotivoParaOrganizadorNaoTrocar", tela);

        // O portão antigo não pode ter sobrado: com ele, a chave sorteada esconde o botão que o
        // servidor agora aceita — e o Felipe fica de novo sem caminho no card do Paulo.
        Assert.DoesNotContain(
            "var podeTrocarParceiro = Model.Status == \"Inscrições Abertas\"",
            tela);
    }

    [Fact]
    public void O_card_do_proprio_jogador_continua_presa_no_status()
    {
        // A outra metade: a janela larga é DO ORGANIZADOR. Se ela vazasse pro card do inscrito,
        // a tela ofereceria ao Paulo uma troca que o controller recusa.
        var tela = Tela();

        var meuCard = tela.IndexOf("var inscricoesAbertas = Model.Status == \"Inscrições Abertas\";", StringComparison.Ordinal);
        Assert.True(meuCard > 0, "Não achei a régua do card da própria inscrição.");

        var painelDoOrganizador = tela.IndexOf("MotivoParaOrganizadorNaoTrocar", StringComparison.Ordinal);
        Assert.True(painelDoOrganizador > meuCard,
            "A régua do organizador tem que ficar no painel dele, depois do card do próprio inscrito.");
    }
}
