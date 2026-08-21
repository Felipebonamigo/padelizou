using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O NOME DA QUADRA É A IDENTIDADE DELA (21/08/2026) — e não era.
//
// ⚠️ O QUE ESTAVA ERRADO: `Quadra.Nome` é texto livre e nenhuma das TRÊS portas que escrevem
// nome conferia nada — nem entre si, nem contra a lista do torneio, nem havia índice no banco.
// Dava pra ter duas "Quadra 1" no mesmo torneio, e dava pra grudar num jogo o nome de uma
// quadra que não existe.
//
// Isso importa porque o resto do sistema JÁ trata o nome como identidade: `Partida.NomeQuadra`
// é uma string, e é por ela que a grade sabe se a quadra está ocupada (GradeDeJogos.Encaixar),
// que o link de transmissão acha a quadra (PartidasController.aplicarLinkNaQuadra) e que o
// organizador troca um jogo de lugar (TrocaDeQuadra). Com nome repetido — ou com nome que não
// está na lista — os três agem sobre a quadra errada, EM SILÊNCIO, porque a string bate.
//
// As três portas: criar torneio, editar torneio (as duas em TorneiosController.Criacao) e o
// formulário do placar (PartidasController.ControlePlacar) — esta última era a mais silenciosa,
// e por ela a colisão voltava depois de todo o resto estar certo.
public class NomeDeQuadraUnicoTests
{
    // ---------- a régua, sozinha ----------

    [Fact]
    public void Lista_sem_repeticao_passa()
    {
        Assert.Null(NomeDeQuadraUnico.MotivoParaNaoSalvar(new[] { "Quadra 1", "Quadra 2", "Central" }));
    }

    [Fact]
    public void Nome_repetido_e_recusado_com_o_nome_no_recado()
    {
        var motivo = NomeDeQuadraUnico.MotivoParaNaoSalvar(new[] { "Quadra 1", "Quadra 2", "Quadra 1" });

        Assert.NotNull(motivo);
        // O organizador precisa saber QUAL das três está repetida — recado genérico manda ele
        // conferir uma a uma numa tela que pode ter oito quadras.
        Assert.Contains("Quadra 1", motivo);
    }

    [Theory]
    [InlineData("Quadra 1", "quadra 1")]      // maiúscula
    [InlineData("Quadra 1", "QUADRA 1")]
    [InlineData("Quadra 1", " Quadra 1 ")]    // espaço na ponta
    [InlineData("Central", "central ")]
    public void Duas_que_o_olho_le_igual_contam_como_repetidas(string a, string b)
    {
        // ⚠️ Deixar estas passar seria pior que não ter régua nenhuma: o organizador veria duas
        // linhas com aparência diferente e o sistema teria dois nomes que ele lê como um só.
        Assert.NotNull(NomeDeQuadraUnico.MotivoParaNaoSalvar(new[] { a, b }));
    }

    [Fact]
    public void Campo_vazio_nao_conta_como_repeticao()
    {
        // A tela manda oito campos e o organizador preenche três. Os vazios viram "Quadra D",
        // "Quadra E"… no chamador — se contassem como repetidos, ninguém salvaria nada.
        Assert.Null(NomeDeQuadraUnico.MotivoParaNaoSalvar(new[] { "Quadra 1", "", "  ", null, "Quadra 2" }));
    }

    // ---------- a terceira porta: o formulário do placar ----------

