using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Padelizou.Services;

public sealed record EnderecoDoCep(
    string Cep,
    string? Logradouro,
    string? Bairro,
    string Cidade,
    string Uf,
    // Código IBGE do município (7 dígitos). O ViaCEP sempre mandou este campo; a gente é que
    // não lia. Ele entrou porque a nota fiscal EXIGE o código do município, e pedir pro dono
    // do clube descobrir o IBGE da própria cidade seria o fim do cadastro fiscal ali mesmo —
    // com default nulo pra quem já constrói um endereço destes não precisar saber que existe.
    string? Ibge = null);

// Três desfechos, e eles NÃO são a mesma coisa — é o que decide se o CEP digitado vai ou não
// pro banco:
//  - Achou      → o endereço é autoridade: a cidade dele vale mais do que a digitada.
//  - NaoExiste  → o ViaCEP respondeu e disse que esse CEP não é de ninguém. Não grava.
//  - NaoConsultado → não deu pra perguntar (fora do ar, timeout). Grava o CEP como veio e
//                    tenta de novo na próxima gravação; recusar aqui seria transformar uma
//                    queda na casa dos outros em perfil que ninguém consegue salvar.
public enum ResultadoDoCep { Achou, NaoExiste, NaoConsultado }

public sealed record ConsultaDeCepResultado(ResultadoDoCep Situacao, EnderecoDoCep? Endereco)
{
    public static readonly ConsultaDeCepResultado NaoExiste = new(ResultadoDoCep.NaoExiste, null);
    public static readonly ConsultaDeCepResultado NaoConsultado = new(ResultadoDoCep.NaoConsultado, null);
    public static ConsultaDeCepResultado Achou(EnderecoDoCep e) => new(ResultadoDoCep.Achou, e);
}

public interface IConsultaDeCep
{
    Task<ConsultaDeCepResultado> BuscarAsync(string? cep, CancellationToken ct = default);
}

// Pergunta o endereço de um CEP ao ViaCEP (viacep.com.br) — serviço público, sem chave.
//
// REGRA DE OURO, a mesma do RankingRsService: NUNCA LANÇAR. Quem chama está no meio do POST
// do cadastro ou do perfil, com a pessoa olhando pra tela. Servidor deles fora do ar, JSON
// diferente do esperado, DNS falhando — tudo vira `NaoConsultado`, e a gravação segue. O CEP
// é campo OPCIONAL: ele não pode ser o motivo de alguém não conseguir criar a conta.
//
// ⚠️ O "não encontrei" do ViaCEP é uma armadilha: ele responde **HTTP 200** com `{"erro":...}`
// no corpo — e o valor desse campo já veio como booleano `true` numa versão e como a string
// `"true"` em outra. Por isso a decisão aqui NÃO olha o campo `erro`: olha se veio
// `localidade`. Sem cidade não há nada de útil, seja qual for o formato do aviso de erro.
public class ConsultaDeCep : IConsultaDeCep
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConsultaDeCep> _logger;

    // CEP não muda de cidade. O que muda é o nome da rua, de anos em anos — guardar por um
    // mês evita bater no serviço dos outros a cada gravação de perfil.
    private static readonly TimeSpan ValidadeDoAchado = TimeSpan.FromDays(30);

    // O "não existe" fica guardado por pouco tempo: quase sempre é erro de digitação, e
    // memorizar isso por um mês seria memorizar o engano de alguém.
    private static readonly TimeSpan ValidadeDoNaoExiste = TimeSpan.FromMinutes(30);

    public ConsultaDeCep(HttpClient http, IMemoryCache cache, ILogger<ConsultaDeCep> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ConsultaDeCepResultado> BuscarAsync(string? cep, CancellationToken ct = default)
    {
        var digitos = Documentos.SomenteDigitos(cep);

        // Formato errado nem vira requisição — e "00000000" não é CEP de ninguém.
        if (digitos.Length != 8 || digitos.All(d => d == '0')) return ConsultaDeCepResultado.NaoExiste;

        var chave = $"cep:{digitos}";
        if (_cache.TryGetValue<ConsultaDeCepResultado>(chave, out var guardado) && guardado != null)
            return guardado;

        ConsultaDeCepResultado resultado;
        try
        {
            var resposta = await _http.GetAsync($"https://viacep.com.br/ws/{digitos}/json/", ct);

            // 400 é o que ele devolve pra CEP mal formado; qualquer outro fora da faixa 2xx é
            // problema DELES, e problema deles não pode virar recusa nossa.
            if (!resposta.IsSuccessStatusCode)
            {
                resultado = resposta.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? ConsultaDeCepResultado.NaoExiste
                    : ConsultaDeCepResultado.NaoConsultado;
            }
            else
            {
                var corpo = await resposta.Content.ReadFromJsonAsync<RespostaViaCep>(cancellationToken: ct);
                resultado = Interpretar(digitos, corpo);
            }
        }
        catch (Exception erro)
        {
            _logger.LogWarning(erro, "Não deu pra consultar o CEP {Cep}.", digitos);
            resultado = ConsultaDeCepResultado.NaoConsultado;
        }

        // "Não consultado" NÃO entra no cache: guardar a queda deles faria a próxima tentativa
        // falhar sem nem tentar, e o serviço podia já ter voltado.
        if (resultado.Situacao == ResultadoDoCep.Achou)
            _cache.Set(chave, resultado, ValidadeDoAchado);
        else if (resultado.Situacao == ResultadoDoCep.NaoExiste)
            _cache.Set(chave, resultado, ValidadeDoNaoExiste);

        return resultado;
    }

    private static ConsultaDeCepResultado Interpretar(string digitos, RespostaViaCep? corpo)
    {
        // Ver o comentário do arquivo: a decisão é pela CIDADE, não pelo campo `erro`.
        if (string.IsNullOrWhiteSpace(corpo?.Localidade) || string.IsNullOrWhiteSpace(corpo.Uf))
            return ConsultaDeCepResultado.NaoExiste;

        return ConsultaDeCepResultado.Achou(new EnderecoDoCep(
            digitos,
            VazioViraNulo(corpo.Logradouro),
            VazioViraNulo(corpo.Bairro),
            // Passa pelo mesmo arrumador do resto do sistema: o ViaCEP escreve certo (com
            // acento e caixa de gente), e é justamente por isso que ele conserta a duplicidade.
            NomeDeCidade.Arrumar(corpo.Localidade),
            NomeDeCidade.ArrumarEstado(corpo.Uf)!,
            // Só passa adiante o que TEM 7 dígitos: CEP de município novo às vezes volta com
            // o campo vazio, e "" gravado num campo obrigatório da nota é rejeição na SEFAZ
            // disfarçada de cadastro preenchido.
            Documentos.SomenteDigitos(corpo.Ibge).Length == 7 ? Documentos.SomenteDigitos(corpo.Ibge) : null));
    }

    private static string? VazioViraNulo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed class RespostaViaCep
    {
        [JsonPropertyName("logradouro")] public string? Logradouro { get; set; }
        [JsonPropertyName("bairro")] public string? Bairro { get; set; }
        [JsonPropertyName("localidade")] public string? Localidade { get; set; }
        [JsonPropertyName("uf")] public string? Uf { get; set; }
        [JsonPropertyName("ibge")] public string? Ibge { get; set; }
    }
}
