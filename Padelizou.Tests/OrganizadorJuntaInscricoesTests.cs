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

// O caso real do "Interno Los Corneteiros" (04/08/2026): o organizador tentou pôr o Gabriel
// — que estava inscrito SOZINHO, procurando parceiro — como parceiro do Anderson, que também
// estava sozinho. A recusa veio dizendo "marque 'juntar com a inscrição que já existe'", e
// essa caixa só existia na aba Inscreva-se. Ele lia a instrução e não tinha o que marcar.
public class OrganizadorJuntaInscricoesTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat,
        Jogador organizador, Jogador anderson, Jogador gabriel)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Interno", Codigo = "INT1", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var organizador = new Jogador { Nome = "Organizador", Cpf = "11144477735" };
        var anderson = new Jogador { Nome = "Anderson Matteus Schwaab", Cpf = "22255588846" };
        var gabriel = new Jogador { Nome = "Gabriel Moreira", Cpf = "33366699957" };
        ctx.Jogadores.AddRange(organizador, anderson, gabriel);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = organizador.Id, NivelAcesso = "Criador",
        });
        await ctx.SaveChangesAsync();

        return (ctx, torneio, cat, organizador, anderson, gabriel);
    }

    private static Dupla Sozinho(DbPadelContext ctx, Categoria cat, Jogador quem)
    {
        var dupla = new Dupla { CategoriaId = cat.Id, Jogador1Id = quem.Id, Jogador2Id = null, Codigo = "D" + quem.Id };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return dupla;
    }

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new DuplasController(
            ctx, new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            // Ranking RS sem chave — ver AceitarConviteTests.
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
    public async Task Sem_confirmar_a_juncao_nada_muda_e_a_tela_explica_o_que_fazer()
    {
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);
        Sozinho(ctx, cat, gabriel);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, gabriel.Cpf, null, juntarComInscricaoSolo: false);

        // As duas inscrições continuam de pé — apagar uma sem perguntar seria pior que recusar.
        Assert.Equal(2, await ctx.Duplas.CountAsync());
        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);

        var erro = controller.TempData["Erro"] as string;
        Assert.NotNull(erro);
        Assert.Contains("Gabriel", erro);
        Assert.Contains("juntar com a inscrição que já existe", erro);
    }

    [Fact]
    public async Task Confirmando_vira_UMA_dupla_e_a_inscricao_sozinha_sai()
    {
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);
        Sozinho(ctx, cat, gabriel);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, gabriel.Cpf, null, juntarComInscricaoSolo: true);

        // Uma dupla só, completa, e a vaga do Gabriel não virou uma segunda inscrição.
        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(anderson.Id, duplas[0].Jogador1Id);
        Assert.Equal(gabriel.Id, duplas[0].Jogador2Id);
        Assert.Null(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task O_organizador_pode_fazer_isso_mesmo_sem_estar_na_dupla()
    {
        // Era o ponto do pedido: até aqui a leitura fácil era "só quem está na dupla mexe".
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, gabriel.Cpf, null);

        Assert.Equal(gabriel.Id, (await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
    }

    [Fact]
    public async Task Quem_nao_organiza_nem_esta_na_dupla_continua_barrado()
    {
        // A caixa nova não pode ter afrouxado quem PODE mexer.
        var (ctx, _t, cat, _organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var intruso = new Jogador { Nome = "Intruso", Cpf = "52998224725" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, intruso.Id);
        var resultado = await controller.TrocarParceiro(duplaDoAnderson.Id, gabriel.Cpf, null,
            juntarComInscricaoSolo: true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
    }

    [Fact]
    public async Task Time_nao_aceita_parceiro_nem_convite()
    {
        // Time é gravado como Dupla com NomeTime e Jogador1Id = ORGANIZADOR. Sem esta recusa
        // nada estouraria: a troca simplesmente penduraria um jogador na linha do time, calada,
        // quebrando a premissa de que nenhuma regra de jogador enxerga essa linha.
        var (ctx, _t, cat, organizador, _a, gabriel) = await MontarAsync();
        using var _ = ctx;

        var time = new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = organizador.Id, NomeTime = "Argentus XP", Codigo = "TIME1", Pago = true,
        };
        ctx.Duplas.Add(time);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, organizador.Id);

        await controller.TrocarParceiro(time.Id, gabriel.Cpf, null, juntarComInscricaoSolo: true);
        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == time.Id)).Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);

        controller.TempData.Clear();
        await controller.GerarConvite(time.Id);
        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == time.Id)).ConviteToken);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Dupla_ja_fechada_continua_sendo_um_nao_mesmo_marcando_a_caixa()
    {
        // Juntar só vale pra inscrição SOZINHA. Quem já tem parceiro sairia de uma dupla que
        // não é dele — a caixa não pode virar atalho pra isso.
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var outro = new Jogador { Nome = "Outro", Cpf = "52998224725" };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = gabriel.Id, Jogador2Id = outro.Id, Codigo = "DUP",
        });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, gabriel.Cpf, null, juntarComInscricaoSolo: true);

        Assert.Equal(2, await ctx.Duplas.CountAsync());
        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ---- Definir o parceiro pelo NOME (pedido do Felipe, 20/08/2026) ----
    //
    // O painel do organizador só aceitava CPF. Agora a lista de sugestões manda o Id, e o
    // servidor resolve o resto — ninguém precisa saber o documento de terceiro.

    [Fact]
    public async Task Escolhido_pelo_nome_vira_parceiro_sem_ninguem_digitar_CPF()
    {
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var controller = Controller(ctx, organizador.Id);
        // CPF vazio de propósito: é assim que a tela envia quando a escolha veio da lista.
        await controller.TrocarParceiro(duplaDoAnderson.Id, "", null, novoParceiroId: gabriel.Id);

        Assert.Equal(gabriel.Id, (await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
        Assert.Null(controller.TempData["Erro"]);
    }

    // ⚠️ O PONTO DA IMPLEMENTAÇÃO: escolher pelo Id preenche o CPF lá em cima e segue pelo
    // MESMO caminho de sempre. Se abrisse um atalho paralelo, a checagem de inscrição
    // repetida ficaria de fora e o caminho novo recriaria o bug que o antigo já resolve.
    [Fact]
    public async Task Pelo_nome_a_recusa_de_inscricao_repetida_continua_valendo()
    {
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);
        Sozinho(ctx, cat, gabriel);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, "", null, novoParceiroId: gabriel.Id);

        Assert.Equal(2, await ctx.Duplas.CountAsync());
        var erro = controller.TempData["Erro"] as string;
        Assert.NotNull(erro);
        Assert.Contains("juntar com a inscrição que já existe", erro);
    }

    [Fact]
    public async Task Pelo_nome_com_a_caixa_marcada_junta_as_duas_inscricoes()
    {
        var (ctx, _t, cat, organizador, anderson, gabriel) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);
        Sozinho(ctx, cat, gabriel);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, "", null,
            juntarComInscricaoSolo: true, novoParceiroId: gabriel.Id);

        var duplas = await ctx.Duplas.ToListAsync();
        Assert.Single(duplas);
        Assert.Equal(gabriel.Id, duplas[0].Jogador2Id);
    }

    [Fact]
    public async Task Id_que_nao_existe_e_recusado_sem_mexer_na_dupla()
    {
        var (ctx, _t, cat, organizador, anderson, _g) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, "", null, novoParceiroId: 987654);

        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
        Assert.NotNull(controller.TempData["Erro"] as string);
    }

    // Escolher a si mesmo pela lista tem que doer igual a digitar o próprio CPF — a recusa
    // mora depois da resolução do Id, e é justamente isso que este teste protege.
    [Fact]
    public async Task Pelo_nome_ninguem_vira_parceiro_de_si_mesmo()
    {
        var (ctx, _t, cat, organizador, anderson, _g) = await MontarAsync();
        using var _ = ctx;
        var duplaDoAnderson = Sozinho(ctx, cat, anderson);

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(duplaDoAnderson.Id, "", null, novoParceiroId: anderson.Id);

        Assert.Null((await ctx.Duplas.FirstAsync(d => d.Id == duplaDoAnderson.Id)).Jogador2Id);
        Assert.Contains("você mesmo", controller.TempData["Erro"] as string ?? "");
    }
}