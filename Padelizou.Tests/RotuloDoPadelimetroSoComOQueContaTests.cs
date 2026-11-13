using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// 13/11/2026 — O RÓTULO DO PADELÍMETRO SÓ ANCORA EM CATEGORIA QUE CONTA (item 3 de PROXIMOS-PASSOS.md).
//
// 🗣️ Felipe, depois da análise: *"pode começar pelo item 1, 2, 3, 4"*.
//
// 🕳️ A ÂNCORA AINDA ENTRAVA POR OUTRA PORTA. Desde 09/11/2026 (#397) o rótulo de quem está em
// calibração é a categoria JOGADA — e a régua que escolhe essa categoria
// (`FaixasDePadelimetro.CategoriaDeReferencia`) só tirava mista e data futura. Americano, torneio
// restrito, lista de espera, inscrição sem parceiro e torneio cancelado contavam: um Americano numa
// categoria acima renomeava a pessoa sem ela ter jogado nada que mexe no número. E a mesma
// consulta estava escrita três vezes (ranking, perfil, card), uma de cada jeito.
//
// 🕳️ E QUEM SÓ JOGOU MISTA caía na escada MASCULINA no perfil e no card (o "faixa crua" de
// 14/09/2026 escolhia a escada pelo nome da categoria, e mista não diz nada), enquanto o ranking
// mostrava "—" pra mesma pessoa. Agora a escada sai do sexo do cadastro; sem sexo informado, fica
// a masculina, como sempre foi — e os três lugares dizem a mesma coisa.
//
// 🕳️ /Jogadores/Ranking?clubeId=1 dava 500 EM PRODUÇÃO (conferido no site): pedia a view
// "RankingPorClube", que nunca existiu. O ramo saiu; e um teste varre todos os controllers.
public class RotuloDoPadelimetroSoComOQueContaTests
{
    private const string Quinta = "5ª Categoria Masculina";   // a que ele joga de verdade
    private const string Terceira = "3ª Categoria Masculina"; // a que NÃO pode virar âncora

    public enum Porta { Americano, AmericanoDeDuplas, Restrito, Cancelado, ListaDeEspera, SemParceiro }

    // Um jogador em calibração (4 jogos, 520 PDZ) que joga a 5ª — e, MAIS RECENTE, uma inscrição
    // na 3ª que entrou pela porta errada.
    private static async Task<(DbPadelContext ctx, Jogador jogador)> MontarAsync(Porta porta)
    {
        var ctx = TestInfra.NovoContexto();
        var jogador = new Jogador { Nome = "Deivid Santos", Cpf = "77700000101", Padelimetro = 520, JogosDePadelimetro = 4 };
        var parceiro = new Jogador { Nome = "Parceiro", Cpf = "77700000102" };
        ctx.Jogadores.AddRange(jogador, parceiro);

        ctx.Duplas.Add(new Dupla { Categoria = Categoria(Quinta, DateTime.Today.AddDays(-30), 1), Jogador1 = jogador, Jogador2 = parceiro });

        var porOutraPorta = Categoria(Terceira, DateTime.Today.AddDays(-3), 2);
        var dupla = new Dupla { Categoria = porOutraPorta, Jogador1 = jogador, Jogador2 = parceiro };
        switch (porta)
        {
            case Porta.Americano: porOutraPorta.Torneio.Formato = FormatoDoTorneio.Americano; break;
            case Porta.AmericanoDeDuplas: porOutraPorta.Torneio.Formato = FormatoDoTorneio.AmericanoDeDuplas; break;
            case Porta.Restrito: porOutraPorta.Torneio.Restrito = true; break;
            case Porta.Cancelado: porOutraPorta.Torneio.Status = CancelamentoDoTorneio.Status; break;
            case Porta.ListaDeEspera: dupla.EmListaDeEspera = true; break;
            case Porta.SemParceiro: dupla.Jogador2 = null; break;
        }
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        return (ctx, jogador);
    }

    private static Categoria Categoria(string nome, DateTime data, int n) => new()
    {
        Nome = nome,
        Codigo = $"C{n:00}",
        Torneio = new Torneio { Nome = $"T{n}", Codigo = $"TRN{n:00}", Status = "Finalizado", DataInicio = data },
    };

