using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Services;
// Mesmo empate do AdminController.cs: a ação PixDireto() esconde a classe de regras de mesmo
// nome, e o alias é por ARQUIVO — um partial novo precisa do seu.
using RegrasDoPix = Padelizou.Services.PixDireto;

namespace padelizou.Controllers
{
    // A tela dos professores: quem são, em que pé está o plano de cada um, quem já pagou e
    // quanto entrou por mês (pedido do Felipe, 10/08/2026).
    //
    // As contagens que ela mostra não existiam em lugar nenhum: o /Admin/Financeiro soma o
    // caixa inteiro sem separar por frente, e a tela do professor mostra o plano de UM
    // professor — o dele. Faltava a visão de quem vende.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> Professores(
            [FromServices] IOptions<PlanoProfessorSettings> plano)
        {
            // Tela de leitura, como a de Métricas: qualquer administrador entra, e o assistente
            // do sistema também (a trava dele é o verbo, e aqui não há gravação nenhuma).
            //
            // MISTA, também como a de Métricas: quem dá aula e em que pé está o plano é
            // operação; QUANTO cada um pagou é dinheiro, e some pra quem não é raiz
            // (18/08/2026). A situação do plano ("Assinante", "Vencido") fica pra todos de
            // propósito — é ela que diz se o professor pode dar aula, não o valor.
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.PodeVerDinheiro = PoderesNoSistema.PodeVerDinheiro(admin);

            var agora = DateTime.Now;

            // ⚠️ `ExcluidoEm == null`: quem apagou a conta não é cliente perdido, é linha que
            // não deveria aparecer em contagem nenhuma.
            var professores = await _context.Jogadores
                .Where(j => j.IsProfessor && j.ExcluidoEm == null)
                .AsNoTracking()
                .ToListAsync();

            var ids = professores.Select(p => p.Id).ToList();

            // Só assinatura CONFIRMADA, em qualquer forma de recebimento (Pix direto, gateway
            // ou registrada à mão pelo admin) — cobrança pendente não é dinheiro que entrou.
            var assinaturas = await _context.Pagamentos
                .Where(p => p.Tipo == RegrasDoPix.TipoAssinatura
                         && p.Status == "Confirmado"
                         && ids.Contains(p.JogadorId))
                .AsNoTracking()
                .ToListAsync();

            var aulas = await _context.Aulas
                .Where(a => ids.Contains(a.ProfessorId))
                .AsNoTracking()
                .ToListAsync();

            // Quem concedeu cada cortesia, por NOME. Só consulta se existir alguma — e não há
            // tabela de auditoria no projeto, então essa linha da tela É o rastro que sobra.
            var idsDeQuemDeu = professores
                .Select(p => p.CortesiaConcedidaPorJogadorId)
                .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();

            var nomeDeQuemDeu = idsDeQuemDeu.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Jogadores
                    .Where(j => idsDeQuemDeu.Contains(j.Id))
                    .Select(j => new { j.Id, j.Nome })
                    .ToDictionaryAsync(j => j.Id, j => j.Nome);

            var linhas = ProfessoresNoAdmin.Montar(professores, assinaturas, aulas, agora, plano.Value, nomeDeQuemDeu);
            var meses = ProfessoresNoAdmin.PorMes(assinaturas);

            // A ordem PADRÃO é por quanto cada um pagou — quem sustenta a conta primeiro. Pra
            // quem não vê dinheiro, essa ordem seria o valor vazando pela porta dos fundos (a
            // lista inteira em ordem de faturamento), então a régua vira movimento de aula. É o
            // que a frase da tela promete nos dois casos.
            if (!PoderesNoSistema.PodeVerDinheiro(admin))
                linhas = linhas
                    .OrderByDescending(l => l.AulasNoTotal)
                    .ThenBy(l => l.Nome)
                    .ToList();

            ViewBag.Resumo = ProfessoresNoAdmin.Resumir(linhas, meses, agora);
            ViewBag.Meses = meses;
            ViewBag.Cfg = plano.Value;

            return View(linhas);
        }

        // ===================== CORTESIA E PLANO, PELA MÃO DO DONO =====================
        //
        // ⚠️ SÓ O RAIZ, nos três POSTs abaixo. Cortesia é dinheiro por DUAS portas: a
        // mensalidade que se decide não cobrar, e a taxa de cada aula caindo de 10% pra 3%
        // (PlanoDoProfessor.CondicoesDeAssinante). A régua é a mesma de todo o resto do
        // financeiro — PoderesNoSistema.PodeVerDinheiro. O assistente do sistema cai duas
        // vezes: pela porta raiz e pelo verbo HTTP.
        //
        // ⚠️ E NENHUM DELES ENCOSTA EM `AssinaturaProfessorPagaAte`. Aquela coluna tem UM
        // escritor só — a confirmação de pagamento (PagamentoInscricaoService) — e é a
        // contraprova de que existiu dinheiro. Um segundo escritor faria a tela dizer
        // "Pago até 20/08/2027" pra quem nunca pagou, e essa mentira aparece até pra quem não
        // enxerga a coluna de valores.

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirCortesiaDoProfessor(
            int professorId, DateTime? ate, string? motivo)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var agora = DateTime.Now;

            if (PlanoDoProfessor.ProblemaParaDarCortesia(ate, motivo, agora) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction(nameof(Professores));
            }

            var professor = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.Id == professorId && j.IsProfessor && j.ExcluidoEm == null);
            if (professor == null)
            {
                TempData["Erro"] = "Professor não encontrado.";
                return RedirectToAction(nameof(Professores));
            }

            professor.CortesiaProfessorAte = ate!.Value.Date;
            professor.MotivoDaCortesia = motivo!.Trim();
            professor.CortesiaConcedidaPorJogadorId = admin.Id;
            professor.CortesiaConcedidaEm = agora;

            // ⚠️ A VIGÊNCIA É OUTRA, ENTÃO O CICLO DE LEMBRETES RECOMEÇA — mesma razão de
            // EfetivarAssinaturaAsync zerar isto a cada pagamento. Sem esta linha, quem já
            // levou o estágio 22 ("sua taxa voltou ao cheio") nunca mais receberia aviso
            // nenhum, nem o do fim da própria cortesia.
            professor.UltimoLembreteDeAssinatura = null;

            await _context.SaveChangesAsync();

            _logger?.LogInformation("Cortesia do professor {ProfessorId} até {Ate} pelo admin {AdminId}: {Motivo}",
                professor.Id, professor.CortesiaProfessorAte, admin.Id, professor.MotivoDaCortesia);

            try
            {
                await _pushNotificationService.EnviarParaJogadorAsync(professor.Id,
                    "Seu plano está garantido",
                    $"O Padelizou garantiu seu plano de Assinante até {professor.CortesiaProfessorAte:dd/MM/yyyy}, "
                    + "sem mensalidade.",
                    Url.Action("Index", "PlanoProfessor"));
            }
            catch (Exception ex)
            {
                // Push é acessório: falhar aqui não pode desfazer uma cortesia já gravada.
                _logger?.LogWarning(ex, "Falha ao avisar o professor {ProfessorId} da cortesia.", professor.Id);
            }

            TempData["Sucesso"] =
                $"{professor.Nome}: plano de Assinante por cortesia até {professor.CortesiaProfessorAte:dd/MM/yyyy} "
                + $"({professor.MotivoDaCortesia}). As aulas dele passam a pagar a taxa de assinante. "
                + "Nada entrou no caixa — a receita da tela não muda.";

            // ⚠️ A trava nova impede ele de GERAR uma cobrança nova, mas não alcança uma que já
            // esteja aberta: pagá-la gravaria "pago até" e ele teria pago uma permuta.
            bool temCobrancaAberta = await _context.Pagamentos.AnyAsync(p =>
                p.JogadorId == professor.Id
                && p.Tipo == RegrasDoPix.TipoAssinatura
                && p.Status != "Confirmado" && p.Status != "Cancelado");

            if (temCobrancaAberta)
            {
                TempData["Erro"] = $"⚠️ {professor.Nome} tem uma cobrança de assinatura EM ABERTO. "
                                 + "Cancele em Financeiro antes que ele pague sem precisar.";
            }

            return RedirectToAction(nameof(Professores));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TirarCortesiaDoProfessor(int professorId)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var professor = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.Id == professorId && j.IsProfessor && j.ExcluidoEm == null);
            if (professor == null)
            {
                TempData["Erro"] = "Professor não encontrado.";
                return RedirectToAction(nameof(Professores));
            }

            // ⚠️ SÓ A DATA volta a ser nula. Motivo, quem deu e quando FICAM: são o rastro, e
            // apagá-los faria o professor reaparecer como "nunca abriu o plano".
            professor.CortesiaProfessorAte = null;
            professor.UltimoLembreteDeAssinatura = null;
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Cortesia do professor {ProfessorId} retirada pelo admin {AdminId}.",
                professor.Id, admin.Id);

            TempData["Sucesso"] = $"Cortesia de {professor.Nome} encerrada. "
                                + "Ele volta à situação que já tinha por conta própria.";
            return RedirectToAction(nameof(Professores));
        }

        // "Assinou ou não" — o plano que ELE escolheu, e nada mais.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirPlanoDoProfessor(int professorId, string? plano)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var escolhido = plano switch
            {
                PlanoDoProfessor.Assinante => PlanoDoProfessor.Assinante,
                PlanoDoProfessor.Avulso => PlanoDoProfessor.Avulso,
                _ => null,   // "ainda não escolheu"
            };

            var professor = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.Id == professorId && j.IsProfessor && j.ExcluidoEm == null);
            if (professor == null)
            {
                TempData["Erro"] = "Professor não encontrado.";
                return RedirectToAction(nameof(Professores));
            }

            professor.PlanoProfessor = escolhido;
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Plano do professor {ProfessorId} definido como {Plano} pelo admin {AdminId}.",
                professor.Id, escolhido ?? "(não escolheu)", admin.Id);

            // ⚠️ O TEXTO DIZ A CONSEQUÊNCIA, porque ela surpreende: marcar "Assinante" sem
            // mensalidade paga produz um selo VERMELHO ("Assinante em atraso") e taxa cheia.
            // Sem este aviso, quem apertou acha que resolveu — e não resolveu.
            TempData["Sucesso"] = escolhido switch
            {
                PlanoDoProfessor.Assinante when professor.AssinaturaProfessorPagaAte == null
                    => $"Marquei o plano de {professor.Nome} como Assinante. Como não há mensalidade paga "
                     + "registrada, ele aparece como \"Assinante em atraso\" e paga taxa cheia até um "
                     + "pagamento entrar — ou até você dar cortesia.",
                PlanoDoProfessor.Assinante => $"{professor.Nome} agora consta como Assinante.",
                PlanoDoProfessor.Avulso => $"{professor.Nome} agora consta como Avulso (taxa cheia por aula).",
                _ => $"{professor.Nome} volta a constar como quem ainda não escolheu plano.",
            };

            return RedirectToAction(nameof(Professores));
        }
    }
}
