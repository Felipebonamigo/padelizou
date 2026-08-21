using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// DE QUE LADO JOGA QUEM ESTÁ PROCURANDO PARCEIRO (Felipe, 17/08/2026).
//
// Numa lista de 9 inscrições "procurando parceiro", o lado é o que separa uma da outra — sem
// ele a escolha é no escuro e a conversa começa por "de que lado tu joga?".
//
// ⚠️ A DECISÃO DE MODELAGEM que estes testes protegem: `Dupla.LadoJogador1` NULO não é
// "tanto faz", é "nunca escolheu aqui" — e aí vale o lado do PERFIL. Foi o pedido ("os que já
// estão inscritos, coloca o que ele tem no perfil"), e resolve sem UPDATE nenhum no banco.
// Quem escolhe "tanto faz" grava "Ambos", que é escolha explícita e não olha o perfil.
public class LadoDeQuemProcuraParceiroTests
{
    // ═══════════ A RÉGUA (Services/LadoNaQuadra) ═══════════

    [Theory]
    [InlineData("Esquerda", "Esquerda")]
    [InlineData("esquerda", "Esquerda")]
    [InlineData("ESQ", "Esquerda")]
    [InlineData("Direita", "Direita")]
    [InlineData("dir", "Direita")]
    [InlineData("Ambos", "Ambos")]
    // ⚠️ Nulo, vazio e qualquer coisa estranha caem em "Ambos" — o lado seguro: "não sei de
    // que lado joga" e "joga dos dois" dão no mesmo pra quem procura, porque em nenhum dos
    // dois dá pra descartar a pessoa.
    [InlineData(null, "Ambos")]
    [InlineData("", "Ambos")]
    [InlineData("qualquer coisa", "Ambos")]
    public void Normalizar_aceita_o_que_o_perfil_ja_grava(string? entrada, string esperado)
    {
        Assert.Equal(esperado, LadoNaQuadra.Normalizar(entrada));
    }

    // O CORAÇÃO DA DECISÃO: nulo cai no perfil, escolha explícita vence o perfil.
    [Theory]
    // Inscrição antiga (nunca escolheu) → vale o perfil. É o caso de quem já estava inscrito.
    [InlineData(null, "Direita", "Direita")]
    [InlineData(null, "Esquerda", "Esquerda")]
    // Perfil vazio também → "Ambos", e ninguém fica de fora por isso.
    [InlineData(null, null, "Ambos")]
    // Escolheu na inscrição → o perfil não é consultado, nem quando diz outra coisa.
    [InlineData("Esquerda", "Direita", "Esquerda")]
    // ⚠️ E "Ambos" ESCOLHIDO não pode ser tratado como "não escolheu": se caísse no perfil,
    // quem marcou "tanto faz" apareceria como "joga na direita" sem ter pedido isso.
    [InlineData("Ambos", "Direita", "Ambos")]
    public void Efetivo_usa_o_perfil_so_quando_a_inscricao_nao_escolheu(
        string? naInscricao, string? noPerfil, string esperado)
    {
        Assert.Equal(esperado, LadoNaQuadra.Efetivo(naInscricao, noPerfil));
    }

    // "Ambos" não vira selo: repetido em nove linhas seria ruído empurrando pra baixo
    // justamente a informação que distingue uma inscrição da outra.
    [Fact]
    public void So_esquerda_e_direita_viram_selo_na_lista()
    {
        Assert.Equal("Joga na esquerda", LadoNaQuadra.Rotulo(LadoNaQuadra.Esquerda));
        Assert.Equal("Joga na direita", LadoNaQuadra.Rotulo(LadoNaQuadra.Direita));
        Assert.Null(LadoNaQuadra.Rotulo(LadoNaQuadra.Ambos));
    }

