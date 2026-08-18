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
    private readonly ModuloDoBar _modulo;
    private readonly ILogger<ClubeGestaoController> _logger;

    public ClubeGestaoController(DbPadelContext context, IPushNotificationService pushService,
        ModuloDoBar modulo, ILogger<ClubeGestaoController> logger)
    {
        _context = context;
        _pushService = pushService;
        _modulo = modulo;
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

    // ===================== PAINEL (a casa do clube) =====================
    //
    // As telas de gestão existiam soltas — mapa da semana, horários e preços, financeiro,
    // política — sem um lugar que respondesse "como está meu clube hoje" nem mostrasse QUEM
    // agendou. Este painel é essa casa: números do dia, próximas reservas com nome e contato,
    // e os atalhos pro resto.
    [HttpGet]
    public async Task<IActionResult> Painel(int id)
    {
        if (!await PodeGerenciarAsync(id)) return Forbid();

        var clube = await _context.Clubes.FindAsync(id);
        if (clube == null) return NotFound();

        var hoje = DateTime.Today;
        var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
        var fimSemana = inicioSemana.AddDays(7);
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);

        var quadras = await _context.QuadrasClube.CountAsync(q => q.ClubeId == id && q.Ativa);

        // O preço não fica gravado na reserva: sai da regra do horário publicado (quadra +
        // dia da semana + faixa). Por isso as regras vêm inteiras e a conta usa o MESMO
        // PrecoDe do Financeiro — dois cálculos de receita divergiriam com o tempo.
        var regras = await _context.HorariosMarcacaoDisponivel
            .Where(h => h.ClubeId == id)
            .ToListAsync();

        // Só reserva de gente conta como reserva; bloqueio é o clube fechando a própria
        // agenda e apareceria como movimento que não existe.
        var reservasDoClube = await _context.MarcacoesJogo
            .Where(m => m.ClubeId == id && m.Status != "Cancelada" && !m.EhBloqueio
                     && m.DataHora >= inicioMes && m.DataHora < fimSemana.AddDays(31))
            .ToListAsync();

        var vm = new PainelClubeVM
        {
            Clube = clube,
            QuadrasAtivas = quadras,
            HorariosPublicados = regras.Count(h => h.Ativo),
            ReservasHoje = reservasDoClube.Count(m => m.DataHora >= hoje && m.DataHora < hoje.AddDays(1)),
            ReservasNaSemana = reservasDoClube.Count(m => m.DataHora >= inicioSemana && m.DataHora < fimSemana),
            ReceitaDoMes = reservasDoClube
                .Where(m => m.DataHora >= inicioMes && m.DataHora < inicioMes.AddMonths(1))
                .Sum(m => PrecoDe(regras, m)),
        };

        // Termômetro de ocupação: quanto do que está publicado pra semana já foi tomado.
        // Aproximação de propósito — o número exato por faixa é o mapa da semana.
        var slotsDaSemana = vm.HorariosPublicados * 7;
        vm.PercentualOcupacaoSemana = slotsDaSemana == 0
            ? 0
            : Math.Min(100, (int)Math.Round(vm.ReservasNaSemana * 100.0 / slotsDaSemana));

        ReservaDoPainelVM MontarLinha(MarcacaoJogo m) => new()
        {
            MarcacaoId = m.Id,
            DataHora = m.DataHora,
            Quadra = m.QuadraClube.Nome,
            QuadraId = m.QuadraClubeId,
            Jogador = m.EhBloqueio ? "" : ReservaDeBalcao.NomeNaAgenda(m),
            Celular = m.EhBloqueio ? null : ReservaDeBalcao.CelularDeContato(m),
            EhMensalista = m.MensalidadeId != null,
            EhBloqueio = m.EhBloqueio,
            MotivoBloqueio = m.MotivoBloqueio,
            Valor = m.EhBloqueio ? null : PrecoDe(regras, m),
            Selo = ReservaDeBalcao.Selo(m, PrecoDe(regras, m)),
            EhBalcao = ReservaDeBalcao.EhBalcao(m),
        };

        // ---- HOJE: o dia inteiro em ordem de hora, reservas E livres ----
        // O dono opera o dia, não o mês. As reservas que já rolaram ficam (é onde ele vê
        // quem ainda "paga lá"); os livres só aparecem de agora em diante, com o botão de
        // marcar — livre das 8h da manhã de ontem não serve pra nada às 20h.
        var doDia = await _context.MarcacoesJogo
            .Include(m => m.QuadraClube)
            .Include(m => m.Jogador)
            .Where(m => m.ClubeId == id && m.Status != "Cancelada"
                     && m.DataHora >= hoje && m.DataHora < hoje.AddDays(1))
            .ToListAsync();

        var quadrasDoClube = await _context.QuadrasClube
            .Where(q => q.ClubeId == id && q.Ativa)
            .OrderBy(q => q.Nome)
            .ToListAsync();

        var linhasDoDia = doDia.Select(MontarLinha).ToList();
        foreach (var q in quadrasDoClube)
        {
            foreach (var r in regras.Where(r => r.Ativo && r.QuadraClubeId == q.Id
                                             && r.DiaSemana == (int)hoje.DayOfWeek))
            {
                for (var h = r.HoraInicio; h.Add(TimeSpan.FromMinutes(r.DuracaoMinutos)) <= r.HoraFim;
                     h = h.Add(TimeSpan.FromMinutes(r.DuracaoMinutos)))
                {
                    var quando = hoje.Add(h);
                    if (quando < DateTime.Now) continue;
                    if (doDia.Any(m => m.QuadraClubeId == q.Id && m.DataHora == quando)) continue;

                    linhasDoDia.Add(new ReservaDoPainelVM
                    {
                        Livre = true,
                        DataHora = quando,
                        Quadra = q.Nome,
                        QuadraId = q.Id,
                        Valor = r.Preco,
                    });
                }
            }
        }
        vm.Hoje = linhasDoDia.OrderBy(l => l.DataHora).ThenBy(l => l.Quadra).ToList();

        // As de amanhã em diante — hoje já tem seção própria, repetir seria ler duas vezes.
        var proximas = await _context.MarcacoesJogo
            .Include(m => m.QuadraClube)
            .Include(m => m.Jogador)
            .Where(m => m.ClubeId == id && m.Status != "Cancelada" && m.DataHora >= hoje.AddDays(1))
            .OrderBy(m => m.DataHora)
            .Take(12)
            .ToListAsync();

        vm.Proximas = proximas.Select(MontarLinha).ToList();

        // Bar e contas ainda estão em construção: enquanto `Bar:Habilitado` estiver
        // desligado, só admin do Padelizou vê os atalhos. Esconder o link é cortesia — a
        // trava de verdade está nos controllers, que repetem a checagem em toda ação.
        ViewBag.MostrarBar = await _modulo.MostrarAtalhoAsync(UsuarioId());
        ViewBag.BarEmConstrucao = _modulo.EmConstrucao;

        // O QUE DISSERAM DO CLUBE, vindo da enquete de fim de torneio. Inclui o que foi
        // escrito sem identificação: o dono do clube é um dos três destinatários combinados
        // com quem respondeu (ver Services/EnqueteDoTorneio) — e o nome não vem junto nem
        // aqui, porque o serviço não o entrega quando a resposta é anônima.
        ViewBag.ComentariosDoClube = await EnqueteDoTorneio.DoClubeAsync(_context, id, limite: 10);

        return View(vm);
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
                // No mapa cabe pouco texto — no balcão vale o nome que o dono digitou;
                // no site, o apelido é o que ele reconhece.
                Titulo = m.EhBloqueio ? (m.MotivoBloqueio ?? "Bloqueado")
                    : ReservaDeBalcao.EhBalcao(m) ? ReservaDeBalcao.NomeNaAgenda(m)
                    : m.Jogador.ComoChamar,
                EhBloqueio = m.EhBloqueio,
                EhMensalista = m.MensalidadeId != null,
                Status = m.Status,
                Selo = ReservaDeBalcao.Selo(m, PrecoDe(regras, m)),
                EhBalcao = ReservaDeBalcao.EhBalcao(m),
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

    // ===================== BALCÃO =====================
    // O cliente ligou ou chamou no WhatsApp e o dono registra aqui — sem isso ele era
    // obrigado a manter o caderninho junto com o sistema (ver Services/ReservaDeBalcao).

    [HttpPost]
    public async Task<IActionResult> MarcarBalcao(int clubeId, int quadraId, DateTime dataHora,
        string nome, string? celular, bool jaPagou = false, string? voltarPara = null)
    {
        if (!await PodeGerenciarAsync(clubeId)) return Forbid();

        IActionResult Voltar() => voltarPara == "Painel"
            ? RedirectToAction("Painel", new { id = clubeId })
            : RedirectToAction("Ocupacao", new { id = clubeId, semana = dataHora });

        if (ReservaDeBalcao.ProblemaCom(nome, dataHora, DateTime.Now) is { } problema)
        {
            TempData["Erro"] = problema;
            return Voltar();
        }

        var quadra = await _context.QuadrasClube.FirstOrDefaultAsync(q => q.Id == quadraId && q.ClubeId == clubeId);
        if (quadra == null) return NotFound();

        // A razão de existir do balcão é impedir venda dupla — então aqui o conflito é
        // recusado com a mesma régua da reserva do site.
        bool jaOcupado = await _context.MarcacoesJogo.AnyAsync(m =>
            m.QuadraClubeId == quadraId && m.DataHora == dataHora && m.Status != "Cancelada");
        if (jaOcupado)
        {
            TempData["Erro"] = "Esse horário já está ocupado.";
            return Voltar();
        }

        celular = Documentos.SomenteDigitosOuNulo(celular);

        // Se o celular bater com uma conta, a reserva aparece no "Minhas marcações" do
        // cliente de graça. Sem celular (ou sem conta), aponta pra quem registrou — o
        // mesmo truque do bloqueio, que mantém o FK obrigatório em paz.
        var clienteComConta = celular == null
            ? null
            : await _context.Jogadores.FirstOrDefaultAsync(j => j.Celular == celular);

        // Duração e preço saem da regra publicada daquele horário; encaixe fora da grade
        // vale também (o dono manda na própria agenda) e cai no padrão de 60 min.
        var regra = await _context.HorariosMarcacaoDisponivel.FirstOrDefaultAsync(h =>
            h.QuadraClubeId == quadraId && h.DiaSemana == (int)dataHora.DayOfWeek
            && h.HoraInicio <= dataHora.TimeOfDay && dataHora.TimeOfDay < h.HoraFim);

        _context.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clubeId,
            QuadraClubeId = quadraId,
            JogadorId = clienteComConta?.Id ?? UsuarioId()!.Value,
            DataHora = dataHora,
            DuracaoMinutos = regra?.DuracaoMinutos ?? 60,
            Status = "Confirmada",
            NomeClienteBalcao = nome.Trim(),
            CelularClienteBalcao = celular,
            PagaNoLocal = true,
            PagoEm = jaPagou ? DateTime.Now : null,
        });
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = $"Reserva de {nome.Trim()} marcada pra {dataHora:dd/MM 'às' HH:mm}.";
        return Voltar();
    }

    // O dono recebeu o dinheiro no balcão e dá o toque. Só marca, nunca desmarca por
    // aqui: "recebi sem querer" se resolve falando com a gente, e um botão de desfazer
    // viraria o jeito fácil de sumir com receita registrada.
    [HttpPost]
    public async Task<IActionResult> ReceberPagamento(int marcacaoId, string? voltarPara = null)
    {
        var m = await _context.MarcacoesJogo.FindAsync(marcacaoId);
        if (m == null || m.EhBloqueio) return NotFound();
        if (!await PodeGerenciarAsync(m.ClubeId)) return Forbid();

        if (m.PagoEm == null)
        {
            m.PagoEm = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Pagamento recebido.";
        }

        return voltarPara == "Ocupacao"
            ? RedirectToAction("Ocupacao", new { id = m.ClubeId, semana = m.DataHora })
            : RedirectToAction("Painel", new { id = m.ClubeId });
    }

    // Só reserva de BALCÃO se cancela por aqui: a do site tem dono com conta, política de
    // cancelamento e possivelmente pagamento online — cancelar essa por cima do jogador
    // é outra conversa, com estorno no meio.
    [HttpPost]
    public async Task<IActionResult> CancelarBalcao(int marcacaoId)
    {
        var m = await _context.MarcacoesJogo.FindAsync(marcacaoId);
        if (m == null || !ReservaDeBalcao.EhBalcao(m)) return NotFound();
        if (!await PodeGerenciarAsync(m.ClubeId)) return Forbid();

        m.Status = "Cancelada";
        m.CanceladaEm = DateTime.Now;
        m.CanceladaPor = "Clube";
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = "Reserva cancelada — o horário voltou a ficar livre.";
        return RedirectToAction("Ocupacao", new { id = m.ClubeId, semana = m.DataHora });
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
            // De onde veio e o que ainda não entrou: balcão × site, e o "paga lá" pendente.
            // É a conta que fecha o caixa — receita "teórica" sozinha não diz quem deve.
            ReceitaBalcao = valeram.Where(ReservaDeBalcao.EhBalcao).Sum(m => PrecoDe(regras, m)),
            AReceber = valeram
                .Where(m => m.PagaNoLocal && m.PagoEm == null)
                .Sum(m => PrecoDe(regras, m)),
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
