using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers;

// OS CARDS QUE O JOGADOR POSTA.
//
// Duas artes, um controller, porque o que elas têm em comum é o que importa: são as ÚNICAS
// páginas do Padelizou cujo objetivo é sair do Padelizou. Tudo aqui é público de propósito —
// card que só quem tem login vê não é divulgação, é relatório.
//
// ⚠️ NADA É GRAVADO EM DISCO. O PNG é desenhado a cada pedido e vai embora na resposta. Guardar
// entraria no `tar` completo diário do backup (14 cópias) pra economizar um desenho de ~40 ms —
// é a mesma conta que manteve a galeria de fotos fora do sistema.
//
// ⚠️ O CACHE AQUI NÃO É OTIMIZAÇÃO, É REQUISITO. Quando alguém manda o link no WhatsApp, o
// servidor da Meta busca a imagem pra montar a prévia; o Instagram faz o mesmo. Sem
// `Cache-Control` a mesma arte é redesenhada a cada olhada de cada pessoa do grupo.
public class CartoesController : Controller
{
    private readonly DbPadelContext _context;
    private readonly FonteDoCartao _fontes;
    private readonly IWebHostEnvironment _ambiente;
    private readonly IEstatisticasService _estatisticas;

    public CartoesController(DbPadelContext context, FonteDoCartao fontes,
        IWebHostEnvironment ambiente, IEstatisticasService estatisticas)
    {
        _context = context;
        _fontes = fontes;
        _ambiente = ambiente;
        _estatisticas = estatisticas;
    }

    // Uma hora. Curto o bastante pra o card do ano acompanhar quem jogou hoje, longo o bastante
    // pra um grupo de WhatsApp inteiro abrir a mesma prévia sem redesenhar.
    private const int SegundosDeCache = 3600;

    private FileContentResult Png(byte[] bytes, string nomeDoArquivo)
    {
        Response.Headers.CacheControl = $"public, max-age={SegundosDeCache}";

        // O nome sugerido na hora de salvar. `inline` e não `attachment`: a imagem precisa
        // ABRIR no navegador (é assim que a prévia do link funciona e é assim que a pessoa
        // segura o dedo pra salvar no celular). O download forçado fica no botão da tela.
        Response.Headers.ContentDisposition = $"inline; filename=\"{nomeDoArquivo}\"";
        return File(bytes, "image/png");
    }

    // ───────────────────────── O CARD DE CAMPEÃO ─────────────────────────

    // A página com os campeões de um torneio, uma arte por categoria.
    [HttpGet]
    public async Task<IActionResult> Campeoes(int id)
    {
        var torneio = await _context.Torneios
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (torneio == null) return NotFound();

        var campeoes = await CampeoesDoTorneio.DoTorneioAsync(_context, id);

        ViewBag.Torneio = torneio;
        ViewBag.FonteDisponivel = _fontes.Disponivel;
        return View(campeoes.Where(c => c.TemOQueMostrar).ToList());
    }

    // O PNG de uma categoria.
    [HttpGet]
    public async Task<IActionResult> Campeao(int id, int categoriaId)
    {
        // ⚠️ A recusa por falta de fonte vem ANTES de qualquer consulta, e é 404 e não uma
        // imagem em branco: sem a Poppins o card sairia com todos os textos invisíveis no
        // Linux, e uma arte muda circulando com a nossa marca é pior que arte nenhuma.
        if (!_fontes.Disponivel) return NotFound();

        var campeao = await CampeoesDoTorneio.DaCategoriaAsync(_context, id, categoriaId);
        if (campeao == null || !campeao.TemOQueMostrar) return NotFound();

        var png = CartaoDeCampeao.Desenhar(campeao, _fontes, _ambiente.WebRootPath);
        return Png(png, $"campeoes-{Arquivo(campeao.Categoria)}.png");
    }

    // ───────────────────────── O CARD DO ANO ─────────────────────────

    // "Seu ano no padel". Sem `id`, é o ano de quem está logado.
    [HttpGet]
    public async Task<IActionResult> Ano(int? id, int? ano)
    {
        int? meuId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;

        var jogadorId = id ?? meuId;
        if (jogadorId == null) return RedirectToAction("Login", "Auth");

        var jogador = await _context.Jogadores.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jogadorId.Value);
        if (jogador == null || jogador.Excluido) return NotFound();

        var anoEscolhido = ano ?? RetrospectivaDoAno.AnoPadrao(DateTime.Now);
        if (!RetrospectivaDoAno.AnoValido(anoEscolhido, DateTime.Now)) return NotFound();

        var retrospectiva = await _estatisticas.ObterRetrospectivaAsync(jogadorId.Value, anoEscolhido);

        ViewBag.SouEu = meuId == jogadorId;
        ViewBag.FonteDisponivel = _fontes.Disponivel;
        ViewBag.AnoAtual = DateTime.Now.Year;
        return View(retrospectiva);
    }

    // O PNG do ano.
    [HttpGet]
    public async Task<IActionResult> AnoImagem(int id, int ano)
    {
        if (!_fontes.Disponivel) return NotFound();
        if (!RetrospectivaDoAno.AnoValido(ano, DateTime.Now)) return NotFound();

        // ⚠️ Conta EXCLUÍDA (LGPD) não vira card. A linha continua no banco por causa dos jogos
        // — que são dado de quatro pessoas — mas o nome dela foi raspado justamente pra deixar
        // de circular, e um card é a forma mais pública que existe de fazer um nome circular.
        var jogador = await _context.Jogadores.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (jogador == null || jogador.Excluido) return NotFound();

        var retrospectiva = await _estatisticas.ObterRetrospectivaAsync(id, ano);

        // Menos de três jogos não vira arte — ver RetrospectivaDoAno.JogosMinimosParaCard.
        if (!retrospectiva.TemCard) return NotFound();

        var png = CartaoDoAno.Desenhar(retrospectiva, _fontes, _ambiente.WebRootPath);
        return Png(png, $"meu-ano-{ano}.png");
    }

    // Nome de arquivo previsível a partir de um texto livre. O nome da categoria vai pro
    // `Content-Disposition`, e cabeçalho HTTP é ASCII: qualquer byte acima de 127 derruba a
    // resposta inteira com `InvalidOperationException` no Kestrel — 500, não header feio.
    //
    // ⚠️ A PENEIRA É ASCII EXPLÍCITO, e não `char.IsLetterOrDigit`. Foi assim que isto quebrou:
    // tirar acento com `FormD` + descartar `NonSpacingMark` resolve "ã" e "ç", mas NÃO resolve o
    // "ª" de "6ª Categoria Masculina" — ele é uma letra por conta própria (U+00AA), passa no
    // `IsLetterOrDigit` e chega inteiro no cabeçalho. O card de TODA categoria numerada dava
    // 500; só as "Open" funcionavam, que foi justamente a primeira que eu testei.
    // Público só pra o teste poder provar que o resultado é ASCII — a alternativa era montar o
    // controller inteiro com banco pra ler um cabeçalho.
    public static string Arquivo(string texto)
    {
        var limpo = new string(texto
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                     != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(c => EhAsciiSeguro(c) ? char.ToLowerInvariant(c) : '-')
            .ToArray());

        while (limpo.Contains("--")) limpo = limpo.Replace("--", "-");
        limpo = limpo.Trim('-');

        // Categoria escrita só com caracteres estranhos viraria um nome vazio, e
        // `filename=""` é cabeçalho inútil.
        return limpo.Length == 0 ? "padelizou" : limpo;
    }

    public static bool EhAsciiSeguro(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
}
