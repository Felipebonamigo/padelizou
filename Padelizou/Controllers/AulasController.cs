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
    [Authorize] // Só quem está logado pode acessar as rotas de aula
    public partial class AulasController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly IPushNotificationService _pushService;
        private readonly PlanoProfessorSettings _plano;
        private readonly ILogger<AulasController> _logger;

        private const int DuracaoPadraoMinutos = 60;

        // Quanto tempo pra frente a busca de aula enxerga. Eram 14 dias, e isso prendia o aluno
        // ao mês corrente: quem quer marcar "dia 5 do mês que vem" não achava a data porque ela
        // não existia na resposta — não era limite de tela, era de dados.
        //
        // 60 dias cobrem o mês seguinte inteiro a partir de QUALQUER dia do mês (mesmo no dia
        // 31 ainda sobram 30). A grade do professor é semanal e se repete, então esticar não
        // inventa horário nenhum: o custo é só tamanho de resposta — uma cidade com 68 horários
        // em 14 dias fica na casa dos 300, que é JSON de dezenas de KB.
        private const int DiasDeJanelaBusca = 60;

        // A cobrança da conta do mês pelo app — a mesma peça que já cobra torneio, aula avulsa
        // e mensalidade, pra taxa e split saírem de um lugar só.
        private readonly IPagamentoInscricaoService _pagamentos;

        public AulasController(
            DbPadelContext context,
            IEmailService emailService,
            IGoogleCalendarService googleCalendarService,
            IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos,
            Microsoft.Extensions.Options.IOptions<PlanoProfessorSettings> plano,
            ILogger<AulasController> logger)
        {
            _context = context;
            _emailService = emailService;
            _googleCalendarService = googleCalendarService;
            _pushService = pushService;
            _pagamentos = pagamentos;
            _plano = plano.Value;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // Antes de abrir o painel, cobra o que falta pro aluno conseguir marcar. Um aviso no
            // topo já existia e não bastou — professor nenhum tinha cidade. Não dá pra entrar em
            // laço: MinhasCidades e MeusLocais não têm esta checagem, e as duas salvam e voltam.
            var pendencia = await CadastroDeProfessor.PendenciaAsync(_context, professorId.Value);

            if (pendencia != PendenciaDoProfessor.Nenhuma)
            {
                TempData["AvisoCadastroProfessor"] = CadastroDeProfessor.MensagemPara(pendencia);
                return RedirectToAction(CadastroDeProfessor.AcaoPara(pendencia));
            }

            var todasAulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId)
                .ToListAsync();

            var hoje = DateTime.Today;
            var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
            var fimSemana = inicioSemana.AddDays(7);

            // Os acordos particulares deste professor, indexados pela mesma chave que agrupa
            // os alunos logo abaixo — é o que casa "o Pedro paga 90" com a linha do Pedro.
            var precosCombinados = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId)
                .ToListAsync();
            var precoPorChave = precosCombinados.ToLookup(p => PrecoDaAula.Chave(p));

            // O cadastro de cada aluno (celular guardado e quem paga), pela MESMA chave.
            var cadastros = await _context.CadastrosDeAlunos
                .Where(f => f.ProfessorId == professorId)
                .ToListAsync();
            var cadastroPorChave = CadastrosDeAlunos.PorChave(cadastros);

            // Contas do Padelizou que atendem pelos celulares cadastrados. Uma consulta só, com
            // os números que interessam — não a base inteira, e não uma ida ao banco por aluno.
            var celulares = cadastros
                .Select(f => f.Celular)
                .Where(c => CadastrosDeAlunos.CelularServeParaAchar(c))
                .Distinct()
                .ToList();

            var contasPorCelular = celulares.Count == 0
                ? new Dictionary<string, (int Id, string Nome)>()
                : (await _context.Jogadores
                        .Where(j => j.Celular != null && celulares.Contains(j.Celular) && j.ExcluidoEm == null)
                        .Select(j => new { j.Id, j.Nome, j.Celular })
                        .ToListAsync())
                    .GroupBy(j => j.Celular!)
                    .ToDictionary(g => g.Key, g => (g.First().Id, g.First().Nome));

            var alunos = todasAulas
                .GroupBy(a => PrecoDaAula.Chave(a))
                .Select(g =>
                {
                    var combinado = precoPorChave[g.Key].FirstOrDefault();
                    cadastroPorChave.TryGetValue(g.Key, out var cadastro);

                    // Só faz sentido sugerir conta pra quem ainda NÃO tem uma: com o vínculo
                    // já feito, o convite viraria ruído permanente na linha do aluno.
                    var sugestao = g.First().AlunoId == null && cadastro?.Celular != null
                                   && contasPorCelular.TryGetValue(cadastro.Celular, out var achada)
                        ? achada
                        : ((int Id, string Nome)?)null;

                    return new AlunoResumo
                    {
                        Nome = g.First().Aluno?.ComoChamar ?? g.First().NomeAlunoAvulso ?? "Aluno avulso",
                        Celular = g.First().Aluno?.Celular ?? g.First().TelefoneAlunoAvulso,
                        TotalAulas = g.Count(a => a.Status != "Cancelada" && a.Status != "Recusada"),
                        UltimaAula = g.Max(a => a.DataHora),
                        ProximaAula = g.Where(a => a.DataHora >= hoje && (a.Status == "Pendente" || a.Status == "Confirmada"))
                                        .OrderBy(a => a.DataHora)
                                        .Select(a => (DateTime?)a.DataHora)
                                        .FirstOrDefault(),
                        AlunoId = g.First().AlunoId,
                        NomeAvulso = g.First().NomeAlunoAvulso,
                        PrecoCombinado = combinado?.Preco,
                        PrecoCombinadoId = combinado?.Id,
                        CelularCadastrado = cadastro?.Celular,
                        ResponsavelNome = cadastro?.ResponsavelNome,
                        ResponsavelCelular = cadastro?.ResponsavelCelular,
                        ResponsavelCpf = cadastro?.ResponsavelCpf,
                        Observacao = cadastro?.Observacao,
                        ContaSugeridaId = sugestao?.Id,
                        ContaSugeridaNome = sugestao?.Nome,
                    };
                })
                .OrderByDescending(a => a.UltimaAula)
                .ToList();

            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId)
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();

            var cidades = await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .Select(pc => pc.Cidade)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
            var fimMes = inicioMes.AddMonths(1).AddSeconds(-1);
            var financeiro = await CalcularRelatorioAsync(professorId.Value, inicioMes, fimMes);

            var painel = new PainelProfessorViewModel
            {
                TotalAlunosAtivos = alunos.Count,
                AulasEstaSemana = todasAulas.Count(a => a.DataHora >= inicioSemana && a.DataHora < fimSemana &&
                                                         (a.Status == "Pendente" || a.Status == "Confirmada")),
                AulasPendentes = todasAulas.Count(a => a.Status == "Pendente"),
                FinanceiroMesAtual = financeiro,
                Alunos = alunos,
                Locais = locais,
                ProximasAulas = todasAulas
                    .Where(a => a.DataHora >= hoje && (a.Status == "Pendente" || a.Status == "Confirmada"))
                    .OrderBy(a => a.DataHora)
                    .Take(10)
                    .ToList(),
                MinhasCidades = cidades
            };

            ViewBag.ProfessorId = professorId;
            var professor = await _context.Jogadores.FindAsync(professorId);
            ViewBag.Professor = professor;

            // O relógio dos 15 dias de teste do plano começa na primeira visita ao painel —
            // é aqui que o professor passa a ver a contagem (ver Services/PlanoDoProfessor).
            if (professor != null)
            {
                if (professor.TesteProfessorInicio == null)
                {
                    professor.TesteProfessorInicio = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                ViewBag.SituacaoPlano = PlanoDoProfessor.SituacaoDe(professor, DateTime.Now, _plano);
                ViewBag.FimDoTestePlano = PlanoDoProfessor.FimDoTeste(professor, _plano);
            }

            return View(painel);
        }


        // A agenda do professor já tem alguma coisa NESSE PEDAÇO DE TEMPO?
        //
        // A comparação é de intervalo, não de horário de início: desde que a aula tem duração
        // (10/08/2026), marcar 18h numa quadra ocupada das 17h às 19h é conflito. A conta roda
        // em memória de propósito — são as aulas de um dia, e a soma de DataHora + duração
        // dentro do SQL depende de tradução de `AddMinutes` que muda com o provedor.
        private async Task<bool> HorarioOcupadoAsync(int professorId, DateTime inicio, int duracaoMinutos,
            int? ignorarAulaId = null, Guid? ignorarTurmaId = null)
        {
            // Janela de um dia pra cada lado: aula que começa 23h e dura 2h termina no dia seguinte.
            var de = inicio.Date.AddDays(-1);
            var ate = inicio.Date.AddDays(2);

            var vizinhas = await _context.Aulas
                .Where(a => a.ProfessorId == professorId
                         && a.DataHora >= de && a.DataHora < ate
                         && (a.Status == PoliticaAula.Pendente || a.Status == PoliticaAula.Confirmada)
                         && (ignorarAulaId == null || a.Id != ignorarAulaId)
                         // Colega de TURMA não é conflito — é a mesma sessão (ver Models/Aula.TurmaId
                         // e a mesma exclusão em Services/RenovacaoDaAulaFixa). Sem isto, editar o
                         // horário de UM aluno de uma turma esbarraria nos próprios colegas.
                         && (ignorarTurmaId == null || a.TurmaId != ignorarTurmaId))
                .Select(a => new { a.DataHora, a.DuracaoMinutos })
                .ToListAsync();

            return vizinhas.Any(v => DuracaoDaAula.Conflita(inicio, duracaoMinutos, v.DataHora, v.DuracaoMinutos));
        }

        // Quanto dura a aula naquele horário, segundo a GRADE do professor (a mesma regra que
        // gerou o slot que o aluno escolheu). Sem regra que cubra o horário — professor que
        // mudou a grade depois de publicar, ou pedido fora dela — vale a duração padrão.
        private async Task<int> DuracaoDaGradeAsync(int professorId, int localId, DateTime quando)
        {
            var hora = quando.TimeOfDay;

            var regra = await _context.HorariosDisponiveis
                .Where(h => h.ProfessorId == professorId && h.LocalAulaId == localId && h.Ativo
                         && h.DiaSemana == (int)quando.DayOfWeek
                         && h.HoraInicio <= hora && h.HoraFim > hora)
                .Select(h => (int?)h.DuracaoMinutos)
                .FirstOrDefaultAsync();

            return DuracaoDaAula.Valida(regra);
        }

        private async Task<int?> ObterProfessorLogadoAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return null;
            }

            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && jogador.IsProfessor ? userId : null;
        }

        // ------------------------------------------------------------------
        // Anotações da aula: o caderno compartilhado entre professor e aluno.
        // Cada linha guarda quem escreveu — a regra de quem participa mora em
        // Services/AnotacoesDeAula.
        // ------------------------------------------------------------------

    }
}
