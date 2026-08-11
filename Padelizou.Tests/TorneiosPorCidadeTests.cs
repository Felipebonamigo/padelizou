using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "Torneios de padel em Porto Alegre" — a página que responde à busca de quem ainda não
// conhece o site. O que estes testes seguram é o que não dá pra ver olhando a tela: uma
// cidade que some da lista, um torneio escondido que reaparece, ou uma página nascendo vazia.
public class TorneiosPorCidadeTests
{
    // ── O endereço ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Porto Alegre", "RS", "porto-alegre-rs")]
    [InlineData("porto alegre", "rs", "porto-alegre-rs")]
    [InlineData("GRAVATAÍ", "RS", "gravatai-rs")]
    [InlineData("São José dos Campos", "SP", "sao-jose-dos-campos-sp")]
    [InlineData("Brusque", null, "brusque")]
    public void O_endereco_da_cidade_nao_depende_de_como_ela_foi_digitada(string nome, string? uf, string esperado)
    {
        Assert.Equal(esperado, TorneiosPorCidade.SlugDe(nome, uf));
    }

    // "São Francisco" existe em meia dúzia de estados. Sem a UF no endereço, dois lugares
    // diferentes disputariam a mesma página e um deles simplesmente não abriria.
    [Fact]
    public void Cidades_homonimas_de_estados_diferentes_nao_dividem_o_mesmo_endereco()
    {
        Assert.NotEqual(TorneiosPorCidade.SlugDe("São Francisco", "RS"),
                        TorneiosPorCidade.SlugDe("São Francisco", "SP"));
    }

    // ── O nome que vai pro título ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("porto alegre", "Porto Alegre")]
    [InlineData("GRAVATAI", "Gravatai")]
    [InlineData("Gravataí", "Gravataí")]
    [InlineData("são josé dos campos", "São José dos Campos")]
    [InlineData("bento gonçalves", "Bento Gonçalves")]
    public void A_cidade_vai_pro_titulo_escrita_como_gente(string cadastrada, string esperado)
    {
        Assert.Equal(esperado, NomeDeCidade.ComoTitulo(cadastrada));
    }

    // O catálogo de produção tem "porto alegre" em minúsculo, e MelhorGrafia só escolhe entre
    // as grafias que existem. Sem a passada de título, era isso que o Google mostraria.
    [Fact]
    public async Task A_cidade_cadastrada_em_minusculo_nao_sai_minuscula_na_pagina()
    {
        var ctx = Cenario();
        var lugares = await TorneiosPorCidade.ListarAsync(ctx);

        Assert.Contains(lugares, l => l.Nome == "Porto Alegre");
        Assert.DoesNotContain(lugares, l => l.Nome == "porto alegre");
    }

    // ── Quem entra na lista ────────────────────────────────────────────────────────────────

    private const int PortoAlegre = 1, BentoGoncalves = 2, Curitiba = 3, PortoAlegreGritado = 4;

