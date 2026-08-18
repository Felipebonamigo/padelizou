using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // PUBLICAR e ESCONDER o torneio — o interruptor que faltava (Felipe, 18/08/2026: "ainda
    // vamos divulgar, e só aí permitir que o pessoal veja").
    //
    // ⚠️ BOTÃO PRÓPRIO, e não mais uma caixinha no formulão de edição. Eram duas coisas
    // erradas juntas: a caixa vivia DENTRO da seção do "torneio restrito" (então o torneio
    // aberto — o caso comum — não tinha como ficar escondido), e qualquer salvamento da
    // edição regravava o campo, o que fazia mexer no preço poder publicar o torneio sem
    // ninguém pedir.
    //
    // A régua de QUEM VÊ mora em Services/VisibilidadeDoTorneio, e é a mesma que o Details,
    // os Jogos, a classificação e as duas inscrições consultam. Aqui só entra o que é HTTP.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AlternarVisibilidade(int id, bool ocultar)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Publicar/esconder é poder de ORGANIZADOR, não de quem só olha: `EhOrganizadorAsync`
            // (que inclui o admin do Padelizou) e não `PodeOlharAGestaoAsync` — o assistente do
            // sistema enxerga o painel inteiro e não muda nada nele.
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // Já está como se pediu: sai sem gravar e sem mentir na tela. Dois cliques no mesmo
            // botão (ou o F5 depois dele) não podem virar duas mensagens diferentes.
            if (torneio.Oculto == ocultar)
            {
                TempData["Sucesso"] = ocultar
                    ? $"\"{torneio.Nome}\" já estava oculto."
                    : $"\"{torneio.Nome}\" já estava publicado.";
                return RedirectToAction("Details", new { id });
            }

            torneio.Oculto = ocultar;
            await _context.SaveChangesAsync();

            if (ocultar)
            {
                // Quem já está dentro NÃO perde a página (ver VisibilidadeDoTorneio) — e a
                // frase diz isso, porque esconder um torneio com gente inscrita é exatamente
                // a hora em que o organizador precisa saber que não trancou ninguém do lado
                // de fora.
                var inscritos = await _context.Duplas.CountAsync(d => d.Categoria.TorneioId == id)
                                + await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == id);

                TempData["Sucesso"] = $"\"{torneio.Nome}\" está OCULTO: sumiu da lista de torneios, "
                    + "da página inicial e do Google, e quem tiver o link não consegue mais abrir."
                    + (inscritos > 0
                        ? $" Quem já está inscrito ({inscritos}) continua vendo a página normalmente."
                        : "");
            }
            else
            {
                // ── O SEGUNDO GATILHO DO "NOVO TORNEIO ABERTO" ────────────────────────
                // Sem isto, o caminho normal do torneio guardado — criar oculto, ser aprovado,
                // publicar no dia do anúncio — não avisava NINGUÉM: o disparo só existia na
                // aprovação, e lá o torneio ainda estava escondido. O organizador publicava e
                // ficava esperando um movimento que nunca vinha.
                //
                // Quem decide se sai é o serviço, não este `if`: ele recusa torneio não
                // aprovado (ninguém anuncia o que o Padelizou ainda não olhou) e torneio já
                // avisado — o carimbo `AvisoDeTorneioNovoEm` é o que impede que esconder e
                // publicar de novo vire um anúncio novo à base a cada volta.
                var aviso = await AvisoDeTorneioNovo.EnviarSePuderAsync(
                    _context, _pushService, torneio,
                    Url.Action("Details", "Torneios", new { id = torneio.Id }));

                TempData["Sucesso"] = (torneio.AprovadoEm == null
                    ? $"\"{torneio.Nome}\" foi publicado — assim que o Padelizou aprovar, ele entra na lista de torneios e o aviso sai."
                    : $"\"{torneio.Nome}\" foi publicado: já aparece na lista de torneios e pra quem receber o link.")
                    + AvisoDeTorneioNovo.Frase(aviso);
            }

            return RedirectToAction("Details", new { id });
        }
    }
}
