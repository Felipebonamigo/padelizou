using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // OS PALPITEIROS: quem mais acertou no palpitômetro deste torneio.
    //
    // Regra inteira em Services/PontosDoPalpite (quanto vale um palpite) e
    // Services/RankingDePalpiteiros (quem entra na conta) — aqui só entra o que é HTTP.
    public partial class TorneiosController
    {
        // A tabela. Pública, como a do MVP: ver quem acertou é justamente o que faz a página
        // valer a pena compartilhar no grupo do torneio.
        // ⚠️ O `fasePalpiteiros` (12/09/2026) usa o MESMO nome na aba do Details, e é isso que
        // deixa a partial trocar uma chave só sem saber em qual das duas telas ela está. Valor
        // desconhecido cai em "todas as fases" (ver FaseDoPalpitometro.Normalizar) — ele vem da
        // query string, ou seja, de fora.
        [HttpGet]
        public async Task<IActionResult> Palpiteiros(int id, string? fasePalpiteiros = null)
        {
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            // Mesma porta do Details: torneio oculto não tem página pública nenhuma. Deixar UMA
            // porta de fora é como o torneio escondido reaparece.
            var torneio = await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null || !await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, meuId))
                return NotFound();

            var ranking = await RankingDePalpiteiros.DoTorneioAsync(_context, id, meuId, fasePalpiteiros);

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

        // GET: Torneios/PalpitesDoPalpiteiro/5?jogadorId=42 — o que UMA pessoa palpitou neste
        // torneio, pro modal que abre ao clicar no nome dela na tabela (12/09/2026).
        //
        // 🗣️ Felipe: *"ai clicar no nome, permita ver os resultados q a pessoa colocou mas de um
        // modo que nao quebre a tela"*.
        //
        // Público como a tabela que o abre, e pela mesma razão: ver quem acertou é o que faz a
        // página valer a pena mandar no grupo do torneio. ⚠️ Não expõe nada novo — o modal "quem
        // palpitou o quê" já lista nome e placar de todo mundo, jogo a jogo, desde 10/09/2026.
        [HttpGet]
        public async Task<IActionResult> PalpitesDoPalpiteiro(int id, int jogadorId)
        {
            // ⚠️ MESMA PORTA DO Details E DA TABELA: torneio oculto não tem página pública
            // nenhuma, e deixar UMA porta de fora é como o torneio escondido reaparece — aqui
            // com o nome e o palpite de quem votou nele.
            var torneio = await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null || !await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();

            var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(_context, id, jogadorId);

            // ⚠️ 404 e não lista vazia, pelo mesmo motivo da página inteira: quem não palpitou
            // neste torneio não tem nada aqui. E é o que o modal mostra como "não foi possível
            // carregar" em vez de um painel em branco — ver wwwroot/js/palpites-do-palpiteiro.js.
            if (lista == null) return NotFound();

            return Json(lista);
        }
    }
}
