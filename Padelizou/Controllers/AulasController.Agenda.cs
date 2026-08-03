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
    // A agenda do professor: aula lançada à mão (aluno avulso, sem conta), o calendário
    // (dia/semana/mês, ver Services/PeriodoAgenda) e a troca de status.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> AdicionarManual()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .ToListAsync();

            // A tela sugere o preço sozinha quando o professor escolhe o local e o tamanho da
            // aula, e desconta na hora se o nome digitado for de um aluno com preço combinado.
            // Tudo de uma vez, porque o JS precisa reagir a cada tecla — buscar no servidor a
            // cada letra do nome seria pior em toda quadra com sinal ruim.
            ViewBag.PrecosCombinados = await PrecosCombinadosDoProfessorAsync(professorId.Value);

            return View(locais);
        }

        // O mapa de "quem paga quanto" deste professor, pronto pra consulta por chave
        // (ver Services/PrecoDaAula.Chave). Usado pela tela de marcar aula e pelo cálculo
        // do preço na hora de gravar.
        private async Task<Dictionary<string, decimal>> PrecosCombinadosDoProfessorAsync(int professorId)
        {
            var precos = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId)
                .ToListAsync();

            return PrecoDaAula.PorAluno(precos);
        }

        [HttpPost]
        public async Task<IActionResult> AdicionarManual(int localId, string nomeAluno, string? telefoneAluno,
            DateTime dataHora, decimal? preco, bool recorrente, int semanasRecorrencia, int quantidadeAlunos = 1)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (string.IsNullOrWhiteSpace(nomeAluno))
            {
                TempData["Erro"] = "Informe o nome do aluno.";
                return RedirectToAction("AdicionarManual");
            }

            if (string.IsNullOrWhiteSpace(telefoneAluno))
            {
                TempData["Erro"] = "Informe o telefone do aluno.";
                return RedirectToAction("AdicionarManual");
            }

            // As duas colunas são varchar e o Postgres recusa o que passa do tamanho — o
            // professor perderia a aula inteira num erro 500 por colar um nome comprido.
            var textoLongo = LimitesDeTexto.Problema(nomeAluno, LimitesDeTexto.NomeDeAlunoAvulso, "O nome do aluno")
                             ?? LimitesDeTexto.Problema(telefoneAluno, LimitesDeTexto.TelefoneDeAlunoAvulso, "O telefone");
            if (textoLongo != null)
            {
                TempData["Erro"] = textoLongo;
                return RedirectToAction("AdicionarManual");
            }

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);
            if (local == null)
            {
                TempData["Erro"] = "Local inválido.";
                return RedirectToAction("AdicionarManual");
            }

            var quantidade = recorrente ? Math.Clamp(semanasRecorrencia, MinSemanasRecorrencia, MaxSemanasRecorrencia) : 1;
            var recorrenciaId = quantidade > 1 ? Guid.NewGuid() : (Guid?)null;

            // Quantos alunos e, com isso, quanto custa. O campo `preco` continua mandando
            // quando vem preenchido — a tela sugere, o professor decide. Sem ele, a conta é a
            // de Services/PrecoDaAula: tamanho da aula, e o acordo com aquele aluno na
            // individual. Refeita aqui no servidor porque o valor que chega do formulário é o
            // que o navegador quis mandar.
            var alunos = PrecoDaAula.Tamanho(quantidadeAlunos);
            var combinados = await PrecosCombinadosDoProfessorAsync(professorId.Value);
            combinados.TryGetValue(PrecoDaAula.Chave(null, nomeAluno), out var combinado);
            var precoDaAula = preco ?? PrecoDaAula.Sugerido(local, alunos, combinado > 0 ? combinado : null);

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

                novasAulas.Add(new Aula
                {
                    ProfessorId = professorId.Value,
                    AlunoId = null,
                    NomeAlunoAvulso = nomeAluno.Trim(),
                    TelefoneAlunoAvulso = string.IsNullOrWhiteSpace(telefoneAluno) ? null : telefoneAluno.Trim(),
                    LocalAulaId = localId,
                    LocalAula = local,
                    DataHora = horario,
                    Preco = precoDaAula,
                    QuantidadeAlunos = alunos,
                    Status = "Confirmada",
                    RecorrenciaId = recorrenciaId
                });
            }

            if (novasAulas.Count > 0)
            {
                _context.Aulas.AddRange(novasAulas);
                await _context.SaveChangesAsync();

                foreach (var aula in novasAulas)
                {
                    try
                    {
                        var eventId = await _googleCalendarService.CriarEventoAsync(aula);
                        if (eventId != null)
                        {
                            aula.GoogleEventId = eventId;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para a aula manual {AulaId}", aula.Id);
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = puladas > 0
                ? $"{novasAulas.Count} aula(s) criada(s). {puladas} horário(s) pulado(s) por já estarem ocupados."
                : $"{novasAulas.Count} aula(s) criada(s) com sucesso.";

            return RedirectToAction("MinhaAgenda");
        }

        // 3. TELA DE GERENCIAMENTO DO PROFESSOR (Minha Agenda)
        [HttpGet]
        public async Task<IActionResult> MinhaAgenda(string? vista, string? periodo, DateTime? data)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var referencia = (data ?? DateTime.Today).Date;
            var (inicio, fim) = PeriodoAgenda.Janela(periodo, referencia);

            var noPeriodo = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId
                         && a.DataHora >= inicio && a.DataHora < fim)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            // Pendentes de QUALQUER data: uma solicitação pro mês que vem sumiria da tela de
            // quem está olhando esta semana, e o professor perderia o prazo sem nunca ver.
            var pendentes = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId && a.Status == "Pendente")
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            var vm = new AgendaProfessorVM
            {
                Vista = PeriodoAgenda.NormalizarVista(vista),
                Periodo = PeriodoAgenda.Normalizar(periodo),
                Referencia = referencia,
                Inicio = inicio,
                Fim = fim,
                Titulo = PeriodoAgenda.Titulo(periodo, referencia),
                NoPeriodo = noPeriodo,
                Pendentes = pendentes,
                GoogleConectado = await _googleCalendarService.EstaConectadoAsync(professorId.Value),
            };

            return View(vm);
        }

        // 4. ATUALIZAR STATUS DA AULA (Finalizar ou Cancelar) — só a partir de uma aula já Confirmada
        [HttpPost]
        public async Task<IActionResult> AtualizarStatus(int aulaId, string novoStatus)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aula = await _context.Aulas.FindAsync(aulaId);

            var transicaoValida = novoStatus == PoliticaAula.Realizada || novoStatus == PoliticaAula.Cancelada;
            if (aula != null && aula.ProfessorId == userId && aula.Status == PoliticaAula.Confirmada && transicaoValida)
            {
                aula.Status = novoStatus;
                if (novoStatus == PoliticaAula.Realizada) aula.Compareceu = true;
                if (novoStatus == PoliticaAula.Cancelada)
                {
                    aula.CanceladaEm = DateTime.Now;
                    aula.CanceladaPor = "Professor";
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MinhaAgenda");
        }

        // 5. APAGAR A AULA — o desfazer de quem lançou errado. Diferente de Cancelar, que é
        // um fato registrado (ver Services/ExclusaoDeAula).
        [HttpPost]
        public async Task<IActionResult> ExcluirAula(int aulaId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O ProfessorId no filtro é a autorização: sem ele, qualquer professor logado
            // apagaria a aula de qualquer outro só mandando o id.
            var aula = await _context.Aulas
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (aula == null)
            {
                TempData["Erro"] = "Aula não encontrada.";
                return RedirectToAction("MinhaAgenda");
            }

            var avisar = ExclusaoDeAula.PrecisaAvisarAluno(aula, DateTime.Now);
            var alunoId = aula.AlunoId;
            var quando = aula.DataHora;
            var ondeSeria = aula.LocalAula.Nome;
            var eventoGoogle = aula.GoogleEventId;

            // As anotações caem por cascade (ver DbPadelContext) — são sobre esta aula.
            _context.Aulas.Remove(aula);
            await _context.SaveChangesAsync();

            if (eventoGoogle != null)
            {
                await _googleCalendarService.RemoverEventoAsync(professorId.Value, eventoGoogle);
            }

            if (avisar && alunoId is int destinatario)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(destinatario,
                        "Aula apagada pelo professor",
                        $"A aula de {quando:dd/MM 'às' HH:mm} em {ondeSeria} foi apagada da agenda. "
                        + "Fale com seu professor se não era pra ser.",
                        Url.Action("MinhasAulas", "Aulas"));
                }
                catch (Exception ex)
                {
                    // A aula já foi apagada; falhar o aviso não desfaz nem justifica erro na tela.
                    _logger.LogWarning(ex, "Falha ao avisar o aluno {AlunoId} da exclusão da aula {AulaId}", destinatario, aulaId);
                }
            }

            TempData["Sucesso"] = avisar
                ? $"Aula de {quando:dd/MM 'às' HH:mm} apagada. O aluno foi avisado."
                : $"Aula de {quando:dd/MM 'às' HH:mm} apagada.";

            return RedirectToAction("MinhaAgenda");
        }

    }
}
