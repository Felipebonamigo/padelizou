using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O que o Google vê do site. Nada disto aparece numa tela, e é exatamente por isso que precisa
// de teste: um erro aqui não quebra nada, não aparece no log e não estoura no CI — ele só
// deixa de trazer gente, e a gente descobre semanas depois procurando "padelizou" na busca.
public class BuscaNoGoogleTests
{
    // ── A régua de quem entra no índice ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Auth", "Login")]
    [InlineData("Auth", "Cadastro")]
    [InlineData("Notificacoes", "Index")]
    [InlineData("Pagamentos", "Meus")]
    [InlineData("Admin", "Index")]
    public void Tela_que_nao_serve_pra_busca_fica_fora_do_indice(string controller, string action)
    {
        Assert.True(MetaDaBusca.ForaDoIndice(controller, action));
    }

    [Theory]
    [InlineData("Home", "Index")]
    [InlineData("Torneios", "Index")]
    [InlineData("Torneios", "Details")]
    [InlineData("Jogadors", "Ranking")]
    [InlineData("Professores", "Index")]
    public void O_conteudo_publico_continua_indexavel(string controller, string action)
    {
        Assert.False(MetaDaBusca.ForaDoIndice(controller, action));
    }

    // O ponto da allowlist: a tela que ninguém cadastrou nasce FORA do índice. Se um dia isso
    // virar uma lista de exclusões, a tela privada nova passa a nascer indexável — e ninguém
    // descobre por um teste vermelho, descobre pelo Google.
    [Fact]
    public void Tela_nova_que_ninguem_cadastrou_nasce_fora_do_indice()
    {
        Assert.True(MetaDaBusca.ForaDoIndice("ModuloQueAindaNaoExiste", "Index"));
    }

    [Fact]
    public void Cada_secao_publica_tem_frase_propria_e_nenhuma_repete_a_generica()
    {
        var secoes = new[]
        {
            ("Torneios", "Index"), ("Torneios", "Details"),
            ("Jogadors", "Ranking"), ("Professores", "Index"),
        };

        foreach (var (controller, action) in secoes)
        {
            var frase = MetaDaBusca.Descricao(controller, action);
            Assert.NotEqual(MetaDaBusca.DescricaoDoSite, frase);
            // O Google corta o que passa de ~160; frase mais longa aparece truncada na busca.
            Assert.True(frase.Length <= 160, $"{controller}/{action} tem {frase.Length} caracteres");
        }
    }

    [Fact]
    public void Tela_sem_frase_propria_cai_na_descricao_do_site()
    {
        Assert.Equal(MetaDaBusca.DescricaoDoSite, MetaDaBusca.Descricao("Auth", "Login"));
    }

    // ── Os dois endereços do site ──────────────────────────────────────────────────────────

    // O site atende em padelizou.com.br E www.padelizou.com.br, idêntico, sem redirecionar.
    // Servido pelo www, o canônico apontava pro PRÓPRIO www — confirmando os dois endereços
    // como legítimos, que é o contrário do que ele existe pra fazer.
    [Fact]
    public void Servido_pelo_www_o_canonico_aponta_pra_raiz()
    {
        Assert.Equal("padelizou.com.br", MetaDaBusca.HostCanonico("www.padelizou.com.br"));
        Assert.Equal("padelizou.com.br", MetaDaBusca.HostCanonico("WWW.Padelizou.com.br"));
    }

    // Canônico de desenvolvimento apontando pra produção mandaria o buscador indexar outra
    // página — e nada na tela denunciaria isso.
    [Theory]
    [InlineData("padelizou.com.br")]
    [InlineData("localhost:5038")]
    [InlineData("dev.padelizou.com.br")]
    public void Os_outros_enderecos_continuam_apontando_pra_si_mesmos(string host)
    {
        Assert.Equal(host, MetaDaBusca.HostCanonico(host));
    }

    // ── O corte da frase ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_frase_longa_e_cortada_no_espaco_e_nao_no_meio_da_palavra()
    {
        var cortada = MetaDaBusca.Encurtar(new string('a', 100) + " palavraquenaocabe", 110);

        Assert.EndsWith("…", cortada);
        Assert.DoesNotContain("palavraquenaocabe", cortada);
    }

    // O resumo do torneio vem de <textarea>, então chega com \n cru — e \n dentro do atributo
    // de uma <meta> vira uma descrição partida no meio.
    [Fact]
    public void Quebra_de_linha_e_espaco_repetido_viram_um_espaco_so()
    {
        Assert.Equal("Torneio de padel em Porto Alegre",
            MetaDaBusca.Encurtar("Torneio de padel\n\nem   Porto Alegre"));
    }

    [Fact]
    public void Frase_curta_passa_intacta()
    {
        Assert.Equal("Torneio de padel", MetaDaBusca.Encurtar("Torneio de padel"));
    }