    [Theory]
    [InlineData(Porta.Americano)]
    [InlineData(Porta.AmericanoDeDuplas)]
    [InlineData(Porta.Restrito)]
    [InlineData(Porta.Cancelado)]
    [InlineData(Porta.ListaDeEspera)]
    [InlineData(Porta.SemParceiro)]
    public async Task No_ranking_a_ancora_e_a_categoria_que_conta(Porta porta)
    {
        var (ctx, _) = await MontarAsync(porta);

        var lista = await new PadelimetroService(ctx).ListarRankingAsync(null);

        Assert.Equal("5ª", Assert.Single(lista).FaixaRotulo);
    }

    [Theory]
    [InlineData(Porta.Americano)]
    [InlineData(Porta.ListaDeEspera)]
    [InlineData(Porta.Cancelado)]
    public async Task No_perfil_a_ancora_e_a_categoria_que_conta(Porta porta)
    {
        var (ctx, jogador) = await MontarAsync(porta);
        var controller = TestInfra.NovoJogadoresController(ctx);

        Assert.IsType<ViewResult>(await controller.Perfil(jogador.Id, TestInfra.PortaDosDesafiosDe(ctx)));

        var faixa = Assert.IsType<FaixasDePadelimetro.Faixa>(controller.ViewBag.PadelimetroFaixa);
        Assert.Equal("5ª", faixa.Rotulo);
    }

    [Theory]
    [InlineData(Porta.Americano)]
    [InlineData(Porta.SemParceiro)]
    public async Task No_card_a_ancora_e_a_categoria_que_conta(Porta porta)
    {
        var (ctx, jogador) = await MontarAsync(porta);

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoCartoesController(ctx, null).Jogador(jogador.Id));

