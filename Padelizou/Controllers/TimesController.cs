using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers;

// Vitrine dos times. O time já existia como entidade (o jogador escolhe a "bandeira" no
// perfil e ela aparece no ranking), mas não havia tela pra ver quem está em cada um.
public class TimesController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;

    public TimesController(DbPadelContext context, IEstatisticasService estatisticas)
    {
        _context = context;
        _estatisticas = estatisticas;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var times = await _context.Times
            .Select(t => new TimeResumoVM
            {
                Id = t.Id,
                Nome = t.Nome,
                Logo = t.Logo,
                Membros = _context.Jogadores.Count(j => j.TimeId == t.Id),
            })
            .ToListAsync();

        // Pontos do time = soma dos pontos reais dos membros (mesma regra do ranking).
        var pontosPorTime = (await _estatisticas.ObterRankingTimesAsync())
            .ToDictionary(r => r.TimeId, r => r.Pontos);

        // As sedes de todos os times numa consulta só — são várias por time agora, e uma
        // consulta por cartão faria dezenas de idas ao banco pra desenhar a vitrine.
        var sedesPorTime = await SedesDoTime.PorTimeAsync(_context, times.Select(t => t.Id));

        foreach (var t in times)
        {
            t.Pontos = pontosPorTime.GetValueOrDefault(t.Id);
            t.Sedes = sedesPorTime.GetValueOrDefault(t.Id) ?? new List<string>();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            times = times.Where(t => t.Nome.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.Query = q;

        return View(times
            .OrderByDescending(t => t.Membros)
            .ThenBy(t => t.Nome)
            .ToList());
    }

    // A aba "Transferências recentes": quem trocou de camisa, com filtro por time, por
    // sentido (entradas/saídas) e busca por jogador.
    //
    // ⚠️ Tela pública e SEM login, igual à vitrine ao lado — quem muda de time é informação
    // que já aparece no perfil e no ranking. Quem pediu exclusão de conta pela LGPD fica de
    // fora da busca por nome (é o BuscaJogador.Filtrar que garante isso).
    [HttpGet]
    public async Task<IActionResult> Transferencias(int? timeId, string? sentido, string? q)
    {
        var escolhido = TransferenciasDeTime.SentidoDe(sentido);

        var vm = new TransferenciasVM
        {
            TimeId = timeId,
            Sentido = escolhido,
            Busca = q,
            Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync(),
            Movimentos = await TransferenciasDeTime.RecentesAsync(_context, timeId, escolhido, q),
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        var time = await _context.Times.FirstOrDefaultAsync(t => t.Id == id);

        if (time == null) return NotFound();

        var membros = await _context.Jogadores
            .Where(j => j.TimeId == id)
            .OrderBy(j => j.Nome)
            .ToListAsync();

        var idsDosMembros = membros.Select(j => j.Id).ToList();

        var pontos = await _estatisticas.ObterPontosPorJogadorAsync(idsDosMembros);

        // As categorias que cada um aceita jogar — é o que reparte o elenco na tela. Numa
        // consulta só, e não uma por jogador: um time grande faria dezenas de idas ao banco.
        var categoriasPorJogador = (await _context.JogadorCategorias
                .AsNoTracking()
                .Where(jc => idsDosMembros.Contains(jc.JogadorId))
                .Select(jc => new { jc.JogadorId, Nome = jc.CategoriaPadrao.Nome })
                .ToListAsync())
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());

        var trofeus = await TitulosDoTime.TrofeusPorJogadorAsync(_context, idsDosMembros);

        var administradores = await _context.TimeAdministradores
            .Include(a => a.Jogador)
            .Where(a => a.TimeId == id)
            .ToListAsync();

        var idsAdmin = administradores.Select(a => a.JogadorId).ToHashSet();

        // Nome de quem concedeu, numa consulta só em vez de uma por linha.
        var concedentes = await _context.Jogadores
            .Where(j => administradores.Select(a => a.ConcedidoPorId).Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.Nome);

        var meuId = ObterJogadorIdLogado();

        var vm = new TimeDetalheVM
        {
            Time = time,
            Membros = membros
                .Select(j => new MembroTimeVM
                {
                    Jogador = j,
                    Pontos = pontos.GetValueOrDefault(j.Id),
                    EhAdministrador = idsAdmin.Contains(j.Id),
                    Categorias = categoriasPorJogador.GetValueOrDefault(j.Id) ?? new List<string>(),
                    Trofeus = trofeus.GetValueOrDefault(j.Id),
                })
                .OrderByDescending(m => m.EhAdministrador)   // quem administra aparece primeiro
                .ThenByDescending(m => m.Pontos)
                .ThenBy(m => m.Jogador.Nome)
                .ToList(),
            Administradores = administradores
                .Select(a => new AdministradorTimeVM
                {
                    Jogador = a.Jogador,
                    ConcedidoEm = a.ConcedidoEm,
                    ConcedidoPor = a.ConcedidoPorId != null
                        ? concedentes.GetValueOrDefault(a.ConcedidoPorId.Value)
                        : null,
                })
                .OrderBy(a => a.ConcedidoEm)
                .ToList(),
            Sedes = await SedesDoTime.DoTimeAsync(_context, id),
            Titulos = await TitulosDoTime.DoTimeAsync(_context, id),
            MaioresVencedores = TitulosDoTime.MaioresVencedores(membros, trofeus),
            // Só o que passou por ESTE vestiário, e pouca coisa: a tela do time mostra um
            // resumo, e a aba de transferências é quem lista a janela inteira.
            Transferencias = await TransferenciasDeTime.RecentesAsync(_context, id, quantidade: 8),
            SouAdminDoSistema = await SouAdminDoSistemaAsync(meuId),
        };

        // O elenco repartido por categoria sai dos membros JÁ ordenados acima — assim a ordem
        // dentro de cada grupo é a mesma da lista corrida, e não uma segunda regra de ordem.
        vm.Elenco = ElencoPorCategoria.Agrupar(vm.Membros, m => m.Categorias, m => m.Jogador.Sexo);

        vm.Presidente = AdministracaoTime.Presidente(vm.Administradores, a => a.ConcedidoEm);

        vm.PossoGerenciar = AdministracaoTime.PodeGerenciar(
            vm.SouAdminDoSistema, meuId != null && idsAdmin.Contains(meuId.Value));

        // Candidatos a administrador: quem já veste a camisa vem primeiro (é o caso comum),
        // mas a lista é geral porque um time recém-importado não tem membro nenhum — sem
        // isso, designar o primeiro administrador seria impossível pela tela.
        vm.CandidatosAAdministrador = vm.PossoGerenciar
            ? await _context.Jogadores
                .Where(j => !idsAdmin.Contains(j.Id))
                .OrderByDescending(j => j.TimeId == id)
                .ThenBy(j => j.Nome)
                .ToListAsync()
            : new List<Jogador>();

        return View(vm);
    }

    // ── Administradores do time ───────────────────────────────────────────────────────
    // Regra: um time tem VÁRIOS administradores. O primeiro de cada time só entra pela mão
    // de um admin do Padelizou (os 44 times importados do ranking nasceram sem nenhum);
    // daí em diante, um administrador do time inclui o próximo.

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IncluirAdministrador(int timeId, int jogadorId)
    {
        var meuId = ObterJogadorIdLogado();
        if (meuId == null) return Forbid();

        var time = await _context.Times.FindAsync(timeId);
        if (time == null) return NotFound();

        var novo = await _context.Jogadores.FindAsync(jogadorId);
        if (novo == null)
        {
            TempData["Erro"] = "Jogador não encontrado.";
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        var problema = AdministracaoTime.ProblemaParaConceder(
            quemPedeEhAdminDoSistema: await SouAdminDoSistemaAsync(meuId),
            quemPedeEhAdminDoTime: await AdministracaoTime.EhAdministradorAsync(_context, timeId, meuId.Value),
            alvoJaEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId));

        if (problema != null)
        {
            TempData["Erro"] = problema;
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        // Concede o cargo e veste a camisa — a regra inteira mora em AdministracaoTime,
        // porque o painel /Admin/Times faz a mesma coisa e não pode fazer diferente.
        AdministracaoTime.Conceder(_context, timeId, novo, meuId);

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = $"{novo.ComoChamar} agora administra o {time.Nome}.";
        return RedirectToAction("Detalhes", new { id = timeId });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverAdministrador(int timeId, int jogadorId)
    {
        var meuId = ObterJogadorIdLogado();
        if (meuId == null) return Forbid();

        var quantos = await _context.TimeAdministradores.CountAsync(a => a.TimeId == timeId);

        var problema = AdministracaoTime.ProblemaParaRemover(
            quemPedeEhAdminDoSistema: await SouAdminDoSistemaAsync(meuId),
            quemPedeEhAdminDoTime: await AdministracaoTime.EhAdministradorAsync(_context, timeId, meuId.Value),
            alvoEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId),
            quantosAdministradores: quantos);

        if (problema != null)
        {
            TempData["Erro"] = problema;
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        var linha = await _context.TimeAdministradores
            .FirstAsync(a => a.TimeId == timeId && a.JogadorId == jogadorId);
        _context.TimeAdministradores.Remove(linha);
        await _context.SaveChangesAsync();

        // Sai da administração, mas continua no time: perder o cargo não é ser expulso.
        TempData["Sucesso"] = "Administrador removido.";
        return RedirectToAction("Detalhes", new { id = timeId });
    }

    private int? ObterJogadorIdLogado()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private async Task<bool> SouAdminDoSistemaAsync(int? jogadorId)
    {
        if (jogadorId == null) return false;
        return await _context.Jogadores
            .AnyAsync(j => j.Id == jogadorId && (j.IsAdminGeral || j.IsAdminRaiz));
    }
}
