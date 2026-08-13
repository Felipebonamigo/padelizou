using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers;

// A ÚNICA porta do Padelizou que serve dado pra um sistema de fora ler.
//
// Quem chama é o Ranking Brasil (mundodoatleta.com.br), o parceiro que valida as inscrições dos
// torneios que aderiram — e o pedido foi deles, em 12/08/2026: a lista dos torneios com
// inscrição aberta que escolheram pontuar no ranking, "apenas informações dos torneios".
// A régua do que sai (e o que NUNCA sai) mora em Services/TorneiosParaOParceiroDoRanking.
//
// ⚠️ `ControllerBase` e não `Controller`: aqui não há view, não há TempData e não há sessão —
// e herdar o que não se usa é como uma ação de API acaba redirecionando alguém pro login um
// dia. De quebra, os testes de reflexão que varrem as TELAS do site (antifalsificação, meta de
// busca) não passam por aqui, que é o certo: esta porta não é tela.
//
// ⚠️ SÓ LEITURA, e pra sempre. Se um dia eles precisarem ESCREVER alguma coisa aqui dentro,
// isso é outra decisão, com outro nível de confiança — não é "mais um método neste arquivo".
[ApiController]
[Route("api/ranking")]
// A mesma trava por IP das consultas do site (60 a cada 5 minutos). Não é desconfiança do
// parceiro: é o laço `while(true)` que qualquer integração ganha um dia por engano, e que sem
// isto viraria uma varredura completa da tabela de torneios a cada milissegundo.
[EnableRateLimiting(TravaDeEntrada.PoliticaConsulta)]
public class TorneiosDoRankingApiController : ControllerBase
{
    private readonly DbPadelContext _context;
    private readonly RankingRsSettings _settings;
    private readonly ILogger<TorneiosDoRankingApiController> _logger;

    public TorneiosDoRankingApiController(DbPadelContext context,
        IOptions<RankingRsSettings> settings, ILogger<TorneiosDoRankingApiController> logger)
    {
        _context = context;
        _settings = settings.Value;
        _logger = logger;
    }

    // GET /api/ranking/torneios     (cabeçalho: x-api-key: <a chave que a gente deu a eles>)
    //
    // O cabeçalho é `x-api-key` de propósito: é EXATAMENTE o nome que a API deles usa com a
    // gente (ver RankingRsService). Quem for escrever o cliente do outro lado já tem o código
    // que manda esse cabeçalho — inventar outro nome só criaria uma primeira tentativa que
    // falha.
    [HttpGet("torneios")]
    public async Task<IActionResult> Torneios()
    {
        // ⚠️ 503 e não 401 quando a chave nem foi configurada do nosso lado. São duas falhas
        // muito diferentes e quem está do outro lado não consegue distinguir sozinho: com um
        // 401 genérico, eles passariam a tarde conferindo a chave que receberam enquanto o
        // problema mora no nosso systemd. Não vaza nada — "a integração ainda não foi ligada
        // aqui" é o que a gente diria no WhatsApp de qualquer jeito.
        if (!_settings.ApiDoParceiroLigada)
        {
            _logger.LogError("API de torneios do parceiro chamada sem `RankingRs__ChaveDoParceiro` "
                + "configurada (ou com chave menor que {Minimo} caracteres) — respondendo 503.",
                RankingRsSettings.TamanhoMinimoDaChave);

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                erro = "A integração de torneios ainda não foi ligada deste lado. Fale com o Padelizou.",
            });
        }

        if (!_settings.ChaveDoParceiroConfere(Request.Headers["x-api-key"].ToString()))
        {
            // Warning e não Error: chave errada é o dia a dia de uma integração nova, e um Error
            // por tentativa faria o vigia de erros acordar o Felipe por causa de um curl.
            _logger.LogWarning("API de torneios do parceiro recusada: chave ausente ou inválida.");
            return Unauthorized(new { erro = "Chave de API inválida ou ausente." });
        }

        // Resposta com chave dentro não pode ficar guardada em cache de ninguém no caminho.
        Response.Headers.CacheControl = "no-store";

        var torneios = (await _context.Torneios.AsNoTracking().ToListAsync())
            .Where(TorneiosParaOParceiroDoRanking.EntraNaLista)
            .ToList();

        var ids = torneios.Select(t => t.Id).ToList();

        // Consultas de CONJUNTO, nunca uma por torneio: é o mesmo cuidado da listagem pública.
        var categorias = (await _context.Categorias.AsNoTracking()
                .Where(c => ids.Contains(c.TorneioId))
                .ToListAsync())
            .ToLookup(c => c.TorneioId);

        var clubeIds = torneios.Select(t => t.ClubeId).Distinct().ToList();
        var clubes = await _context.Clubes.AsNoTracking()
            .Where(c => clubeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var cidadeIds = clubes.Values.Where(c => c.CidadeId != null).Select(c => c.CidadeId!.Value).ToList();
        var cidades = await _context.Cidades.AsNoTracking()
            .Where(c => cidadeIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        // O endereço vem da REQUISIÇÃO, e não do SiteSettings: assim o dev responde links de dev
        // (é lá que eles vão testar) e a produção responde links de produção, sem uma
        // configuração a mais pra alguém esquecer de trocar. Mesmo caminho do sitemap.
        var enderecoBase = $"{Request.Scheme}://{Request.Host}";

        return Ok(TorneiosParaOParceiroDoRanking.Montar(
            torneios, clubes, cidades, categorias, enderecoBase, DateTime.Now));
    }
}
