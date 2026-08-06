using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Padelizou.Services;

// Fala com a API do Ranking RS (mundodoatleta.com.br) pra perguntar se um atleta pode disputar
// uma categoria.
//
// REGRA DE OURO DESTE ARQUIVO: NUNCA LANÇAR, E NUNCA RECUSAR POR CONTA PRÓPRIA.
// Quem chama está no meio do POST da inscrição, com a pessoa olhando pra tela. Servidor deles
// fora do ar, chave errada, JSON diferente do esperado — tudo isso vira `NaoConsultado`, e a
// inscrição segue. O contrário (recusar quando não deu pra perguntar) transformaria uma queda
// na casa dos outros em torneio que ninguém consegue se inscrever, no sábado de manhã.
//
// ⚠️ TRÊS COISAS QUE A DOCUMENTAÇÃO DELES DIZ ERRADO, conferidas contra a API em 06/08/2026:
//  1. `GET /external/ranking` está documentado como público — ele EXIGE a chave (sem ela,
//     responde `{"error":"Chave de API inválida ou ausente"}`).
//  2. O `category` do ranking não aceita o id numérico, só o NOME ("6ª Masculina").
//  3. `esporte` inválido NÃO dá erro: mandando "TENIS" ela responde 200 APROVADO com
//     "(Sem registros)". Por isso o esporte é constante aqui e não vem de configuração —
//     um dia alguém escreveria "Padel" em vez de "PADEL" e TODO MUNDO passaria, calado.
public class RankingRsService : IRankingRsService
{
    private readonly HttpClient _http;
    private readonly RankingRsSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RankingRsService> _logger;

    // Ver o item 3 acima: constante de propósito.
    private const string Esporte = "PADEL";