    // ── A ficha de evento (JSON-LD) ────────────────────────────────────────────────────────

    private static Torneio TorneioComData() => new()
    {
        Id = 7,
        Nome = "NATA PADEL TOUR",
        Codigo = "NATA",
        Status = "Inscrições Abertas",
        DataInicio = new DateTime(2026, 12, 5, 19, 0, 0),
        PrecoInscricao = 120m,
    };

    [Fact]
    public void Torneio_sem_data_marcada_nao_gera_ficha_de_evento()
    {
        var torneio = TorneioComData();
        torneio.DataInicio = null;

        // "startDate" é obrigatório pro Google: ficha incompleta não vira resultado rico, vira
        // aviso de erro no Search Console. Melhor não afirmar nada.
        Assert.Null(DadosEstruturados.Torneio(torneio, null, "https://padelizou.com.br/Torneios/Details/7", null, "x"));
    }

    [Fact]
    public void A_ficha_do_evento_leva_nome_data_e_valor_da_inscricao()
    {
        var ficha = DadosEstruturados.Torneio(TorneioComData(), null,
            "https://padelizou.com.br/Torneios/Details/7", null, "Torneio de padel");

        Assert.NotNull(ficha);
        Assert.Contains("\"SportsEvent\"", ficha);
        Assert.Contains("NATA PADEL TOUR", ficha);
        Assert.Contains("\"120\"", ficha);
        Assert.Contains("BRL", ficha);
    }

    // O app inteiro fala no horário de Brasília. Carimbar "Z" aqui empurraria todo torneio
    // três horas pra frente na ficha que o buscador mostra — e ninguém confere isso.
    [Fact]
    public void A_data_da_ficha_sai_sem_fuso_carimbado()
    {
        var ficha = DadosEstruturados.Torneio(TorneioComData(), null,
            "https://padelizou.com.br/Torneios/Details/7", null, "x")!;

        Assert.Contains("\"startDate\":\"2026-12-05T19:00:00\"", ficha);
        Assert.DoesNotContain("2026-12-05T19:00:00Z", ficha);
    }

    // Boa parte dos torneios é cadastrada só com o dia. Aí o T00:00:00 não é a hora do
    // torneio, é a ausência dela — e o buscador mostraria "meia-noite" na ficha.
    [Fact]
    public void Torneio_sem_hora_marcada_sai_com_a_data_pura()
    {
        var torneio = TorneioComData();
        torneio.DataInicio = new DateTime(2026, 12, 5);
        torneio.DataFim = new DateTime(2026, 12, 7);

        var ficha = DadosEstruturados.Torneio(torneio, null, "https://padelizou.com.br/x", null, "x")!;

        Assert.Contains("\"startDate\":\"2026-12-05\"", ficha);
        Assert.Contains("\"endDate\":\"2026-12-07\"", ficha);
        Assert.DoesNotContain("T00:00:00", ficha);
    }

    // O clube vem por fora porque a navegação é obrigatória (Include dela vira INNER JOIN e
    // some com a página). Faltando, a ficha tem que sair mesmo assim — só sem o local.
    [Fact]
    public void Sem_clube_a_ficha_sai_do_mesmo_jeito_so_que_sem_local()
    {
        var ficha = DadosEstruturados.Torneio(TorneioComData(), null,
            "https://padelizou.com.br/Torneios/Details/7", null, "x")!;

        Assert.DoesNotContain("\"location\"", ficha);
    }

    [Fact]
    public void Com_clube_a_cidade_entra_no_endereco_do_evento()
    {
        var clube = new Clube { Id = 1, Nome = "Arena Beira Rio", Cidade = new Cidade { Id = 1, Nome = "Porto Alegre" } };

        var ficha = DadosEstruturados.Torneio(TorneioComData(), clube,
            "https://padelizou.com.br/Torneios/Details/7", null, "x")!;

        Assert.Contains("Arena Beira Rio", ficha);
        Assert.Contains("Porto Alegre", ficha);
    }

    // Nome de torneio com aspas quebraria o JSON, e o buscador descarta o bloco inteiro sem
    // dizer nada. Quem escapa é o serializador — este teste é o que impede alguém "simplificar"
    // isto pra uma string interpolada na view.
    [Fact]
    public void Nome_com_aspas_nao_quebra_a_ficha()
    {
        var torneio = TorneioComData();
        torneio.Nome = "Torneio \"Rei da Quadra\" & Cia";

        var ficha = DadosEstruturados.Torneio(torneio, null, "https://padelizou.com.br/x", null, "x")!;

        Assert.DoesNotContain("\"name\":\"Torneio \"Rei", ficha);
        var lido = System.Text.Json.JsonDocument.Parse(ficha);
        Assert.Equal("Torneio \"Rei da Quadra\" & Cia", lido.RootElement.GetProperty("name").GetString());
    }

