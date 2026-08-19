using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // OS PALPITEIROS: quem mais acertou no palpitrômetro deste torneio.
    //
    // Regra inteira em Services/PontosDoPalpite (quanto vale um palpite) e
    // Services/RankingDePalpiteiros (quem entra na conta) — aqui só entra o que é HTTP.
    public partial class TorneiosController
    {
        // A tabela. Pública, como a do MVP: ver quem acertou é justamente o que faz a página
        // valer a pena compartilhar no grupo do torneio.
        [HttpGet]
        public async Task<IActionResult> Palpiteiros(int id)
        {
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            // Mesma porta do Details: torneio oculto não tem página pública nenhuma. Deixar UMA
            // porta de fora é como o torneio escondido reaparece.
            var torneio = await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null || !await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, meuId))
                return NotFound();

            var ranking = await RankingDePalpiteiros.DoTorneioAsync(_context, id, meuId);

            // ⚠️ 404 e não uma tela vazia, como no MVP: torneio sem jogo terminado (ou sem
            // palpite nenhum) não tem NADA pra mostrar, e uma página dizendo "nada aqui" é um
            // link que só sabe decepcionar.
            //
            // ⚠️ O botão do Details pergunta menos que isto — ele só sabe que EXISTE palpite em
            // jogo terminado, e não se algum deles conta pro ranking. Num torneio em que os
            // únicos palpites vieram dos quatro jogadores de suas PRÓPRIAS partidas (que ficam
            // de fora da conta), o botão aparece e esta página responde 404. É raro e é o
            // desfecho barato: a alternativa é a página do torneio, que é a mais visitada do
            // site, montar o ranking inteiro só pra decidir se desenha um botão.
            if (ranking == null || !ranking.TemRanking) return NotFound();

            return View(ranking);
        }
    }
}