    // ═══════════ A GRAVAÇÃO (DuplasController.Create) ═══════════

    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador eu)>
        MontarAsync(string? meuLadoNoPerfil)
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "Copa", Codigo = "CP1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var eu = new Jogador
        {
            Nome = "Emerson Pisoni", Cpf = "11144477735", Login = "emerson",
            LadoQuadra = meuLadoNoPerfil,
        };
        ctx.Jogadores.Add(eu);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, cat, eu);
    }

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            NSubstitute.Substitute.For<IPushNotificationService>(),
            NSubstitute.Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, NSubstitute.Substitute.For<IRankingRsService>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, NSubstitute.Substitute.For<IPushNotificationService>()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DuplasController>.Instance);

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        new[] { new System.Security.Claims.Claim(
                            System.Security.Claims.ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) },
                        "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext,
            NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    [Fact]
    public async Task Inscricao_sozinha_grava_o_lado_escolhido()
    {
        var (ctx, _, cat, eu) = await MontarAsync(meuLadoNoPerfil: "Direita");
        using var _1 = ctx;

        await Controller(ctx, eu.Id).Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: eu.Nome, cpf1: eu.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: null, cpf2: null, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            semParceiro: true, ladoJogador1: LadoNaQuadra.Esquerda);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(LadoNaQuadra.Esquerda, dupla.LadoJogador1);
        Assert.Null(dupla.Jogador2Id);
    }

    // ⚠️ COM PARCEIRO A PERGUNTA NÃO FOI FEITA — a tela esconde o campo. Gravar o que vier no
    // POST seria guardar um palpite, e o palpite apareceria como fato se a dupla desfizesse.
    [Fact]
    public async Task Inscricao_COM_parceiro_nao_grava_lado_nenhum()
    {
        var (ctx, _, cat, eu) = await MontarAsync(meuLadoNoPerfil: "Direita");
        using var _1 = ctx;

        // Nome COM sobrenome: o servidor recusa "Parceiro" sozinho, e com razão — é assim que
        // os rankings encontram a pessoa depois.
        var parceiro = new Jogador { Nome = "Ivan Holleweger", Cpf = "22255588846", Login = "parc" };
        ctx.Jogadores.Add(parceiro);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, eu.Id);
        await controller.Create(
            torneioId: cat.TorneioId, categoriaId: cat.Id,
            nome1: eu.Nome, cpf1: eu.Cpf, celular1: null, cidade1: null, estado1: null,
            nome2: parceiro.Nome, cpf2: parceiro.Cpf, celular2: null, cidade2: null, estado2: null,
            impQuintaNoite: false, impSextaNoite: false, impSabadoManha: false, impSabadoTarde: false,
            // O POST até manda (formulário velho em cache, ou POST à mão), e o servidor ignora.
            ladoJogador1: LadoNaQuadra.Esquerda);

        // Se a inscrição foi recusada, o teste diz o MOTIVO em vez de estourar num `Single`
        // vazio — erro de teste que não explica nada custa mais tempo que o defeito.
        var duplas = await ctx.Duplas.ToListAsync();
        Assert.True(duplas.Count == 1,
            $"esperava 1 inscrição, achei {duplas.Count}. Erro do controller: {controller.TempData["Erro"]}");

        Assert.Null(duplas[0].LadoJogador1);
    }

    // O caso do Felipe: quem JÁ ESTAVA inscrito (coluna nula) aparece com o lado do perfil,
    // sem ninguém ter rodado UPDATE nenhum.
    [Fact]
    public async Task Quem_ja_estava_inscrito_aparece_com_o_lado_do_perfil()
    {
        var (ctx, _, cat, eu) = await MontarAsync(meuLadoNoPerfil: "Esquerda");
        using var _1 = ctx;

        // Inscrição "antiga": criada direto, sem passar pelo campo novo.
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = eu.Id, LadoJogador1 = null });
        await ctx.SaveChangesAsync();

        var dupla = await ctx.Duplas.Include(d => d.Jogador1).SingleAsync();
        var efetivo = LadoNaQuadra.Efetivo(dupla.LadoJogador1, dupla.Jogador1.LadoQuadra);

        Assert.Equal(LadoNaQuadra.Esquerda, efetivo);
        Assert.Equal("Joga na esquerda", LadoNaQuadra.Rotulo(efetivo));
    }

    // ═══════════ A FRASE DO PERFIL E DA BUSCA (Felipe, 21/08/2026) ═══════════
    //
    // O perfil e a busca colavam `"Joga do lado " + LadoQuadra.ToLower()` e saía "joga do lado
    // esquerda" e "joga do lado ambos". Os valores gravados são femininos porque descrevem a
    // QUADRA, e "lado" é masculino — a frase não podia sair de uma colagem. E "Ambos" não é
    // lado nenhum: pede outra frase inteira.
    [Theory]
    [InlineData("Esquerda", "Joga do lado esquerdo")]
    [InlineData("Direita", "Joga do lado direito")]
    [InlineData("Ambos", "Joga em ambos os lados")]
    // Passa por `Normalizar`, então grafia solta e nulo também têm resposta — e a resposta
    // segura é a dos dois lados, igual ao resto do arquivo.
    [InlineData("esq", "Joga do lado esquerdo")]
    [InlineData("", "Joga em ambos os lados")]
    [InlineData(null, "Joga em ambos os lados")]
    public void A_frase_do_lado_concorda_em_genero(string? gravado, string esperado)
    {
        Assert.Equal(esperado, LadoNaQuadra.Frase(gravado));
    }

    // ⚠️ A GUARDA CONTRA A COLAGEM VOLTAR. O erro não quebra nada — a tela abre, o texto sai,
    // e só quem lê português percebe. Nenhum teste de controller pegaria.
    [Theory]
    [InlineData("Views", "Auth", "Perfil.cshtml")]
    [InlineData("Views", "Jogadores", "Buscar.cshtml")]
    public void As_telas_usam_a_frase_pronta_e_nao_colam_o_valor_cru(params string[] caminho)
    {
        var tela = LerDaWeb(caminho);

        Assert.Contains("LadoNaQuadra.Frase(", tela);
        Assert.DoesNotContain("Joga do lado @", tela);
        Assert.DoesNotContain("LadoQuadra.ToLower()", tela);
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
