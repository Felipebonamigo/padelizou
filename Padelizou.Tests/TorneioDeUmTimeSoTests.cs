using Microsoft.AspNetCore.Hosting;
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

// TORNEIO DE UM TIME SÓ (Felipe, 16/09/2026): "permitir criar o torneio restrito para um
// time, apenas quem estiver em um determinado time, poderia jogar".
//
// Três decisões dele moldam o que está testado aqui:
//   1. O ORGANIZADOR FURA a trava (mesmo `ignorarBloqueio` da trava de categoria e do
//      Ranking RS) — o convidado de fora existe, e quem responde por ele é quem organiza.
//   2. NÃO PONTUA no ranking: é evento fechado, igual ao torneio de chave de acesso.
//   3. A trava vale só na PORTA: quem sai do time depois de inscrito FICA.
//
// ⚠️ O defeito mais caro que este bloco arrisca é CALADO: a trava existir só num dos dois
// caminhos de inscrição. Dupla e Americano entram por controllers diferentes, e a versão
// que esquece um dos dois não quebra tela nenhuma — ela só deixa entrar quem não podia.
// Por isso cada regra daqui é perguntada NOS DOIS.
public class TorneioDeUmTimeSoTests
{
    private const int TimeDaCasa = 7;
    private const int OutroTime = 8;

    // Cenário: um torneio trancado no time 7, e três pessoas — uma do time, uma de outro
    // time e uma sem camisa nenhuma.
    private static async Task<DbPadelContext> MontarAsync(
        int? timeExclusivoId = TimeDaCasa, string formato = "Padrao")
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Times.AddRange(
            new Time { Id = TimeDaCasa, Nome = "Nata Padel" },
            new Time { Id = OutroTime, Nome = "ER Padel" });

