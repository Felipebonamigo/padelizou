using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O CEP entrou no cadastro por causa da CIDADE: digitada à mão ela vira "GRAVATAI",
// "Gravatai" e "gravatai"; vinda do CEP ela vem escrita de um jeito só.
//
// O que estes testes prendem é o comportamento sob DEFEITO, porque o campo é opcional e
// nenhuma falha do ViaCEP pode impedir alguém de criar a conta.
public class ConsultaDeCepTests
{
    // ── ViaCEP dublado ───────────────────────────────────────────────────────────────────

    private sealed class HttpFalso : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Chamadas++;
            if (Responder == null) throw new HttpRequestException("servidor fora do ar");
            return Task.FromResult(Responder(request));
        }
    }

    private static HttpResponseMessage Resposta(string corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private static ConsultaDeCep Novo(HttpFalso http) =>
        new(new HttpClient(http), new MemoryCache(new MemoryCacheOptions()), NullLogger<ConsultaDeCep>.Instance);

    private const string RespostaBoa = """
        {"cep":"94010-020","logradouro":"Rua Dom Feliciano","bairro":"Centro",
         "localidade":"Gravataí","uf":"RS"}
        """;

    // ── Caminho feliz ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_endereco_vem_do_CEP_com_a_cidade_escrita_certo()
    {
        var http = new HttpFalso { Responder = _ => Resposta(RespostaBoa) };

        var r = await Novo(http).BuscarAsync("94010-020");

        Assert.Equal(ResultadoDoCep.Achou, r.Situacao);
        Assert.Equal("Gravataí", r.Endereco!.Cidade);
        Assert.Equal("RS", r.Endereco.Uf);
        Assert.Equal("Rua Dom Feliciano", r.Endereco.Logradouro);
        Assert.Equal("Centro", r.Endereco.Bairro);
        Assert.Equal("94010020", r.Endereco.Cep);   // guardado só com dígitos, como CPF e celular
    }

    [Fact]
    public async Task O_mesmo_CEP_nao_e_perguntado_duas_vezes()
    {
        // O CEP é reconsultado a cada gravação de perfil. Sem cache, isso vira uma
        // requisição no serviço dos outros toda vez que alguém salva o cadastro.
        var http = new HttpFalso { Responder = _ => Resposta(RespostaBoa) };
        var consulta = Novo(http);

        await consulta.BuscarAsync("94010020");
        await consulta.BuscarAsync("94010-020");   // mesma coisa, escrita diferente

        Assert.Equal(1, http.Chamadas);
    }

    // ── "Não existe" ─────────────────────────────────────────────────────────────────────

    // ⚠️ O ViaCEP responde 200 com `{"erro":...}` no corpo — e esse campo já veio booleano
    // numa versão e string em outra. Por isso a decisão olha a CIDADE, não o campo `erro`.
    [Theory]
    [InlineData("""{"erro": true}""")]
    [InlineData("""{"erro": "true"}""")]
    [InlineData("{}")]
    [InlineData("""{"localidade":"","uf":""}""")]
    public async Task Resposta_sem_cidade_e_CEP_que_nao_existe(string corpo)
    {
        var http = new HttpFalso { Responder = _ => Resposta(corpo) };

        var r = await Novo(http).BuscarAsync("99999999");

        Assert.Equal(ResultadoDoCep.NaoExiste, r.Situacao);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("00000000")]
    public async Task CEP_com_cara_errada_nem_vira_requisicao(string? cep)
    {
        var http = new HttpFalso { Responder = _ => Resposta(RespostaBoa) };

        var r = await Novo(http).BuscarAsync(cep);

        Assert.Equal(ResultadoDoCep.NaoExiste, r.Situacao);
        Assert.Equal(0, http.Chamadas);
    }

    // ── Fora do ar ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Servico_fora_do_ar_nao_lanca_e_nao_e_confundido_com_CEP_errado()
    {
        // A diferença importa: "não existe" apaga o CEP e avisa a pessoa que ela errou;
        // "não consultado" guarda o CEP e fica calado, porque o erro não foi dela.
        var http = new HttpFalso();   // sem Responder = estoura

        var r = await Novo(http).BuscarAsync("94010020");

        Assert.Equal(ResultadoDoCep.NaoConsultado, r.Situacao);
    }

    [Fact]
    public async Task Erro_500_deles_nao_vira_CEP_invalido_pra_gente()
    {
        var http = new HttpFalso { Responder = _ => Resposta("", HttpStatusCode.InternalServerError) };

        var r = await Novo(http).BuscarAsync("94010020");

        Assert.Equal(ResultadoDoCep.NaoConsultado, r.Situacao);
    }

    [Fact]
    public async Task A_queda_deles_nao_fica_guardada()
    {
        // Guardar "não consultado" faria a tentativa seguinte falhar sem nem tentar — e o
        // serviço podia já ter voltado no minuto seguinte.
        var http = new HttpFalso();
        var consulta = Novo(http);

        await consulta.BuscarAsync("94010020");
        http.Responder = _ => Resposta(RespostaBoa);
        var segunda = await consulta.BuscarAsync("94010020");

        Assert.Equal(ResultadoDoCep.Achou, segunda.Situacao);
    }

    [Fact]
    public async Task Json_diferente_do_esperado_nao_derruba_o_cadastro()
    {
        var http = new HttpFalso { Responder = _ => Resposta("isto não é json") };

        var r = await Novo(http).BuscarAsync("94010020");

        Assert.Equal(ResultadoDoCep.NaoConsultado, r.Situacao);
    }

    // ── O que vai pro jogador ────────────────────────────────────────────────────────────

    private static readonly EnderecoDoCep Achado =
        new("94010020", "Rua Dom Feliciano", "Centro", "Gravataí", "RS");

    [Fact]
    public void A_cidade_do_CEP_manda_na_cidade_digitada()
    {
        // É o ponto do trabalho todo. Também fecha a porta de mandar um CEP de Gravataí com
        // a cidade escrita "POA" no mesmo formulário.
        var jogador = new Jogador { Nome = "J", Cpf = "1", Cidade = "POA", Estado = "SC" };

        var aviso = EnderecoPeloCep.Aplicar(jogador, "94010-020", ConsultaDeCepResultado.Achou(Achado), false);

        Assert.Null(aviso);
        Assert.Equal("Gravataí", jogador.Cidade);
        Assert.Equal("RS", jogador.Estado);
        Assert.Equal("Rua Dom Feliciano", jogador.Logradouro);
        Assert.Equal("Centro", jogador.Bairro);
    }

    [Fact]
    public void CEP_que_nao_existe_avisa_e_nao_grava_nada()
    {
        var jogador = new Jogador { Nome = "J", Cpf = "1", Cidade = "Canoas", Estado = "RS" };

        var aviso = EnderecoPeloCep.Aplicar(jogador, "99999999", ConsultaDeCepResultado.NaoExiste, false);

        Assert.Equal(EnderecoPeloCep.NaoEncontrado, aviso);
        Assert.Null(jogador.Cep);
        // ⚠️ A cidade que a pessoa já tinha NÃO se perde por causa de um CEP errado.
        Assert.Equal("Canoas", jogador.Cidade);
    }

    [Fact]
    public void Servico_fora_do_ar_guarda_o_CEP_e_nao_reclama()
    {
        var jogador = new Jogador { Nome = "J", Cpf = "1" };

        var aviso = EnderecoPeloCep.Aplicar(jogador, "94010-020", ConsultaDeCepResultado.NaoConsultado, false);

        Assert.Null(aviso);
        Assert.Equal("94010020", jogador.Cep);
    }

    [Fact]
    public void Apagar_o_CEP_apaga_o_endereco_junto()
    {
        // Rua e bairro órfãos guardariam o endereço de quem acabou de pedir pra tirá-lo.
        var jogador = new Jogador
        {
            Nome = "J", Cpf = "1",
            Cep = "94010020", Logradouro = "Rua Dom Feliciano", Bairro = "Centro",
            Cidade = "Gravataí", Estado = "RS",
        };

        EnderecoPeloCep.Aplicar(jogador, "", ConsultaDeCepResultado.NaoExiste, false);

        Assert.Null(jogador.Cep);
        Assert.Null(jogador.Logradouro);
        Assert.Null(jogador.Bairro);
        // A cidade fica: ela é do perfil, não do endereço, e o ranking depende dela.
        Assert.Equal("Gravataí", jogador.Cidade);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_escolha_de_mostrar_o_endereco_e_sempre_a_da_pessoa(bool publico)
    {
        var jogador = new Jogador { Nome = "J", Cpf = "1", EnderecoPublico = !publico };

        EnderecoPeloCep.Aplicar(jogador, "94010020", ConsultaDeCepResultado.Achou(Achado), publico);

        Assert.Equal(publico, jogador.EnderecoPublico);
    }
}
