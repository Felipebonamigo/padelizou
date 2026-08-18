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

// INSCREVER ESCOLHENDO O JOGADOR PELO NOME, sem saber o CPF dele (pedido do Felipe,
// 14/08/2026): "permita que ao adicionar uma inscrição no torneio, possa procurar o jogador
// pelo nome também, além do cpf".
//
// O parceiro já tinha essa porta; o jogador 1 não — e é o jogador 1 que o organizador
// preenche quando inscreve alguém na secretaria do clube. A tela manda `jogador1Id` (ou
// `jogador2Id`) e o servidor completa CPF e nome pelo cadastro.
//
// ⚠️ POR QUE O SERVIDOR COMPLETA, e a tela não: a busca por nome devolve só Id, nome e foto
// (Services/BuscaJogador). CPF e celular de terceiro NÃO saem do servidor — com eles na
// resposta, três letras varreriam a base inteira. Estes testes seguram justamente isso: a
// inscrição tem que nascer completa a partir do Id, sem a tela nunca ter visto o CPF.
public class InscricaoEscolhidaPeloNomeTests
{
    private static async Task<(DbPadelContext ctx, Categoria categoria, Jogador a, Jogador b)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Aberto do Clube", Codigo = "ABC1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(categoria);

        // CPFs com dígito verificador de verdade — o controller recusa CPF inventado.
        var a = new Jogador { Nome = "Otávio Wunsch", Cpf = "11144477735", Celular = "51999990001" };
        var b = new Jogador { Nome = "Geovani Batista", Cpf = "22255588846", Celular = "51999990002" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();

        return (ctx, categoria, a, b);
    }

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
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // A tela manda o Id e NADA do jogador 1 — é exatamente o que ela tem depois de escolher
    // um nome na lista. Se o servidor não completar pelo cadastro, a inscrição não nasce.
    [Fact]
    public async Task Jogador_1_escolhido_pelo_NOME_inscreve_sem_a_tela_saber_o_CPF()
    {
        var (ctx, cat, a, b) = await MontarAsync();
        using var _ = ctx;

        var controller = Controller(ctx, b.Id);

        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            // Vazios de propósito: é assim que o formulário chega quando a pessoa foi
            // escolhida na lista — os campos de CPF ficam escondidos e limpos.
            nome1: "", cpf1: "", celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            jogador1Id: a.Id);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(a.Id, dupla.Jogador1Id);
        Assert.Equal(b.Id, dupla.Jogador2Id);
    }

    // Os DOIS lados pelo nome, que é o caso do organizador inscrevendo uma dupla inteira de
    // gente que já tem conta: nenhum CPF passa pelo navegador.
    [Fact]
    public async Task A_dupla_inteira_pode_ser_escolhida_pelo_nome()
    {
        var (ctx, cat, a, b) = await MontarAsync();
        using var _ = ctx;

        var controller = Controller(ctx, a.Id);

        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: "", cpf1: "", celular1: null, cidade1: null, estado1: null,
            nome2: "", cpf2: "", celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            jogador1Id: a.Id, jogador2Id: b.Id);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(a.Id, dupla.Jogador1Id);
        Assert.Equal(b.Id, dupla.Jogador2Id);
    }

    // ⚠️ O CADASTRO DE QUEM JÁ EXISTE NÃO PODE SER APAGADO. Escolhido pela lista, celular e
    // cidade chegam VAZIOS no POST — se o gravador sobrescrevesse, inscrever alguém pelo nome
    // limparia o WhatsApp dele, e o organizador perderia o contato do próprio inscrito.
    [Fact]
    public async Task Escolher_pelo_nome_nao_apaga_o_celular_de_quem_ja_tem_cadastro()
    {
        var (ctx, cat, a, b) = await MontarAsync();
        using var _ = ctx;

        var controller = Controller(ctx, b.Id);

        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: "", cpf1: "", celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            jogador1Id: a.Id);

        var otavio = await ctx.Jogadores.FindAsync(a.Id);
        Assert.Equal("51999990001", otavio!.Celular);
        Assert.Equal("Otávio Wunsch", otavio.Nome);
    }

    // Id que não existe (lista velha numa aba aberta há muito tempo, ou POST montado à mão)
    // não pode virar inscrição fantasma nem página de erro: volta com recado.
    [Fact]
    public async Task Id_que_nao_existe_recusa_com_recado_e_nao_cria_nada()
    {
        var (ctx, cat, _, b) = await MontarAsync();
        using var _ctx = ctx;

        var controller = Controller(ctx, b.Id);

        var resultado = await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: "", cpf1: "", celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            jogador1Id: 999999);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Contains("Não encontrei esse jogador", (string?)controller.TempData["Erro"]);
        Assert.Empty(await ctx.Duplas.ToListAsync());
    }

    // ⚠️ O JS ACHA OS CAMPOS POR ID, e id errado não é erro: `getElementById` devolve null, o
    // bloco defensivo (`if (!campo) return;`) engole, e a busca simplesmente NÃO liga — a tela
    // abre bonita, com um campo que não faz nada. Como agora são dois lados chamando a mesma
    // função com listas de ids diferentes, trocar uma letra numa delas é fácil e silencioso.
    //
    // O teste lê a view e cobra que todo id pedido pelo JS exista no HTML.
    [Fact]
    public void Todo_id_que_a_busca_por_nome_pede_existe_no_HTML_da_tela()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Views", "Torneios", "Details.cshtml")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a Details.cshtml.");

        var view = File.ReadAllText(Path.Combine(dir!.FullName, "Padelizou", "Views", "Torneios", "Details.cshtml"));

        var noHtml = System.Text.RegularExpressions.Regex.Matches(view, @"id=""(pdz[A-Za-z0-9]+)""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        // Os dois blocos de configuração passados pra pdzLigarBuscaPorNome.
        var pedidos = System.Text.RegularExpressions.Regex.Matches(
                view, @"pdzLigarBuscaPorNome\(\{(.*?)\}\)", System.Text.RegularExpressions.RegexOptions.Singleline)
            .SelectMany(m => System.Text.RegularExpressions.Regex.Matches(m.Groups[1].Value, @"'(pdz[A-Za-z0-9]+)'")
                .Select(i => i.Groups[1].Value))
            .ToList();

        // Se a regex não achar as chamadas, o teste passaria vazio e não protegeria nada.
        Assert.True(pedidos.Count >= 18,
            $"Esperava os ids das DUAS chamadas (9 cada); achei {pedidos.Count}. A chamada mudou de forma?");

        var faltando = pedidos.Where(id => !noHtml.Contains(id)).Distinct().ToList();
        Assert.True(faltando.Count == 0,
            "O JS procura id que não existe na tela — a busca não liga e ninguém vê erro: "
            + string.Join(", ", faltando));
    }

    // O Id VENCE o que veio digitado. Sem isto, um CPF esquecido no campo escondido (a pessoa
    // digitou, depois escolheu na lista) inscreveria alguém DIFERENTE do nome que está na
    // tela — e o formulário mostraria um nome enquanto o banco guardaria outro.
    [Fact]
    public async Task O_Id_escolhido_vence_um_CPF_que_tenha_sobrado_no_campo()
    {
        var (ctx, cat, a, b) = await MontarAsync();
        using var _ = ctx;

        var controller = Controller(ctx, b.Id);

        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            // O campo escondido ainda carrega o CPF do Geovani...
            nome1: b.Nome, cpf1: b.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: b.Nome, cpf2: b.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            // ...mas quem foi escolhido na lista é o Otávio.
            jogador1Id: a.Id);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(a.Id, dupla.Jogador1Id);
    }
}
