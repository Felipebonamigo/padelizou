using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Xunit;

namespace Padelizou.Tests;

// O mesmo jogador inscrito DUAS VEZES na mesma categoria. Aconteceu no "Interno Los
// Corneteiros" (03/08/2026): o Otávio como parceiro de um e, logo abaixo, sozinho
// "procurando parceiro" — duas vagas pra uma pessoa e uma dupla fantasma no sorteio.
public class InscricaoRepetidaTests
{
    private static async Task<(DbPadelContext ctx, Categoria categoria, Jogador a, Jogador b, Jogador c)>
        MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Interno", Codigo = "INT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(categoria);

        // CPFs com dígito verificador de verdade: estes passam pelo controller, e o
        // controller agora recusa CPF inventado (ver Services/Documentos.CpfEhValido).
        var a = new Jogador { Nome = "Otávio Wunsch", Cpf = "11144477735" };
        var b = new Jogador { Nome = "Geovani Batista", Cpf = "22255588846" };
        var c = new Jogador { Nome = "Diego Martins", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(a, b, c);
        await ctx.SaveChangesAsync();

        return (ctx, categoria, a, b, c);
    }

    private static Dupla Inscrever(DbPadelContext ctx, Categoria cat, Jogador j1, Jogador? j2 = null)
    {
        var dupla = new Dupla { CategoriaId = cat.Id, Jogador1Id = j1.Id, Jogador2Id = j2?.Id, Codigo = "D" + j1.Id };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return dupla;
    }

