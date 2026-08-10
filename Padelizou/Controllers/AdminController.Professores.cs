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
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

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

            var linhas = ProfessoresNoAdmin.Montar(professores, assinaturas, aulas, agora, plano.Value);
            var meses = ProfessoresNoAdmin.PorMes(assinaturas);

            ViewBag.Resumo = ProfessoresNoAdmin.Resumir(linhas, meses, agora);
            ViewBag.Meses = meses;
            ViewBag.Cfg = plano.Value;

            return View(linhas);
        }
    }
}
