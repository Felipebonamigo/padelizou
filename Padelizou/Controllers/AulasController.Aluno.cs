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
    // O lado do ALUNO: a escada de marcar aula (cidade -> professor -> local -> tipo -> horário),
    // a solicitação, "Minhas Aulas" e o cancelamento pela política de 24h.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        // 1. TELA DE BUSCA (cidade -> professor -> local -> horário)
        [HttpGet]
        public async Task<IActionResult> Solicitar()
        {
            var cidadesComProfessor = await _context.ProfessorCidades
                .Where(pc => pc.Professor.IsProfessor)
                .Select(pc => pc.Cidade)
                .Distinct()
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return View(new SolicitarViewModel { Cidades = cidadesComProfessor });
        }

        [HttpGet]
        public async Task<IActionResult> ObterProfessoresPorCidade(int cidadeId)
        {
            var professores = await _context.ProfessorCidades
                .Where(pc => pc.CidadeId == cidadeId && pc.Professor.IsProfessor)
                .Select(pc => new { pc.Professor.Id, pc.Professor.Nome })
                .OrderBy(p => p.Nome)
                .ToListAsync();

            return Json(professores);
        }

        [HttpGet]
        public async Task<IActionResult> ObterLocais(int professorId)
        {
            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .Select(l => new
                {
                    l.Id,
                    l.Nome,
                    l.Endereco,
                    l.PrecoPadrao,
                    // Nulos de propósito quando o professor não anunciou o tamanho: a tela usa
                    // isso pra oferecer só o que ele realmente faz.
                    l.PrecoDupla,
                    l.PrecoTrio,
                    // Vários pacotes por local: a tela monta um <select> com eles.
                    pacotes = l.Pacotes
                        .Where(p => p.Ativo && p.QuantidadeAulas > 1 && p.Preco > 0)
                        .OrderBy(p => p.QuantidadeAulas)
                        .Select(p => new { p.Id, p.QuantidadeAulas, p.Preco })
                        .ToList()
                })
                .ToListAsync();

            return Json(locais);
        }

        [HttpGet]
        public async Task<IActionResult> ObterHorarios(int professorId, int localId)
        {
            var regras = await _context.HorariosDisponiveis
                .Where(h => h.ProfessorId == professorId && h.LocalAulaId == localId && h.Ativo)
                .ToListAsync();

            if (regras.Count == 0)
            {
                return Json(Array.Empty<object>());
            }

            var aulasOcupadas = (await _context.Aulas
                .Where(a => a.ProfessorId == professorId &&
                            (a.Status == "Pendente" || a.Status == "Confirmada") &&
                            a.DataHora >= DateTime.Today)
                .Select(a => a.DataHora)
                .ToListAsync())
                .ToHashSet();

            var slots = new List<DateTime>();
            var hoje = DateTime.Today;

            for (var dia = 0; dia < DiasDeJanelaBusca; dia++)
            {
                var data = hoje.AddDays(dia);
                var regrasDoDia = regras.Where(r => (int)data.DayOfWeek == r.DiaSemana);

                foreach (var regra in regrasDoDia)
                {
                    var horario = data.Add(regra.HoraInicio);
                    var fimJanela = data.Add(regra.HoraFim);

                    while (horario.AddMinutes(regra.DuracaoMinutos) <= fimJanela)
                    {
                        if (horario > DateTime.Now && !aulasOcupadas.Contains(horario))
                        {
                            slots.Add(horario);
                        }
                        horario = horario.AddMinutes(regra.DuracaoMinutos);
                    }
                }
            }

            slots.Sort();
            return Json(slots.Select(s => new { valor = s.ToString("yyyy-MM-ddTHH:mm:ss") }));
        }

        // 2. SALVA A SOLICITAÇÃO (fica Pendente até o professor confirmar) — pode gerar uma aula
        // avulsa, uma série de pacote (quantidade fixa do local) ou uma série fixa semanal.
        [HttpPost]
        public async Task<IActionResult> Solicitar(int professorId, int localId, DateTime dataHora,
            bool ehPacote, bool recorrente, int semanasRecorrencia,
            // Quem chega na quadra: o nome com que o aluno se apresenta nesta aula e quem mais
            // vem com ele. O professor precisa saber isso ANTES de aceitar.
            string? nomeCompleto = null, string? acompanhantes = null,
            // QUAL pacote, agora que o local pode ter vários. Opcional pra uma página aberta
            // antes deste deploy não quebrar: sem id, vale o primeiro pacote ativo.
            int? pacoteId = null,
            // Quantos alunos dividem a quadra (1, 2 ou 3): é o que decide o preço, junto com
            // a tabela do local. Padrão 1 pela mesma razão — aba antiga não manda o campo.
            int quantidadeAlunos = 1)
        {
            var alunoIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(alunoIdValue, out var alunoId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var local = await _context.LocaisAula
                .Include(l => l.Pacotes)
                .FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);
            if (local == null)
            {
                TempData["Erro"] = "Local inválido para este professor.";
                return RedirectToAction("Solicitar");
            }

            // O pacote escolhido tem que ser DESTE local: sem esse filtro, mandar o id de um
            // pacote barato de outro professor compraria a aula cara pelo preço do outro.
            var pacotesValidos = local.Pacotes
                .Where(p => p.Ativo && p.QuantidadeAulas > 1 && p.Preco > 0)
                .OrderBy(p => p.QuantidadeAulas)
                .ToList();

            var pacote = !ehPacote ? null
                : pacoteId is int id ? pacotesValidos.FirstOrDefault(p => p.Id == id)
                : pacotesValidos.FirstOrDefault();

            int quantidade;
            decimal precoPorAula;
            var pacoteValido = pacote != null;

            // Quanto custa a aula deste tamanho pra este aluno: a tabela do local por número
            // de alunos, e o acordo particular dele quando é aula individual. A conta mora em
            // Services/PrecoDaAula — aqui só se decide entre ela e o preço do pacote.
            var alunos = PrecoDaAula.Tamanho(quantidadeAlunos);
            var combinado = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId && p.AlunoId == alunoId)
                .Select(p => (decimal?)p.Preco)
                .FirstOrDefaultAsync();
            var precoAvulso = PrecoDaAula.Sugerido(local, alunos, combinado);

            if (pacoteValido)
            {
                // O pacote tem preço anunciado próprio e ele vale como está: é uma oferta
                // fechada do professor, não uma conta a refazer por tamanho de aula.
                quantidade = pacote!.QuantidadeAulas;
                precoPorAula = pacote.PrecoPorAula;
            }
            else if (recorrente)
            {
                quantidade = Math.Clamp(semanasRecorrencia, MinSemanasRecorrencia, MaxSemanasRecorrencia);
                precoPorAula = precoAvulso;
            }
            else
            {
                quantidade = 1;
                precoPorAula = precoAvulso;
            }

            var recorrenciaId = quantidade > 1 ? Guid.NewGuid() : (Guid?)null;
            var novasAulas = new List<Aula>();
            var puladas = 0;

            for (var i = 0; i < quantidade; i++)
            {
                var horario = dataHora.AddDays(7 * i);

                var ocupado = await _context.Aulas.AnyAsync(a =>
                    a.ProfessorId == professorId &&
                    a.DataHora == horario &&
                    (a.Status == "Pendente" || a.Status == "Confirmada"));

                if (ocupado)
                {
                    puladas++;
                    continue;
                }

                // Ajusta o resto da divisão do pacote na última aula, pra a soma das aulas
                // fechar exatamente com o preço anunciado do pacote.
                var preco = pacoteValido && i == quantidade - 1
                    ? pacote!.Preco - precoPorAula * (quantidade - 1)
                    : precoPorAula;

                novasAulas.Add(new Aula
                {
                    ProfessorId = professorId,
                    AlunoId = alunoId,
                    LocalAulaId = localId,
                    DataHora = horario,
                    Preco = preco,
                    Status = "Pendente",
                    QuantidadeAlunos = alunos,
                    RecorrenciaId = recorrenciaId,
                    // Vazio vira nulo: string em branco no banco depois exige checar as duas
                    // coisas em toda tela que exibe.
                    NomeCompletoAluno = string.IsNullOrWhiteSpace(nomeCompleto) ? null : nomeCompleto.Trim(),
                    Acompanhantes = string.IsNullOrWhiteSpace(acompanhantes) ? null : acompanhantes.Trim(),
                });
            }

            if (novasAulas.Count == 0)
            {
                TempData["Erro"] = "Todos os horários dessa série já estão ocupados. Escolha outro horário.";
                return RedirectToAction("Solicitar");
            }

            _context.Aulas.AddRange(novasAulas);
            await _context.SaveChangesAsync();

            var professor = await _context.Jogadores.FindAsync(professorId);
            var aluno = await _context.Jogadores.FindAsync(alunoId);

            try
            {
                if (novasAulas.Count == 1)
                {
                    var aula = novasAulas[0];
                    var linkAceitar = Url.Action("ConfirmarPorEmail", "Aulas",
                        new { aulaId = aula.Id, token = aula.TokenConfirmacao, aceitar = true }, Request.Scheme);
                    var linkRecusar = Url.Action("ConfirmarPorEmail", "Aulas",
                        new { aulaId = aula.Id, token = aula.TokenConfirmacao, aceitar = false }, Request.Scheme);

                    await _emailService.EnviarAsync(professor!.Email!, professor.Nome,
                        "Nova solicitação de aula - Padelizou",
                        $@"<p>Olá {professor.Nome},</p>
                           <p><strong>{aula.NomeCompletoAluno ?? aluno!.Nome}</strong> solicitou uma aula em
                           <strong>{local.Nome}</strong>
                           no dia <strong>{aula.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>
                           {(aula.Acompanhantes == null ? "" :
                             // Quantos vêm muda o treino e quantas bolas levar — então vai no
                             // e-mail, que é onde o professor decide se aceita.
                             $"<p><strong>Vem mais gente:</strong> {aula.Acompanhantes}</p>")}
                           <p>
                             <a href=""{linkAceitar}"" style=""padding:10px 20px;background:#28a745;color:#fff;text-decoration:none;border-radius:6px;"">Aceitar</a>
                             &nbsp;
                             <a href=""{linkRecusar}"" style=""padding:10px 20px;background:#dc3545;color:#fff;text-decoration:none;border-radius:6px;"">Recusar</a>
                           </p>");
                }
                else
                {
                    var linkAgenda = Url.Action("MinhaAgenda", "Aulas", null, Request.Scheme);
                    var tipoSerie = pacoteValido ? "pacote" : "série fixa semanal";
                    var listaDatas = string.Join("", novasAulas.Select(a => $"<li>{a.DataHora:dd/MM/yyyy 'às' HH:mm}</li>"));

                    await _emailService.EnviarAsync(professor!.Email!, professor.Nome,
                        "Nova solicitação de aulas (série) - Padelizou",
                        $@"<p>Olá {professor.Nome},</p>
                           <p><strong>{aluno!.Nome}</strong> solicitou um {tipoSerie} de {novasAulas.Count} aula(s) em <strong>{local.Nome}</strong>:</p>
                           <ul>{listaDatas}</ul>
                           <p>Acesse sua <a href=""{linkAgenda}"">Minha Agenda</a> para aceitar tudo de uma vez ou aula por aula.</p>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de solicitação para a série de aulas do professor {ProfessorId}", professorId);
            }

            // Push além do e-mail: solicitação de aula é o aviso mais urgente do professor —
            // enquanto ele não responde, o horário fica travado pro aluno.
            try
            {
                var primeira = novasAulas[0];
                await _pushService.EnviarParaJogadorAsync(professorId,
                    "Nova solicitação de aula",
                    novasAulas.Count == 1
                        ? $"{aluno!.Nome} quer aula em {local.Nome}, {primeira.DataHora:dd/MM 'às' HH:mm}."
                        : $"{aluno!.Nome} pediu {novasAulas.Count} aulas em {local.Nome}, a partir de {primeira.DataHora:dd/MM}.",
                    // O professor perde dinheiro se demorar pra responder — e professor não
                    // fica de olho em e-mail entre uma aula e outra.
                    Url.Action("MinhaAgenda", "Aulas"), AlcanceDoAviso.AppEWhatsApp);
            }
            catch (Exception ex)
            {
                // Push é acessório — a solicitação já está gravada e o e-mail já saiu.
                _logger.LogWarning(ex, "Falha ao enviar push de solicitação de aula ao professor {ProfessorId}", professorId);
            }

            return RedirectToAction("SolicitacaoEnviada", new { recorrenciaId, id = novasAulas[0].Id, puladas });
        }

        [HttpGet]
        public async Task<IActionResult> SolicitacaoEnviada(int id, Guid? recorrenciaId, int puladas = 0)
        {
            List<Aula> aulas;

            if (recorrenciaId.HasValue)
            {
                aulas = await _context.Aulas
                    .Include(a => a.Professor)
                    .Include(a => a.LocalAula)
                    .Where(a => a.RecorrenciaId == recorrenciaId)
                    .OrderBy(a => a.DataHora)
                    .ToListAsync();
            }
            else
            {
                var aula = await _context.Aulas
                    .Include(a => a.Professor)
                    .Include(a => a.LocalAula)
                    .FirstOrDefaultAsync(a => a.Id == id);
                aulas = aula != null ? new List<Aula> { aula } : new List<Aula>();
            }

            if (aulas.Count == 0) return NotFound();

            ViewBag.AulasPuladas = puladas;
            return View(aulas);
        }


        // Aluno desmarcando a própria aula. Aplica a política do professor pra decidir
        // se fica marcada como cobrável.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CancelarComoAluno(int aulaId)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas
                .Include(a => a.Professor)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.AlunoId == userId);

            if (aula == null) return NotFound();

            if (!PoliticaAula.ContaComoAtiva(aula.Status))
            {
                TempData["Erro"] = "Essa aula já não está ativa.";
                return RedirectToAction("MinhasAulas");
            }

            var agora = DateTime.Now;
            bool cobra = PoliticaAula.DeveCobrar(aula.Professor, aula, "Aluno", agora);

            aula.Status = PoliticaAula.Cancelada;
            aula.CanceladaEm = agora;
            aula.CanceladaPor = "Aluno";
            aula.HorasDeAntecedenciaCancelamento = PoliticaAula.HorasAntes(aula.DataHora, agora);
            aula.CobrarMesmoFaltando = cobra;

            await _context.SaveChangesAsync();

            try
            {
                await _pushService.EnviarParaJogadorAsync(aula.ProfessorId,
                    "Aula desmarcada",
                    $"O aluno desmarcou a aula de {aula.DataHora:dd/MM 'às' HH:mm}"
                      + (cobra ? " (fora do prazo — marcada como cobrável)." : "."),
                    // Sem isso o professor viaja até a quadra pra não dar aula nenhuma.
                    Url.Action("MinhaAgenda", "Aulas"), AlcanceDoAviso.AppEWhatsApp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar professor do cancelamento da aula {AulaId}", aulaId);
            }

            TempData["Sucesso"] = cobra
                ? "Aula desmarcada. Como foi fora do prazo do professor, ela pode ser cobrada."
                : "Aula desmarcada. Obrigado por avisar!";

            return RedirectToAction("MinhasAulas");
        }

        // 5. TELA DE HISTÓRICO DO ALUNO (Minhas Aulas)
        [HttpGet]
        public async Task<IActionResult> MinhasAulas()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var minhasAulas = await _context.Aulas
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .Where(a => a.AlunoId == userId)
                .OrderByDescending(a => a.DataHora)
                .ToListAsync();

            return View(minhasAulas);
        }

    }
}