    public RankingRsService(HttpClient http, IOptions<RankingRsSettings> settings,
        IMemoryCache cache, ILogger<RankingRsService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _cache = cache;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSegundos, 2, 30));
    }

    public bool Configurado => _settings.Configurado;

    public async Task<ResultadoDoRanking> ValidarAsync(string nome, int categoriaRsId,
        CancellationToken ct = default)
    {
        var lote = await ValidarLoteAsync(new[] { new AtletaParaValidar(nome, categoriaRsId) }, ct);
        return lote.Count > 0 ? lote[0] : NaoConsultado(nome, categoriaRsId);
    }

    public async Task<IReadOnlyList<ResultadoDoRanking>> ValidarLoteAsync(
        IReadOnlyList<AtletaParaValidar> atletas, CancellationToken ct = default)
    {
        if (atletas.Count == 0) return Array.Empty<ResultadoDoRanking>();

        // Categoria que não existe no ranking nem chega a virar requisição: a API devolve 400
        // e o lote inteiro se perde por causa de uma linha.
        var validos = atletas.Where(a => CategoriaDoRankingRs.IdExiste(a.CategoriaRsId)
                                         && !string.IsNullOrWhiteSpace(a.Nome)).ToList();
        var semResposta = atletas.Except(validos)
            .Select(a => NaoConsultado(a.Nome, a.CategoriaRsId, a.Referencia)).ToList();

        if (validos.Count == 0) return semResposta;

        if (!_settings.Configurado)
        {
            // Debug e não Warning: no localhost e nos testes a chave não existe de propósito,
            // e em Warning isso viraria ruído que esconde problema de verdade.
            _logger.LogDebug("Ranking RS sem chave configurada — {Quantos} atleta(s) não validados.",
                validos.Count);
            return semResposta.Concat(validos.Select(a => NaoConsultado(a.Nome, a.CategoriaRsId, a.Referencia)))
                              .ToList();
        }

        try
        {
            // A `Referencia` vai no `external_registration_user_id`, que a API ecoa de volta —
            // é como cada resposta reencontra a linha que a pediu. Casar por NOME não serviria:
            // duas pessoas homônimas no mesmo torneio embaralhariam os resultados.
            var corpo = new
            {
                esporte = Esporte,
                atletas = validos.Select((a, i) => new
                {
                    nome = a.Nome.Trim(),
                    categoriaId = a.CategoriaRsId,
                    external_registration_user_id = a.Referencia ?? $"#{i}",
                }).ToArray(),
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/validar-categoria")
            {
                Content = JsonContent.Create(corpo),
            };
            req.Headers.Add("x-api-key", _settings.ApiKey);

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // 401 é problema de CONFIGURAÇÃO nossa, não instabilidade deles: merece Error,
                // senão a integração fica desligada em silêncio por semanas.
                var nivel = resp.StatusCode == HttpStatusCode.Unauthorized
                    ? LogLevel.Error : LogLevel.Warning;
                _logger.Log(nivel, "Ranking RS devolveu {Status} ao validar {Quantos} atleta(s): {Corpo}",
                    resp.StatusCode, validos.Count, await LerAsync(resp, ct));
                return TodosSemResposta(atletas);
            }

            var lote = await resp.Content.ReadFromJsonAsync<LoteResposta>(cancellationToken: ct);
            if (lote?.Resultados == null)
            {
                _logger.LogWarning("Ranking RS respondeu 200 sem a lista de resultados.");
                return TodosSemResposta(atletas);
            }

            var porReferencia = lote.Resultados
                .Where(r => r.Referencia != null)
                .ToDictionary(r => r.Referencia!, r => r, StringComparer.Ordinal);

            var respostas = validos.Select((a, i) =>
            {
                var chave = a.Referencia ?? $"#{i}";
                return porReferencia.TryGetValue(chave, out var r)
                    ? Converter(r, a)
                    // Veio 200, mas sem esta linha na resposta. Não inventar veredito.
                    : NaoConsultado(a.Nome, a.CategoriaRsId, a.Referencia);
            });

            return semResposta.Concat(respostas).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Fora do ar, estourou o tempo, ou mudou o formato do JSON: a inscrição não pode
            // parar por isso.
            _logger.LogWarning(ex, "Não deu pra consultar o Ranking RS — inscrição segue sem a validação.");
            return TodosSemResposta(atletas);
        }
    }

    // ── Vitrine: onde a pessoa está no ranking ────────────────────────────────────────────

    public async Task<IReadOnlyList<PosicaoNoRanking>> PosicoesAsync(string nome,
        CancellationToken ct = default)
    {
        if (!_settings.Configurado || string.IsNullOrWhiteSpace(nome)) return Vazio;

        var procurado = Achatar(nome);
        if (procurado.Length == 0) return Vazio;

        // Cache de horas, e não de minutos: isto abre num PERFIL, que é das telas mais
        // visitadas do site, e a chave tem cota. O ranking não muda de hora em hora.
        if (_cache.TryGetValue(ChaveDeCache(procurado), out IReadOnlyList<PosicaoNoRanking>? guardado)
            && guardado != null)
        {
            return guardado;
        }

        var posicoes = await BuscarPosicoesAsync(nome, procurado, ct);

        // Guarda inclusive a lista VAZIA: quem não está no ranking é a maioria das pessoas, e
        // sem isso todo perfil de quem não pontua viraria uma consulta a cada visita.
        _cache.Set(ChaveDeCache(procurado), posicoes, TimeSpan.FromHours(6));
        return posicoes;
    }

    private async Task<IReadOnlyList<PosicaoNoRanking>> BuscarPosicoesAsync(
        string nome, string procurado, CancellationToken ct)
    {
        try
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/external/ranking"
                    + $"?sport={Esporte}&name={Uri.EscapeDataString(nome.Trim())}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            // ⚠️ A documentação diz que este endpoint é público. NÃO É: sem a chave ele
            // responde {"error":"Chave de API inválida ou ausente"}.
            req.Headers.Add("x-api-key", _settings.ApiKey);

            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ranking RS devolveu {Status} ao buscar a posição de {Nome}.",
                    resp.StatusCode, nome);
                return Vazio;
            }

            var lista = await resp.Content.ReadFromJsonAsync<RankingResposta>(cancellationToken: ct);
            if (lista?.Atletas == null) return Vazio;

            // O `name` da API é busca PARCIAL — procurar "Silva" devolve 129 pessoas. Num
            // perfil isso viraria a lista de homônimos de outra gente, então só entra quem
            // bate o nome inteiro.
            return lista.Atletas
                .Where(a => Achatar(a.Nome) == procurado)
                .Select(a => new PosicaoNoRanking(a.Categoria ?? "", a.Posicao, a.Pontos, a.Promovido))
                .OrderByDescending(p => p.Pontos)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Perfil não pode quebrar porque o ranking caiu. Sem posição, a seção some.
            _logger.LogWarning(ex, "Não deu pra buscar a posição de {Nome} no Ranking RS.", nome);
            return Vazio;
        }
    }

    private static readonly IReadOnlyList<PosicaoNoRanking> Vazio = Array.Empty<PosicaoNoRanking>();

    private static string ChaveDeCache(string nomeAchatado) => $"rankingrs:pos:{nomeAchatado}";

    // Minúsculo, sem acento e sem espaço sobrando — a mesma tolerância que a API deles usa
    // pra casar nome (conferido: "SILVANO  HERNANDORENA" acha "Silvano Hernandorena").
    private static string Achatar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var c in texto.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        var semAcento = sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant();
        return string.Join(' ', semAcento.Split(' ', StringSplitOptions.RemoveEmptyEntries
                                                    | StringSplitOptions.TrimEntries));
    }

    private IReadOnlyList<ResultadoDoRanking> TodosSemResposta(IReadOnlyList<AtletaParaValidar> atletas) =>
        atletas.Select(a => NaoConsultado(a.Nome, a.CategoriaRsId, a.Referencia)).ToList();

    private static ResultadoDoRanking NaoConsultado(string nome, int categoriaRsId, string? referencia = null) =>
        new(RespostaDoRanking.NaoConsultado, nome, CategoriaDoRankingRs.NomeDoId(categoriaRsId),
            EncontradoNoRanking: false, Array.Empty<CategoriaBloqueante>(), referencia);

    private static ResultadoDoRanking Converter(AtletaResposta r, AtletaParaValidar pedido)
    {
        // Compara pelo texto exato que a API manda. Qualquer coisa que não seja um dos dois
        // status conhecidos vira NaoConsultado — status novo do lado deles não pode virar
        // "aprovado" por omissão nem recusa por engano.
        var resposta = r.Status switch
        {
            "APROVADO" => RespostaDoRanking.Aprovado,
            "FORA_DE_CATEGORIA" => RespostaDoRanking.ForaDeCategoria,
            _ => RespostaDoRanking.NaoConsultado,
        };

        var bloqueantes = (r.Bloqueantes ?? new List<BloqueanteResposta>())
            .Select(b => new CategoriaBloqueante(b.Categoria ?? "", b.Pontos, b.Posicao))
            .ToList();

        return new ResultadoDoRanking(resposta, pedido.Nome,
            r.Categoria ?? CategoriaDoRankingRs.NomeDoId(pedido.CategoriaRsId),
            r.Encontrado, bloqueantes, pedido.Referencia);
    }

    private static async Task<string> LerAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { return (await resp.Content.ReadAsStringAsync(ct)).Trim(); }
        catch { return "(sem corpo)"; }
    }

    // ---- O JSON que a API devolve. Nomes conferidos contra a resposta de verdade. ----
    // ⚠️ "categoriasBlockeantes" está escrito assim mesmo do lado deles.
    private class LoteResposta
    {
        [JsonPropertyName("resultados")] public List<AtletaResposta>? Resultados { get; set; }
    }

    private class AtletaResposta
    {
        [JsonPropertyName("nome")] public string? Nome { get; set; }
        [JsonPropertyName("categoria")] public string? Categoria { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("encontrado")] public bool Encontrado { get; set; }
        [JsonPropertyName("categoriasBlockeantes")] public List<BloqueanteResposta>? Bloqueantes { get; set; }
        [JsonPropertyName("external_registration_user_id")] public string? Referencia { get; set; }
    }

    private class BloqueanteResposta
    {
        [JsonPropertyName("categoria")] public string? Categoria { get; set; }
        [JsonPropertyName("pontos")] public int Pontos { get; set; }
        [JsonPropertyName("posicao")] public int Posicao { get; set; }
    }

    // ⚠️ O /external/ranking responde em INGLÊS, ao contrário do /validar-categoria.
    private class RankingResposta
    {
        [JsonPropertyName("athletes")] public List<AtletaNoRanking>? Atletas { get; set; }
    }

    private class AtletaNoRanking
    {
        [JsonPropertyName("name")] public string? Nome { get; set; }
        [JsonPropertyName("category")] public string? Categoria { get; set; }
        [JsonPropertyName("position")] public int Posicao { get; set; }
        [JsonPropertyName("points")] public int Pontos { get; set; }
        [JsonPropertyName("isPromoted")] public bool Promovido { get; set; }
    }
}
