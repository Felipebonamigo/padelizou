using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // A ARTE DO JOGO PRO STORY (14/09/2026).
    //
    // 🗣️ Felipe, com o print de uma arte da semifinal do ER Padel Tour montada à mão:
    // *"Conseguimos fazer um Botao no sistema, que ele ja crie essa arte e apenas tiremos a
    // foto na hora para postarmos nos stories do instagram, colocando o @ da pessoa ja quando
    // tiver no cadastro?"*
    //
    // ⚠️ ESTAS AÇÕES MORAM AQUI, E NÃO NO `CartoesController` — e não é arrumação de arquivo, é
    // a régua de autorização. Quem gera a arte é organizador OU marcador (é o marcador que
    // está na quadra com o celular na hora da foto), e essa pergunta é
    // `PodeOperarODiaDeJogoAsync`, que é PRIVADA deste controller. Levá-la pro controller dos
    // cards criaria o QUARTO lugar de uma checagem que o CLAUDE.md já registra como precisando
    // andar junto em três — e uma dessincronia dessas quebrou a Mesa de Controle em 31/07.
    // Precedente exato: o `CartaoDoPlacarAoVivo` mora no `TorneiosController.Placar.cs` pelo
    // mesmo tipo de razão.
    //
    // ⚠️ FAMÍLIA FECHADA, pela taxonomia escrita no `CartoesController`: a arte imprime o @ de
    // quatro pessoas que não escolheram aparecer nela. Então exige login, a tela NÃO declara
    // `og:image` (o servidor da Meta busca a prévia sem sessão nenhuma — é por ali que o dado
    // vazaria) e o PNG sai com `Cache-Control: private`.
    //
    // ⚠️ NADA É GRAVADO EM DISCO: nem o PNG que sai, nem a foto que entra. Ver a nota do
    // `ArteDoJogoParaStory`.
    public partial class TorneiosController
    {
        // A lista de jogos do torneio pra escolher de qual gerar a arte.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ArtesDosJogos(int id, [FromServices] FonteDoCartao fontes)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            ViewBag.Torneio = torneio;
            ViewBag.FonteDisponivel = fontes.Disponivel;
            return View(await JogosParaArte.DoTorneioAsync(_context, id));
        }

        // A tela de um jogo: a arte já desenhada (sem foto) e o campo pra tirar a foto.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ArteDoJogo(int id, [FromServices] FonteDoCartao fontes)
        {
            var (jogo, torneioId) = await JogoQuePodeVirarArteAsync(id);
            if (torneioId == null) return NotFound();
            if (!await PodeOperarODiaDeJogoAsync(torneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();
            if (jogo == null) return NotFound();

            ViewBag.TorneioId = torneioId.Value;
            ViewBag.FonteDisponivel = fontes.Disponivel;
            return View(jogo);
        }

        // O PNG SEM FOTO: é a prévia que a tela mostra no `<img>` e também serve baixado, pra
        // colar a foto por cima no editor do Instagram.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ArteDoJogoImagem(int id, [FromServices] FonteDoCartao fontes) =>
            await DesenharAsync(id, foto: null, fontes);

        // O PNG COM A FOTO QUE ACABOU DE SER TIRADA.
        //
        // ⚠️ `[HttpPost]` com `[Authorize]` e antifalsificação, mesmo NÃO GRAVANDO NADA no
        // banco: é POST porque recebe arquivo, e o gate mecânico
        // (`GateDeAutorizacaoDosPostsTests`) varre o assembly e quebra se um POST novo atender
        // sem login. A checagem de dono é a linha do `PodeOperarODiaDeJogoAsync` — o gate não
        // cobre essa parte, ela é trabalho deste arquivo.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ArteDoJogoImagem(
            int id, IFormFile? foto, [FromServices] FonteDoCartao fontes)
        {
            // ⚠️ A AUTORIZAÇÃO VEM ANTES DE LER O ARQUIVO. Decodificar a imagem custa memória
            // e CPU: fazer isso antes de saber quem está pedindo deixaria qualquer pessoa
            // logada gastar o servidor mandando fotos de 25 MB pra um torneio alheio.
            var (_, torneioId) = await JogoQuePodeVirarArteAsync(id);
            if (torneioId == null) return NotFound();
            if (!await PodeOperarODiaDeJogoAsync(torneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();

            using var imagem = await ArteDoJogoParaStory.FotoDoEnvioAsync(foto, _logger);

            // Foto que não pôde ser lida NÃO derruba a arte (a moldura vazia ainda é postável),
            // mas também não passa calada: sem este aviso a pessoa escolhe a foto, recebe a
            // arte vazia e vai embora achando que o sistema não sabe pôr foto.
            if (foto != null && foto.Length > 0 && imagem == null)
            {
                TempData["Erro"] = "Não conseguimos ler essa foto. Tente de novo, ou escolha outra.";
                return RedirectToAction(nameof(ArteDoJogo), new { id });
            }

            return await DesenharAsync(id, imagem, fontes);
        }

        private async Task<IActionResult> DesenharAsync(int id, SkiaSharp.SKBitmap? foto, FonteDoCartao fontes)
        {
            // A recusa por falta de fonte vem ANTES de qualquer consulta, e é 404 e não uma
            // imagem em branco — mesma régua de todos os cards (ver FonteDoCartao): sem a
            // Poppins a arte sairia com todos os textos invisíveis no Linux.
            if (!fontes.Disponivel) return NotFound();

            var (jogo, torneioId) = await JogoQuePodeVirarArteAsync(id);
            if (torneioId == null) return NotFound();
            if (!await PodeOperarODiaDeJogoAsync(torneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();
            if (jogo == null) return NotFound();

            var png = ArteDoJogoParaStory.Desenhar(jogo, foto, fontes, _env.WebRootPath ?? "");

            // `publico: false` — a arte carrega o @ de quatro pessoas, e `Cache-Control: public`
            // autorizaria qualquer cache no caminho a devolvê-la a OUTRA pessoa.
            return EntregaDeCard.Png(Response, png, $"arte-jogo-{jogo.PartidaId}.png", publico: false);
        }

        // O jogo e o torneio dele, numa consulta só.
        //
        // Devolve o TORNEIO separado de propósito: a autorização precisa do torneio mesmo
        // quando o jogo não tem o que desenhar, e sem isso a ordem das recusas vazaria a
        // existência do jogo pra quem não organiza (404 pra jogo que não existe, 403 pra jogo
        // de torneio alheio — a diferença é uma resposta a quem não devia receber nenhuma).
        private async Task<(JogoParaArte? Jogo, int? TorneioId)> JogoQuePodeVirarArteAsync(int partidaId)
        {
            var torneioId = await _context.Partidas.AsNoTracking()
                .Where(p => p.Id == partidaId)
                .Select(p => p.TorneioId)
                .FirstOrDefaultAsync();

            if (torneioId == null) return (null, null);

            return (await JogosParaArte.DoJogoAsync(_context, partidaId), torneioId);
        }
    }
}
