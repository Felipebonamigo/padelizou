using Microsoft.AspNetCore.Mvc;
using padelizou.Models;   // namespace legado (minúsculo): CategoriaPadrao
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
// JogadoresController não tem namespace (fica no global) — daí não haver using pra ele.

namespace Padelizou.Tests;

// Busca de jogadores com filtros combináveis (nome, categoria, cidade/estado, clube).
public class BuscaJogadoresTests
{
    private static async Task<BuscaJogadoresVM> Buscar(DbPadelContext ctx,
        string? q = null, int? categoriaId = null, string? estado = null, string? cidade = null, int? clubeId = null)
    {
        var controller = new JogadoresController(ctx, new EstatisticasService(ctx));
        var result = (ViewResult)await controller.Buscar(q, categoriaId, estado, cidade, clubeId);
        return (BuscaJogadoresVM)result.Model!;
    }

    private static Jogador NovoJogador(DbPadelContext ctx, string nome, string cpf, string? cidade = null, string? estado = null)
    {
        var j = new Jogador { Nome = nome, Cpf = cpf, Cidade = cidade, Estado = estado };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    [Fact]
    public async Task Sem_filtro_nenhum_nao_lista_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana", "1", "Caxias do Sul", "RS");

        var vm = await Buscar(ctx);

        // A tela abre convidando a filtrar, não despejando o banco inteiro.
        Assert.False(vm.TemFiltro);
        Assert.Empty(vm.Resultados);
    }

    [Fact]
    public async Task Filtra_por_cidade_e_estado()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana Caxias", "1", "Caxias do Sul", "RS");
        NovoJogador(ctx, "Bruno Poa", "2", "Porto Alegre", "RS");
        NovoJogador(ctx, "Carla SP", "3", "Campinas", "SP");

        var porEstado = await Buscar(ctx, estado: "rs");           // minúsculo de propósito
        Assert.Equal(2, porEstado.Resultados.Count);

