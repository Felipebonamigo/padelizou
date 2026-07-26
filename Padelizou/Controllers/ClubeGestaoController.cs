using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers;

// Gestão da agenda do clube: mapa de ocupação, bloqueio de horário, mensalista e
// financeiro por quadra. Separado de ClubesController (que cuida de cadastro e
// administradores) pra não engordar mais um controller já grande.
[Authorize]
public class ClubeGestaoController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ClubeGestaoController> _logger;

    public ClubeGestaoController(DbPadelContext context, IPushNotificationService pushService,
        ILogger<ClubeGestaoController> logger)
    {
        _context = context;
        _pushService = pushService;
        _logger = logger;
    }

    private int? UsuarioId() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // Dono ou administrador do clube.
    private async Task<bool> PodeGerenciarAsync(int clubeId)
    {
        var id = UsuarioId();
        if (id == null) return false;

        return await _context.Clubes.AnyAsync(c => c.Id == clubeId && c.DonoId == id)
            || await _context.ClubeAdministradores.AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == id);
    }

    // ===================== MAPA DE OCUPAÇÃO =====================

    [HttpGet]
    public async Task<IActionResult> Ocupacao(int id, DateTime? semana)
    {
        if (!await PodeGerenciarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        // Semana começa no domingo, igual ao DayOfWeek do .NET.
        var baseDia = (semana ?? DateTime.Today).Date;
        var inicio = baseDia.AddDays(-(int)baseDia.DayOfWeek);
        var fim = inicio.AddDays(7);

        var quadras = await _context.QuadrasClube
            .Where(q => q.ClubeId == id && q.Ativa)
            .OrderBy(q => q.Nome)
            .ToListAsync();

        var regras = await _context.HorariosMarcacaoDisponivel
            .Where(h => h.ClubeId == id && h.Ativo)
            .ToListAsync();

        var marcacoes = await _context.MarcacoesJogo
            .Include(m => m.Jogador)
            .Where(m => m.ClubeId == id && m.DataHora >= inicio && m.DataHora < fim
                     && m.Status != "Cancelada")
            .ToListAsync();

        // As faixas de horário do mapa saem das regras cadastradas — só mostramos linha
        // pra hora em que o clube realmente abre.
        var horarios = new SortedSet<TimeSpan>();
        foreach (var r in regras)
        {
            for (var h = r.HoraInicio; h < r.HoraFim; h = h.Add(TimeSpan.FromMinutes(r.DuracaoMinutos)))
                horarios.Add(h);
        }

        var vm = new OcupacaoClubeVM
        {
            Clube = clube,
            InicioSemana = inicio,
            Quadras = quadras,
            Horarios = horarios.ToList(),
        };

        foreach (var m in marcacoes)
        {
            var chave = (m.QuadraClubeId, m.DataHora.Date, new TimeSpan(m.DataHora.Hour, m.DataHora.Minute, 0));
            vm.Slots[chave] = new SlotVM
            {
                MarcacaoId = m.Id,
                // No mapa cabe pouco texto — o apelido é o que o dono do clube reconhece.
                Titulo = m.EhBloqueio ? (m.MotivoBloqueio ?? "Bloqueado") : m.Jogador.ComoChamar,
                EhBloqueio = m.EhBloqueio,
                EhMensalista = m.MensalidadeId != null,
                Status = m.Status,
            };
        }

        // Ocupação só conta slot que o clube abriu pra venda naquele dia da semana.
        int totalSlots = 0;
        foreach (var dia in Enumerable.Range(0, 7).Select(i => inicio.AddDays(i)))
        {
            foreach (var q in quadras)
            {
                foreach (var h in vm.Horarios)
                {
                    bool abre = regras.Any(r => r.QuadraClubeId == q.Id
                                             && r.DiaSemana == (int)dia.DayOfWeek
                                             && h >= r.HoraInicio && h < r.HoraFim);
                    if (abre) totalSlots++;
                }
            }
        }

        vm.TotalSlots = totalSlots;
        vm.SlotsOcupados = vm.Slots.Count;
        vm.ReceitaSemana = marcacoes
            .Where(m => !m.EhBloqueio)
            .Sum(m => PrecoDe(regras, m));

        return View(vm);
    }

    // Preço de uma marcação = preço da regra que cobre aquele horário/quadra.
    private static decimal PrecoDe(List<HorarioMarcacaoDisponivel> regras, MarcacaoJogo m)
    {
        var hora = new TimeSpan(m.DataHora.Hour, m.DataHora.Minute, 0);
        var regra = regras.FirstOrDefault(r => r.QuadraClubeId == m.QuadraClubeId
                                            && r.DiaSemana == (int)m.DataHora.DayOfWeek
                                            && hora >= r.HoraInicio && hora < r.HoraFim);
        return regra?.Preco ?? 0;
    }

    // ===================== BLOQUEIO DE HORÁRIO =====================

    [HttpPost]
    public async Task<IActionResult> Bloquear(int clubeId, int quadraId, DateTime dataHora,
        int duracaoMinutos, string motivo)
    {
        if (!await PodeGerenciarAsync(clubeId)) return Forbid();

        bool jaOcupado = await _context.MarcacoesJogo.AnyAsync(m =>
            m.QuadraClubeId == quadraId && m.DataHora == dataHora && m.Status != "Cancelada");

        if (jaOcupado)
        {
            TempData["Erro"] = "Esse horário já está ocupado. Cancele a reserva antes de bloquear.";
            return RedirectToAction("Ocupacao", new { id = clubeId, semana = dataHora });
        }

        _context.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clubeId,
            QuadraClubeId = quadraId,
            JogadorId = UsuarioId()!.Value,   // quem bloqueou
            DataHora = dataHora,
            DuracaoMinutos = duracaoMinutos <= 0 ? 60 : duracaoMinutos,
            Status = "Confirmada",
            EhBloqueio = true,
            MotivoBloqueio = string.IsNullOrWhiteSpace(motivo) ? "Bloqueado" : motivo.Trim(),
        });

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = "Horário bloqueado.";
        return RedirectToAction("Ocupacao", new { id = clubeId, semana = dataHora });
    }

    [HttpPost]
    public async Task<IActionResult> Desbloquear(int marcacaoId)
    {
        var m = await _context.MarcacoesJogo.FindAsync(marcacaoId);
        if (m == null || !m.EhBloqueio) return NotFound();
        if (!await PodeGerenciarAsync(m.ClubeId)) return Forbid();

        var (clubeId, quando) = (m.ClubeId, m.DataHora);
        _context.MarcacoesJogo.Remove(m);
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Bloqueio removido.";
        return RedirectToAction("Ocupacao", new { id = clubeId, semana = quando });
    }

    // ===================== HORÁRIO FIXO (MENSALISTA) =====================

    // Gera as reservas de um mesmo horário toda semana. É o caso mais comum de clube:
    // "terça 19h é do Felipe o mês inteiro".
    [HttpPost]
    public async Task<IActionResult> CriarMensalista(int clubeId, int quadraId, int jogadorId,
        DateTime primeiraData, int duracaoMinutos, int semanas)
    {
        if (!await PodeGerenciarAsync(clubeId)) return Forbid();

        semanas = Math.Clamp(semanas, 1, 52);
        var mensalidadeId = Guid.NewGuid();
        int criadas = 0, puladas = 0;

        for (int i = 0; i < semanas; i++)
        {
            var quando = primeiraData.AddDays(7 * i);

            bool ocupado = await _context.MarcacoesJogo.AnyAsync(m =>
                m.QuadraClubeId == quadraId && m.DataHora == quando && m.Status != "Cancelada");

            if (ocupado) { puladas++; continue; }

            _context.MarcacoesJogo.Add(new MarcacaoJogo
            {
                ClubeId = clubeId,
                QuadraClubeId = quadraId,
                JogadorId = jogadorId,
                DataHora = quando,
                DuracaoMinutos = duracaoMinutos <= 0 ? 60 : duracaoMinutos,
                Status = "Confirmada",
                MensalidadeId = mensalidadeId,
            });
            criadas++;
        }

        await _context.SaveChangesAsync();

        try
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            await _pushService.EnviarParaJogadorAsync(jogadorId,
                "Seu horário fixo está marcado",
                $"{criadas} semana(s) em {clube?.Nome}, {primeiraData:dddd 'às' HH:mm}.",
                Url.Action("MinhasMarcacoes", "MarcarJogo"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao avisar mensalista {JogadorId} do clube {ClubeId}", jogadorId, clubeId);
        }

        TempData["Sucesso"] = puladas == 0
            ? $"Horário fixo criado: {criadas} semana(s)."
            : $"Horário fixo criado: {criadas} semana(s). {puladas} pulada(s) por conflito.";

        return RedirectToAction("Ocupacao", new { id = clubeId, semana = primeiraData });
    }

    [HttpPost]
    public async Task<IActionResult> CancelarMensalista(int clubeId, Guid mensalidadeId)
    {
        if (!await PodeGerenciarAsync(clubeId)) return Forbid();

        // Só o que ainda não aconteceu — histórico não se apaga.
        var futuras = await _context.MarcacoesJogo
            .Where(m => m.MensalidadeId == mensalidadeId && m.DataHora >= DateTime.Now)
            .ToListAsync();

        foreach (var m in futuras)
        {
            m.Status = "Cancelada";
            m.CanceladaEm = DateTime.Now;
            m.CanceladaPor = "Clube";
        }

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = $"{futuras.Count} reserva(s) futura(s) do horário fixo cancelada(s).";
        return RedirectToAction("Ocupacao", new { id = clubeId });
    }

    // ===================== NO-SHOW E POLÍTICA =====================

    [HttpPost]
    public async Task<IActionResult> RegistrarNoShow(int marcacaoId, bool compareceu, bool cobrar = false)
    {
        var m = await _context.MarcacoesJogo.FindAsync(marcacaoId);
        if (m == null) return NotFound();
        if (!await PodeGerenciarAsync(m.ClubeId)) return Forbid();

        m.Status = compareceu ? "Realizada" : "Faltou";
        m.CobrarMesmoAssim = !compareceu && cobrar;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = compareceu ? "Presença registrada." : "Falta registrada.";
        return RedirectToAction("Ocupacao", new { id = m.ClubeId, semana = m.DataHora });
    }

    [HttpPost]
    public async Task<IActionResult> SalvarPolitica(int clubeId, int horasMinimas, bool cobraNoShow, string? texto)
    {
        if (!await PodeGerenciarAsync(clubeId)) return Forbid();

        var clube = await _context.Clubes.FindAsync(clubeId);
        if (clube == null) return NotFound();

        clube.HorasMinimasCancelamento = Math.Clamp(horasMinimas, 0, 168);
        clube.CobraNoShow = cobraNoShow;
        clube.PoliticaCancelamentoTexto = string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = "Política do clube atualizada.";
        return RedirectToAction("Ocupacao", new { id = clubeId });
    }

    // ===================== FINANCEIRO DO CLUBE =====================

    [HttpGet]
    public async Task<IActionResult> Financeiro(int id, string? periodo)
    {
        if (!await PodeGerenciarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var hoje = DateTime.Today;
        periodo = (periodo ?? "mes").Trim().ToLower();

        var (de, rotulo) = periodo switch
        {
            "ano" => (new DateTime(hoje.Year, 1, 1), $"em {hoje.Year}"),
            "sempre" => (DateTime.MinValue, "desde sempre"),
            _ => (new DateTime(hoje.Year, hoje.Month, 1), "neste mês"),
        };

        var regras = await _context.HorariosMarcacaoDisponivel
            .Where(h => h.ClubeId == id)
            .ToListAsync();

        var marcacoes = await _context.MarcacoesJogo
            .Include(m => m.QuadraClube)
            .Where(m => m.ClubeId == id && m.DataHora >= de && !m.EhBloqueio)
            .ToListAsync();

        var valeram = marcacoes.Where(m => m.Status == "Confirmada" || m.Status == "Realizada").ToList();
        var faltas = marcacoes.Where(m => m.Status == "Faltou").ToList();

        var vm = new FinanceiroClubeVM
        {
            Clube = clube,
            Periodo = periodo,
            PeriodoRotulo = rotulo,
            Receita = valeram.Sum(m => PrecoDe(regras, m)),
            Recuperado = faltas.Where(m => m.CobrarMesmoAssim).Sum(m => PrecoDe(regras, m)),
            APerder = faltas.Where(m => !m.CobrarMesmoAssim).Sum(m => PrecoDe(regras, m)),
            Reservas = valeram.Count,
            Cancelamentos = marcacoes.Count(m => m.Status == "Cancelada"),
            NoShows = faltas.Count,
            PorQuadra = valeram
                .GroupBy(m => m.QuadraClube.Nome)
                .Select(g => new ReceitaQuadraVM
                {
                    Quadra = g.Key,
                    Reservas = g.Count(),
                    Receita = g.Sum(m => PrecoDe(regras, m)),
                    Horas = (int)Math.Round(g.Sum(m => m.DuracaoMinutos) / 60.0),
                })
                .OrderByDescending(q => q.Receita)
                .ToList(),
            PorDiaSemana = valeram
                .GroupBy(m => m.DataHora.DayOfWeek)
                .Select(g => new ReceitaDiaVM
                {
                    Dia = g.Key,
                    Reservas = g.Count(),
                    Receita = g.Sum(m => PrecoDe(regras, m)),
                })
                .OrderBy(d => (int)d.Dia)
                .ToList(),
        };

        return View(vm);
    }
}
