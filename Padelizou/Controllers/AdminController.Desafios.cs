using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace padelizou.Controllers
{
    // A tela /Admin/DesafiosEmDisputa: dar saída pro placar que as duas duplas não acertaram.
    //
    // POR QUE ELA EXISTE: quando alguém contesta, o desafio para em `Em disputa` e ninguém
    // pontua — o sistema não escolhe um lado sozinho. Sem esta tela, porém, o jogo ficava preso
    // PARA SEMPRE: nenhum botão em lugar nenhum, e o primeiro desacordo de placar viraria um
    // chamado no WhatsApp do Felipe sem nada do outro lado pra resolver.
    //
    // Por que no PAINEL e não em /Desafios: dentro de admin.padelizou.com.br o AdminHostMiddleware
    // serve SÓ /Admin e /Auth — de lá, qualquer caminho /Desafios dá 404. É a mesma lição do
    // /Admin/Times.
    //
    // ⚠️ As regras não são reescritas aqui: quem diz o que pode ser resolvido é
    // Services/ResolucaoDaDisputa, e quem FECHA o placar continua sendo o
    // Services/FechamentoDoDesafio — o mesmo caminho do botão da outra dupla e do relógio das
    // 72h. Fechar "na mão" aqui seria a segunda cópia da conta de pontos e do cinturão.
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> DesafiosEmDisputa()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var emDisputa = await _context.Desafios
                .AsNoTracking()
                .Include(d => d.DesafianteJogador1).Include(d => d.DesafianteJogador2)
                .Include(d => d.DesafiadoJogador1).Include(d => d.DesafiadoJogador2)
                .Include(d => d.CategoriaPadrao).Include(d => d.Clube)
                .Where(d => d.Status == Desafio.EmDisputa)
                .OrderBy(d => d.DataHora)
                .ToListAsync();

            // Quem lançou o placar — a tela precisa dizer de quem é a versão que está na mesa.
            var idsQueLancaram = emDisputa
                .Where(d => d.LancadoPorId != null)
                .Select(d => d.LancadoPorId!.Value)
                .Distinct()
                .ToList();

            var nomes = await _context.Jogadores
                .AsNoTracking()
                .Where(j => idsQueLancaram.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => NomeBonito.ComApelido(j.Nome, j.Apelido));

            // As últimas resolvidas ficam logo abaixo: é o que permite conferir uma decisão
            // antiga quando alguém reclama semanas depois.
            var resolvidas = await _context.Desafios
                .AsNoTracking()
                .Include(d => d.DesafianteJogador1).Include(d => d.DesafianteJogador2)
                .Include(d => d.DesafiadoJogador1).Include(d => d.DesafiadoJogador2)
                .Include(d => d.CategoriaPadrao).Include(d => d.Clube)
                .Where(d => d.ResolvidoPorId != null)
                .OrderByDescending(d => d.Id)
                .Take(10)
                .ToListAsync();

            return View(new DesafiosEmDisputaVM(
                emDisputa.Select(d => new DisputaNaTela(d,
                    DuplaNaTela.Nome(d.DesafianteJogador1, d.DesafianteJogador2),
                    DuplaNaTela.Nome(d.DesafiadoJogador1, d.DesafiadoJogador2),
                    d.LancadoPorId != null ? nomes.GetValueOrDefault(d.LancadoPorId.Value) : null)).ToList(),
                resolvidas.Select(d => new DisputaNaTela(d,
                    DuplaNaTela.Nome(d.DesafianteJogador1, d.DesafianteJogador2),
                    DuplaNaTela.Nome(d.DesafiadoJogador1, d.DesafiadoJogador2),
                    null)).ToList()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolverDisputa(int id, string desfecho, string? justificativa,
            int? setsDesafiante, int? setsDesafiado, int? gamesDesafiante, int? gamesDesafiado,
            [FromServices] FechamentoDoDesafio fechamento)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return Forbid();

            var desafio = await _context.Desafios.FirstOrDefaultAsync(d => d.Id == id);
            if (desafio == null) return NotFound();

            var anular = desfecho == "anular";
            var escolha = anular ? DesfechoDaDisputa.Anular : DesfechoDaDisputa.ValeOPlacar;

            // Formato sem sets não guarda sets: o mesmo cuidado do lançamento do jogador, senão
            // um número digitado aqui decidiria o vencedor por um campo que a tela não mostrou.
            if (!FormatoDoDesafio.PedeSets(desafio.Formato))
            {
                setsDesafiante = null;
                setsDesafiado = null;
            }

            // No "vale o placar" sem correção, o que vale é o que já estava lançado.
            var sets1 = setsDesafiante ?? desafio.SetsDesafiante;
            var sets2 = setsDesafiado ?? desafio.SetsDesafiado;
            var games1 = gamesDesafiante ?? desafio.GamesDesafiante;
            var games2 = gamesDesafiado ?? desafio.GamesDesafiado;

            var impedimento = ResolucaoDaDisputa.MotivoParaNaoResolver(
                desafio, escolha, justificativa, sets1, sets2, games1, games2);

            if (impedimento != null)
            {
                TempData["Erro"] = impedimento;
                return RedirectToAction(nameof(DesafiosEmDisputa));
            }

            desafio.ResolvidoPorId = admin.Id;
            desafio.ResolucaoDaDisputa = ResolucaoDaDisputa.TextoDaResolucao(escolha, justificativa!);

            if (anular)
            {
                desafio.Status = Desafio.Anulado;
                // Jogo cujo placar não se estabeleceu não mede nada — mesma régua do W.O.
                desafio.PontosDesafiante = 0;
                desafio.PontosDesafiado = 0;
                await _context.SaveChangesAsync();
            }
            else
            {
                desafio.SetsDesafiante = sets1;
                desafio.SetsDesafiado = sets2;
                desafio.GamesDesafiante = games1;
                desafio.GamesDesafiado = games2;

                // ⚠️ Fecha pelo caminho ÚNICO: é ele que congela os pontos e move o cinturão.
                // `confirmadoPorId` é o admin — "quem confirmou este placar?" é a primeira
                // pergunta de qualquer discussão, e a resposta aqui é uma pessoa, não o relógio.
                await fechamento.FecharAsync(desafio, admin.Id, DateTime.Now);
            }

            await AvisarOsQuatroAsync(desafio, anular);

            TempData["Sucesso"] = anular
                ? "Desafio anulado. Ninguém pontuou."
                : "Placar validado. Os pontos entraram no ranking.";

            return RedirectToAction(nameof(DesafiosEmDisputa));
        }

        // Os quatro envolvidos são avisados — inclusive quem ganhou a disputa. Uma decisão sobre
        // briga entre usuários que ninguém comunica é uma decisão que vira a próxima reclamação.
        private async Task AvisarOsQuatroAsync(Desafio desafio, bool anulado)
        {
            var aviso = anulado
                ? AvisoDoDesafio.DisputaAnulada()
                : AvisoDoDesafio.DisputaResolvida(PlacarDoDesafio.Texto(desafio));

            foreach (var jogadorId in desafio.Envolvidos.Distinct())
            {
                await _pushNotificationService.EnviarParaJogadorAsync(jogadorId,
                    aviso.Titulo, aviso.Corpo, "/Desafios/Meus", AlcanceDoAviso.SoApp);
            }
        }
    }
}
