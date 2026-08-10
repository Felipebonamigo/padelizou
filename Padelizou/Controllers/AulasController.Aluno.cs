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
                .ToListAsync();

            // Esta tela já mostrava só cidade COM professor; o que faltava era não mostrar a
            // mesma cidade duas vezes quando o catálogo tem duas linhas pra ela.
            return View(new SolicitarViewModel { Cidades = CidadesSemRepetir.Agrupar(cidadesComProfessor) });
        }

        [HttpGet]
        public async Task<IActionResult> ObterProfessoresPorCidade(int cidadeId)
        {
            // ⚠️ Todos os ids da mesma cidade: com duas linhas de catálogo pra "Gravataí", o
            // aluno escolhia uma e metade dos professores não aparecia — sem erro nenhum.
            var idsDaCidade = CidadesSemRepetir.IdsDaMesma(cidadeId, await _context.Cidades.ToListAsync());

            var professores = await _context.ProfessorCidades
                .Where(pc => idsDaCidade.Contains(pc.CidadeId) && pc.Professor.IsProfessor)
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

            // O que já está marcado, com a duração de cada um: aula de 2h ocupa DOIS slots de
            // uma hora, e comparar só o horário de início oferecia ao aluno o segundo deles.
            var aulasOcupadas = (await _context.Aulas
                .Where(a => a.ProfessorId == professorId &&
                            (a.Status == "Pendente" || a.Status == "Confirmada") &&
                            a.DataHora >= DateTime.Today)
                .Select(a => new { a.DataHora, a.DuracaoMinutos })
                .ToListAsync())
                .Select(a => (a.DataHora, a.DuracaoMinutos))
                .ToList();

            var slots = new List<(DateTime Quando, int Duracao)>();
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
                        var livre = !aulasOcupadas.Any(o =>
                            DuracaoDaAula.Conflita(horario, regra.DuracaoMinutos, o.DataHora, o.DuracaoMinutos));

                        if (horario > DateTime.Now && livre)
                        {
                            slots.Add((horario, regra.DuracaoMinutos));
                        }
                        horario = horario.AddMinutes(regra.DuracaoMinutos);
                    }
                }
            }

            return Json(slots
                .OrderBy(s => s.Quando)
                .Select(s => new
                {
                    valor = s.Quando.ToString("yyyy-MM-ddTHH:mm:ss"),
                    // A tela mostra a duração junto do horário: "sáb 15/08 09:00 (1h30)". O que
                    // vale mesmo é o que o servidor recalcula no POST — isto é só o rótulo.
                    duracao = DuracaoDaAula.Rotulo(s.Duracao),
                }));
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

            // Quanto dura a aula é a GRADE do professor que diz, não o formulário: o aluno
            // escolheu um slot dela, e um campo de duração vindo do navegador viraria pedido de
            // aula de 4 horas pelo preço de uma.
            var duracao = await DuracaoDaGradeAsync(professorId, localId, dataHora);

            var recorrenciaId = quantidade > 1 ? Guid.NewGuid() : (Guid?)null;
            var novasAulas = new List<Aula>();
            var puladas = 0;

            for (var i = 0; i < quantidade; i++)
            {
                var horario = dataHora.AddDays(7 * i);

                // Sobreposição, e não igualdade: com aula de 1h30 e 2h na agenda, comparar só o
                // início deixava o aluno pedir horário que já estava tomado pela metade.
                if (await HorarioOcupadoAsync(professorId, horario, duracao))
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
                    DuracaoMinutos = duracao,
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
            //
            // ⚠️ SEM E-MAIL aqui desde 09/08/2026, e não é corte de aviso: é corte de DUPLICATA.
            // O e-mail já saiu logo acima, e é o bom — o que leva os botões Aceitar e Recusar.
            // O do funil chegava atrás dele com o mesmo assunto e sem botão nenhum.
            try
            {
                var primeira = novasAulas[0];
                await _pushService.EnviarParaJogadorAsync(professorId,
                    "Nova solicitação de aula",
                    novasAulas.Count == 1
                        ? $"{aluno!.Nome} quer aula em {local.Nome}, {primeira.DataHora:dd/MM 'às' HH:mm}."
                        : $"{aluno!.Nome} pediu {novasAulas.Count} aulas em {local.Nome}, a partir de {primeira.DataHora:dd/MM}.",
                    // Fora do WhatsApp por decisão do Felipe (09/08/2026). O professor tem o
                    // painel e o e-mail, e a solicitação fica lá esperando — nada se perde se
                    // ele vir uma hora depois. Diferente da aula DESMARCADA, que continua no
                    // WhatsApp: essa faz ele viajar até a quadra à toa.
                    Url.Action("MinhaAgenda", "Aulas"), AlcanceDoAviso.AppSemEmail);
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

        // 5. AS AULAS DO ALUNO — a aba "Aulas marcadas", irmã de "Marcar aula".
        //
        // Chega de dois jeitos: pela aba, no alto da tela de marcar (que é onde a pessoa está
        // quando se pergunta "quando mesmo é a minha próxima?"), e pelos links de sempre —
        // perfil, Home e o aviso que o professor manda.
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
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            // A separação é em memória de propósito: são poucas aulas por aluno, e a regra do
            // que "ainda vai acontecer" olha o FIM da aula (início + duração), que o banco
            // não sabe calcular sem espalhar a conta por aqui.
            var agora = DateTime.Now;

            var vm = new MinhasAulasVM
            {
                Proximas = minhasAulas
                    .Where(a => PoliticaAula.AindaVaiAcontecer(a, agora))
                    .ToList(),

                // Tudo o mais, do mais recente pro mais antigo: o que já aconteceu, o que foi
                // desmarcado e o pedido que o professor recusou.
                Anteriores = minhasAulas
                    .Where(a => !PoliticaAula.AindaVaiAcontecer(a, agora))
                    .OrderByDescending(a => a.DataHora)
                    .ToList(),
            };

            return View(vm);
        }

    }
}
