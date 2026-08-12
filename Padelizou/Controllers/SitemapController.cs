using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Middleware;
using Padelizou.Models;
using Padelizou.Services;
using System.Xml.Linq;

namespace Padelizou.Controllers;

// A lista de endereços que o Google deve conhecer.
//
// Sem ela, o buscador só acha um torneio se topar com um link pra ele em algum lugar — e os
// nossos circulam por WhatsApp, que ele não lê. Um torneio que ninguém linkou de fora era, na
// prática, invisível na busca.
//
// Vive fora das Views de propósito: é XML pra robô, não tela.
public class SitemapController : Controller
{
    private readonly DbPadelContext _context;

    public SitemapController(DbPadelContext context) => _context = context;

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Index()
    {
        // O mesmo binário serve três hosts. Entregar a lista no dev ou no admin seria convidar
        // o buscador a rastrear justamente os dois que o RobotsMiddleware mantém fora do índice.
        if (AdminHostMiddleware.EhHostDev(HttpContext) || AdminHostMiddleware.EhHostAdmin(HttpContext))
        {
            return NotFound();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var enderecos = new List<string>
        {
            "/",
            "/Torneios",
            // ⚠️ "Jogadores", com E — é o nome da CLASSE que vira rota, não o do arquivo
            // (`JogadorsController.cs`, sem E). Escrito errado, isto entregava ao Google uma
            // URL que responde 404, e ele registra a falha contra o site.
            "/Jogadores/Ranking",
            "/Professores",
        };

        // ⚠️ A régua de quem aparece é a MESMA das telas públicas
        // (PermissaoDeOrganizador.ApareceParaOPublico) — copiar a condição aqui faria o torneio
        // oculto continuar entrando no Google no dia em que ela mudasse, e ninguém liga um
        // resultado de busca a um `if` esquecido num sitemap.
        var torneios = await _context.Torneios.AsNoTracking().ToListAsync();
        enderecos.AddRange(torneios
            .Where(PermissaoDeOrganizador.ApareceParaOPublico)
            .OrderByDescending(t => t.DataInicio)
            .Select(t => $"/Torneios/Details/{t.Id}"));

        // As páginas de cidade ("torneios de padel em porto alegre"). Vêm logo depois da
        // listagem geral porque são elas que respondem à busca de quem ainda não conhece o
        // site — e o buscador só as encontra se alguém as apontar.
        enderecos.AddRange((await TorneiosPorCidade.ListarAsync(_context))
            .Select(lugar => $"/torneios-de-padel-em-{lugar.Slug}"));

        // Professor é quem tem interesse próprio em ser achado no Google — é a página que
        // vende a aula dele. O perfil de JOGADOR fica de fora: é página de pessoa física, e
        // pôr as contas todas numa lista pra buscador é decisão de privacidade, não de SEO.
        var professores = await _context.Jogadores.AsNoTracking()
            .Where(j => j.IsProfessor)
            .Select(j => j.Id)
            .ToListAsync();
        enderecos.AddRange(professores.Select(id => $"/Professores/Perfil/{id}"));

        var mapa = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Ns + "urlset",
                enderecos.Select(caminho =>
                    new XElement(Ns + "url",
                        new XElement(Ns + "loc", baseUrl + caminho)))));

        // Sem <lastmod> de propósito: não temos uma data de "última alteração" confiável por
        // torneio, e data inventada é pior que data nenhuma — o buscador aprende a ignorar o
        // campo, e aí ele deixa de servir no dia em que a tivermos.
        return Content($"{mapa.Declaration}{Environment.NewLine}{mapa}", "application/xml; charset=utf-8");
    }
}
