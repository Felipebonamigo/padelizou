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
        // `semanas` é o mês do card de semanas ("2026-08"), independente do `periodo` dos
        // cartões do topo: um responde "quanto entrou nesta semana", o outro "como foi agosto".
        [HttpGet]
        public async Task<IActionResult> Financeiro(string? periodo, string? semanas = null)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var hoje = DateTime.Today;
            periodo = (periodo ?? "mes").Trim().ToLower();

            // A semana entrou em 09/08/2026: o professor acerta a quadra com o clube POR SEMANA,
            // e o mês inteiro somado não responde "quanto eu levo lá na sexta". Segunda a
            // domingo, a mesma régua do card de semanas do mês logo abaixo (Services/SemanasDoMes)
            // — duas definições de semana na mesma tela seriam dois números pro mesmo dia.
            var estaSegunda = hoje.AddDays(-(((int)hoje.DayOfWeek + 6) % 7));

            var (de, rotulo) = periodo switch
            {
                "semana" => (estaSegunda, "nesta semana"),
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

            // ⚠️ "Recebido" É CAIXA, e desde 25/08/2026 ele pergunta pelo DINHEIRO, não pelo
            // status: até aqui somava toda aula `Realizada`, e portanto contava como recebida a
            // aula dada que o aluno ainda não pagou (ver Services/RecebimentoDaAula).
            // `recebidas` e `aReceber` partem em duas exatamente o que gerou cobrança.
            var realizadas = doPeriodo.Where(a => a.Status == PoliticaAula.Realizada).ToList();
            var faltas = doPeriodo.Where(a => a.Status == PoliticaAula.Faltou).ToList();
            var recebidas = doPeriodo.Where(RecebimentoDaAula.FoiRecebida).ToList();
            var aReceber = doPeriodo.Where(RecebimentoDaAula.EstaAReceber).ToList();

            var vm = new FinanceiroProfessorVM
            {
                Periodo = periodo,
                PeriodoRotulo = rotulo,
                Recebido = recebidas.Sum(a => a.Preco),
                // Confirmada e ainda por acontecer: o que entra se ninguém desmarcar.
                Previsto = aulas
                    .Where(a => a.Status == PoliticaAula.Confirmada && a.DataHora >= DateTime.Now)
                    .Sum(a => a.Preco),
                AReceber = aReceber.Sum(a => a.Preco),
                PerdidoComFaltas = faltas.Where(a => !a.CobrarMesmoFaltando).Sum(a => a.Preco),
                AulasRealizadas = realizadas.Count,
                AulasCanceladas = doPeriodo.Count(a => a.Status == PoliticaAula.Cancelada),
                Faltas = faltas.Count,
            };

            // Quem está devendo: agrupa por aluno o que gerou cobrança e ainda não foi pago.
            // ⚠️ Até 25/08/2026 o critério era só `CobrarMesmoFaltando` — a aula DADA e não paga
            // não aparecia aqui, porque não havia como saber que ela não tinha sido paga.
            //
            // A identidade do aluno é a MESMA de PrecoDaAula.Chave (conta quando existe, nome
            // anotado quando não): é ela que faz esta lista, o cadastro e a conta do mês
            // encontrarem a mesma pessoa sem conversão no meio.
            vm.Devedores = aReceber
                .GroupBy(PrecoDaAula.Chave)
                .Select(g => new DevedorVM
                {
                    Nome = g.First().Aluno?.ComoChamar ?? g.First().NomeAlunoAvulso ?? "Aluno avulso",
                    Celular = g.First().Aluno?.Celular ?? g.First().TelefoneAlunoAvulso,
                    AulasEmAberto = g.Count(),
                    Valor = g.Sum(a => a.Preco),
                    AulaMaisAntiga = g.Min(a => a.DataHora),
                    // Os ids vão pro botão "Recebi": dar baixa não pode depender de recalcular
                    // o grupo no POST, que é como a lista da tela e a do servidor divergem.
                    AulaIds = g.Select(a => a.Id).ToList(),
                    // E as MESMAS aulas, com data, preço e status, pro botão do WhatsApp poder
                    // escrever a cobrança detalhada (Services/CobrancaDasAulasEmAberto).
                    //
                    // ⚠️ São as aulas do PERÍODO ESCOLHIDO na tela — as mesmas que somam o valor
                    // mostrado ao lado do nome. Mandar "todas de sempre" faria a mensagem cobrar
                    // um total diferente do que está escrito na linha logo acima do botão, que é
                    // exatamente o defeito que a tabela por local já levou uma correção pra não
                    // ter. Quem quer cobrar o histórico inteiro troca o período pra "Sempre".
                    Aulas = g.OrderBy(a => a.DataHora)
                             .Select(a => new AulaEmAbertoVM
                             {
                                 DataHora = a.DataHora,
                                 Preco = a.Preco,
                                 Status = a.Status,
                             })
                             .ToList(),
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
                    // O total desta coluna tem que bater com o card "Recebido" do topo da
                    // mesma tela — dois números de dinheiro se contradizendo numa página só é
                    // pior do que não mostrar nenhum. Já `Aulas` e `Custo` contam aula DADA:
                    // a quadra foi alugada quer o aluno tenha pago ou não.
                    Recebido = g.Where(RecebimentoDaAula.FoiRecebida).Sum(a => a.Preco),
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

            // As semanas do MÊS escolhido, de segunda a domingo — no mês fechado a mordida de
            // uma semana fraca some na média.
            //
            // ⚠️ Até 01/09/2026 isto era uma janela ROLANTE de 6 semanas, e a última barra
            // atravessava a virada do mês ("31/08–06/09" no print do Felipe): ela não pertencia
            // a mês nenhum, e a soma das barras não batia com mês nenhum. Agora as fatias são
            // recortadas no mês (ver Services/SemanasDoMes) e a soma É o faturamento dele — o
            // MESMO número que o card "Últimos 6 meses" mostra logo abaixo.
            var mesDasSemanas = SemanasDoMes.Escolhido(semanas, hoje);
            vm.MesDasSemanas = mesDasSemanas;
            vm.Semanas = SemanasDoMes.Fatiar(mesDasSemanas).Select(fatia =>
            {
                // `Fim` é o último DIA da fatia, inclusive: a comparação vai até o dia seguinte
                // pra não perder a aula das 20h do domingo.
                var daSemana = aulas.Where(a => a.Status == PoliticaAula.Realizada
                                             && a.DataHora >= fatia.Inicio
                                             && a.DataHora < fatia.Fim.AddDays(1)).ToList();
                return new SemanaFaturamentoVM
                {
                    Inicio = fatia.Inicio,
                    Fim = fatia.Fim,
                    Valor = daSemana.Sum(a => a.Preco),
                    Aulas = daSemana.Count,
                };
            }).ToList();

            // As setas param onde o dado para: não existe faturamento no futuro, e nem antes da
            // primeira aula. `aulas` já está inteiro na memória — nenhuma consulta nova.
            vm.PodeAvancarSemanas = mesDasSemanas < new DateTime(hoje.Year, hoje.Month, 1);
            vm.PodeVoltarSemanas = aulas.Any(a => a.DataHora < mesDasSemanas);

            // E os últimos 6 anos — semana e mês respondem o dia a dia, mas não "esse ano
            // deu mais aula que o passado".
            var primeiroAno = hoje.Year - 5;
            vm.UltimosAnos = Enumerable.Range(0, 6).Select(i =>
            {
                var ano = primeiroAno + i;
                var inicio = new DateTime(ano, 1, 1);
                var fim = inicio.AddYears(1);
                var doAno = aulas.Where(a => a.Status == PoliticaAula.Realizada && a.DataHora >= inicio && a.DataHora < fim).ToList();
                return new AnoFaturamentoVM { Ano = ano, Valor = doAno.Sum(a => a.Preco), Aulas = doAno.Count };
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

            // Aula na fila de reposição não passa por aqui: registrar presença gravaria
            // `CobrarMesmoFaltando = false` por baixo e ela sairia do financeiro calada,
            // continuando na fila. Quem já disse "vai recuperar" desfaz pela própria fila.
            if (aula.Status == PoliticaAula.ARecuperar)
            {
                TempData["Erro"] = "Essa aula está na fila de reposição. Encaixe a reposição ou "
                                 + "marque que ela não vai mais ser recuperada.";
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

        // O Pix que chegou na sexta pela aula de terça. Dar baixa é um momento DIFERENTE de
        // concluir a aula (pedido do Felipe em 25/08/2026), e é por isso que existe uma ação
        // própria em vez de um parâmetro a mais no fechamento da presença.
        //
        // `recebida = false` é o desfazer: o professor deu baixa no aluno errado, ou o Pix não
        // caiu. Mesma simetria de MarcarFaturaPaga/ReabrirFatura.
        [HttpPost]
        public async Task<IActionResult> MarcarRecebida(int aulaId, bool recebida)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O ProfessorId no filtro É a autorização: sem ele, qualquer professor logado dá
            // baixa na aula de qualquer outro só mandando o id.
            var aula = await _context.Aulas.FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);
            if (aula == null) return NotFound();

            if (recebida && !RecebimentoDaAula.PodeMarcar(aula))
            {
                TempData["Erro"] = RecebimentoDaAula.MotivoParaNaoMarcar(aula);
                return RedirectToAction("MinhaAgenda", new { data = aula.DataHora.ToString("yyyy-MM-dd") });
            }

            // A folha mostra UM card pra turma inteira, com o preço SOMADO — então a baixa
            // feita dali vale pra sessão, igual a Concluir e Cancelar. Quem precisa separar
            // aluno por aluno (dois pagaram, um não) faz isso na lista de devedores do
            // Financeiro, onde cada um tem a própria linha.
            var linhas = aula.TurmaId != null
                ? await _context.Aulas.Where(a => a.TurmaId == aula.TurmaId && a.ProfessorId == professorId).ToListAsync()
                : new List<Aula> { aula };

            var agora = DateTime.Now;
            foreach (var linha in linhas)
            {
                if (!recebida) linha.PagaEm = null;
                else if (RecebimentoDaAula.PodeMarcar(linha)) linha.PagaEm = agora;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = recebida
                ? "Aula marcada como recebida."
                : "Aula voltou pra lista do que você tem a receber.";

            return RedirectToAction("MinhaAgenda", new { data = aula.DataHora.ToString("yyyy-MM-dd") });
        }

        // Dar baixa em VÁRIAS aulas de uma vez: é o botão da lista de "quem está devendo", onde
        // o aluno aparece com as N aulas em aberto dele. Sem lista, o professor teria que abrir
        // a agenda dia a dia pra achar cada uma — e a lista existe justamente porque ele não
        // sabe quais são.
        //
        // ⚠️ Aqui NÃO cascadeia por turma, ao contrário do MarcarRecebida: na lista de devedores
        // cada aluno da turma já é a própria linha, e cascatear daria baixa no colega que não
        // pagou.
        [HttpPost]
        public async Task<IActionResult> MarcarRecebidas(int[] aulaIds, string? periodo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var ids = (aulaIds ?? Array.Empty<int>()).Distinct().ToList();

            var aulas = await _context.Aulas
                .Where(a => ids.Contains(a.Id) && a.ProfessorId == professorId)
                .ToListAsync();

            var agora = DateTime.Now;
            var baixadas = 0;
            foreach (var aula in aulas.Where(RecebimentoDaAula.PodeMarcar))
            {
                aula.PagaEm = agora;
                baixadas++;
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = baixadas == 1
                ? "1 aula marcada como recebida."
                : $"{baixadas} aulas marcadas como recebidas.";

            return RedirectToAction(nameof(Financeiro), new { periodo });
        }

        // "O aluno não vem hoje, mas paga e recupera depois" — o caso do mensalista, que o
        // sistema não sabia dizer (ver Services/Reposicao). O horário fica LIVRE na hora:
        // é isso que deixa o professor encaixar outro aluno no lugar, no mesmo dia.
        [HttpPost]
        public async Task<IActionResult> MarcarParaRecuperar(int aulaId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O ProfessorId no filtro É a autorização: sem ele, qualquer professor logado
            // mexeria na aula de qualquer outro só mandando o id.
            var aula = await _context.Aulas.FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);
            if (aula == null) return NotFound();

            if (!Reposicao.PodeMarcar(aula))
            {
                TempData["Erro"] = Reposicao.MotivoParaNaoMarcar(aula);
                return RedirectToAction("MinhaAgenda");
            }

            aula.Status = PoliticaAula.ARecuperar;

            // A cobrança fica de pé pela MESMA chave que a falta cobrável já usava. Reaproveitar
            // é o ponto: o financeiro (previsão, devedores, relatório) não precisou aprender um
            // conceito novo pra continuar certo.
            aula.CobrarMesmoFaltando = true;

            var eventoGoogle = aula.GoogleEventId;
            aula.GoogleEventId = null;

            await _context.SaveChangesAsync();

            // O horário vagou aqui dentro; deixar o evento na Google Agenda faria o professor
            // olhar o celular e achar que ainda está ocupado.
            if (eventoGoogle != null)
            {
                try
                {
                    await _googleCalendarService.RemoverEventoAsync(professorId.Value, eventoGoogle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao remover da Google Agenda a aula {AulaId} que foi pra reposição", aula.Id);
                }
            }

            TempData["Sucesso"] = "Aula na fila de reposição: o horário ficou livre e a cobrança continua de pé.";
            return RedirectToAction("MinhaAgenda");
        }

        // O outro fim possível da fila: combinaram que não vai ter reposição. Vira falta
        // cobrada — que é exatamente o que sobrou do combinado — e sai da fila.
        [HttpPost]
        public async Task<IActionResult> NaoVaiRecuperar(int aulaId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas.FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);
            if (aula == null) return NotFound();

            if (aula.Status != PoliticaAula.ARecuperar)
            {
                TempData["Erro"] = "Essa aula não está na fila de reposição.";
                return RedirectToAction("MinhaAgenda");
            }

            aula.Status = PoliticaAula.Faltou;
            aula.Compareceu = false;
            // CobrarMesmoFaltando fica como está (ligado): o combinado era cobrar, e é só a
            // reposição que caiu.
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Aula fora da fila de reposição. Segue registrada como falta cobrada.";
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
                // ⚠️ "Recebido" é o dinheiro que ENTROU; `TotalAulas` logo acima continua
                // contando aula DADA. São duas perguntas diferentes desde 25/08/2026, e este
                // relatório responde as duas na mesma tela (ver Services/RecebimentoDaAula).
                TotalRecebido = aulas.Where(RecebimentoDaAula.FoiRecebida).Sum(a => a.Preco),
                PorLocal = aulas
                    .GroupBy(a => a.LocalAula)
                    .Select(g => new RelatorioPorLocal
                    {
                        NomeLocal = g.Key.Nome,
                        QuantidadeAulas = g.Count(),
                        Recebido = g.Where(RecebimentoDaAula.FoiRecebida).Sum(a => a.Preco),
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
