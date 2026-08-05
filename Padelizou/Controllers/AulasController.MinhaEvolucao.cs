using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // O lado do ALUNO: as fichas que o professor liberou, e a evolução entre elas.
    //
    // ⚠️ Este arquivo nunca lê `AnotacaoPrivada`. A trava é a consulta em si — o campo não sai
    // do banco —, e não um `@if` na view: texto que chega no HTML e é escondido por CSS está
    // publicado, basta ver o código-fonte da página. Se alguém precisar mexer aqui, essa é a
    // regra que não pode cair.
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> MinhaEvolucao(int? professorId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
                return RedirectToAction("Perfil", "Auth");

            // Projeção explícita, campo a campo: é o que garante que a anotação privada não
            // venha junto nem por descuido de um `Include` futuro.
            var fichas = await _context.AvaliacoesDeAluno
                .Where(a => a.AlunoId == meuId && a.VisivelParaAluno)
                .OrderByDescending(a => a.CriadoEm)
                .Select(a => new FichaDoAluno(
                    a.Id,
                    a.ProfessorId,
                    a.Professor.Nome,
                    a.Modulo,
                    a.CriadoEm,
                    a.LiberadaEm,
                    a.RecadoAoAluno,
                    a.Notas.Select(n => new EvolucaoDoAluno.NotaLida(
                        n.FundamentoDoProfessorId,
                        n.Fundamento.Nome,
                        n.Fundamento.Familia,
                        n.Execucao,
                        n.Acertos)).ToList()))
                .ToListAsync();

            if (professorId.HasValue) fichas = fichas.Where(f => f.ProfessorId == professorId).ToList();

            // A evolução compara cada ficha com a ANTERIOR do mesmo professor: fichas de
            // professores diferentes usam réguas diferentes (cada um edita a sua), então
            // cruzá-las anunciaria uma queda que é só troca de critério.
            var movimentos = new Dictionary<int, List<EvolucaoDoAluno.Movimento>>();
            foreach (var doProfessor in fichas.GroupBy(f => f.ProfessorId))
            {
                var emOrdem = doProfessor.OrderBy(f => f.CriadoEm).ToList();
                for (int i = 1; i < emOrdem.Count; i++)
                    movimentos[emOrdem[i].Id] = EvolucaoDoAluno.Comparar(emOrdem[i - 1].Notas, emOrdem[i].Notas);
            }

            ViewBag.Movimentos = movimentos;
            ViewBag.Professores = fichas
                .GroupBy(f => (f.ProfessorId, f.Professor))
                .Select(g => g.Key)
                .ToList();
            ViewBag.ProfessorFiltro = professorId;

            return View(fichas);
        }
    }

    // O que o aluno pode ver de uma ficha. A anotação privada NÃO está aqui de propósito —
    // um tipo que não tem o campo não vaza o campo.
    public record FichaDoAluno(
        int Id,
        int ProfessorId,
        string Professor,
        string Modulo,
        DateTime CriadoEm,
        DateTime? LiberadaEm,
        string? RecadoAoAluno,
        List<EvolucaoDoAluno.NotaLida> Notas);
}
