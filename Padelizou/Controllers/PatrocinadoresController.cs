using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers;

// O SALTO QUE FAZ O CLIQUE NO PATROCINADOR EXISTIR (29/09/2026). O logo do rodapé passa por
// aqui antes de ir pro site da marca — sem isso o clique nunca tocava o servidor e não havia o
// que contar.
//
// Sem [Authorize] de propósito: o rodapé aparece pra visitante anônimo, e é justamente o
// tráfego dele que o patrocinador comprou.
public class PatrocinadoresController : Controller
{
    private readonly DbPadelContext _context;
    private readonly PatrocinadoresSettings _cfg;

    public PatrocinadoresController(DbPadelContext context, IOptions<PatrocinadoresSettings> cfg)
    {
        _context = context;
        _cfg = cfg.Value;
    }

    // ⚠️ O PARÂMETRO É O NOME, NUNCA A URL — e esta é a linha que separa um contador de cliques
    // de um open redirect. Com `?url=`, qualquer um monta um link de phishing saindo de
    // `padelizou.com.br`: o domínio que as pessoas reconhecem, e no qual elas clicam por isso.
    // O destino sai de PatrocinadoresSettings, no servidor, sempre.
    //
    // GET e não POST porque isto é um LINK: tem que funcionar no clique do meio, no "abrir em
    // nova aba" e no compartilhamento. Gravar num GET contraria a Regra 0 à primeira vista, mas
    // o que ele grava não é dado de ninguém — é um contador anônimo, e a alternativa (POST via
    // JS) perderia justamente quem tem JS bloqueado.
    [HttpGet]
    [Route("ir/patrocinador/{nome}")]
    public async Task<IActionResult> Ir(string nome)
    {
        if (CliquesDoPatrocinio.Achar(_cfg, nome) is not { } patrocinador
            || string.IsNullOrWhiteSpace(patrocinador.Link))
        {
            return NotFound();
        }

        // ⚠️ A contagem não pode derrubar o salto: se o banco falhar, a pessoa ainda tem que
        // chegar no site do patrocinador. Perder um clique da estatística é barato; perder a
        // visita que a marca pagou pra ter, não.
        try
        {
            _context.CliquesNoPatrocinador.Add(new CliqueNoPatrocinador
            {
                Patrocinador = patrocinador.Nome,
                Quando = DateTime.Now,
            });
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Engolido de propósito, e é o único lugar deste bloco em que isso é certo.
        }

        return Redirect(patrocinador.Link);
    }
}
