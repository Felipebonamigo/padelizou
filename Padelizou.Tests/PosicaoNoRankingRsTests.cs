using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Services;

namespace Padelizou.Tests;

// A posição no Ranking RS mostrada no perfil.
//
// O teste que mais importa é o do NOME PARCIAL: o `name` da API é busca por pedaço — procurar
// "Silva" devolve 129 pessoas. Sem filtrar pelo nome inteiro, o perfil de um Silva qualquer
// mostraria a pontuação de dezenas de estranhos como se fosse dele.
public class PosicaoNoRankingRsTests
{
    // Um servidor de mentira: devolve o JSON que o teste mandar, sem sair da máquina.
    private class HttpFalso : HttpMessageHandler
    {
        public string Corpo { get; set; } = """{"count":0,"athletes":[]}""";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<string> UrlsChamadas { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            // AbsoluteUri, e NÃO ToString(): o ToString() de Uri desescapa pra exibição, então
            // ele mostraria "name=João Silva" mesmo com a URL indo codificada no fio. Guardar a
            // forma de exibição faria o teste de codificação testar coisa nenhuma.
            UrlsChamadas.Add(request.RequestUri!.AbsoluteUri);
            if (Responder != null) return Task.FromResult(Responder(request));

            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Corpo, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static (RankingRsService servico, HttpFalso http) Montar(string? chave = "rrs_teste")
    {
        var http = new HttpFalso();
        var settings = Options.Create(new RankingRsSettings
        {
            ApiKey = chave ?? "",
            BaseUrl = "https://exemplo.test/api",
        });
        var servico = new RankingRsService(
            new HttpClient(http), settings,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RankingRsService>.Instance);
        return (servico, http);
    }

    private const string TresSilvas = """
    {"count":3,"athletes":[
      {"name":"Manuela Schuck Silva","sport":"PADEL","category":"1ª Feminina","position":1,"points":300,"isPromoted":false},
      {"name":"Leonardo Silva Freitas","sport":"PADEL","category":"2ª Masculina","position":44,"points":140,"isPromoted":false},
      {"name":"João Silva","sport":"PADEL","category":"4ª Masculina","position":12,"points":95,"isPromoted":false}
    ]}
    """;

    // O CASO QUE MAIS IMPORTA.
    [Fact]
    public async Task Busca_parcial_da_API_nao_vira_pontuacao_de_estranho_no_perfil()
    {
        var (servico, http) = Montar();
        http.Corpo = TresSilvas;

        var posicoes = await servico.PosicoesAsync("João Silva");

        var unica = Assert.Single(posicoes);
        Assert.Equal("4ª Masculina", unica.Categoria);
        Assert.Equal(12, unica.Posicao);
        Assert.Equal(95, unica.Pontos);
    }

    [Theory]
    [InlineData("JOAO SILVA")]        // caixa alta e sem acento
    [InlineData("  joão   silva  ")]  // espaço sobrando no meio e nas pontas
    public async Task Acha_apesar_de_acento_caixa_e_espaco(string comoEstaNoPadelizou)
    {
        var (servico, http) = Montar();
        http.Corpo = TresSilvas;

        Assert.Single(await servico.PosicoesAsync(comoEstaNoPadelizou));
    }

    // Quem não pontua é a MAIORIA das pessoas do Padelizou. Lista vazia, e a seção do perfil
    // nem aparece — "sem ranking" na cara de quem nunca jogou torneio federado seria uma
    // cobrança sem motivo.
    [Fact]
    public async Task Quem_nao_esta_no_ranking_volta_vazio()
    {
        var (servico, http) = Montar();
        http.Corpo = TresSilvas;

        Assert.Empty(await servico.PosicoesAsync("Fulano Que Nao Existe"));
    }

    [Fact]
    public async Task Uma_pessoa_pode_pontuar_em_mais_de_uma_categoria()
    {
        var (servico, http) = Montar();
        http.Corpo = """
        {"count":2,"athletes":[
          {"name":"Silvano Hernandorena","sport":"PADEL","category":"2ª Masculina","position":9,"points":235,"isPromoted":false},
          {"name":"Silvano Hernandorena","sport":"PADEL","category":"3ª Masculina","position":22,"points":170,"isPromoted":false}
        ]}
        """;

        var posicoes = await servico.PosicoesAsync("Silvano Hernandorena");

        Assert.Equal(2, posicoes.Count);
        // Maior pontuação primeiro: é a categoria que define o nível dela.
        Assert.Equal(235, posicoes[0].Pontos);
        Assert.Equal(170, posicoes[1].Pontos);
    }

    // Posição 0 = PROMOVIDO. O ranking tira a colocação da categoria antiga e mantém os pontos.
    // Mostrar "0º" na cara do jogador seria pior do que não mostrar nada.
    [Fact]
    public async Task Promovido_vem_sem_colocacao_e_nao_como_primeiro_lugar()
    {
        var (servico, http) = Montar();
        http.Corpo = """
        {"count":1,"athletes":[
          {"name":"Juliano Escobar","sport":"PADEL","category":"6ª Masculina","position":0,"points":395,"isPromoted":true}
        ]}
        """;

        var pos = Assert.Single(await servico.PosicoesAsync("Juliano Escobar"));
        Assert.False(pos.TemColocacao);
        Assert.True(pos.Promovido);
        Assert.Equal(395, pos.Pontos);
    }

    // ── Nada disso pode derrubar um perfil ────────────────────────────────────────────────

    [Fact]
    public async Task Ranking_fora_do_ar_nao_derruba_o_perfil()
    {
        var (servico, http) = Montar();
        http.Responder = _ => throw new HttpRequestException("caiu");

        Assert.Empty(await servico.PosicoesAsync("João Silva"));
    }

    [Fact]
    public async Task Erro_HTTP_nao_derruba_o_perfil()
    {
        var (servico, http) = Montar();
        http.Status = HttpStatusCode.InternalServerError;

        Assert.Empty(await servico.PosicoesAsync("João Silva"));
    }

    [Fact]
    public async Task Json_diferente_do_esperado_nao_derruba_o_perfil()
    {
        var (servico, http) = Montar();
        http.Corpo = """{"isso":"nao e o que a gente espera"}""";

        Assert.Empty(await servico.PosicoesAsync("João Silva"));
    }

    [Fact]
    public async Task Sem_chave_configurada_nem_sai_requisicao()
    {
        var (servico, http) = Montar(chave: null);

        Assert.Empty(await servico.PosicoesAsync("João Silva"));
        Assert.Empty(http.UrlsChamadas);
    }

    [Fact]
    public async Task Nome_vazio_nem_sai_requisicao()
    {
        var (servico, http) = Montar();

        Assert.Empty(await servico.PosicoesAsync("   "));
        Assert.Empty(http.UrlsChamadas);
    }

    // ── A chave tem cota ──────────────────────────────────────────────────────────────────

    // O perfil é das telas mais visitadas do site. Sem cache, cada visita seria uma consulta.
    [Fact]
    public async Task Consulta_repetida_do_mesmo_nome_vai_uma_vez_so()
    {
        var (servico, http) = Montar();
        http.Corpo = TresSilvas;

        await servico.PosicoesAsync("João Silva");
        await servico.PosicoesAsync("joão silva");   // mesma pessoa, escrita diferente
        await servico.PosicoesAsync("JOAO  SILVA");

        Assert.Single(http.UrlsChamadas);
    }

    // Guardar a resposta VAZIA também: quem não pontua é a maioria, e sem isso todo perfil de
    // quem não está no ranking viraria uma consulta a cada visita.
    [Fact]
    public async Task Ate_o_resultado_vazio_fica_guardado()
    {
        var (servico, http) = Montar();
        http.Corpo = TresSilvas;

        await servico.PosicoesAsync("Fulano Que Nao Existe");
        await servico.PosicoesAsync("Fulano Que Nao Existe");

        Assert.Single(http.UrlsChamadas);
    }

    // A documentação deles diz que este endpoint é público. NÃO É — sem a chave ele recusa.
    [Fact]
    public async Task A_chave_vai_no_cabecalho_mesmo_sendo_endpoint_dito_publico()
    {
        var (servico, http) = Montar();
        string? chaveEnviada = null;
        http.Responder = req =>
        {
            chaveEnviada = req.Headers.TryGetValues("x-api-key", out var v) ? v.FirstOrDefault() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"count":0,"athletes":[]}""", Encoding.UTF8, "application/json"),
            };
        };

        await servico.PosicoesAsync("João Silva");

        Assert.Equal("rrs_teste", chaveEnviada);
    }

    // Nome com espaço e acento tem que sair codificado na URL, senão a requisição nasce torta.
    [Fact]
    public async Task O_nome_vai_codificado_na_url()
    {
        var (servico, http) = Montar();

        await servico.PosicoesAsync("João Silva");

        var url = Assert.Single(http.UrlsChamadas);
        Assert.Contains("sport=PADEL", url);
        Assert.DoesNotContain("name=João Silva", url);   // espaço cru na URL, nunca
        Assert.Contains("Jo%C3%A3o%20Silva", url);
    }
}