    private static async Task<(DbPadelContext ctx, Partida jogo, Jogador org)> ComQuadrasAsync(
        params string[] quadras)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        foreach (var nome in quadras)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nome });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        jogo.NomeQuadra = quadras.FirstOrDefault();
        await ctx.SaveChangesAsync();

        return (ctx, jogo, org);
    }

    [Fact]
    public async Task Quadra_da_lista_do_torneio_e_gravada()
    {
        var (ctx, jogo, org) = await ComQuadrasAsync("Quadra 1", "Quadra 2");
        using var _ = ctx;

        await TestInfra.NovoPartidasController(ctx, org.Id)
            .ControlePlacar(jogo.Id, "AoVivo", 3, 1, "Quadra 2", null);

        Assert.Equal("Quadra 2", (await ctx.Partidas.FindAsync(jogo.Id))!.NomeQuadra);
    }

    // ⚠️ O TESTE QUE PROVA O FURO. Sem a correção ele FALHA: o jogo sai daqui numa "Quadra 7"
    // que não existe — some da grade (que só conhece as cadastradas) e o organizador fica
    // sem conseguir arrumar, porque o seletor oferece a mesma lista que não tem esse nome.
    [Fact]
    public async Task Quadra_que_nao_e_do_torneio_nao_gruda_no_jogo()
    {
        var (ctx, jogo, org) = await ComQuadrasAsync("Quadra 1", "Quadra 2");
        using var _ = ctx;

        await TestInfra.NovoPartidasController(ctx, org.Id)
            .ControlePlacar(jogo.Id, "AoVivo", 3, 1, "Quadra 7", null);

        Assert.Equal("Quadra 1", (await ctx.Partidas.FindAsync(jogo.Id))!.NomeQuadra);
    }

    [Fact]
    public async Task Torneio_sem_quadra_cadastrada_continua_aceitando_o_que_for_digitado()
    {
        // A válvula: torneio antigo, ou criado sem listar quadra nenhuma, não pode ficar sem
        // conseguir escrever onde o jogo aconteceu. Recusar aqui seria trocar um bug por outro.
        var (ctx, jogo, org) = await ComQuadrasAsync();
        using var _ = ctx;

        await TestInfra.NovoPartidasController(ctx, org.Id)
            .ControlePlacar(jogo.Id, "AoVivo", 3, 1, "Quadra do vizinho", null);

        Assert.Equal("Quadra do vizinho", (await ctx.Partidas.FindAsync(jogo.Id))!.NomeQuadra);
    }

    [Fact]
    public async Task Deixar_em_branco_continua_tirando_a_quadra_do_jogo()
    {
        // O organizador que apaga o campo está dizendo "ainda não sei em qual" — e a régua nova
        // não pode transformar isso em "mantém a de antes".
        var (ctx, jogo, org) = await ComQuadrasAsync("Quadra 1", "Quadra 2");
        using var _ = ctx;

        await TestInfra.NovoPartidasController(ctx, org.Id)
            .ControlePlacar(jogo.Id, "AoVivo", 3, 1, null, null);

        Assert.Null((await ctx.Partidas.FindAsync(jogo.Id))!.NomeQuadra);
    }

    // ---------- a quarta trava, no banco ----------

    // ⚠️ ESTE TESTE GUARDA UMA PALAVRA. A constraint no banco tem que ser DEFERRABLE INITIALLY
    // DEFERRED, e não um `HasIndex().IsUnique()` normal, por causa da tela de editar torneio:
    // TorneiosController.Criacao renomeia as quadras UMA A UMA, num laço. Trocar "Quadra A" por
    // "Quadra B" e vice-versa — coisa que o organizador faz — passa por um instante em que as
    // duas se chamam "Quadra B".
    //
    // Medi isso num Postgres de verdade em 21/08/2026: com a constraint comum o swap ESTOURA no
    // meio do UPDATE, erro de banco na cara do usuário numa edição perfeitamente legítima. Com
    // DEFERRABLE, passa — a conferência acontece no COMMIT.
    //
    // O risco de perder isso é concreto: um `dotnet ef migrations add` posterior regenera o
    // arquivo VAZIO (o modelo do EF não conhece a constraint), e quem for "arrumar" tem toda a
    // chance de escrever o índice único comum.
    [Fact]
    public void A_constraint_no_banco_e_deferivel()
    {
        var migration = Path.Combine(PastaDoProjeto(), "Migrations",
            "20260821151631_NomeDeQuadraUnicoNoTorneio.cs");

        Assert.True(File.Exists(migration),
            "A migration do nome único de quadra sumiu — sem ela a trava fica só no C#, e o "
            + "banco volta a aceitar duas quadras com o mesmo nome.");

        var sql = File.ReadAllText(migration);

        Assert.Contains("UQ_Quadra_Torneio_Nome", sql);
        Assert.Contains("DEFERRABLE INITIALLY DEFERRED", sql);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Migrations");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }

    // ---------- as duas primeiras portas: criar e editar o torneio ----------

    // ⚠️ SÃO DUAS, E A SEGUNDA É SEMPRE A QUE FICA PRA TRÁS. A tela de criar e a de editar
    // gravam nomes de quadra pelo mesmo campo, em métodos diferentes do mesmo controller —
    // validar só uma deixa a porta aberta com aparência de fechada.
    [Fact]
    public async Task Editar_o_torneio_com_duas_quadras_de_mesmo_nome_e_recusado()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);

        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.Editar(
            id: torneio.Id, nome: "Torneio de Teste", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2,
            nomesQuadras: new[] { "Central", "central" },
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        Assert.Equal(
            new[] { "Quadra 1", "Quadra 2" },
            await ctx.Quadras.Where(q => q.TorneioId == torneio.Id)
                .OrderBy(q => q.Id).Select(q => q.Nome).ToArrayAsync());

        // O recado nomeia a quadra repetida — e nomeia a SEGUNDA grafia, que é o campo que o
        // organizador precisa mexer.
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Contains("central", (string)controller.TempData["Erro"]!);
    }

    [Fact]
    public async Task Editar_com_nomes_diferentes_grava_normalmente()
    {
        // A rede contra o exagero: a régua nova não pode atrapalhar quem só quer renomear.
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);

        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).Editar(
            id: torneio.Id, nome: "Torneio de Teste", localTorneio: null, dataInicio: null,
            precoInscricao: 0, clubeId: 0, quantidadeQuadras: 2,
            nomesQuadras: new[] { "Central", "Coberta" },
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null);

        Assert.Equal(
            new[] { "Central", "Coberta" },
            await ctx.Quadras.Where(q => q.TorneioId == torneio.Id)
                .OrderBy(q => q.Id).Select(q => q.Nome).ToArrayAsync());
    }

    [Fact]
    public async Task Criar_o_torneio_com_duas_quadras_de_mesmo_nome_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3, Nome = "3ª Categoria Masculina", Codigo = "3CatM", Tipo = "Masculina",
        });
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "Copa Teste",
            ClubeId = 1,
            Status = "Inscrições Abertas",
            SetsFaseGrupos = 1,
            GamesFaseGrupos = 6,
            RestricaoCategoria = "Livre",
            FormaPagamento = "Externo",
            QuantidadeQuadras = 2,
        };

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(torneio, new[] { 3 }, null, new[] { "Quadra 1", "quadra 1" }, null, null);

        // Recusa ANTES de gravar: torneio meio-criado por causa de nome repetido seria pior que
        // o nome repetido.
        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);
        Assert.Contains("mesmo nome", (string)view.ViewData["Erro"]!);
        Assert.Empty(ctx.Torneios);
    }
}