        // 1 e 2 vestem a camisa da casa; 3 é de outro time; 4 não tem time.
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Dono do Time", Cpf = "11144477735", TimeId = TimeDaCasa },
            new Jogador { Id = 2, Nome = "Parceiro da Casa", Cpf = "52998224725", TimeId = TimeDaCasa },
            new Jogador { Id = 3, Nome = "Gente do ER", Cpf = "22255588846", TimeId = OutroTime },
            new Jogador { Id = 4, Nome = "Sem Camisa", Cpf = "33366699957", TimeId = null });

        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar" });
        ctx.Torneios.Add(new Torneio
        {
            Id = 1, Nome = "Interno do Nata", Codigo = "NATA1", ClubeId = 1,
            Status = "Inscrições Abertas", Formato = formato,
            TimeExclusivoId = timeExclusivoId,
            DataInicio = new DateTime(2026, 10, 10),
        });
        ctx.Categorias.Add(new Categoria { Id = 1, TorneioId = 1, Nome = "4ª Masculina", Codigo = "4M" });

        // Quem organiza é o jogador 9 — de propósito FORA do time, pra o teste do escape
        // provar que quem fura é o CARGO, e não a camisa de quem está logado.
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Organizador", Cpf = "12345678909", TimeId = null });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = 1, JogadorId = 9, NivelAcesso = "Criador",
        });

        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ── A régua pura ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Torneio_sem_time_exclusivo_nao_tranca_ninguem()
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x" };

        Assert.False(TimeExclusivoDoTorneio.Vale(torneio));
        Assert.Null(TimeExclusivoDoTorneio.MotivoDaRecusa(
            torneio, "Nata Padel", new[] { new TimeExclusivoDoTorneio.Pessoa("Qualquer um", null) }));
    }

    [Fact]
    public void Quem_veste_a_camisa_passa()
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = TimeDaCasa };

        Assert.Null(TimeExclusivoDoTorneio.MotivoDaRecusa(torneio, "Nata Padel", new[]
        {
            new TimeExclusivoDoTorneio.Pessoa("Dono do Time", TimeDaCasa),
            new TimeExclusivoDoTorneio.Pessoa("Parceiro da Casa", TimeDaCasa),
        }));
    }

    [Fact]
    public void Quem_e_de_outro_time_e_recusado_com_o_nome_dele_na_frase()
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = TimeDaCasa };

        var motivo = TimeExclusivoDoTorneio.MotivoDaRecusa(torneio, "Nata Padel", new[]
        {
            new TimeExclusivoDoTorneio.Pessoa("Dono do Time", TimeDaCasa),
            new TimeExclusivoDoTorneio.Pessoa("Gente do ER", OutroTime),
        });

        // O nome tem que aparecer: "alguém da dupla não pode" deixa o organizador
        // adivinhando qual dos dois é, com as duas pessoas do lado dele no balcão.
        Assert.NotNull(motivo);
        Assert.Contains("Gente do ER", motivo);
        Assert.Contains("Nata Padel", motivo);
    }

    [Fact]
    public void Basta_UM_dos_dois_estar_fora_pra_dupla_inteira_ser_recusada()
    {
        // ⚠️ A dupla é indivisível: metade dela no time não é "meio autorizada". Se a
        // checagem parasse no primeiro jogador, a dupla do dono do time com um amigo de
        // fora entraria — que é exatamente o furo que a trava existe pra fechar.
        var torneio = new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = TimeDaCasa };

        Assert.NotNull(TimeExclusivoDoTorneio.MotivoDaRecusa(torneio, "Nata Padel", new[]
        {
            new TimeExclusivoDoTorneio.Pessoa("Dono do Time", TimeDaCasa),
            new TimeExclusivoDoTorneio.Pessoa("Sem Camisa", null),
        }));
    }

    [Fact]
    public void Quem_nao_tem_perfil_aqui_e_recusado_e_a_frase_explica_o_que_fazer()
    {
        // CPF que o sistema não conhece nasceria como jogador NOVO, e jogador novo nasce sem
        // time. Recusar é a consequência — o que não pode é a frase ser a mesma de quem já
        // tem conta em outro time, porque o conserto é outro (criar perfil e vestir a camisa).
        var torneio = new Torneio { Nome = "x", Codigo = "x", TimeExclusivoId = TimeDaCasa };

        var motivo = TimeExclusivoDoTorneio.MotivoDaRecusa(torneio, "Nata Padel", new[]
        {
            new TimeExclusivoDoTorneio.Pessoa("Ninguém Conhecido", null, TemPerfil: false),
        });

        Assert.NotNull(motivo);
        Assert.Contains("Ninguém Conhecido", motivo);
        Assert.Contains("perfil", motivo, StringComparison.OrdinalIgnoreCase);
    }

    // ── A trava na inscrição EM DUPLA ─────────────────────────────────────────────────────

    private static DuplasController Duplas(DbPadelContext ctx, int usuarioLogadoId)
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
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static Task<IActionResult> InscreverDupla(
        DuplasController controller, Jogador j1, Jogador j2, bool ignorarBloqueio = false) =>
        controller.Create(
            torneioId: 1, categoriaId: 1,
            nome1: j1.Nome, cpf1: j1.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: j2.Nome, cpf2: j2.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            ignorarBloqueio: ignorarBloqueio);

    [Fact]
    public async Task Dupla_do_time_entra()
    {
        using var ctx = await MontarAsync();
        var controller = Duplas(ctx, usuarioLogadoId: 1);

        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(2) ?? throw new Exception());

        Assert.Single(await ctx.Duplas.ToListAsync());
        Assert.Null(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Dupla_com_gente_de_fora_do_time_nao_entra()
    {
        using var ctx = await MontarAsync();
        var controller = Duplas(ctx, usuarioLogadoId: 1);

        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(3) ?? throw new Exception());

        Assert.Empty(await ctx.Duplas.ToListAsync());
        Assert.Contains("Gente do ER", (string?)controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Torneio_sem_time_exclusivo_segue_aceitando_todo_mundo()
    {
        // A trava nova não pode mudar nada pros torneios que já existem — e todos eles têm
        // a coluna nula (a migration não faz backfill).
        using var ctx = await MontarAsync(timeExclusivoId: null);
        var controller = Duplas(ctx, usuarioLogadoId: 1);

        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(3) ?? throw new Exception());

        Assert.Single(await ctx.Duplas.ToListAsync());
    }

    [Fact]
    public async Task O_organizador_fura_a_trava_pelo_convidado()
    {
        // Decisão do Felipe: quem cuida do torneio consegue pôr um convidado de fora.
        using var ctx = await MontarAsync();
        var controller = Duplas(ctx, usuarioLogadoId: 9);   // o organizador

        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(3) ?? throw new Exception(),
                                          ignorarBloqueio: true);

        Assert.Single(await ctx.Duplas.ToListAsync());
    }

    [Fact]
    public async Task Marcar_a_caixinha_sem_ser_organizador_nao_fura_nada()
    {
        // ⚠️ `ignorarBloqueio` chega pelo POST, e POST se monta à mão. Sem o cargo do outro
        // lado, a caixinha seria a própria chave da porta que ela deveria guardar.
        using var ctx = await MontarAsync();
        var controller = Duplas(ctx, usuarioLogadoId: 1);   // do time, mas não organiza

        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(3) ?? throw new Exception(),
                                          ignorarBloqueio: true);

        Assert.Empty(await ctx.Duplas.ToListAsync());
    }

    // ── A MESMA trava na inscrição do AMERICANO ───────────────────────────────────────────

    [Fact]
    public async Task Americano_recusa_quem_nao_e_do_time()
    {
        using var ctx = await MontarAsync(formato: "Americano");
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 3);

        await controller.InscreverIndividual(torneioId: 1, categoriaId: 1,
            nome: "Gente do ER", cpf: "22255588846");

        Assert.Empty(await ctx.InscricoesAmericanas.ToListAsync());
        Assert.Contains("Nata Padel", (string?)controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Americano_aceita_quem_e_do_time()
    {
        using var ctx = await MontarAsync(formato: "Americano");
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.InscreverIndividual(torneioId: 1, categoriaId: 1,
            nome: "Dono do Time", cpf: "11144477735");

        Assert.Single(await ctx.InscricoesAmericanas.ToListAsync());
    }

    [Fact]
    public async Task Americano_tambem_deixa_o_organizador_furar()
    {
        using var ctx = await MontarAsync(formato: "Americano");
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 9);

        await controller.InscreverIndividual(torneioId: 1, categoriaId: 1,
            nome: "Gente do ER", cpf: "22255588846", ignorarBloqueio: true);

        Assert.Single(await ctx.InscricoesAmericanas.ToListAsync());
    }

    // ── A trava vale só na PORTA ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_sai_do_time_depois_de_inscrito_continua_inscrito()
    {
        // Decisão do Felipe. Tirar alguém que já se inscreveu (e talvez já pagou) é grave
        // demais pra acontecer sozinho — é a mesma leitura do ExcluirSeNaoPagar, que só
        // PERGUNTA em vez de remover.
        using var ctx = await MontarAsync();
        var controller = Duplas(ctx, usuarioLogadoId: 1);
        await InscreverDupla(controller, await ctx.Jogadores.FindAsync(1) ?? throw new Exception(),
                                          await ctx.Jogadores.FindAsync(2) ?? throw new Exception());
        Assert.Single(await ctx.Duplas.ToListAsync());

        // O parceiro troca de camisa depois de tudo feito.
        var parceiro = await ctx.Jogadores.FindAsync(2) ?? throw new Exception();
        parceiro.TimeId = OutroTime;
        await ctx.SaveChangesAsync();

        Assert.Single(await ctx.Duplas.ToListAsync());
    }
}