        var porCidade = await Buscar(ctx, cidade: "caxias do sul"); // idem
        Assert.Single(porCidade.Resultados);
        Assert.Equal("Ana Caxias", porCidade.Resultados[0].Jogador.Nome);
    }

    [Fact]
    public async Task Filtra_por_categoria_e_inclui_quem_nao_declarou_preferencia()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.CategoriasPadrao.AddRange(
            new CategoriaPadrao { Nome = "2ª Masculina", Codigo = "2M", Tipo = "Masculina" },
            new CategoriaPadrao { Nome = "4ª Masculina", Codigo = "4M", Tipo = "Masculina" });
        ctx.SaveChanges();
        var segunda = ctx.CategoriasPadrao.First(c => c.Codigo == "2M");
        var quarta = ctx.CategoriasPadrao.First(c => c.Codigo == "4M");

        var daSegunda = NovoJogador(ctx, "Joga na 2a", "1");
        var daQuarta = NovoJogador(ctx, "Joga na 4a", "2");
        var semDeclarar = NovoJogador(ctx, "Nao declarou", "3");

        ctx.JogadorCategorias.Add(new JogadorCategoria { JogadorId = daSegunda.Id, CategoriaPadraoId = segunda.Id });
        ctx.JogadorCategorias.Add(new JogadorCategoria { JogadorId = daQuarta.Id, CategoriaPadraoId = quarta.Id });
        ctx.SaveChanges();

        var vm = await Buscar(ctx, categoriaId: segunda.Id);
        var nomes = vm.Resultados.Select(r => r.Jogador.Nome).ToList();

        Assert.Contains("Joga na 2a", nomes);
        // "Sem linha" significa "aceita qualquer categoria" (ver JogadorCategoria) —
        // esconder essa gente esvaziaria a busca, já que quase ninguém preenche.
        Assert.Contains("Nao declarou", nomes);
        Assert.DoesNotContain("Joga na 4a", nomes);

        // Quem declarou vem primeiro e com selo; quem só "não disse nada", depois.
        Assert.Equal("Joga na 2a", vm.Resultados[0].Jogador.Nome);
        Assert.True(vm.Resultados[0].Declarou);
        Assert.False(vm.Resultados.Single(r => r.Jogador.Nome == "Nao declarou").Declarou);
        Assert.Equal(1, vm.QtdDeclarou);
        Assert.True(vm.FiltraPreferencia);
    }

    [Fact]
    public async Task Busca_so_por_nome_nao_marca_ninguem_como_declarado()
    {
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana", "1", "Caxias do Sul", "RS");

        var vm = await Buscar(ctx, q: "Ana");

        // Sem filtro de categoria/clube não existe "combina" — o selo perderia sentido.
        Assert.False(vm.FiltraPreferencia);
        Assert.Equal(0, vm.QtdDeclarou);
        Assert.False(vm.Resultados.Single().Declarou);
    }

    [Fact]
    public async Task Filtra_por_clube()
    {
        using var ctx = TestInfra.NovoContexto();
        var arena = new Clube { Nome = "Arena Beira Rio", Contato = "x", Endereco = "y" };
        var garden = new Clube { Nome = "Padel Garden", Contato = "x", Endereco = "y" };
        ctx.Clubes.AddRange(arena, garden);
        ctx.SaveChanges();

        var doArena = NovoJogador(ctx, "Do Arena", "1");
        var doGarden = NovoJogador(ctx, "Do Garden", "2");
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = doArena.Id, ClubeId = arena.Id });
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = doGarden.Id, ClubeId = garden.Id });
        ctx.SaveChanges();

        var vm = await Buscar(ctx, clubeId: arena.Id);
        var nomes = vm.Resultados.Select(r => r.Jogador.Nome).ToList();

        Assert.Contains("Do Arena", nomes);
        Assert.DoesNotContain("Do Garden", nomes);
    }

    [Fact]
    public async Task Filtros_se_combinam_e_estreitam_o_resultado()
    {
        using var ctx = TestInfra.NovoContexto();
        var clube = new Clube { Nome = "Arena", Contato = "x", Endereco = "y" };
        ctx.Clubes.Add(clube);
        ctx.SaveChanges();

        var alvo = NovoJogador(ctx, "Marina Alvo", "1", "Caxias do Sul", "RS");
        var mesmaCidadeOutroClube = NovoJogador(ctx, "Outro Clube", "2", "Caxias do Sul", "RS");
        NovoJogador(ctx, "Marina Longe", "3", "Porto Alegre", "RS");

        var outroClube = new Clube { Nome = "Garden", Contato = "x", Endereco = "y" };
        ctx.Clubes.Add(outroClube);
        ctx.SaveChanges();
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = alvo.Id, ClubeId = clube.Id });
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = mesmaCidadeOutroClube.Id, ClubeId = outroClube.Id });
        ctx.SaveChanges();

        // Nome + cidade + clube ao mesmo tempo devem sobrar só o alvo.
        var vm = await Buscar(ctx, q: "Marina", cidade: "Caxias do Sul", clubeId: clube.Id);

        Assert.Single(vm.Resultados);
        Assert.Equal("Marina Alvo", vm.Resultados[0].Jogador.Nome);
    }

    [Fact]
    public async Task Resultado_traz_pontos_reais_time_e_preferencias()
    {
        using var ctx = TestInfra.NovoContexto();
        var time = new Time { Nome = "Nata Padel" };
        ctx.Times.Add(time);
        ctx.CategoriasPadrao.Add(new CategoriaPadrao { Nome = "2ª Masculina", Codigo = "2M", Tipo = "Masculina" });
        ctx.SaveChanges();

        var j = NovoJogador(ctx, "Craque", "1", "Caxias do Sul", "RS");
        var parceiro = NovoJogador(ctx, "Parceiro", "2");
        j.TimeId = time.Id;
        ctx.JogadorCategorias.Add(new JogadorCategoria
        {
            JogadorId = j.Id,
            CategoriaPadraoId = ctx.CategoriasPadrao.First().Id,
        });

        var torneio = new Torneio { Nome = "T", Codigo = "T", DataInicio = DateTime.Now };
        var cat = new Categoria { Nome = "2ª Masculina", Codigo = "TC", Torneio = torneio };
        ctx.Torneios.Add(torneio);
        ctx.Categorias.Add(cat);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = j.Id, Jogador2Id = parceiro.Id, UltimaFase = "Campeao" });
        ctx.SaveChanges();

        var vm = await Buscar(ctx, q: "Craque");
        var achado = vm.Resultados.Single();

        Assert.Equal(100, achado.Pontos);          // campeão = 100
        Assert.Equal("Nata Padel", achado.Time);
        Assert.Contains("2ª Masculina", achado.Categorias);
    }
}
