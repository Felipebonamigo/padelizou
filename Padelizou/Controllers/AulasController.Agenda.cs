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

        // Cria (ou completa) a ficha do aluno a partir do que o professor digitou pra marcar a
        // aula. Ver Models/CadastroDoAluno.
        //
        // ⚠️ NUNCA APAGA o que já está lá: campo em branco no formulário significa "não digitei
        // agora", não "apague o telefone que você já tinha". O professor marca aula sem
        // telefone o tempo todo — é opcional desde 04/08 — e cada uma dessas apagaria o número
        // que ele cadastrou na primeira.
        private async Task GravarFichaRapidaAsync(int professorId, int? alunoId, string nome, string? celular)
        {
            var nomeLimpo = nome.Trim();

            var fichas = await _context.CadastrosDeAlunos
                .Where(f => f.ProfessorId == professorId)
                .ToListAsync();

            var ficha = CadastrosDeAlunos.Achar(fichas, alunoId, nomeLimpo);
            var numero = CadastrosDeAlunos.CelularServeParaAchar(celular)
                ? CadastrosDeAlunos.CelularNormalizado(celular)
                : null;

            if (ficha == null)
            {
                _context.CadastrosDeAlunos.Add(new CadastroDoAluno
                {
                    ProfessorId = professorId,
                    // Aluno com conta entra pela conta; sem conta, pelo nome — a mesma divisão
                    // que a agenda e o acordo de preço já fazem (ver PrecoDaAula.Chave).
                    AlunoId = alunoId,
                    NomeAvulso = alunoId == null ? nomeLimpo : null,
                    Celular = numero,
                });
            }
            else if (numero != null)
            {
                ficha.Celular = numero;
            }

            await _context.SaveChangesAsync();
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
            int? duracaoMinutos = null, bool semPrazo = false,
            List<string>? nomesAlunos = null, List<int?>? alunoIds = null, List<string?>? telefonesAlunos = null)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var quantidadeAlunosValida = PrecoDaAula.Tamanho(quantidadeAlunos);

            // `nomesAlunos` só chega preenchido quando o professor usa a tela nova de "cada um
            // sua cobrança" (ver AdicionarManual.cshtml). Sem ele, é a aula de sempre: um nome
            // só, mesmo numa turma de 3 — os outros dois, se existirem, são "Acompanhantes",
            // que esta ação nem vê (campo de outra tela).
            var multiplo = quantidadeAlunosValida > 1 && nomesAlunos != null && nomesAlunos.Count > 0;

            var entradas = new List<(string Nome, int? AlunoId, string? Telefone)>();
            if (multiplo)
            {
                for (var i = 0; i < quantidadeAlunosValida; i++)
                {
                    var nome = (i < nomesAlunos!.Count ? nomesAlunos[i] : null)?.Trim();
                    if (string.IsNullOrWhiteSpace(nome))
                    {
                        TempData["Erro"] = $"Informe o nome dos {quantidadeAlunosValida} alunos dessa aula.";
                        return RedirectToAction("AdicionarManual");
                    }

                    entradas.Add((
                        nome!,
                        alunoIds != null && i < alunoIds.Count ? alunoIds[i] : null,
                        telefonesAlunos != null && i < telefonesAlunos.Count ? telefonesAlunos[i] : null));
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(nomeAluno))
                {
                    TempData["Erro"] = "Informe o nome do aluno.";
                    return RedirectToAction("AdicionarManual");
                }

                entradas.Add((nomeAluno.Trim(), alunoId, telefoneAluno));
            }

            // O telefone deixou de ser obrigatório em 04/08/2026. Quem marca a aula é o
            // PROFESSOR, pelo aluno, e ele quase nunca tem o número na mão na beira da
            // quadra — exigir isso fazia ele parar a marcação pra ir atrás do contato de
            // alguém que ele já conhece. O campo continua, opcional.

            // As duas colunas são varchar e o Postgres recusa o que passa do tamanho — o
            // professor perderia a aula inteira num erro 500 por colar um nome comprido.
            foreach (var entrada in entradas)
            {
                var textoLongo = LimitesDeTexto.Problema(entrada.Nome, LimitesDeTexto.NomeDeAlunoAvulso, "O nome do aluno")
                                 ?? LimitesDeTexto.Problema(entrada.Telefone, LimitesDeTexto.TelefoneDeAlunoAvulso, "O telefone");
                if (textoLongo != null)
                {
                    TempData["Erro"] = textoLongo;
                    return RedirectToAction("AdicionarManual");
                }
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

            // Aluno escolhido da lista TEM conta: a aula passa a apontar pra ela, e não pra um
            // nome solto. É o que faz o aluno enxergar a aula no próprio app, receber o aviso
            // e ver o histórico — coisa que a marcação por nome nunca deu, mesmo quando a
            // pessoa estava cadastrada o tempo todo.
            //
            // O id vem do formulário, então é conferido: sem isto daria pra pendurar aula na
            // conta de qualquer pessoa mandando outro número. Feito pra todos de uma vez —
            // um só round trip mesmo com N alunos na turma.
            var idsInformados = entradas.Where(e => e.AlunoId.HasValue).Select(e => e.AlunoId!.Value).Distinct().ToList();
            var jogadoresValidos = idsInformados.Count > 0
                ? (await _context.Jogadores.Where(j => idsInformados.Contains(j.Id)).ToListAsync())
                    .ToDictionary(j => j.Id)
                : new Dictionary<int, Jogador>();

            // Quantos alunos e, com isso, quanto custa. O campo `preco` continua mandando
            // quando vem preenchido — a tela sugere, o professor decide. Sem ele, a conta é a
            // de Services/PrecoDaAula: tamanho da aula, e o acordo com aquele aluno na
            // individual (só existe quando a aula tem UM aluno — Sugerido já faz essa conta).
            // Refeita aqui no servidor porque o valor que chega do formulário é o que o
            // navegador quis mandar.
            //
            // Com N alunos cobrados à parte, o total da turma é rachado em N fatias que somam
            // exatas (ver PrecoDaAula.DivididoIgualmente) — cada linha de Aula leva a sua.
            decimal? combinadoUnico = null;
            if (!multiplo)
            {
                var idUnico = entradas[0].AlunoId is int aid && jogadoresValidos.ContainsKey(aid) ? aid : (int?)null;
                var combinados = await PrecosCombinadosDoProfessorAsync(professorId.Value);
                combinados.TryGetValue(PrecoDaAula.Chave(idUnico, entradas[0].Nome), out var combinado);
                combinadoUnico = combinado > 0 ? combinado : null;
            }
            var precoTotal = preco ?? PrecoDaAula.Sugerido(local, quantidadeAlunosValida, combinadoUnico);
            var fatias = PrecoDaAula.DivididoIgualmente(precoTotal, entradas.Count);

            // Série sem prazo é série mesmo com uma aula só marcada até agora: sem o id, o
            // renovador não teria como achar a repetição pra continuar. Cada aluno tem a
            // PRÓPRIA série — um sai da turma, os outros continuam sem quebrar nada (ver
            // Models/Aula.TurmaId).
            var recorrenciaIdsPorAluno = entradas
                .Select(_ => horarios.Count > 1 || semPrazoDeVerdade ? Guid.NewGuid() : (Guid?)null)
                .ToList();

            // Uma turma inteira, uma identidade só — ESTÁVEL pra sempre (não por sessão): é o
            // que deixa a renovação semanal (Services/RenovacaoDaAulaFixa) reconhecer "estas
            // aulas são a mesma turma" mesmo com cada aluno tendo a própria RecorrenciaId, e
            // por isso mesmo poder DISTINGUIR os colegas de turma (mesmo horário de propósito)
            // de um conflito de verdade (outra coisa marcada na mesma quadra).
            var turmaId = entradas.Count > 1 ? Guid.NewGuid() : (Guid?)null;

            var novasAulas = new List<Aula>();
            var grupos = new List<List<Aula>>();
            var puladas = 0;

            foreach (var horario in horarios)
            {
                // Conflito é SOBREPOSIÇÃO, não igualdade de horário: com aula de 1h30 e 2h,
                // comparar só o início deixava 17:00–19:00 conviver com 18:00–19:00. Um
                // conflito bloqueia a SESSÃO inteira — não dá pra criar 2 dos 3 alunos e pular
                // o terceiro, os três jogam junto na mesma quadra.
                if (await HorarioOcupadoAsync(professorId.Value, horario, duracao))
                {
                    puladas++;
                    continue;
                }

                var grupo = new List<Aula>();

                for (var i = 0; i < entradas.Count; i++)
                {
                    var (nome, aid, tel) = entradas[i];
                    var idResolvido = aid is int candidato && jogadoresValidos.ContainsKey(candidato) ? aid : (int?)null;

                    grupo.Add(new Aula
                    {
                        DuracaoMinutos = duracao,
                        RecorrenciaSemFim = semPrazoDeVerdade,
                        ProfessorId = professorId.Value,
                        AlunoId = idResolvido,
                        // O nome escrito fica MESMO com conta vinculada: é como o professor chama
                        // a pessoa, e é o que ele procura depois na agenda.
                        NomeAlunoAvulso = nome,
                        TelefoneAlunoAvulso = string.IsNullOrWhiteSpace(tel) ? null : tel!.Trim(),
                        LocalAulaId = localId,
                        LocalAula = local,
                        DataHora = horario,
                        Preco = fatias[i],
                        QuantidadeAlunos = quantidadeAlunosValida,
                        TurmaId = turmaId,
                        AlunoPagaQuadra = alunoPagaQuadra,
                        Status = "Confirmada",
                        RecorrenciaId = recorrenciaIdsPorAluno[i]
                    });
                }

                novasAulas.AddRange(grupo);
                grupos.Add(grupo);
            }

            if (novasAulas.Count > 0)
            {
                _context.Aulas.AddRange(novasAulas);
                await _context.SaveChangesAsync();

                // Um evento só por SESSÃO, não por aluno: os N alunos de uma turma jogam junto
                // na mesma quadra, e a Google Agenda do professor não precisa de 3 avisos
                // idênticos com hora e local repetidos. Título leva todos os nomes.
                foreach (var grupo in grupos)
                {
                    try
                    {
                        var representante = grupo[0];
                        var aulaParaOEvento = grupo.Count > 1
                            ? new Aula
                            {
                                ProfessorId = representante.ProfessorId,
                                LocalAula = representante.LocalAula,
                                DataHora = representante.DataHora,
                                DuracaoMinutos = representante.DuracaoMinutos,
                                NomeAlunoAvulso = NomesJuntos(grupo.Select(a => a.NomeAlunoAvulso)),
                            }
                            : representante;

                        var eventId = await _googleCalendarService.CriarEventoAsync(aulaParaOEvento);
                        if (eventId != null)
                        {
                            foreach (var aula in grupo) aula.GoogleEventId = eventId;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para a aula manual {AulaId}", grupo[0].Id);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // O CADASTRO RÁPIDO acontece aqui, e não numa tela à parte: o professor já digitou
            // nome e telefone pra marcar a aula, e pedir os mesmos dados de novo noutro lugar é
            // o atrito que faz ele não cadastrar ninguém (o pedido do Rafael era literalmente
            // "coloca ali e deu"). A ficha nasce de graça, no fluxo que ele já faz — uma por
            // aluno, mesmo numa turma com N.
            foreach (var (nome, aid, tel) in entradas)
            {
                var idResolvido = aid is int candidato && jogadoresValidos.ContainsKey(candidato) ? aid : (int?)null;
                await GravarFichaRapidaAsync(professorId.Value, idResolvido, nome, tel);
            }

            // O contador conta SESSÕES (o que o professor pensa como "uma aula"), não linhas
            // no banco — uma turma de 3 que virou 3 linhas ainda é "1 aula criada" pra ele.
            var sessoesCriadas = grupos.Count;

            TempData["Sucesso"] = puladas > 0
                ? $"{sessoesCriadas} aula(s) criada(s). {puladas} horário(s) pulado(s) por já estarem ocupados."
                : $"{sessoesCriadas} aula(s) criada(s) com sucesso.";

            return RedirectToAction("MinhaAgenda");
        }

        // "Fulano, Beltrano e Cicrano" — o título do evento único que a turma toda compartilha
        // na Google Agenda. Mesma junção que a agenda usa pro nome no card (ver
        // Services/AgendaDeTurma), que é onde a implementação mora.
        private static string NomesJuntos(IEnumerable<string?> nomes) => AgendaDeTurma.NomesJuntos(nomes);

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
                // "Um card só, com os N nomes" — as linhas de uma mesma turma (TurmaId) viram
                // uma representante na tela; Pendentes e ARecuperar ficam de fora de propósito,
                // são filas por AÇÃO individual, não cards de sessão (ver Services/AgendaDeTurma).
                NoPeriodo = AgendaDeTurma.Colapsar(noPeriodo),
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
                // A tela mostra UM card pra turma inteira (ver Models/Aula.TurmaId) — "Concluir"
                // ou "Cancelar" nesse card precisa valer pros N alunos, senão o professor marca
                // a turma como dada e 2 dos 3 alunos ficam "Confirmada" pra sempre, quietos.
                var linhas = aula.TurmaId != null
                    ? await _context.Aulas
                        .Where(a => a.TurmaId == aula.TurmaId && a.ProfessorId == userId && a.Status == PoliticaAula.Confirmada)
                        .ToListAsync()
                    : new List<Aula> { aula };

                foreach (var linha in linhas)
                {
                    linha.Status = novoStatus;
                    if (novoStatus == PoliticaAula.Realizada) linha.Compareceu = true;
                    if (novoStatus == PoliticaAula.Cancelada)
                    {
                        linha.CanceladaEm = DateTime.Now;
                        linha.CanceladaPor = "Professor";
                    }
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
            // mudasse só o preço. Os colegas de turma (mesmo TurmaId) também saem: são a MESMA
            // sessão no mesmo horário de propósito, não uma aula concorrente.
            if (await HorarioOcupadoAsync(professorId.Value, dataHora, duracao, aula.Id, aula.TurmaId))
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

            // Horário/local/duração são da SESSÃO — valem pra turma inteira (ver
            // Models/Aula.TurmaId). Preço fica de fora de propósito: é a fatia do ALUNO desta
            // linha, os colegas mantêm a própria.
            var colegasDeTurma = aula.TurmaId != null && mudanca.MudouOQueVaiProGoogle
                ? await _context.Aulas
                    .Where(a => a.TurmaId == aula.TurmaId && a.ProfessorId == professorId && a.Id != aula.Id)
                    .ToListAsync()
                : new List<Aula>();

            foreach (var colega in colegasDeTurma)
            {
                colega.DataHora = dataHora;
                colega.DuracaoMinutos = duracao;
                colega.LocalAulaId = local.Id;
                colega.LocalAula = local;
            }

            await _context.SaveChangesAsync();

            // O Google só sabe de horário e local — preço não vai pro evento, e disparar um
            // "atualizado" na agenda do aluno por causa de R$ 10 seria barulho à toa. Os
            // colegas de turma compartilham o MESMO evento (GoogleEventId igual em todas as
            // linhas) — atualizar aqui já atualiza pra turma inteira, sem chamada por aluno.
            if (mudanca.MudouOQueVaiProGoogle)
            {
                try
                {
                    var eventoId = await _googleCalendarService.AtualizarEventoAsync(aula);
                    if (eventoId != aula.GoogleEventId)
                    {
                        aula.GoogleEventId = eventoId;
                        foreach (var colega in colegasDeTurma) colega.GoogleEventId = eventoId;
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

            // A tela mostra UM card pra turma inteira (ver Models/Aula.TurmaId) — "Apagar"
            // dali precisa apagar a turma inteira. Os colegas compartilham o MESMO evento na
            // Google Agenda (GoogleEventId igual nas N linhas): apagar só esta linha removeria
            // o evento pra quem ficasse, e "apagado" na tela mas ainda marcado no Google pra
            // dois terços da turma é pior que os dois problemas separados.
            var linhas = aula.TurmaId != null
                ? await _context.Aulas
                    .Include(a => a.LocalAula)
                    .Where(a => a.TurmaId == aula.TurmaId && a.ProfessorId == professorId)
                    .ToListAsync()
                : new List<Aula> { aula };

            var avisos = linhas
                .Where(a => ExclusaoDeAula.PrecisaAvisarAluno(a, DateTime.Now) && a.AlunoId.HasValue)
                .Select(a => (AlunoId: a.AlunoId!.Value, a.DataHora, Onde: a.LocalAula.Nome))
                .ToList();

            var quando = aula.DataHora;
            var eventoGoogle = aula.GoogleEventId;   // o mesmo em todas as linhas do grupo

            // As anotações caem por cascade (ver DbPadelContext) — são sobre cada aula.
            _context.Aulas.RemoveRange(linhas);
            await _context.SaveChangesAsync();

            if (eventoGoogle != null)
            {
                await _googleCalendarService.RemoverEventoAsync(professorId.Value, eventoGoogle);
            }

            foreach (var (destinatario, quandoDele, ondeDele) in avisos)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(destinatario,
                        "Aula apagada pelo professor",
                        $"A aula de {quandoDele:dd/MM 'às' HH:mm} em {ondeDele} foi apagada da agenda. "
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

            TempData["Sucesso"] = linhas.Count > 1
                ? $"Turma de {quando:dd/MM 'às' HH:mm} apagada ({linhas.Count} alunos)."
                : avisos.Count > 0
                    ? $"Aula de {quando:dd/MM 'às' HH:mm} apagada. O aluno foi avisado."
                    : $"Aula de {quando:dd/MM 'às' HH:mm} apagada.";

            return RedirectToAction("MinhaAgenda");
        }

    }
}
