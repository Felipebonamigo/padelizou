using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // Fechar o mês e cobrar: o modelo do professor mensalista ("fecha as aulas do mês e cobra
    // no mês subsequente"). A regra do que entra em cada conta mora em Services/FechamentoDoMes.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> Faturamento(int? ano, int? mes)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O mês PASSADO por padrão: quem abre esta tela está fechando o que acabou, não o
            // que está correndo. Abrir no mês corrente mostraria uma conta pela metade toda vez.
            var referencia = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            var anoAlvo = ano ?? referencia.Year;
            var mesAlvo = mes is int m && m >= 1 && m <= 12 ? m : referencia.Month;

            var vm = await MontarFaturamentoAsync(professorId.Value, anoAlvo, mesAlvo);
            return View(vm);
        }

        private async Task<FaturamentoVM> MontarFaturamentoAsync(int professorId, int ano, int mes)
        {
            var aulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Where(a => a.ProfessorId == professorId && a.DataHora.Year == ano && a.DataHora.Month == mes)
                .ToListAsync();

            var cadastros = await _context.CadastrosDeAlunos
                .Where(f => f.ProfessorId == professorId)
                .ToListAsync();

            var doMes = await _context.FaturasDeAlunos
                .Include(f => f.Aluno)
                // A cobrança emitida, quando existe: é dela que sai o link pra mandar a quem paga.
                .Include(f => f.Pagamento)
                .Where(f => f.ProfessorId == professorId && f.Ano == ano && f.Mes == mes)
                .ToListAsync();

            var jaFechadas = doMes
                .Where(f => f.Status != FechamentoDoMes.Cancelada)
                .Select(f => PrecoDaAula.Chave(f.AlunoId, f.NomeAvulso))
                .ToHashSet();

            var abertas = await _context.FaturasDeAlunos
                .Include(f => f.Aluno)
                .Where(f => f.ProfessorId == professorId && f.Status == FechamentoDoMes.Aberta)
                .OrderBy(f => f.Vencimento)
                .ToListAsync();

            return new FaturamentoVM
            {
                Ano = ano,
                Mes = mes,
                Previas = FechamentoDoMes.Previas(
                    aulas, cadastros, aulas.Select(a => a.Aluno).Distinct(), ano, mes, jaFechadas),
                Fechadas = doMes.OrderByDescending(f => f.Valor).ToList(),
                EmAbertoDeTodosOsMeses = abertas,
                PodeFechar = FechamentoDoMes.PodeFechar(ano, mes, DateTime.Now),
                MotivoParaNaoFechar = FechamentoDoMes.PodeFechar(ano, mes, DateTime.Now)
                    ? null
                    : FechamentoDoMes.MotivoParaNaoFechar(ano, mes),
            };
        }

        [HttpPost]
        public async Task<IActionResult> FecharMes(int ano, int mes, int diaVencimento)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (!FechamentoDoMes.PodeFechar(ano, mes, DateTime.Now))
            {
                TempData["ErroFatura"] = FechamentoDoMes.MotivoParaNaoFechar(ano, mes);
                return RedirectToAction(nameof(Faturamento), new { ano, mes });
            }

            var vm = await MontarFaturamentoAsync(professorId.Value, ano, mes);

            // Quem JÁ tem conta desta competência fica de fora. É o que faz clicar duas vezes
            // em "fechar" não cobrar o mesmo mês duas vezes — e o professor clica de novo com
            // frequência, porque a tela continua mostrando o mês depois de fechado.
            var novas = vm.Previas
                .Where(p => !p.JaFechada)
                .Select(p => FechamentoDoMes.Fechar(professorId.Value, p, ano, mes, diaVencimento, DateTime.Now))
                .ToList();

            if (novas.Count == 0)
            {
                TempData["ErroFatura"] = "Nada novo pra fechar neste mês.";
                return RedirectToAction(nameof(Faturamento), new { ano, mes });
            }

            _context.FaturasDeAlunos.AddRange(novas);
            await _context.SaveChangesAsync();

            TempData["SucessoFatura"] = $"{novas.Count} conta(s) fechada(s), "
                + $"total de {novas.Sum(f => f.Valor):C}, vencendo em {novas[0].Vencimento:dd/MM/yyyy}.";
            return RedirectToAction(nameof(Faturamento), new { ano, mes });
        }

        // O professor recebeu por fora (Pix na mão, dinheiro) e dá baixa. Continua existindo
        // mesmo com a cobrança pelo app ligada: a maior parte das aulas ainda é acertada assim.
        [HttpPost]
        public async Task<IActionResult> MarcarFaturaPaga(int id)
        {
            var (professorId, fatura) = await MinhaFaturaAsync(id);
            if (professorId == null) return RedirectToAction("Perfil", "Auth");
            if (fatura == null) return NotFound();

            if (fatura.Status == FechamentoDoMes.Cancelada)
            {
                TempData["ErroFatura"] = "Essa conta foi cancelada.";
                return RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });
            }

            fatura.Status = FechamentoDoMes.Paga;
            fatura.PagaEm = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SucessoFatura"] = $"Conta de {fatura.PagadorNome} marcada como paga.";
            return RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });
        }

        // Desfazer a baixa: o professor marcou pago no aluno errado, ou o Pix não caiu.
        [HttpPost]
        public async Task<IActionResult> ReabrirFatura(int id)
        {
            var (professorId, fatura) = await MinhaFaturaAsync(id);
            if (professorId == null) return RedirectToAction("Perfil", "Auth");
            if (fatura == null) return NotFound();

            fatura.Status = FechamentoDoMes.Aberta;
            fatura.PagaEm = null;
            await _context.SaveChangesAsync();

            TempData["SucessoFatura"] = $"Conta de {fatura.PagadorNome} voltou a ficar em aberto.";
            return RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });
        }

        // Cancelar é para a conta que não deveria ter nascido (fechou o mês errado, aluno que
        // saiu). Fica registrada em vez de sumir — e libera a competência pra ser fechada de
        // novo, que é justamente o conserto de quem fechou com o preço errado.
        [HttpPost]
        public async Task<IActionResult> CancelarFatura(int id)
        {
            var (professorId, fatura) = await MinhaFaturaAsync(id);
            if (professorId == null) return RedirectToAction("Perfil", "Auth");
            if (fatura == null) return NotFound();

            if (fatura.PagamentoId != null)
            {
                TempData["ErroFatura"] = "Essa conta já tem cobrança emitida no app — cancele a cobrança primeiro.";
                return RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });
            }

            fatura.Status = FechamentoDoMes.Cancelada;
            await _context.SaveChangesAsync();

            TempData["SucessoFatura"] = $"Conta de {fatura.PagadorNome} cancelada.";
            return RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });
        }

        // Emitir a cobrança da conta do mês pelo app. O dinheiro cai direto na carteira do
        // professor (split), e a nossa comissão é a do plano dele — a mesma régua da aula
        // avulsa. Ver Services/PagamentoInscricaoService.
        [HttpPost]
        public async Task<IActionResult> CobrarFatura(int id)
        {
            var (professorId, fatura) = await MinhaFaturaAsync(id);
            if (professorId == null) return RedirectToAction("Perfil", "Auth");
            if (fatura == null) return NotFound();

            var voltar = RedirectToAction(nameof(Faturamento), new { ano = fatura.Ano, mes = fatura.Mes });

            if (fatura.Status != FechamentoDoMes.Aberta)
            {
                TempData["ErroFatura"] = "Só dá pra cobrar conta que está em aberto.";
                return voltar;
            }

            var professor = await _context.Jogadores.FindAsync(professorId.Value);
            if (professor == null) return voltar;

            // O que falta pra ele receber pelo app (carteira não conectada, por exemplo) já
            // tem texto pronto — é o mesmo de torneio e aula avulsa.
            var impedimento = _pagamentos.OQueFaltaParaReceber(professor);
            if (impedimento != null)
            {
                TempData["ErroFatura"] = impedimento;
                return voltar;
            }

            if (string.IsNullOrWhiteSpace(fatura.PagadorCpf))
            {
                TempData["ErroFatura"] = $"Sem o CPF de {fatura.PagadorNome} não dá pra emitir a cobrança. "
                    + "Preencha no painel, em Quem paga, e feche o mês de novo — ou acerte por fora.";
                return voltar;
            }

            var link = await _pagamentos.IniciarCobrancaDaFaturaAsync(fatura, professor);
            if (link == null)
            {
                TempData["ErroFatura"] = "Não consegui gerar a cobrança agora. Tente de novo em instantes.";
                return voltar;
            }

            TempData["SucessoFatura"] = $"Cobrança de {fatura.PagadorNome} emitida. "
                + "Mande o link pra quem paga — o dinheiro cai direto na sua conta.";
            TempData["LinkDaCobranca"] = link;
            return voltar;
        }

        // O ProfessorId no filtro É a autorização: sem ele, qualquer professor logado daria
        // baixa na conta de qualquer outro só mandando o id.
        private async Task<(int? ProfessorId, FaturaDoAluno? Fatura)> MinhaFaturaAsync(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return (null, null);

            var fatura = await _context.FaturasDeAlunos
                .FirstOrDefaultAsync(f => f.Id == id && f.ProfessorId == professorId);

            return (professorId, fatura);
        }
    }
}
