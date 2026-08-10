using Microsoft.AspNetCore.Mvc;
using NSubstitute;
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
        string? q = null, int? categoriaId = null, string? estado = null, string? cidade = null, int? clubeId = null,
        int pagina = 1, int? logadoComo = null)
    {
        var controller = TestInfra.NovoJogadoresController(ctx, logadoComo);
        var result = (ViewResult)await controller.Buscar(q, categoriaId, estado, cidade, clubeId, pagina);
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
    public async Task Sem_filtro_nenhum_lista_todo_mundo_paginado()
    {
        // A regra era o contrário ("a tela abre convidando a filtrar") e mudou por decisão
        // do Felipe em 29/07/2026: quem abre a busca querendo "ver quem tem por aqui" não
        // deveria precisar adivinhar um nome primeiro. O que protege a tela e o banco de
        // "todo mundo" crescer é a paginação, não a recusa.
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana", "1", "Caxias do Sul", "RS");
        NovoJogador(ctx, "Beto", "2");

        var vm = await Buscar(ctx);

        Assert.False(vm.TemFiltro);
        Assert.Equal(2, vm.TotalEncontrado);
        Assert.Equal(2, vm.Resultados.Count);
    }

    [Fact]
    public async Task A_pagina_corta_em_30_e_a_segunda_traz_o_resto()
    {
        using var ctx = TestInfra.NovoContexto();
        for (int i = 1; i <= 35; i++)
        {
            NovoJogador(ctx, $"Jogador {i:00}", i.ToString());
        }

        var primeira = await Buscar(ctx);
        var segunda = await Buscar(ctx, pagina: 2);

        Assert.Equal(35, primeira.TotalEncontrado);
        Assert.Equal(2, primeira.TotalPaginas);
        Assert.Equal(30, primeira.Resultados.Count);
        Assert.Equal(5, segunda.Resultados.Count);

        // Ninguém aparece em duas páginas nem some entre elas.
        var todos = primeira.Resultados.Concat(segunda.Resultados).Select(r => r.Jogador.Id).ToList();
        Assert.Equal(35, todos.Distinct().Count());
    }

    [Fact]
    public async Task Pagina_fora_do_intervalo_vai_pra_mais_proxima_que_existe()
    {
        // Link velho de "página 9" continua funcionando depois que a base encolher.
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana", "1");

        var alem = await Buscar(ctx, pagina: 9);
        var aquem = await Buscar(ctx, pagina: -3);

        Assert.Equal(1, alem.Pagina);
        Assert.Single(alem.Resultados);
        Assert.Equal(1, aquem.Pagina);
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

    // ---------- SEGUIR SEM SAIR DA BUSCA ----------
    // Achar parceiro e seguir eram duas telas: a busca só levava ao perfil, e era lá que o
    // botão morava. Quem varria a lista tinha que entrar e voltar pessoa por pessoa.

    [Fact]
    public async Task Quem_eu_ja_sigo_vem_marcado_no_resultado()
    {
        // É o que separa o botão "Seguir" do "Seguindo" — sem isso o card ofereceria seguir
        // de novo quem eu já sigo, e o clique não faria nada.
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Eu Mesmo", "1");
        var seguido = NovoJogador(ctx, "Ja Sigo", "2");
        var estranho = NovoJogador(ctx, "Nao Sigo", "3");
        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = eu.Id, SeguidoId = seguido.Id });
        ctx.SaveChanges();

        var vm = await Buscar(ctx, logadoComo: eu.Id);

        Assert.Equal(eu.Id, vm.MeuId);
        Assert.True(vm.Resultados.Single(r => r.Jogador.Id == seguido.Id).EuSigo);
        Assert.False(vm.Resultados.Single(r => r.Jogador.Id == estranho.Id).EuSigo);
    }

    [Fact]
    public async Task Visitante_deslogado_nao_leva_botao_de_seguir()
    {
        // A tela é pública. Sem o `MeuId` nulo aqui, um `int.Parse` num claim inexistente
        // derrubaria a busca inteira pra quem chegou pelo link compartilhado.
        using var ctx = TestInfra.NovoContexto();
        NovoJogador(ctx, "Ana", "1");

        var vm = await Buscar(ctx);

        Assert.Null(vm.MeuId);
        Assert.All(vm.Resultados, r => Assert.False(r.EuSigo));
    }

    [Fact]
    public async Task Seguir_pela_busca_volta_pra_busca_com_os_filtros()
    {
        // O que a busca precisa preservar são os FILTROS: cair no perfil de quem foi seguido
        // apagaria o resultado que a pessoa montou, e ela teria que refazer a busca a cada
        // pessoa que seguisse.
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Eu Mesmo", "1");
        var alvo = NovoJogador(ctx, "Parceiro", "2");
        var controller = ControllerComUrlDeVerdade(ctx, eu.Id);

        var destino = "/Jogadores/Buscar?cidade=Gravata%C3%AD&pagina=2";
        var resultado = await controller.Seguir(alvo.Id, voltarParaRede: null, voltarPara: destino);

        Assert.Equal(destino, Assert.IsType<LocalRedirectResult>(resultado).Url);
        Assert.True(ctx.SeguidoresJogador.Any(s => s.SeguidorId == eu.Id && s.SeguidoId == alvo.Id));
    }

    [Fact]
    public async Task Destino_de_fora_do_site_e_ignorado_no_botao_de_seguir()
    {
        // ⚠️ O campo do formulário é editável por quem quiser: sem o IsLocalUrl, o botão de
        // seguir viraria trampolim pra jogar quem clica em site de fora.
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Eu Mesmo", "1");
        var alvo = NovoJogador(ctx, "Parceiro", "2");
        var controller = ControllerComUrlDeVerdade(ctx, eu.Id);

        var resultado = await controller.Seguir(alvo.Id, voltarParaRede: null, voltarPara: "https://site-de-fora.com");

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Perfil", redirect.ActionName);
    }

    [Fact]
    public async Task Deixar_de_seguir_pela_busca_tambem_volta_pra_busca()
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = NovoJogador(ctx, "Eu Mesmo", "1");
        var alvo = NovoJogador(ctx, "Parceiro", "2");
        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = eu.Id, SeguidoId = alvo.Id });
        ctx.SaveChanges();
        var controller = ControllerComUrlDeVerdade(ctx, eu.Id);

        var resultado = await controller.DeixarDeSeguir(alvo.Id, voltarParaRede: null, voltarPara: "/Jogadores/Buscar?q=Ana");

        Assert.Equal("/Jogadores/Buscar?q=Ana", Assert.IsType<LocalRedirectResult>(resultado).Url);
        Assert.False(ctx.SeguidoresJogador.Any(s => s.SeguidorId == eu.Id && s.SeguidoId == alvo.Id));
    }

    // UrlHelper de verdade, não dublê: quem decide se o destino é de fora é o IsLocalUrl
    // dele, e é exatamente isso que estes testes precisam exercitar.
    private static JogadoresController ControllerComUrlDeVerdade(DbPadelContext ctx, int logadoComo)
    {
        var controller = TestInfra.NovoJogadoresController(ctx, logadoComo);

        // O rota dublada é só pra não estourar: seguir alguém monta o link do aviso com
        // `Url.Action`, e o UrlHelper de verdade vai buscar o roteador na lista — vazia, ele
        // quebraria antes de chegar no redirecionamento, que é o que se mede aqui.
        var rotas = new Microsoft.AspNetCore.Routing.RouteData();
        rotas.Routers.Add(Substitute.For<Microsoft.AspNetCore.Routing.IRouter>());

        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(new ActionContext(
            controller.HttpContext, rotas, new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));
        return controller;
    }
}
