using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // A decisão do organizador sobre quem o Ranking RS barrou.
    //
    // A recusa da inscrição (Services/ValidacaoPeloRankingRs) nunca é a palavra final: ela vira
    // uma linha pendente, e é AQUI que o organizador diz se aquilo fica de pé. Sem esta tela, a
    // recusa seria um beco sem saída — o jogador barrado teria que caçar o organizador no
    // WhatsApp e o organizador não saberia que alguém tentou entrar.
    public partial class TorneiosController
    {
        // Grava qual categoria do Ranking RS cada categoria daqui representa. As duas listas
        // vêm em par, posição a posição, do formulário de configuração do torneio.
        //
        // ⚠️ Lista VAZIA não apaga nada. Uma aba aberta antes deste deploy manda o formulário
        // sem estes campos, e "não veio" não pode ser lido como "o organizador desmarcou tudo"
        // — isso desligaria a validação do torneio inteiro sem ninguém pedir.
        private async Task SalvarDeParaDoRankingAsync(int torneioId, int[]? categoriaIds, int[]? rsIds)
        {
            if (categoriaIds == null || rsIds == null || categoriaIds.Length == 0) return;

            var categorias = await _context.Categorias
                .Where(c => c.TorneioId == torneioId)
                .ToDictionaryAsync(c => c.Id);

            int quantos = Math.Min(categoriaIds.Length, rsIds.Length);
            for (int i = 0; i < quantos; i++)
            {
                // Categoria de outro torneio no meio do formulário não é acidente possível na
                // tela — mas o POST se monta à mão, e aqui se decide quem pode se inscrever.
                if (!categorias.TryGetValue(categoriaIds[i], out var categoria)) continue;

                // 0 (ou id que não existe) = "não validar esta categoria contra o ranking".
                categoria.RankingRsCategoriaId =
                    CategoriaDoRankingRs.IdExiste(rsIds[i]) ? rsIds[i] : null;
            }
        }

        // Quem manda no bloqueio é quem manda no torneio. A checagem é por TORNEIO e não por
        // bloqueio: o id do bloqueio vem do formulário, e sem amarrar um ao outro daria pra
        // liberar gente no torneio dos outros mandando um id qualquer.
        private async Task<BloqueioDoRanking?> BloqueioQuePossoDecidirAsync(int bloqueioId)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var jogadorId) || jogadorId <= 0) return null;

            var bloqueio = await _context.BloqueiosDoRanking
                .FirstOrDefaultAsync(b => b.Id == bloqueioId);
            if (bloqueio == null) return null;

            return await EhOrganizadorAsync(bloqueio.TorneioId, jogadorId) ? bloqueio : null;
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LiberarBloqueioRanking(int id)
        {
            var bloqueio = await BloqueioQuePossoDecidirAsync(id);
            if (bloqueio == null) return Forbid();

            await DecidirAsync(bloqueio, SituacaoDoBloqueio.Liberado);

            TempData["Sucesso"] = $"{NomeBonito.Formatar(bloqueio.NomeConsultado)} liberado(a) para se inscrever "
                + $"em {bloqueio.Categoria?.Nome ?? "esta categoria"}. "
                + "Avise a pessoa: ela precisa fazer a inscrição de novo.";
            return RedirectToAction(nameof(Details), new { id = bloqueio.TorneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManterBloqueioRanking(int id)
        {
            var bloqueio = await BloqueioQuePossoDecidirAsync(id);
            if (bloqueio == null) return Forbid();

            await DecidirAsync(bloqueio, SituacaoDoBloqueio.Mantido);

            // "Não liberado" e não "recusa mantida": o organizador acabou de clicar em
            // "Não liberar", e a confirmação tem que devolver a palavra que ele usou.
            TempData["Sucesso"] = $"{NomeBonito.Formatar(bloqueio.NomeConsultado)} não foi liberado(a). "
                + "A pessoa continua sem conseguir se inscrever nesta categoria.";
            return RedirectToAction(nameof(Details), new { id = bloqueio.TorneioId });
        }

        private async Task DecidirAsync(BloqueioDoRanking bloqueio, string situacao)
        {
            await _context.Entry(bloqueio).Reference(b => b.Categoria).LoadAsync();

            bloqueio.Situacao = situacao;
            bloqueio.DecididoEm = DateTime.Now;
            bloqueio.DecididoPorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var eu) && eu > 0
                ? eu : null;

            await _context.SaveChangesAsync();
        }
    }
}
