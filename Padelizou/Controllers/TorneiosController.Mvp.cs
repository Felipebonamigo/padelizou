using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // O MVP DO TORNEIO: quem jogou elege o melhor entre os campeões.
    //
    // Regra inteira em Services/MvpDoTorneio — aqui só entra o que é HTTP. A tela esconde o que
    // não cabe, mas quem RECUSA é sempre o serviço: um POST montado à mão não passa por view
    // nenhuma.
    public partial class TorneiosController
    {
        // A tela da votação. Pública: quem não jogou não vota, mas ver quem foi eleito é
        // justamente o que faz a página valer a pena compartilhar.
        [HttpGet]
        public async Task<IActionResult> Mvp(int id)
        {
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;

            var votacao = await MvpDoTorneio.DoTorneioAsync(_context, id, meuId, DateTime.Now);
            if (votacao == null) return NotFound();

            // A ENQUETE é perguntada à parte, e não pendurada no `votacao.Aberta`, porque ela
            // ignora o INTERRUPTOR do organizador: no torneio normal com a eleição desligada
            // ela é a única coisa da tela. (No Americano não há nem uma nem outra.)
            var enqueteAberta = EnqueteDoTorneio.Aberta(
                votacao.StatusDoTorneio, votacao.UltimoJogo, DateTime.Now, votacao.Formato);

            // ⚠️ 404 e não uma tela vazia: torneio que ainda não acabou não tem NADA pra
            // mostrar, e uma página dizendo "nada aqui" é um link que só sabe decepcionar.
            // ⚠️ Mas basta UMA das duas coisas existir — senão o Americano, que só tem a
            // enquete, cairia no 404 e a coleta do "Melhor Clube" morreria calada nele.
            if (!votacao.Aberta && !votacao.Encerrada && !enqueteAberta) return NotFound();

            if (meuId != null && votacao.SouEleitor && enqueteAberta)
            {
                ViewBag.EnqueteAberta = true;
                ViewBag.MinhaAvaliacao = await _context.AvaliacoesDeTorneio
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TorneioId == id && a.JogadorId == meuId.Value);

                // O texto sobre o Padelizou mora na caixa de feedback, não na avaliação — mas
                // o formulário precisa devolvê-lo preenchido, senão "ajustar a resposta"
                // apagaria em silêncio o que a pessoa escreveu na primeira vez.
                ViewBag.MeuTextoDoSistema = await _context.FeedbacksSite
                    .AsNoTracking()
                    .Where(f => f.TorneioId == id && f.JogadorId == meuId.Value)
                    .Select(f => f.Texto)
                    .FirstOrDefaultAsync();
            }

            return View(votacao);
        }

        // A resposta da enquete. POST, nunca GET — grava; e o id de quem responde é IMPOSTO
        // pela sessão, como no voto logo abaixo.
        //
        // ⚠️ `anonimo` chega de um par de RADIOS, e não de uma caixa de marcar. Caixa
        // desmarcada não vai no POST, o que obrigaria ao `<input type="hidden">` de
        // acompanhamento — e foi exatamente esse arranjo que, com o escondido na FRENTE, fez o
        // binder ler o primeiro valor e DESLIGAR a votação de MVP de quem a ligava. Radio
        // sempre manda um valor, e a escolha é a coisa mais importante desta tela.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AvaliarTorneio(int id, int notaClube, int notaOrganizacao,
            int? notaSistema, string? comentarioClube, string? comentarioOrganizacao,
            string? comentarioSistema, bool anonimo = false)
        {
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var recusa = await EnqueteDoTorneio.AvaliarAsync(_context, id, meuId, new RespostaDaEnquete
            {
                NotaClube = notaClube,
                NotaOrganizacao = notaOrganizacao,
                NotaSistema = notaSistema,
                ComentarioClube = comentarioClube,
                ComentarioOrganizacao = comentarioOrganizacao,
                ComentarioSistema = comentarioSistema,
                Anonimo = anonimo,
            }, DateTime.Now);

            if (recusa != null) TempData["Erro"] = recusa;
            else if (anonimo)
                TempData["Sucesso"] = "Avaliação registrada — obrigado! Ela chega sem o seu nome pra quem organiza "
                    + "e pro clube. Dá pra ajustar enquanto a janela estiver aberta.";
            else
                TempData["Sucesso"] = "Avaliação registrada — obrigado! Dá pra ajustar enquanto a janela estiver aberta.";

            return RedirectToAction(nameof(Mvp), new { id });
        }

        // PUBLICAR (ou tirar do mural) um comentário. Quem decide é o organizador, o dono do
        // clube ou o Padelizou — e quem CONFERE isso é o serviço, com o id da sessão.
        //
        // Volta pra página do torneio: é lá que estão a lista de moderação e o mural, e mandar
        // a pessoa pra outra tela depois de clicar seria perder o lugar da leitura.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublicarAvaliacao(int id, int avaliacaoId, bool publicar)
        {
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var recusa = await EnqueteDoTorneio.PublicarAsync(
                _context, avaliacaoId, meuId, publicar, DateTime.Now);

            if (recusa != null) TempData["Erro"] = recusa;
            else TempData["Sucesso"] = publicar
                ? "Comentário publicado na página do torneio."
                : "Comentário tirado do mural — ele continua aqui pra você.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // ⚠️ POST, nunca GET — votar grava. Link que grava é disparado por pré-carregamento do
        // navegador e por varredor de página, mesma razão do seguir-torneio e do convite pra
        // panelinha.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VotarMvp(int id, int candidatoId)
        {
            // ⚠️ O id do votante é IMPOSTO pela sessão, nunca lido do formulário. É a mesma
            // trava do 5º perfil: aceitar um "votanteId" do POST deixaria qualquer um votar em
            // nome de todo mundo que jogou.
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var recusa = await MvpDoTorneio.VotarAsync(_context, id, meuId, candidatoId, DateTime.Now);

            if (recusa != null) TempData["Erro"] = recusa;
            else TempData["Sucesso"] = "Voto registrado! Dá pra trocar enquanto a votação estiver aberta.";

            return RedirectToAction(nameof(Mvp), new { id });
        }
    }
}