    // ── O sitemap ──────────────────────────────────────────────────────────────────────────

    private static SitemapController Sitemap(DbPadelContext ctx, string host = "padelizou.com.br")
    {
        var c = new SitemapController(ctx);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.ControllerContext.HttpContext.Request.Scheme = "https";
        c.ControllerContext.HttpContext.Request.Host = new HostString(host);
        return c;
    }

    private static async Task<string> XmlDoSitemapAsync(DbPadelContext ctx)
    {
        var resposta = Assert.IsType<ContentResult>(await Sitemap(ctx).Index());
        return resposta.Content!;
    }

    private static Torneio NoSitemap(int id, string nome) => new()
    {
        Id = id,
        Nome = nome,
        Codigo = $"C{id}",
        Status = "Inscrições Abertas",
        AprovadoEm = new DateTime(2026, 8, 1),
        DataInicio = new DateTime(2026, 12, 1),
    };

    [Fact]
    public async Task O_torneio_da_vitrine_entra_na_lista()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(NoSitemap(1, "Aberto de Verão"));
        await ctx.SaveChangesAsync();

        var xml = await XmlDoSitemapAsync(ctx);

        Assert.Contains("https://padelizou.com.br/Torneios/Details/1", xml);
    }

    // A mesma régua que tira o torneio da vitrine tem que tirá-lo do sitemap — senão o
    // organizador esconde o torneio do site e o encontra no Google no dia seguinte.
    [Fact]
    public async Task Torneio_oculto_nao_aprovado_ou_cancelado_fica_de_fora()
    {
        var ctx = TestInfra.NovoContexto();

        var oculto = NoSitemap(1, "Oculto");
        oculto.Oculto = true;

        var semAprovacao = NoSitemap(2, "Esperando aprovação");
        semAprovacao.AprovadoEm = null;

        var cancelado = NoSitemap(3, "Cancelado");
        cancelado.Status = CancelamentoDoTorneio.Status;

        ctx.Torneios.AddRange(oculto, semAprovacao, cancelado, NoSitemap(4, "Esse aparece"));
        await ctx.SaveChangesAsync();

        var xml = await XmlDoSitemapAsync(ctx);

        Assert.DoesNotContain("/Torneios/Details/1", xml);
        Assert.DoesNotContain("/Torneios/Details/2", xml);
        Assert.DoesNotContain("/Torneios/Details/3", xml);
        Assert.Contains("/Torneios/Details/4", xml);
    }

    [Fact]
    public async Task As_telas_fixas_do_site_estao_na_lista()
    {
        var xml = await XmlDoSitemapAsync(TestInfra.NovoContexto());

        Assert.Contains("<loc>https://padelizou.com.br/</loc>", xml);
        Assert.Contains("<loc>https://padelizou.com.br/Torneios</loc>", xml);
        Assert.Contains("<loc>https://padelizou.com.br/Jogadors/Ranking</loc>", xml);
    }

    [Fact]
    public async Task O_professor_entra_e_o_jogador_comum_nao()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Professor", Cpf = "1", IsProfessor = true });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Jogador", Cpf = "2" });
        await ctx.SaveChangesAsync();

        var xml = await XmlDoSitemapAsync(ctx);

        Assert.Contains("/Professores/Perfil/1", xml);
        // Perfil de pessoa física numa lista pra buscador é decisão de privacidade, e ela não
        // foi tomada. Enquanto não for, ninguém entra por engano num refactor.
        Assert.DoesNotContain("/Jogadors/Perfil/2", xml);
    }

    // O mesmo binário serve três hosts. Entregar a lista no dev seria convidar o buscador a
    // rastrear justamente o ambiente que o RobotsMiddleware mantém fora do índice.
    [Theory]
    [InlineData("dev.padelizou.com.br")]
    [InlineData("admin.padelizou.com.br")]
    public async Task O_sitemap_nao_existe_fora_do_site_publico(string host)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(NoSitemap(1, "Aberto de Verão"));
        await ctx.SaveChangesAsync();

        Assert.IsType<NotFoundResult>(await Sitemap(ctx, host).Index());
    }

    [Fact]
    public async Task O_sitemap_e_xml_valido()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(NoSitemap(1, "Torneio \"Rei da Quadra\" & Cia"));
        await ctx.SaveChangesAsync();

        var xml = await XmlDoSitemapAsync(ctx);

        var lido = System.Xml.Linq.XDocument.Parse(xml);
        Assert.Equal("urlset", lido.Root!.Name.LocalName);
        Assert.Equal("http://www.sitemaps.org/schemas/sitemap/0.9", lido.Root.Name.NamespaceName);
    }
}