        Assert.Equal("5ª categoria", Assert.IsType<DadosDoCardDoJogador>(view.Model).Faixa);
    }

    // ── Quem só jogou mista ──────────────────────────────────────────────────────────────────

    private static async Task<(DbPadelContext ctx, Jogador jogadora)> SoMistaAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var jogadora = new Jogador
        {
            Nome = "Cristina Bassols", Cpf = "77700000201", Sexo = SexoDoJogador.Feminino,
            Padelimetro = 227, JogosDePadelimetro = 4,
        };
        var parceiro = new Jogador { Nome = "Parceiro", Cpf = "77700000202", Sexo = SexoDoJogador.Masculino };
        ctx.Jogadores.AddRange(jogadora, parceiro);
        ctx.Duplas.Add(new Dupla { Categoria = Categoria("Mista B", DateTime.Today.AddDays(-10), 1), Jogador1 = parceiro, Jogador2 = jogadora });
        await ctx.SaveChangesAsync();
        return (ctx, jogadora);
    }

    private static string DaEscadaDela => FaixasDePadelimetro.DoNivel(227, feminina: true).Rotulo;

    [Fact]
    public void O_teste_da_mista_separa_as_duas_escadas()
    {
        // Se 227 desse o mesmo rótulo nas duas escadas, os testes abaixo passariam sem provar nada.
        Assert.NotEqual(FaixasDePadelimetro.DoNivel(227, feminina: false).Rotulo, DaEscadaDela);
    }

    [Fact]
    public async Task Quem_so_jogou_mista_aparece_na_escada_do_proprio_sexo_no_perfil()
    {
        var (ctx, jogadora) = await SoMistaAsync();
        var controller = TestInfra.NovoJogadoresController(ctx);

        await controller.Perfil(jogadora.Id, TestInfra.PortaDosDesafiosDe(ctx));

        var faixa = Assert.IsType<FaixasDePadelimetro.Faixa>(controller.ViewBag.PadelimetroFaixa);
        Assert.Equal(DaEscadaDela, faixa.Rotulo);
    }

    [Fact]
    public async Task Quem_so_jogou_mista_aparece_na_escada_do_proprio_sexo_no_card()
    {
        var (ctx, jogadora) = await SoMistaAsync();

        var view = Assert.IsType<ViewResult>(await TestInfra.NovoCartoesController(ctx, null).Jogador(jogadora.Id));

        Assert.Equal($"{DaEscadaDela} categoria", Assert.IsType<DadosDoCardDoJogador>(view.Model).Faixa);
    }

    [Fact]
    public async Task Quem_so_jogou_mista_aparece_com_o_mesmo_rotulo_no_ranking()
    {
        var (ctx, _) = await SoMistaAsync();

        var lista = await new PadelimetroService(ctx).ListarRankingAsync(null);

        Assert.Equal(DaEscadaDela, Assert.Single(lista).FaixaRotulo);
    }

    // ── O 500 do ?clubeId= ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_ranking_com_clubeId_na_url_abre_o_ranking_e_nao_uma_tela_que_nao_existe()
    {
        using var ctx = TestInfra.NovoContexto();
        var controller = TestInfra.NovoJogadoresController(ctx);
        var americano = NSubstitute.Substitute.For<IRankingAmericanoService>();
        NSubstitute.SubstituteExtensions.Returns(
            americano.ListarAsync(NSubstitute.Arg.Any<HashSet<int>?>(), NSubstitute.Arg.Any<DateTime?>(), NSubstitute.Arg.Any<DateTime?>()),
            new RankingAmericanoVM(new(), new()));

        var view = Assert.IsType<ViewResult>(await controller.Ranking(
            clubeId: 1, torneioId: null, cidade: null, estado: null, periodo: null,
            hubDoRanking: new HubDoRanking(
                ctx, new EstatisticasService(ctx), new PadelimetroService(ctx), americano,
                TestInfra.PortaDosDesafiosDe(ctx), new TelaDoRankingDeDesafios(ctx))));

        Assert.NotEqual("RankingPorClube", view.ViewName);
        Assert.IsType<RankingHubVM>(view.Model);
    }

    [Fact]
    public void Toda_tela_pedida_pelo_nome_num_controller_existe()
    {
        // A classe de defeito do ?clubeId=: `View("Nome")` com uma tela que não existe compila,
        // passa em todo teste que não chama aquele ramo, e só estoura no navegador de alguém.
        var raiz = Raiz();
        var faltando = new List<string>();
        foreach (var arquivo in Directory.GetFiles(Path.Combine(raiz, "Padelizou", "Controllers"), "*.cs"))
        {
            var fonte = File.ReadAllText(arquivo);
            var classe = System.Text.RegularExpressions.Regex.Match(fonte, @"class\s+(\w+)Controller\b");
            if (!classe.Success) continue;
            var pasta = classe.Groups[1].Value;

            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(fonte, @"\bView\(""([A-Za-z_]+)"""))
            {
                var nome = m.Groups[1].Value;
                bool existe = File.Exists(Path.Combine(raiz, "Padelizou", "Views", pasta, nome + ".cshtml"))
                              || File.Exists(Path.Combine(raiz, "Padelizou", "Views", "Shared", nome + ".cshtml"));
                if (!existe) faltando.Add($"{Path.GetFileName(arquivo)}: View(\"{nome}\")");
            }
        }

        Assert.True(faltando.Count == 0, "Telas pedidas que não existem: " + string.Join("; ", faltando));
    }

    // ── A consulta da âncora vira SQL de verdade ─────────────────────────────────────────────

    [Fact]
    public void A_consulta_da_ancora_traduz_no_Postgres()
    {
        // EF InMemory não valida SQL (CLAUDE.md). Navegação Dupla → Categoria → Torneio filtrada por
        // campos dos três níveis: confere contra o provedor de verdade, apontado pra lugar nenhum.
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);
        var ids = new[] { 1, 2, 3 };

        var sql = PadelimetroService.InscricoesQueAncoramORotulo(ctx.Duplas)
            .Where(d => ids.Contains(d.Jogador1Id) || (d.Jogador2Id != null && ids.Contains(d.Jogador2Id.Value)))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.Categoria.Nome, d.Categoria.Torneio.DataInicio })
            .ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório.");
    }
}