    // ⚠️ LINHA DE TIME NÃO É INSCRIÇÃO DE JOGADOR. Ela guarda o ORGANIZADOR em Jogador1Id e
    // nada em Jogador2Id — de fora, é idêntica a "inscrito sozinho, procurando parceiro".
    // Sem a exceção, o organizador que cadastrou um time apareceria como já inscrito na
    // categoria, e quem juntasse APAGARIA O TIME junto com a "inscrição sozinha" dele.
    [Fact]
    public async Task Linha_de_TIME_nao_conta_como_inscricao_do_organizador()
    {
        var (ctx, cat, organizador, _, _) = await MontarAsync();
        using var _ctx = ctx;

        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = organizador.Id, NomeTime = "Argentus XP", Codigo = "TIME1",
        });
        await ctx.SaveChangesAsync();

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { organizador.Id });

        Assert.Empty(achados);
        Assert.Empty(InscricaoRepetida.QuePodemSerJuntadas(achados));
    }

    [Fact]
    public async Task Quem_nao_esta_inscrito_nao_gera_conflito()
    {
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _ctx = ctx;

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id });

        Assert.Empty(achados);
        Assert.Null(InscricaoRepetida.MotivoParaRecusar(achados));
        Assert.Empty(InscricaoRepetida.QuePodemSerJuntadas(achados));
    }

    [Fact]
    public async Task Inscricao_sozinha_pode_ser_juntada_e_nao_recusa()
    {
        // É o caso normal e é o que a nova inscrição vem substituir — dizer "não" aqui seria
        // punir quem usou o sistema do jeito certo.
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, a);

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id });

        Assert.Null(InscricaoRepetida.MotivoParaRecusar(achados));
        var juntaveis = InscricaoRepetida.QuePodemSerJuntadas(achados);
        Assert.Single(juntaveis);
        Assert.Equal("Otávio Wunsch", juntaveis[0].NomeJogador);
    }

    [Fact]
    public async Task Quem_ja_tem_dupla_fechada_e_recusado()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, b, a);   // Geovani + Otávio

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id });
        var motivo = InscricaoRepetida.MotivoParaRecusar(achados);

        Assert.NotNull(motivo);
        Assert.Contains("Otávio", motivo);
        Assert.Contains("Geovani", motivo);   // a mensagem diz COM QUEM ele já está
        Assert.Empty(InscricaoRepetida.QuePodemSerJuntadas(achados));
    }

    [Fact]
    public async Task Quem_esta_na_posicao_de_parceiro_tambem_e_pego()
    {
        // O conflito não pode depender de a pessoa ser Jogador1 ou Jogador2 — foi exatamente
        // como o Otávio escapou: ele era o Jogador2 de uma dupla e o Jogador1 de outra.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, b, a);

        var comoParceiro = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id });
        var comoTitular = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { b.Id });

        Assert.NotNull(InscricaoRepetida.MotivoParaRecusar(comoParceiro));
        Assert.NotNull(InscricaoRepetida.MotivoParaRecusar(comoTitular));
    }

    [Fact]
    public async Task Inscricao_em_OUTRA_categoria_nao_conta()
    {
        // Torneio que permite várias categorias: estar na 4ª não impede entrar na 5ª. Quem
        // cuida disso é InscricaoTorneio.MotivoBloqueioMultiplasCategorias, outra regra.
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _ctx = ctx;

        var outra = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", TorneioId = cat.TorneioId };
        ctx.Categorias.Add(outra);
        await ctx.SaveChangesAsync();
        Inscrever(ctx, outra, a);

        Assert.Empty(await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id }));
    }

    [Fact]
    public async Task A_propria_dupla_nao_se_acusa_na_troca_de_parceiro()
    {
        // Sem `ignorarDuplaId`, definir o parceiro de uma inscrição faria ela apontar o dedo
        // pra si mesma e travar a troca.
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _ctx = ctx;
        var minha = Inscrever(ctx, cat, a);

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id }, ignorarDuplaId: minha.Id);

        Assert.Empty(achados);
    }

    [Fact]
    public async Task Os_dois_da_dupla_inscritos_sozinhos_geram_duas_juncoes()
    {
        // Dois que se inscreveram sozinhos e agora fecham dupla: as DUAS inscrições solo
        // precisam sair, senão sobra uma órfã.
        var (ctx, cat, a, _, c) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, a);
        Inscrever(ctx, cat, c);

        var achados = await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id, c.Id });
        var juntaveis = InscricaoRepetida.QuePodemSerJuntadas(achados);

        Assert.Equal(2, juntaveis.Count);
        Assert.Equal(2, juntaveis.Select(j => j.DuplaId).Distinct().Count());
    }

    [Fact]
    public async Task A_pergunta_nomeia_quem_ja_esta_inscrito()
    {
        var (ctx, cat, a, _, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, a);

        var juntaveis = InscricaoRepetida.QuePodemSerJuntadas(
            await InscricaoRepetida.ProcurarAsync(ctx, cat.Id, new[] { a.Id }));

        var pergunta = InscricaoRepetida.PerguntaParaJuntar(juntaveis);

        Assert.Contains("Otávio", pergunta);
        // Tem que ficar claro que ninguém perde a vaga — é o medo de quem vê "a inscrição sai".
        Assert.Contains("ninguém perde lugar", pergunta);
    }

    [Fact]
    public void Sem_conflito_nenhum_nao_ha_pergunta_nem_recusa()
    {
        var vazio = new List<InscricaoRepetida.Achado>();

        Assert.Null(InscricaoRepetida.MotivoParaRecusar(vazio));
        Assert.Equal("", InscricaoRepetida.PerguntaParaJuntar(vazio));
    }

    // ---- O caminho completo, pelo controller ----

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            // Ranking RS sem chave — ver AceitarConviteTests.
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

    private static Task<IActionResult> Inscrever(
        DuplasController controller, Categoria cat, Jogador j1, Jogador j2, bool juntar = false) =>
        controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: j1.Nome, cpf1: j1.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: j2.Nome, cpf2: j2.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            juntarComInscricaoSolo: juntar);

    [Fact]
    public async Task Inscrever_com_quem_ja_esta_sozinho_pede_confirmacao_antes_de_apagar()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, a);   // Otávio, sozinho

        var controller = Controller(ctx, b.Id);
        await Inscrever(controller, cat, b, a);   // Geovani tenta inscrever com o Otávio

        // Nada foi criado e NADA foi apagado: a inscrição do Otávio continua de pé.
        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(a.Id, duplas[0].Jogador1Id);
        Assert.Contains("Otávio", (string?)controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Confirmando_a_juncao_fica_UMA_dupla_so()
    {
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, a);

        var controller = Controller(ctx, b.Id);
        await Inscrever(controller, cat, b, a, juntar: true);

        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(b.Id, duplas[0].Jogador1Id);
        Assert.Equal(a.Id, duplas[0].Jogador2Id);
    }

    [Fact]
    public async Task Nao_da_pra_inscrever_quem_ja_tem_dupla_fechada_nem_confirmando()
    {
        // Aqui não há o que juntar: ele está jogando com outra pessoa. Confirmar não pode
        // virar uma forma de roubar o parceiro alheio.
        var (ctx, cat, a, b, c) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, b, a);   // Geovani + Otávio, fechada

        var controller = Controller(ctx, c.Id);
        await Inscrever(controller, cat, c, a, juntar: true);   // Diego tenta levar o Otávio

        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(a.Id, duplas[0].Jogador2Id);   // o Otávio continua com o Geovani
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task A_minha_propria_inscricao_sozinha_tambem_e_absorvida()
    {
        // Eu me inscrevi sozinho, achei parceiro e refiz a inscrição em dupla: a minha antiga
        // tem que sair também, senão eu fico ocupando duas vagas.
        var (ctx, cat, a, b, _) = await MontarAsync();
        using var _ctx = ctx;
        Inscrever(ctx, cat, b);   // Geovani, sozinho

        var controller = Controller(ctx, b.Id);
        await Inscrever(controller, cat, b, a, juntar: true);

        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(a.Id, duplas[0].Jogador2Id);
    }
}
