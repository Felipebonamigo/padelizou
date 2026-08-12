using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;

namespace Padelizou.Tests;

// A escolha do organizador — "a Central é da 1ª categoria" — do formulário até o sorteio.
// Os testes da REGRA em si estão em QuadraPreferidaDaCategoriaTests; aqui é o caminho: o que
// a tela manda, o que o banco guarda e o que a grade faz com isso.
public class QuadraPreferidaNoTorneioTests
{
    private const int TerceiraM = 3;
    private const int QuartaM = 4;

    private static Torneio TorneioValido() => new()
    {
        Nome = "Copa da Quadra Boa",
        ClubeId = 1,
        Status = "Inscrições Abertas",
        QuantidadeQuadras = 3,
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 6,
        RestricaoCategoria = "Livre",
        FormaPagamento = "Externo",
    };

    private static readonly string[] TresQuadras = { "Central", "Quadra 2", "Quadra 3" };

    private static DbPadelContext ContextoComCatalogo()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.AddRange(
            new padelizou.Models.CategoriaPadrao
            {
                Id = TerceiraM,
                Nome = "3ª Categoria Masculina",
                Codigo = "3CatM",
                Tipo = "Masculina",
            },
            new padelizou.Models.CategoriaPadrao
            {
                Id = QuartaM,
                Nome = "4ª Categoria Masculina",
                Codigo = "4CatM",
                Tipo = "Masculina",
            });
        ctx.SaveChanges();
        return ctx;
    }

    // ── Da tela pro banco ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Criacao_guarda_a_quadra_marcada_para_a_categoria()
    {
        using var ctx = ContextoComCatalogo();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        // "A 3ª joga na Central" — a Central é a quadra de POSIÇÃO 0 no formulário, porque
        // nem ela nem a categoria têm Id na hora em que a caixinha é marcada.
        await controller.Create(TorneioValido(), new[] { TerceiraM, QuartaM }, null, TresQuadras, null, null,
            quadrasPreferidas: new[] { $"{TerceiraM}:0" });

        var preferida = Assert.Single(await ctx.QuadrasDaCategoria
            .Include(q => q.Categoria).Include(q => q.Quadra).ToListAsync());

        Assert.Equal("3ª Categoria Masculina", preferida.Categoria.Nome);
        Assert.Equal("Central", preferida.Quadra.Nome);
    }

    [Fact]
    public async Task Sem_marcar_nada_o_torneio_nasce_sem_preferencia_nenhuma()
    {
        using var ctx = ContextoComCatalogo();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), new[] { TerceiraM }, null, TresQuadras, null, null);

        Assert.Empty(ctx.QuadrasDaCategoria);
    }

    // O valor vem do navegador: categoria que não foi marcada e quadra que não existe não
    // podem virar linha no banco — a grade obedeceria a elas no sorteio seguinte.
    [Fact]
    public async Task Par_que_nao_casa_com_nada_e_ignorado()
    {
        using var ctx = ContextoComCatalogo();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), new[] { TerceiraM }, null, TresQuadras, null, null,
            quadrasPreferidas: new[]
            {
                $"{QuartaM}:0",       // categoria que o organizador não marcou
                $"{TerceiraM}:9",     // quadra além das três que existem
                "isso:não",           // lixo
            });

        Assert.Empty(ctx.QuadrasDaCategoria);
    }

    // ── Editando depois ───────────────────────────────────────────────────────────────────

    private static async Task<Torneio> TorneioComPreferenciaAsync(DbPadelContext ctx)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);
        await controller.Create(TorneioValido(), new[] { TerceiraM, QuartaM }, null, TresQuadras, null, null,
            quadrasPreferidas: new[] { $"{TerceiraM}:0" });

        return await ctx.Torneios.FirstAsync();
    }

    private static Task EditarAsync(TorneiosController controller, Torneio torneio,
        string[]? quadrasPreferidas, bool informadas) =>
        controller.Editar(torneio.Id, torneio.Nome, null, torneio.DataInicio, torneio.PrecoInscricao,
            torneio.ClubeId, quantidadeQuadras: 3, nomesQuadras: TresQuadras,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: "Livre", capa: null,
            quadrasPreferidas: quadrasPreferidas, quadrasPreferidasInformadas: informadas);

    [Fact]
    public async Task A_gestao_troca_a_quadra_preferida()
    {
        using var ctx = ContextoComCatalogo();
        var torneio = await TorneioComPreferenciaAsync(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var quarta = await ctx.Categorias.FirstAsync(c => c.Nome.StartsWith("4"));

        // Agora a Central é da 4ª, e a 3ª não tem mais preferência nenhuma.
        await EditarAsync(controller, torneio, new[] { $"{quarta.Id}:0" }, informadas: true);

        var preferida = Assert.Single(await ctx.QuadrasDaCategoria
            .Include(q => q.Categoria).Include(q => q.Quadra).ToListAsync());

        Assert.Equal(quarta.Id, preferida.CategoriaId);
        Assert.Equal("Central", preferida.Quadra.Nome);
    }

    [Fact]
    public async Task Desmarcar_todas_na_gestao_limpa_a_preferencia()
    {
        using var ctx = ContextoComCatalogo();
        var torneio = await TorneioComPreferenciaAsync(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await EditarAsync(controller, torneio, quadrasPreferidas: null, informadas: true);

        Assert.Empty(ctx.QuadrasDaCategoria);
    }

    // A armadilha do `dataFimInformada`, de novo: caixa desmarcada não vai no POST, então uma
    // aba aberta antes deste deploy salva a gestão SEM o campo. Sem a marca, esse salvamento
    // apagaria a preferência inteira sem ninguém pedir.
    [Fact]
    public async Task Aba_antiga_salva_o_resto_sem_apagar_a_preferencia()
    {
        using var ctx = ContextoComCatalogo();
        var torneio = await TorneioComPreferenciaAsync(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await EditarAsync(controller, torneio, quadrasPreferidas: null, informadas: false);

        Assert.Single(ctx.QuadrasDaCategoria);
    }

    // ── Do banco pra grade ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_sorteio_os_jogos_da_categoria_dona_saem_na_quadra_dela()
    {
        using var ctx = ContextoComCatalogo();
        var (torneio, central, daCentral, aOutra) = MontarTorneioDeDuasCategorias(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        ctx.QuadrasDaCategoria.Add(new QuadraDaCategoria { CategoriaId = daCentral.Id, QuadraId = central.Id });
        await ctx.SaveChangesAsync();

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.NotEmpty(jogos);

        // Três duplas por categoria = três jogos de grupo em cada, e três rodadas: cabe um
        // jogo da dona na Central em cada uma delas.
        Assert.All(jogos.Where(j => j.CategoriaId == daCentral.Id),
            j => Assert.Equal("Central", j.NomeQuadra));
        Assert.All(jogos.Where(j => j.CategoriaId == aOutra.Id),
            j => Assert.NotEqual("Central", j.NomeQuadra));
    }

    [Fact]
    public async Task Sem_preferencia_o_sorteio_usa_as_tres_quadras_como_sempre()
    {
        using var ctx = ContextoComCatalogo();
        var (torneio, _, _, _) = MontarTorneioDeDuasCategorias(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();

        // Todo jogo com quadra, nenhuma quadra com dois jogos no mesmo horário, e a primeira
        // da lista em uso — que é a grade de sempre: primeira livre, na ordem da fila.
        //
        // ⚠️ Não são três quadras cheias: num grupo de 3, os jogos da MESMA categoria dividem
        // duplas, e ninguém joga duas vezes no mesmo horário. Com duas categorias, cada
        // horário comporta dois jogos — e a terceira quadra fica ociosa por falta de gente,
        // não por causa da preferência.
        Assert.All(jogos, j => Assert.False(string.IsNullOrEmpty(j.NomeQuadra)));
        Assert.Equal(jogos.Count, jogos.Select(j => (j.HorarioPrevisto, j.NomeQuadra)).Distinct().Count());
        Assert.Contains(jogos, j => j.NomeQuadra == "Central");
    }

    // Torneio pronto pro sorteio: 3 quadras, duas categorias de 3 duplas cada (um grupo de 3
    // em cada, três jogos por categoria).
    private static (Torneio torneio, Quadra central, Categoria daCentral, Categoria aOutra)
        MontarTorneioDeDuasCategorias(DbPadelContext ctx)
    {
        var torneio = new Torneio
        {
            Nome = "Copa da Quadra Boa",
            Codigo = "QBOA01",
            ClubeId = 1,
            Status = "Chaves em Sorteio",
            QuantidadeQuadras = 3,
            DataInicio = new DateTime(2026, 8, 15, 8, 0, 0),
            TempoPrevistoPartidaMinutos = 50,
        };
        ctx.Torneios.Add(torneio);

        var daCentral = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "3CatM", Torneio = torneio };
        var aOutra = new Categoria { Nome = "4ª Categoria Masculina", Codigo = "4CatM", Torneio = torneio };
        ctx.Categorias.AddRange(daCentral, aOutra);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = 1 });

        var quadras = TresQuadras.Select(nome => new Quadra { TorneioId = torneio.Id, Nome = nome }).ToList();
        ctx.Quadras.AddRange(quadras);

        int jogador = 0;
        foreach (var categoria in new[] { daCentral, aOutra })
        {
            for (int i = 0; i < 3; i++)
            {
                var j1 = TestInfra.NovoJogador(++jogador);
                var j2 = TestInfra.NovoJogador(++jogador);
                ctx.Jogadores.AddRange(j1, j2);
                ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 });
            }
        }
        ctx.SaveChanges();

        return (torneio, quadras[0], daCentral, aOutra);
    }
}
