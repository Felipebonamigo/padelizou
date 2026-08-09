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
    // Dinheiro e presença: quanto entrou, quem está devendo, presença/falta com a regra de
    // cobrança, e o relatório por período.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        // "Quanto entrou, quanto ainda entra e quem está devendo" — o extrato geral de
        // Pagamentos/Meus só mostra o que passou pelo Asaas, e a maior parte das aulas
        // ainda é acertada por fora (Pix, dinheiro).
        [HttpGet]
        public async Task<IActionResult> Financeiro(string? periodo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var hoje = DateTime.Today;
            periodo = (periodo ?? "mes").Trim().ToLower();

            var (de, rotulo) = periodo switch
            {
                "ano" => (new DateTime(hoje.Year, 1, 1), $"em {hoje.Year}"),
                "sempre" => (DateTime.MinValue, "desde sempre"),
                _ => (new DateTime(hoje.Year, hoje.Month, 1), "neste mês"),
            };

            var aulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId)
                .ToListAsync();

            var doPeriodo = aulas.Where(a => a.DataHora >= de).ToList();

            // "Recebido" = aula que aconteceu. Falta cobrável conta como devida, não recebida.
            var realizadas = doPeriodo.Where(a => a.Status == PoliticaAula.Realizada).ToList();
            var faltas = doPeriodo.Where(a => a.Status == PoliticaAula.Faltou).ToList();
            var cobraveis = doPeriodo.Where(a => a.CobrarMesmoFaltando).ToList();

            var vm = new FinanceiroProfessorVM
            {
                Periodo = periodo,
                PeriodoRotulo = rotulo,
                Recebido = realizadas.Sum(a => a.Preco),
                // Confirmada e ainda por acontecer: o que entra se ninguém desmarcar.
                Previsto = aulas
                    .Where(a => a.Status == PoliticaAula.Confirmada && a.DataHora >= DateTime.Now)
                    .Sum(a => a.Preco),
                AReceber = cobraveis.Sum(a => a.Preco),
                PerdidoComFaltas = faltas.Where(a => !a.CobrarMesmoFaltando).Sum(a => a.Preco),
                AulasRealizadas = realizadas.Count,
                AulasCanceladas = doPeriodo.Count(a => a.Status == PoliticaAula.Cancelada),
                Faltas = faltas.Count,
            };

            // Quem está devendo: agrupa por aluno as aulas realizadas/faltas cobráveis.
            // Sem controle de quitação por aula, o critério é "aconteceu e é cobrável".
            vm.Devedores = cobraveis
                .GroupBy(a => a.AlunoId.HasValue ? $"a{a.AlunoId}" : $"v{(a.NomeAlunoAvulso ?? "").ToLower()}")
                .Select(g => new DevedorVM
                {
                    Nome = g.First().Aluno?.ComoChamar ?? g.First().NomeAlunoAvulso ?? "Aluno avulso",
                    Celular = g.First().Aluno?.Celular ?? g.First().TelefoneAlunoAvulso,
                    AulasEmAberto = g.Count(),
                    Valor = g.Sum(a => a.Preco),
                    AulaMaisAntiga = g.Min(a => a.DataHora),
                })
                .OrderByDescending(d => d.Valor)
                .ToList();

            // O custo do local só conta nas aulas em que o PROFESSOR paga a quadra — às
            // vezes o aluno acerta o aluguel direto com o clube (Aula.AlunoPagaQuadra).
            vm.PorLocal = realizadas
                .GroupBy(a => a.LocalAula)
                .Select(g => new FinanceiroPorLocalVM
                {
                    Local = g.Key.Nome,
                    Aulas = g.Count(),
                    Recebido = g.Sum(a => a.Preco),
                    Custo = g.Key.CustoPorAula.HasValue ? g.Key.CustoPorAula.Value * g.Count(a => !a.AlunoPagaQuadra) : null,
                })
                .OrderByDescending(l => l.Recebido)
                .ToList();

            // Últimos 6 meses de faturamento, pro professor ver a tendência.
            var primeiroMes = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-5);
            vm.UltimosMeses = Enumerable.Range(0, 6).Select(i =>
            {
                var mes = primeiroMes.AddMonths(i);
                var fim = mes.AddMonths(1);
                var doMes = aulas.Where(a => a.Status == PoliticaAula.Realizada && a.DataHora >= mes && a.DataHora < fim).ToList();
                return new MesFaturamentoVM { Mes = mes, Valor = doMes.Sum(a => a.Preco), Aulas = doMes.Count };
            }).ToList();

            // E as últimas 6 semanas, de segunda a domingo — no mês a mordida de uma semana
            // fraca some na média.
            var estaSegunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));
            var primeiraSemana = estaSegunda.AddDays(-7 * 5);
            vm.UltimasSemanas = Enumerable.Range(0, 6).Select(i =>
            {
                var inicio = primeiraSemana.AddDays(7 * i);
                var fim = inicio.AddDays(7);
                var daSemana = aulas.Where(a => a.Status == PoliticaAula.Realizada && a.DataHora >= inicio && a.DataHora < fim).ToList();
                return new SemanaFaturamentoVM { Inicio = inicio, Valor = daSemana.Sum(a => a.Preco), Aulas = daSemana.Count };
            }).ToList();

            return View(vm);
        }


        // O professor fecha a aula dizendo se o aluno veio. "Faltou" é diferente de
        // "Cancelada": cancelamento é aviso prévio, falta é não aparecer.
        [HttpPost]
        public async Task<IActionResult> RegistrarPresenca(int aulaId, bool compareceu, bool cobrarMesmoAssim = false)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas.FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);
            if (aula == null) return NotFound();

            if (aula.Status == PoliticaAula.Cancelada || aula.Status == PoliticaAula.Recusada)
            {
                TempData["Erro"] = "Essa aula foi cancelada — não dá pra registrar presença.";
                return RedirectToAction("MinhaAgenda");
            }

            aula.Compareceu = compareceu;
            aula.Status = compareceu ? PoliticaAula.Realizada : PoliticaAula.Faltou;

            // Falta só vira dinheiro se o professor marcar — mesmo cobrando por política,
            // a última palavra é dele (aluno pode ter avisado por fora).
            aula.CobrarMesmoFaltando = !compareceu && cobrarMesmoAssim;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = compareceu
                ? "Presença registrada. Aula marcada como realizada."
                : aula.CobrarMesmoFaltando
                    ? "Falta registrada e marcada como cobrável."
                    : "Falta registrada, sem cobrança.";

            return RedirectToAction("MinhaAgenda");
        }

        // 6. RELATÓRIO DO PROFESSOR (aulas dadas, alunos, receita e gasto por período)
        [HttpGet]
        public async Task<IActionResult> Relatorio(DateTime? dataInicio, DateTime? dataFim)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var inicio = (dataInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var fim = (dataFim ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);

            var relatorio = await CalcularRelatorioAsync(professorId.Value, inicio, fim);

            return View(relatorio);
        }

        // Reaproveitado pela tela de Relatório (período escolhido pelo professor) e pelo
        // Painel do Professor (sempre o mês corrente).
        private async Task<RelatorioAulasViewModel> CalcularRelatorioAsync(int professorId, DateTime inicio, DateTime fim)
        {
            var aulas = await _context.Aulas
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId &&
                            a.Status == "Realizada" &&
                            a.DataHora >= inicio && a.DataHora <= fim)
                .ToListAsync();

            var relatorio = new RelatorioAulasViewModel
            {
                DataInicio = inicio,
                DataFim = fim,
                TotalAulas = aulas.Count,
                TotalAlunosDiferentes = aulas.Select(a => a.AlunoId).Distinct().Count(),
                TotalRecebido = aulas.Sum(a => a.Preco),
                PorLocal = aulas
                    .GroupBy(a => a.LocalAula)
                    .Select(g => new RelatorioPorLocal
                    {
                        NomeLocal = g.Key.Nome,
                        QuantidadeAulas = g.Count(),
                        Recebido = g.Sum(a => a.Preco),
                        // Mesma régua do Financeiro: quadra que o aluno paga não é gasto do professor.
                        Gasto = g.Key.CustoPorAula.HasValue ? g.Key.CustoPorAula.Value * g.Count(a => !a.AlunoPagaQuadra) : null
                    })
                    .OrderByDescending(l => l.Recebido)
                    .ToList()
            };
            relatorio.TotalGasto = relatorio.PorLocal.Any(l => l.Gasto.HasValue)
                ? relatorio.PorLocal.Sum(l => l.Gasto ?? 0)
                : null;

            return relatorio;
        }

    }
}