    // "porto alegre" e "PORTO ALEGRE" são duas linhas do catálogo e uma cidade só.
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Cidades.AddRange(
            new Cidade { Id = PortoAlegre, Nome = "porto alegre", Estado = "RS" },
            new Cidade { Id = BentoGoncalves, Nome = "Bento Gonçalves", Estado = "RS" },
            new Cidade { Id = Curitiba, Nome = "Curitiba", Estado = "PR" },
            new Cidade { Id = PortoAlegreGritado, Nome = "PORTO ALEGRE", Estado = "RS" });

        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Arena Beira Rio", CidadeId = PortoAlegre },
            new Clube { Id = 2, Nome = "Arena Bento", CidadeId = BentoGoncalves },
            new Clube { Id = 3, Nome = "Arena Curitiba", CidadeId = Curitiba },
            new Clube { Id = 4, Nome = "Outra Arena de POA", CidadeId = PortoAlegreGritado },
            new Clube { Id = 5, Nome = "Clube sem cidade", CidadeId = null });

        // Porto Alegre: 2 clubes, 2 grafias, 3 torneios no total.
        ctx.Torneios.AddRange(
            Torneio(1, clube: 1), Torneio(2, clube: 1), Torneio(3, clube: 4),
            Torneio(4, clube: 2),
            Torneio(5, clube: 5));

        ctx.SaveChanges();
        return ctx;
    }

    private static Torneio Torneio(int id, int clube, bool oculto = false, bool aprovado = true,
        string status = "Inscrições Abertas", DateTime? quando = null) => new()
    {
        Id = id,
        Nome = $"Torneio {id}",
        Codigo = $"T{id}",
        ClubeId = clube,
        Oculto = oculto,
        AprovadoEm = aprovado ? new DateTime(2026, 8, 1) : null,
        Status = status,
        DataInicio = quando ?? new DateTime(2026, 12, 1),
    };

    [Fact]
    public async Task As_duas_grafias_da_mesma_cidade_somam_numa_linha_so()
    {
        var lugares = await TorneiosPorCidade.ListarAsync(Cenario());

        var poa = Assert.Single(lugares, l => l.Slug == "porto-alegre-rs");
        // 2 pelo Arena Beira Rio + 1 pela outra grafia. Agrupar DEPOIS de contar mostraria
        // dois lugares, nenhum com o número certo.
        Assert.Equal(3, poa.Torneios);
    }

    [Fact]
    public async Task Cidade_sem_torneio_nenhum_nao_vira_pagina()
    {
        var lugares = await TorneiosPorCidade.ListarAsync(Cenario());

        // Curitiba tem clube cadastrado, mas nenhum torneio. Página prometendo torneios e
        // entregando nada é pior que página nenhuma.
        Assert.DoesNotContain(lugares, l => l.Slug == "curitiba-pr");
    }

    [Fact]
    public async Task Torneio_de_clube_sem_cidade_nao_inventa_uma()
    {
        var lugares = await TorneiosPorCidade.ListarAsync(Cenario());

        Assert.Equal(2, lugares.Count); // só Porto Alegre e Bento Gonçalves
        Assert.Equal(4, lugares.Sum(l => l.Torneios)); // o torneio 5 ficou de fora
    }

    // A mesma régua que tira o torneio da vitrine tem que tirá-lo daqui. Senão o organizador
    // esconde o torneio e ele continua listado numa página que o Google indexou.
    [Theory]
    [InlineData(true, true, "Inscrições Abertas")]   // oculto
    [InlineData(false, false, "Inscrições Abertas")] // sem aprovação
    [InlineData(false, true, "Cancelado")]           // cancelado
    public async Task Torneio_que_o_publico_nao_ve_nao_conta_pra_cidade(bool oculto, bool aprovado, string status)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Porto Alegre", Estado = "RS" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena", CidadeId = 1 });
        ctx.Torneios.Add(Torneio(1, clube: 1, oculto: oculto, aprovado: aprovado, status: status));
        await ctx.SaveChangesAsync();

        Assert.Empty(await TorneiosPorCidade.ListarAsync(ctx));
    }

    [Fact]
    public async Task A_cidade_com_mais_torneios_vem_primeiro()
    {
        var lugares = await TorneiosPorCidade.ListarAsync(Cenario());

        Assert.Equal("porto-alegre-rs", lugares.First().Slug);
    }

    // ── Abrir a página ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pagina_traz_os_torneios_de_todas_as_grafias_da_cidade()
    {
        var pagina = await TorneiosPorCidade.AbrirAsync(Cenario(), "porto-alegre-rs");

        Assert.NotNull(pagina);
        Assert.Equal(3, pagina.Value.Torneios.Count);
        Assert.Equal("Porto Alegre", pagina.Value.Lugar.Nome);
    }

    [Theory]
    [InlineData("curitiba-pr")]      // cidade existe, torneio não
    [InlineData("cidade-inventada")] // não existe
    [InlineData("")]
    [InlineData(null)]
    public async Task Endereco_que_nao_leva_a_torneio_nenhum_devolve_nada(string? slug)
    {
        Assert.Null(await TorneiosPorCidade.AbrirAsync(Cenario(), slug));
    }

    [Fact]
    public async Task O_torneio_sem_data_marcada_vai_pro_fim_e_nao_encabeca_a_lista()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Porto Alegre", Estado = "RS" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena", CidadeId = 1 });
        var semData = Torneio(1, clube: 1);
        semData.DataInicio = null;
        ctx.Torneios.AddRange(semData, Torneio(2, clube: 1, quando: new DateTime(2026, 12, 20)));
        await ctx.SaveChangesAsync();

        var pagina = await TorneiosPorCidade.AbrirAsync(ctx, "porto-alegre-rs");

        Assert.Equal(2, pagina!.Value.Torneios[0].Id);
        Assert.Equal(1, pagina.Value.Torneios[1].Id);
    }

    // ── O controller e o sitemap ───────────────────────────────────────────────────────────

    // 200 com tela vazia ensinaria o buscador a indexar endereço que não leva a nada — por
    // isso a action responde 404, e não uma página dizendo "nenhum torneio por aqui".
    [Theory]
    [InlineData("curitiba-pr")]      // cidade cadastrada, sem torneio
    [InlineData("cidade-inventada")]
    public async Task A_action_responde_404_em_cidade_sem_torneio(string slug)
    {
        var controller = TestInfra.NovoTorneiosController(Cenario(), usuarioLogadoId: 0);

        Assert.IsType<NotFoundResult>(await controller.Cidade(slug));
    }

    [Fact]
    public async Task A_action_abre_a_cidade_que_tem_torneio()
    {
        var controller = TestInfra.NovoTorneiosController(Cenario(), usuarioLogadoId: 0);

        var resposta = Assert.IsType<ViewResult>(await controller.Cidade("porto-alegre-rs"));
        var torneios = Assert.IsType<List<Torneio>>(resposta.Model);

        Assert.Equal(3, torneios.Count);
        var lugar = Assert.IsType<TorneiosPorCidade.Lugar>(resposta.ViewData["Lugar"]);
        Assert.Equal("Porto Alegre", lugar.Nome);
    }

    [Fact]
    public async Task O_sitemap_lista_as_paginas_de_cidade()
    {
        var ctx = Cenario();
        var c = new SitemapController(ctx);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.ControllerContext.HttpContext.Request.Scheme = "https";
        c.ControllerContext.HttpContext.Request.Host = new HostString("padelizou.com.br");

        var xml = Assert.IsType<ContentResult>(await c.Index()).Content!;

        Assert.Contains("https://padelizou.com.br/torneios-de-padel-em-porto-alegre-rs", xml);
        Assert.Contains("https://padelizou.com.br/torneios-de-padel-em-bento-goncalves-rs", xml);
        Assert.DoesNotContain("curitiba", xml);
    }

    // A página de cidade é conteúdo público — se ela nascesse fora do índice, todo o trabalho
    // não serviria pra nada, e nada na tela denunciaria isso.
    [Fact]
    public void A_pagina_de_cidade_e_indexavel()
    {
        Assert.False(MetaDaBusca.ForaDoIndice("Torneios", "Cidade"));
    }
}
