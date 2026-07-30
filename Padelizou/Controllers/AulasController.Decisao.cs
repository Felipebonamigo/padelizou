using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // A decisÃ£o do PROFESSOR sobre a solicitaÃ§Ã£o: aceitar/recusar uma aula, a sÃ©rie inteira,
    // ou pelo link do e-mail (que ele abre fora do site) â€” tudo por ProcessarDecisaoAsync.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        // Chamado a partir da Minha Agenda (professor logado)
        [HttpPost]
        public async Task<IActionResult> ConfirmarSolicitacao(int aulaId, bool aceitar)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var professorId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (aula == null || aula.Status != "Pendente")
            {
                return RedirectToAction("MinhaAgenda");
            }

            var linkWhatsApp = await ProcessarDecisaoAsync(aula, aceitar);
            TempData["Sucesso"] = aceitar ? "Aula confirmada!" : "Solicitação recusada.";
            TempData["WhatsAppLink"] = linkWhatsApp;

            return RedirectToAction("MinhaAgenda");
        }

        // Aceita ou recusa de uma vez todas as aulas Pendentes de uma série (pacote ou fixa
        // semanal) — alternativa ao aceite aula a aula que já existe em ConfirmarSolicitacao.
        [HttpPost]
        public async Task<IActionResult> ConfirmarSerie(Guid recorrenciaId, bool aceitar)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var professorId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .Where(a => a.RecorrenciaId == recorrenciaId && a.ProfessorId == professorId && a.Status == "Pendente")
                .ToListAsync();

            string? linkWhatsApp = null;
            foreach (var aula in aulas)
            {
                linkWhatsApp = await ProcessarDecisaoAsync(aula, aceitar);
            }

            TempData["Sucesso"] = aceitar
                ? $"{aulas.Count} aula(s) da série confirmada(s)!"
                : $"{aulas.Count} aula(s) da série recusada(s).";
            TempData["WhatsAppLink"] = linkWhatsApp;

            return RedirectToAction("MinhaAgenda");
        }

        // Chamado a partir do link enviado por e-mail (sem exigir login)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ConfirmarPorEmail(int aulaId, Guid token, bool aceitar)
        {
            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.TokenConfirmacao == token);

            if (aula == null)
            {
                return NotFound();
            }

            if (aula.Status != "Pendente")
            {
                ViewBag.JaProcessada = true;
                return View(aula);
            }

            var linkWhatsApp = await ProcessarDecisaoAsync(aula, aceitar);
            ViewBag.Aceitou = aceitar;
            ViewBag.WhatsAppLink = linkWhatsApp;

            return View(aula);
        }

        // Aplica o aceite/recusa: atualiza status, cria evento no Google Calendar (se aceito e conectado)
        // e dispara o e-mail ao aluno. Retorna o link wa.me pronto para o professor avisar o aluno.
        // Só é chamado a partir do fluxo normal de solicitação (ConfirmarSolicitacao/
        // ConfirmarPorEmail), onde a aula sempre tem um Aluno real — nunca recebe aula avulsa.
        private async Task<string> ProcessarDecisaoAsync(Aula aula, bool aceitar)
        {
            aula.Status = aceitar ? "Confirmada" : "Recusada";
            await _context.SaveChangesAsync();

            if (aceitar)
            {
                try
                {
                    var eventId = await _googleCalendarService.CriarEventoAsync(aula);
                    if (eventId != null)
                    {
                        aula.GoogleEventId = eventId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para a aula {AulaId}", aula.Id);
                }

                try
                {
                    await _emailService.EnviarAsync(aula.Aluno!.Email!, aula.Aluno.Nome,
                        "Sua aula foi confirmada! - Padelizou",
                        $@"<p>Olá {aula.Aluno!.Nome},</p>
                           <p>O professor <strong>{aula.Professor.Nome}</strong> confirmou sua aula em
                           <strong>{aula.LocalAula.Nome}</strong> ({aula.LocalAula.Endereco})
                           no dia <strong>{aula.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de confirmação para a aula {AulaId}", aula.Id);
                }
            }
            else
            {
                try
                {
                    await _emailService.EnviarAsync(aula.Aluno!.Email!, aula.Aluno.Nome,
                        "Sua solicitação de aula foi recusada - Padelizou",
                        $@"<p>Olá {aula.Aluno!.Nome},</p>
                           <p>O professor <strong>{aula.Professor.Nome}</strong> não pôde confirmar a aula
                           no dia <strong>{aula.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>. Tente outro horário.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de recusa para a aula {AulaId}", aula.Id);
                }
            }

            var mensagem = aceitar
                ? $"Olá {aula.Aluno!.Nome}! Sua aula comigo dia {aula.DataHora:dd/MM 'às' HH:mm} em {aula.LocalAula.Nome} está confirmada!"
                : $"Olá {aula.Aluno!.Nome}, infelizmente não vou poder dar a aula dia {aula.DataHora:dd/MM 'às' HH:mm}. Vamos combinar outro horário?";

            return WhatsAppLinkHelper.GerarLink(aula.Aluno!.Celular, mensagem);
        }


    }
}
