using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Avaliação técnica do aluno: a régua do professor, as fichas e o que o aluno pode ver.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    //
    // A regra que atravessa este arquivo inteiro: a ficha é do PROFESSOR até ele liberar, e a
    // anotação privada não sai nunca. As duas travas são no servidor — esconder na tela é
    // publicar com um passo a mais.
    public partial class AulasController
    {
        private ReguaDoProfessor Regua => new(_context);

        // ── A régua ───────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> MinhaRegua(string? modulo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var modulos = await Regua.ModulosAsync(professorId.Value);
            var escolhido = modulo ?? modulos.FirstOrDefault() ?? CatalogoDeFundamentos.Iniciante;

            // Aqui vem TUDO, inclusive o desativado: esta é a tela de mexer na régua, e é onde
            // ele precisa poder trazer de volta o que tirou sem querer.
            ViewBag.Fundamentos = await _context.FundamentosDoProfessor
                .Where(f => f.ProfessorId == professorId && f.Modulo == escolhido)
                .OrderBy(f => f.Ordem)
                .ToListAsync();

            ViewBag.Modulos = modulos;
            ViewBag.ModuloAtual = escolhido;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcrescentarFundamento(string modulo, string? familia, string nome)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (await Regua.AcrescentarAsync(professorId.Value, modulo, familia, nome) == null)
                TempData["Erro"] = "Escreva um nome pro fundamento (até 150 caracteres).";

            return RedirectToAction(nameof(MinhaRegua), new { modulo });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenomearFundamento(int id, string nome, string modulo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (!await Regua.RenomearAsync(professorId.Value, id, nome))
                TempData["Erro"] = "Não deu pra renomear — confira o texto.";

            return RedirectToAction(nameof(MinhaRegua), new { modulo });
        }

        // "Tirar" é desativar: a nota que o aluno já levou continua legível na ficha antiga.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarFundamento(int id, bool ativar, string modulo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (ativar) await Regua.ReativarAsync(professorId.Value, id);
            else await Regua.DesativarAsync(professorId.Value, id);

            return RedirectToAction(nameof(MinhaRegua), new { modulo });
        }

        // ── As fichas ─────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> Avaliacoes(int? alunoId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // Só aluno COM conta: sem conta não há ninguém pra ver a ficha depois.
            ViewBag.Alunos = AlunosDoProfessor.Montar(await AulasDoProfessorAsync(professorId.Value))
                .Where(a => a.TemConta)
                .ToList();

            var fichas = _context.AvaliacoesDeAluno
                .Include(a => a.Aluno)
                .Where(a => a.ProfessorId == professorId);

            if (alunoId.HasValue) fichas = fichas.Where(a => a.AlunoId == alunoId);

            ViewBag.Fichas = await fichas.OrderByDescending(a => a.CriadoEm).ToListAsync();
            ViewBag.AlunoFiltro = alunoId;
            ViewBag.Modulos = await Regua.ModulosAsync(professorId.Value);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NovaAvaliacao(int alunoId, string modulo)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O aluno precisa ser aluno DELE: sem isto, um professor abriria ficha de qualquer
            // pessoa do Padelizou só chutando o id no formulário.
            if (!await EhMeuAlunoAsync(professorId.Value, alunoId))
            {
                TempData["Erro"] = "Esse jogador não é seu aluno.";
                return RedirectToAction(nameof(Avaliacoes));
            }

            var ficha = new AvaliacaoDeAluno
            {
                ProfessorId = professorId.Value,
                AlunoId = alunoId,
                Modulo = modulo,
            };

            _context.AvaliacoesDeAluno.Add(ficha);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Avaliacao), new { id = ficha.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Avaliacao(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var ficha = await _context.AvaliacoesDeAluno
                .Include(a => a.Aluno)
                .Include(a => a.Notas)
                .FirstOrDefaultAsync(a => a.Id == id && a.ProfessorId == professorId);
            if (ficha == null) return NotFound();

            // Os ativos da régua MAIS o que já tem nota nesta ficha: se ele desativou um item
            // depois de dar nota, a nota tem que continuar aparecendo — senão ela vira um
            // valor invisível que a evolução usa e o professor não vê.
            var ativos = await Regua.AtivosDoModuloAsync(professorId.Value, ficha.Modulo);
            var comNota = ficha.Notas.Select(n => n.FundamentoDoProfessorId).ToHashSet();
            var extras = await _context.FundamentosDoProfessor
                .Where(f => comNota.Contains(f.Id) && !ativos.Select(a => a.Id).Contains(f.Id))
                .ToListAsync();

            ViewBag.Fundamentos = ativos.Concat(extras).OrderBy(f => f.Ordem).ToList();
            ViewBag.Notas = ficha.Notas.ToDictionary(n => n.FundamentoDoProfessorId);
            return View(ficha);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarAvaliacao(
            int id, string? recadoAoAluno, string? anotacaoPrivada,
            int[]? fundamentoIds, string?[]? execucoes, int?[]? acertos)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var ficha = await _context.AvaliacoesDeAluno
                .Include(a => a.Notas)
                .FirstOrDefaultAsync(a => a.Id == id && a.ProfessorId == professorId);
            if (ficha == null) return NotFound();

            ficha.RecadoAoAluno = string.IsNullOrWhiteSpace(recadoAoAluno) ? null : recadoAoAluno.Trim();
            ficha.AnotacaoPrivada = string.IsNullOrWhiteSpace(anotacaoPrivada) ? null : anotacaoPrivada.Trim();
            ficha.AtualizadoEm = DateTime.Now;

            if (fundamentoIds != null)
            {
                // Os fundamentos precisam ser DELE — o id vem do formulário, e sem esta
                // checagem daria pra pendurar nota da régua de outro professor nesta ficha.
                var meus = await _context.FundamentosDoProfessor
                    .Where(f => f.ProfessorId == professorId && fundamentoIds.Contains(f.Id))
                    .Select(f => f.Id)
                    .ToListAsync();

                for (int i = 0; i < fundamentoIds.Length; i++)
                {
                    int fundamentoId = fundamentoIds[i];
                    if (!meus.Contains(fundamentoId)) continue;

                    var execucao = EscalaDeAvaliacao.LimparExecucao(
                        execucoes != null && i < execucoes.Length ? execucoes[i] : null);
                    var acerto = EscalaDeAvaliacao.LimparAcertos(
                        acertos != null && i < acertos.Length ? acertos[i] : null);

                    var existente = ficha.Notas.FirstOrDefault(n => n.FundamentoDoProfessorId == fundamentoId);

                    if (!EscalaDeAvaliacao.TemNota(execucao, acerto))
                    {
                        // Limpou as duas: a linha sai. Guardar "sem nota" faria a evolução
                        // comparar nada com nada e anunciar mudança onde não houve.
                        if (existente != null) _context.NotasDeFundamento.Remove(existente);
                        continue;
                    }

                    if (existente == null)
                    {
                        _context.NotasDeFundamento.Add(new NotaDeFundamento
                        {
                            AvaliacaoDeAlunoId = ficha.Id,
                            FundamentoDoProfessorId = fundamentoId,
                            Execucao = execucao,
                            Acertos = acerto,
                        });
                    }
                    else
                    {
                        existente.Execucao = execucao;
                        existente.Acertos = acerto;
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Ficha salva.";
            return RedirectToAction(nameof(Avaliacao), new { id });
        }

        // O botão que o Felipe pediu: a ficha só chega no aluno quando o professor quiser.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarVisibilidadeDaFicha(int id, bool liberar)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var ficha = await _context.AvaliacoesDeAluno
                .FirstOrDefaultAsync(a => a.Id == id && a.ProfessorId == professorId);
            if (ficha == null) return NotFound();

            ficha.VisivelParaAluno = liberar;
            ficha.LiberadaEm = liberar ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = liberar
                ? "Ficha liberada — o aluno já consegue ver (a anotação privada continua só sua)."
                : "Ficha escondida do aluno.";

            return RedirectToAction(nameof(Avaliacao), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirAvaliacao(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var ficha = await _context.AvaliacoesDeAluno
                .FirstOrDefaultAsync(a => a.Id == id && a.ProfessorId == professorId);
            if (ficha == null) return NotFound();

            _context.AvaliacoesDeAluno.Remove(ficha);   // as notas vão junto (Cascade)
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Ficha excluída.";
            return RedirectToAction(nameof(Avaliacoes));
        }

        // ── Quem é aluno de quem ──────────────────────────────────────────────────────────

        private async Task<bool> EhMeuAlunoAsync(int professorId, int alunoId) =>
            await _context.Aulas.AnyAsync(a => a.ProfessorId == professorId && a.AlunoId == alunoId);

        // `padelizou.Models` minúsculo: Aula mora no namespace legado (ver AulasController.cs).
        private Task<List<padelizou.Models.Aula>> AulasDoProfessorAsync(int professorId) =>
            _context.Aulas
                .Include(a => a.Aluno)
                .Where(a => a.ProfessorId == professorId)
                .ToListAsync();
    }
}
