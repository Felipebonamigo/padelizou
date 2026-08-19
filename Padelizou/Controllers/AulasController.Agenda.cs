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
                .Include(l => l.PrecosDeTurma)
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .ToListAsync();

            // A tela sugere o preço sozinha quando o professor escolhe o local e o tamanho da
            // aula, e desconta na hora se o nome digitado for de um aluno com preço combinado.
            // Tudo de uma vez, porque o JS precisa reagir a cada tecla — buscar no servidor a
            // cada letra do nome seria pior em toda quadra com sinal ruim.
            ViewBag.PrecosCombinados = await PrecosCombinadosDoProfessorAsync(professorId.Value);

            // Os alunos que já fizeram aula com ele, pré-carregados pelo mesmo motivo do
            // parágrafo acima: a lista inteira de um professor cabe folgada numa página, e
            // filtrar no navegador funciona com o sinal que a quadra tiver.
            ViewBag.MeusAlunos = await MeusAlunosAsync(professorId.Value);

            return View(locais);
        }

        private async Task<List<AlunoDoProfessor>> MeusAlunosAsync(int professorId)
        {
            var aulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Where(a => a.ProfessorId == professorId)
                .ToListAsync();

            return AlunosDoProfessor.Montar(aulas);
        }

        // Procura no cadastro do Padelizou quem o professor ainda NÃO deu aula.
        //
        // Só roda quando ele pede — a lista dos alunos dele já vem pronta na página, e sair
        // pra rede a cada tecla é justamente o que não funciona na beira da quadra. Devolve
        // também quem já é aluno dele, marcado, pra ele não cadastrar a mesma pessoa duas
        // vezes por não ter reconhecido o nome completo.
        [HttpGet]
        public async Task<IActionResult> BuscarAlunoNoSistema(string? termo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return Forbid();

            var procurado = (termo ?? "").Trim();
            if (procurado.Length < 3)
            {
                return Json(new { erro = "Digite pelo menos 3 letras do nome." });
            }

            // Filtra o grosso no banco e afina sem acento em memória: o Postgres compararia
            // "Jonatas" com "Jônatas" como diferentes, e é exatamente esse par que o
            // professor digita errado.
            var candidatos = await _context.Jogadores
                .Where(j => j.Id != professorId && EF.Functions.ILike(j.Nome, $"%{procurado}%"))
                .OrderBy(j => j.Nome)
                .Take(30)
                .Select(j => new { j.Id, j.Nome, j.Apelido, j.Celular })
                .ToListAsync();

            var jaSaoMeus = (await MeusAlunosAsync(professorId.Value))
                .Where(a => a.AlunoId.HasValue)
                .ToDictionary(a => a.AlunoId!.Value, a => a.TotalDeAulas);

            return Json(new
            {
                encontrados = candidatos.Select(j => new
                {
                    alunoId = j.Id,
                    nome = j.Nome,
                    apelido = j.Apelido,
                    celular = j.Celular,
                    jaFezAulaComigo = jaSaoMeus.ContainsKey(j.Id),
                    recorrente = jaSaoMeus.TryGetValue(j.Id, out var total) && total > 1,
                }),
            });
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
            DateTime dataHora, decimal? preco, bool recorrente, int semanasRecorrencia, int quantidadeAlunos = 1,
            int? alunoId = null, bool alunoPagaQuadra = false, List<string>? datas = null,
            int? duracaoMinutos = null, bool semPrazo = false)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (string.IsNullOrWhiteSpace(nomeAluno))
            {
                TempData["Erro"] = "Informe o nome do aluno.";
                return RedirectToAction("AdicionarManual");
            }

            // O telefone deixou de ser obrigatório em 04/08/2026. Quem marca a aula é o
            // PROFESSOR, pelo aluno, e ele quase nunca tem o número na mão na beira da
            // quadra — exigir isso fazia ele parar a marcação pra ir atrás do contato de
            // alguém que ele já conhece. O campo continua, opcional.

            // As duas colunas são varchar e o Postgres recusa o que passa do tamanho — o
            // professor perderia a aula inteira num erro 500 por colar um nome comprido.
            var textoLongo = LimitesDeTexto.Problema(nomeAluno, LimitesDeTexto.NomeDeAlunoAvulso, "O nome do aluno")
                             ?? LimitesDeTexto.Problema(telefoneAluno, LimitesDeTexto.TelefoneDeAlunoAvulso, "O telefone");
            if (textoLongo != null)
            {
                TempData["Erro"] = textoLongo;
                return RedirectToAction("AdicionarManual");
            }

            // Include obrigatório: o preço da turma sai de local.PrecosDeTurma, e sem ele
            // toda aula em grupo nasceria pelo valor da individual (ver PrecoDaAula.DoLocal).
            var local = await _context.LocaisAula
                .Include(l => l.PrecosDeTurma)
                .FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);
            if (local == null)
            {
                TempData["Erro"] = "Local inválido.";
                return RedirectToAction("AdicionarManual");
            }

            // As datas da aula fixa. A tela manda a lista que o professor marcou (mês com 5
            // sextas, feriado no meio, a semana em que ele viaja — nada disso cabe num "repetir
            // por N semanas"); a contagem cega continua valendo como plano B, pro navegador
            // sem JS e pra quem mandou o formulário direto.
            //
            // ⚠️ "Sem prazo definido" ignora a lista de datas: a série não tem fim pra caber
            // numa lista. Cria o horizonte de uma vez e o renovador repõe daí pra frente
            // (ver Services/RenovacaoDaAulaFixa) — o professor não precisa voltar aqui.
            var semPrazoDeVerdade = recorrente && semPrazo;

            var escolhidas = recorrente && !semPrazoDeVerdade
                ? DatasDaAulaFixa.Ler(datas, DateTime.Now)
                : new List<DateTime>();

            if (recorrente && !semPrazoDeVerdade && datas != null && datas.Count > 0 && escolhidas.Count == 0)
            {
                TempData["Erro"] = "Escolha pelo menos uma data para a aula fixa.";
                return RedirectToAction("AdicionarManual");
            }

            var horarios = escolhidas.Count > 0
                ? escolhidas
                : DatasDaAulaFixa.Semanais(dataHora,
                    semPrazoDeVerdade ? RenovacaoDaAulaFixa.HorizonteSemanas
                    : recorrente ? Math.Clamp(semanasRecorrencia, MinSemanasRecorrencia, MaxSemanasRecorrencia)
                    : 1);

            var duracao = DuracaoDaAula.Valida(duracaoMinutos);

            // Série sem prazo é série mesmo com uma aula só marcada até agora: sem o id, o
            // renovador não teria como achar a repetição pra continuar.
            var recorrenciaId = horarios.Count > 1 || semPrazoDeVerdade ? Guid.NewGuid() : (Guid?)null;

            // Aluno escolhido da lista TEM conta: a aula passa a apontar pra ela, e não pra um
            // nome solto. É o que faz o aluno enxergar a aula no próprio app, receber o aviso
            // e ver o histórico — coisa que a marcação por nome nunca deu, mesmo quando a
            // pessoa estava cadastrada o tempo todo.
            //
            // O id vem do formulário, então é conferido: sem isto daria pra pendurar aula na
            // conta de qualquer pessoa mandando outro número.
            var alunoVinculado = alunoId is int id
                ? await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == id)
                : null;

            // Quantos alunos e, com isso, quanto custa. O campo `preco` continua mandando
            // quando vem preenchido — a tela sugere, o professor decide. Sem ele, a conta é a
            // de Services/PrecoDaAula: tamanho da aula, e o acordo com aquele aluno na
            // individual. Refeita aqui no servidor porque o valor que chega do formulário é o
            // que o navegador quis mandar.
            var alunos = PrecoDaAula.Tamanho(quantidadeAlunos);
            var combinados = await PrecosCombinadosDoProfessorAsync(professorId.Value);
            combinados.TryGetValue(PrecoDaAula.Chave(alunoVinculado?.Id, nomeAluno), out var combinado);
            var precoDaAula = preco ?? PrecoDaAula.Sugerido(local, alunos, combinado > 0 ? combinado : null);

            var novasAulas = new List<Aula>();
            var puladas = 0;

            foreach (var horario in horarios)
            {
                // Conflito é SOBREPOSIÇÃO, não igualdade de horário: com aula de 1h30 e 2h,
                // comparar só o início deixava 17:00–19:00 conviver com 18:00–19:00.
                if (await HorarioOcupadoAsync(professorId.Value, horario, duracao))
                {
                    puladas++;
                    continue;
                }

                novasAulas.Add(new Aula
                {
                    DuracaoMinutos = duracao,
                    RecorrenciaSemFim = semPrazoDeVerdade,
                    ProfessorId = professorId.Value,
                    AlunoId = alunoVinculado?.Id,
                    // O nome escrito fica MESMO com conta vinculada: é como o professor chama
                    // a pessoa, e é o que ele procura depois na agenda.
                    NomeAlunoAvulso = nomeAluno.Trim(),
                    TelefoneAlunoAvulso = string.IsNullOrWhiteSpace(telefoneAluno) ? null : telefoneAluno.Trim(),
                    LocalAulaId = localId,
                    LocalAula = local,
                    DataHora = horario,
                    Preco = precoDaAula,
                    QuantidadeAlunos = alunos,
                    AlunoPagaQuadra = alunoPagaQuadra,
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
                // A aula que esta repõe: é o que explica, na agenda, uma aula de R$ 0,00.
                .Include(a => a.RecuperaAula)
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

            // A fila de reposição, de QUALQUER data — pelo mesmo motivo das pendentes: a aula
            // que ficou devendo é de semana passada, e some da tela de quem olha esta semana.
            var aRecuperar = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId && a.Status == PoliticaAula.ARecuperar)
                .ToListAsync();

            // Quem já foi encaixado sai da fila. Uma consulta só, e não uma por aula da fila.
            var jaEncaixadas = await _context.Aulas
                .Where(a => a.ProfessorId == professorId && a.RecuperaAulaId != null)
                .Select(a => a.RecuperaAulaId!.Value)
                .ToListAsync();

            var conectado = await _googleCalendarService.EstaConectadoAsync(professorId.Value);

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
                ARecuperar = Reposicao.AindaSemEncaixe(aRecuperar, jaEncaixadas.ToHashSet()),
                GoogleConectado = conectado,
                // De QUALQUER data futura, não só da janela na tela: a aula que ficou fora do
                // Google costuma ser justamente a que ele não está olhando agora, e era esse
                // silêncio que fazia "algumas vão, outras não" parecer sorte.
                AulasForaDoGoogle = conectado
                    ? await _context.Aulas.CountAsync(a => a.ProfessorId == professorId
                                                        && a.Status == PoliticaAula.Confirmada
                                                        && a.DataHora >= DateTime.Now
                                                        && a.GoogleEventId == null)
                    : 0,
            };

            // Os locais só são carregados quando há fila: o formulário de encaixe é o único
            // que precisa deles, e a agenda é a tela que o professor mais abre no dia.
            vm.Locais = vm.ARecuperar.Count > 0
                ? await _context.LocaisAula
                    .Where(l => l.ProfessorId == professorId && l.Ativo)
                    .OrderBy(l => l.Nome)
                    .ToListAsync()
                : new List<LocalAula>();

            return View(vm);
        }

        // ENCAIXAR A REPOSIÇÃO: a aula nova que quita a que ficou como "A recuperar".
        // A regra de como ela nasce (o que herda, e por que sem preço) mora em
        // Services/Reposicao, que é onde dá pra testá-la sem banco.
        [HttpPost]
        public async Task<IActionResult> Encaixar(int aulaId, int localId, DateTime dataHora,
            int? duracaoMinutos = null)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var original = await _context.Aulas
                .Include(a => a.Aluno)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (original == null) return NotFound();

            if (original.Status != PoliticaAula.ARecuperar)
            {
                TempData["Erro"] = "Essa aula não está na fila de reposição.";
                return RedirectToAction("MinhaAgenda");
            }

            // Dois cliques no mesmo botão (ou dois celulares abertos na mesma tela) criariam
            // duas reposições pra uma aula só — e o professor daria duas aulas de graça.
            if (await _context.Aulas.AnyAsync(a => a.RecuperaAulaId == original.Id))
            {
                TempData["Erro"] = "Essa aula já tem uma reposição marcada.";
                return RedirectToAction("MinhaAgenda");
            }

            var local = await _context.LocaisAula
                .FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);

            if (local == null)
            {
                TempData["Erro"] = "Local inválido.";
                return RedirectToAction("MinhaAgenda");
            }

            var duracao = DuracaoDaAula.Valida(duracaoMinutos ?? original.DuracaoMinutos);

            if (await HorarioOcupadoAsync(professorId.Value, dataHora, duracao))
            {
                TempData["Erro"] = $"Você já tem outra aula em {dataHora:dd/MM 'às' HH:mm}.";
                return RedirectToAction("MinhaAgenda");
            }

            var reposicao = Reposicao.Encaixar(original, localId, dataHora, duracao);
            _context.Aulas.Add(reposicao);
            await _context.SaveChangesAsync();

            try
            {
                // O evento lê o LOCAL da aula (nome + endereço) e ele não vem carregado numa
                // aula recém-criada — sem esta linha o Google levaria uma referência nula.
                reposicao.LocalAula = local;
                var eventId = await _googleCalendarService.CriarEventoAsync(reposicao);
                if (eventId != null)
                {
                    reposicao.GoogleEventId = eventId;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para a reposição {AulaId}", reposicao.Id);
            }

            // O aluno PRECISA saber: ele combinou repor e agora tem dia e hora. É pessoal,
            // urgente e acionável — os três critérios do WhatsApp (ver Services/AlcanceDoAviso).
            if (original.AlunoId is int destinatario)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(destinatario,
                        "Sua reposição está marcada",
                        $"A aula de {original.DataHora:dd/MM} que você ia repor ficou para "
                        + $"{dataHora:dd/MM 'às' HH:mm} em {local.Nome}.",
                        Url.Action("MinhasAulas", "Aulas"), AlcanceDoAviso.AppEWhatsApp);
                }
                catch (Exception ex)
                {
                    // A reposição já está marcada; falhar o aviso não desfaz nem vira erro na tela.
                    _logger.LogWarning(ex, "Falha ao avisar o aluno {AlunoId} da reposição da aula {AulaId}", destinatario, aulaId);
                }
            }

            TempData["Sucesso"] = $"Reposição marcada para {dataHora:dd/MM 'às' HH:mm}, sem cobrar de novo.";
            return RedirectToAction("MinhaAgenda", new { data = dataHora.ToString("yyyy-MM-dd") });
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

        // 5. EDITAR A AULA — mudar horário, local e valor de uma aula que já está marcada.
        //
        // Até aqui o único conserto era APAGAR e lançar de novo, e isso cobrava caro: as
        // anotações da aula iam junto (cascade), o id mudava — matando o link do caderno que o
        // aluno já tinha — e ele recebia um "aula apagada" seguido de nada. Ver
        // Services/EdicaoDeAula pra regra de quem pode editar e de quem precisa ser avisado.
        [HttpGet]
        public async Task<IActionResult> Editar(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == id && a.ProfessorId == professorId);

            if (aula == null)
            {
                TempData["Erro"] = "Aula não encontrada.";
                return RedirectToAction("MinhaAgenda");
            }

            if (!EdicaoDeAula.PodeEditar(aula))
            {
                TempData["Erro"] = EdicaoDeAula.MotivoDeNaoPoderEditar(aula);
                return RedirectToAction("MinhaAgenda");
            }

            ViewBag.Locais = await LocaisParaEscolherAsync(professorId.Value, aula.LocalAulaId);
            return View(aula);
        }

        [HttpPost]
        public async Task<IActionResult> Editar(int aulaId, int localId, DateTime dataHora, decimal preco,
            int? duracaoMinutos = null)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O ProfessorId no filtro é a autorização: sem ele, qualquer professor logado
            // remarcaria a aula de qualquer outro só mandando o id.
            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (aula == null)
            {
                TempData["Erro"] = "Aula não encontrada.";
                return RedirectToAction("MinhaAgenda");
            }

            if (!EdicaoDeAula.PodeEditar(aula))
            {
                TempData["Erro"] = EdicaoDeAula.MotivoDeNaoPoderEditar(aula);
                return RedirectToAction("MinhaAgenda");
            }

            var local = await _context.LocaisAula
                .Include(l => l.PrecosDeTurma)
                .FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);

            if (local == null)
            {
                TempData["Erro"] = "Local inválido.";
                return RedirectToAction(nameof(Editar), new { id = aulaId });
            }

            if (preco < 0)
            {
                TempData["Erro"] = "O valor não pode ser negativo.";
                return RedirectToAction(nameof(Editar), new { id = aulaId });
            }

            // Duração ausente no formulário = a que a aula já tinha. Editar o preço numa aba
            // antiga não pode encolher a aula de 2h pra 1h em silêncio.
            var duracao = duracaoMinutos == null ? aula.DuracaoMinutos : DuracaoDaAula.Valida(duracaoMinutos);

            // Mesma trava da marcação: dois alunos no mesmo horário é o erro que a agenda
            // existe pra evitar — e desde que a aula tem duração, a conta é de SOBREPOSIÇÃO.
            // A própria aula sai da conta: senão ela bloquearia a si mesma quando o professor
            // mudasse só o preço.
            if (await HorarioOcupadoAsync(professorId.Value, dataHora, duracao, aula.Id))
            {
                TempData["Erro"] = $"Você já tem outra aula em {dataHora:dd/MM 'às' HH:mm}.";
                return RedirectToAction(nameof(Editar), new { id = aulaId });
            }

            var mudanca = new MudancaDaAula(
                aula.DataHora, dataHora,
                aula.LocalAula.Nome, local.Nome,
                aula.Preco, preco,
                aula.DuracaoMinutos, duracao);

            if (!mudanca.MudouAlgo)
            {
                TempData["Sucesso"] = "Nada mudou nessa aula.";
                return RedirectToAction("MinhaAgenda", new { data = aula.DataHora.ToString("yyyy-MM-dd") });
            }

            aula.DataHora = dataHora;
            aula.DuracaoMinutos = duracao;
            aula.LocalAulaId = local.Id;
            aula.LocalAula = local;
            aula.Preco = preco;
            await _context.SaveChangesAsync();

            // O Google só sabe de horário e local — preço não vai pro evento, e disparar um
            // "atualizado" na agenda do aluno por causa de R$ 10 seria barulho à toa.
            if (mudanca.MudouOQueVaiProGoogle)
            {
                try
                {
                    var eventoId = await _googleCalendarService.AtualizarEventoAsync(aula);
                    if (eventoId != aula.GoogleEventId)
                    {
                        aula.GoogleEventId = eventoId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    // A aula já foi corrigida aqui; falhar no Google não desfaz nem justifica
                    // erro na tela. O aviso de "fora do Google" na agenda mostra o estrago.
                    _logger.LogWarning(ex, "Falha ao atualizar a aula {AulaId} na Google Agenda", aula.Id);
                }
            }

            if (EdicaoDeAula.PrecisaAvisarAluno(aula, mudanca, DateTime.Now))
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(aula.AlunoId!.Value,
                        "Sua aula mudou",
                        $"A aula com {aula.Professor?.ComoChamar ?? "seu professor"}: {EdicaoDeAula.Recado(mudanca)}.",
                        Url.Action("MinhasAulas", "Aulas"),
                        EdicaoDeAula.CanalDoAviso(mudanca));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao avisar o aluno da mudança na aula {AulaId}", aula.Id);
                }
            }

            TempData["Sucesso"] = EdicaoDeAula.ResumoParaOProfessor(mudanca);

            // Aluno sem conta não recebe aviso nenhum — não há pra onde mandar. O professor
            // combina por fora, e a tela já entrega a mensagem pronta pra ele mandar.
            var celular = aula.Aluno?.Celular ?? aula.TelefoneAlunoAvulso;
            if (!aula.AlunoId.HasValue && !string.IsNullOrWhiteSpace(celular) && mudanca.MudouOQueVaiProGoogle)
            {
                TempData["WhatsAppLink"] = WhatsAppLinkHelper.GerarLink(celular,
                    $"Olá, {aula.NomeAlunoAvulso}! Mudança na nossa aula: {EdicaoDeAula.Recado(mudanca)}.");
            }

            // Volta na semana da aula NOVA: remarcar pro mês que vem e cair na semana de onde
            // ela saiu deixa o professor achando que a mudança não pegou.
            return RedirectToAction("MinhaAgenda", new { data = dataHora.ToString("yyyy-MM-dd") });
        }

        // ENCERRA A REPETIÇÃO de uma aula fixa sem prazo.
        //
        // Não apaga nada: as aulas que já estão na agenda seguem valendo (o aluno já contou com
        // elas, e algumas podem estar pagas). O que para é a REPOSIÇÃO — daqui pra frente o
        // renovador não cria mais nenhuma. Apagar as futuras continua sendo aula por aula, que
        // é o certo pra uma decisão dessas.
        [HttpPost]
        public async Task<IActionResult> EncerrarRepeticao(int aulaId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (aula?.RecorrenciaId == null)
            {
                TempData["Erro"] = "Essa aula não faz parte de uma série sem prazo.";
                return RedirectToAction("MinhaAgenda");
            }

            var daSerie = await _context.Aulas
                .Where(a => a.RecorrenciaId == aula.RecorrenciaId && a.ProfessorId == professorId && a.RecorrenciaSemFim)
                .ToListAsync();

            foreach (var irma in daSerie) irma.RecorrenciaSemFim = false;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "A aula fixa não vai mais se repetir sozinha. As aulas já marcadas continuam na agenda.";
            return RedirectToAction("MinhaAgenda", new { data = aula.DataHora.ToString("yyyy-MM-dd") });
        }

        // MANDA PRO GOOGLE AS AULAS QUE FICARAM PRA TRÁS.
        //
        // POR QUE ISTO EXISTE: o evento só nascia no instante em que a aula era criada
        // (AdicionarManual) ou aceita (ProcessarDecisaoAsync). Se naquele instante o professor
        // ainda não tinha conectado a conta, ou se a chamada falhou, `CriarEventoAsync` devolvia
        // null, o log levava um aviso que ninguém lê e a aula ficava fora da agenda PRA SEMPRE —
        // nada nunca tentava de novo. Da tela, isso aparecia como "algumas aulas vão pro Google,
        // outras não", sem jeito de saber quais nem por quê.
        //
        // Só aula futura: encher a agenda do professor de eventos de três meses atrás não
        // ajuda ninguém, e cada um deles mandaria convite pro aluno.
        [HttpPost]
        public async Task<IActionResult> SincronizarGoogle()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (!await _googleCalendarService.EstaConectadoAsync(professorId.Value))
            {
                TempData["Erro"] = "Conecte sua Google Agenda antes de sincronizar.";
                return RedirectToAction("MinhaAgenda");
            }

            var pendentesDeEnvio = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId
                         && a.Status == PoliticaAula.Confirmada
                         && a.DataHora >= DateTime.Now
                         && a.GoogleEventId == null)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            var enviadas = 0;
            var falharam = 0;

            foreach (var aula in pendentesDeEnvio)
            {
                try
                {
                    var eventoId = await _googleCalendarService.CriarEventoAsync(aula);
                    if (eventoId != null)
                    {
                        aula.GoogleEventId = eventoId;
                        enviadas++;
                    }
                    else
                    {
                        falharam++;
                    }
                }
                catch (Exception ex)
                {
                    falharam++;
                    _logger.LogWarning(ex, "Falha ao sincronizar a aula {AulaId} com a Google Agenda", aula.Id);
                }
            }

            if (enviadas > 0) await _context.SaveChangesAsync();

            TempData["Sucesso"] = (enviadas, falharam) switch
            {
                (0, 0) => "Sua agenda já estava toda no Google.",
                (_, 0) => $"{enviadas} aula(s) enviada(s) pra sua Google Agenda.",
                (0, _) => $"Nenhuma aula foi enviada — o Google recusou {falharam}. Tente reconectar sua conta.",
                _ => $"{enviadas} aula(s) enviada(s); {falharam} o Google recusou. Tente reconectar sua conta.",
            };

            return RedirectToAction("MinhaAgenda");
        }

        // Os locais que o professor pode escolher. O local ATUAL da aula entra mesmo estando
        // desativado: ele desativou o clube depois de marcar a aula, e a lista sem ele faria
        // o <select> abrir em outro local — trocando por acidente o que ninguém pediu.
        private async Task<List<LocalAula>> LocaisParaEscolherAsync(int professorId, int localAtualId)
        {
            return await _context.LocaisAula
                .Include(l => l.PrecosDeTurma)
                .Where(l => l.ProfessorId == professorId && (l.Ativo || l.Id == localAtualId))
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();
        }

        // 6. APAGAR A AULA — o desfazer de quem lançou errado. Diferente de Cancelar, que é
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
                        // Mesmo motivo do lado do professor: o aluno iria à quadra à toa.
                        Url.Action("MinhasAulas", "Aulas"), AlcanceDoAviso.AppEWhatsApp);
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
