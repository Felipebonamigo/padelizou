using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers
{
    // ── "VOCÊ FOI INSCRITO PARA UM TORNEIO POR MAICKEL" (16/09/2026) ─────────────────────
    //
    // 🗣️ Felipe: *"para quando alguem inscrever um parceiro no torneio, avisar o parceiro e
    // permitir recusar, ao recusar o primeiro fica sozinho no torneio e o avisa"*.
    //
    // ⚠️ POR QUE ESTA PORTA EXISTE, se "sair da dupla" já existia: quem foi inscrito por outra
    // pessoa não está desistindo de nada — ela nunca entrou por vontade própria. O botão de
    // desistir PERGUNTA "sai só você ou os dois?", que é uma pergunta sem sentido pra quem não
    // pediu pra estar ali, e cuja resposta errada tira do torneio quem queria jogar. Aqui a
    // resposta já é a certa: sai quem recusou, fica quem inscreveu.
    //
    // ⚠️ E O EFEITO É O MESMO DA DESISTÊNCIA, de propósito (Services/DesistenciaDeInscricao):
    // sai um, o outro continua inscrito e sem parceiro, e a vaga NÃO abre. Duas réguas escritas
    // separadas pra "tirar alguém de uma dupla" é como uma delas acaba deixando sair depois do
    // sorteio — a dessincronia que quebrou a Mesa de Controle em 31/07.
    public partial class TorneiosController
    {
        // A tela da decisão. É pra cá que o aviso leva, e é o que a faixa da tela do torneio
        // abre — aviso é lembrete, não pode ser a única porta.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> RecusarInscricao(int duplaId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;

            var dupla = await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .FirstOrDefaultAsync(d => d.Id == duplaId);

            if (dupla == null) return NotFound();

            var pergunta = await _context.InscritosPorOutro
                .FirstOrDefaultAsync(p => p.DuplaId == duplaId && p.JogadorId == meuId);

            var torneio = dupla.Categoria.Torneio;

            if (InscricaoDeOutraPessoa.MotivoParaNaoRecusar(dupla, torneio, pergunta, meuId) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction(nameof(Details), new { id = torneio.Id });
            }

            var quemInscreveu = await _context.Jogadores
                .Where(j => j.Id == pergunta!.InscritoPorId)
                .Select(j => j.Nome)
                .FirstOrDefaultAsync();

            // Quem CONTINUA inscrito se eu recusar — nulo quando a inscrição inteira acaba.
            // Vem da mesma régua que vai aplicar o efeito, e não de um `if` daqui: tela e
            // servidor prometendo desfechos diferentes é o pior dos dois mundos.
            var ficaId = DesistenciaDeInscricao.QuemFica(dupla, meuId);
            var fica = ficaId == dupla.Jogador1Id ? dupla.Jogador1 : dupla.Jogador2;

            return View(new RecusarInscricaoVM(
                dupla.Id, torneio.Id, torneio.Nome, dupla.Categoria.Nome,
                NomeBonito.Curto(quemInscreveu ?? ""),
                ficaId == null ? null : NomeBonito.Curto(fica?.Nome ?? ""),
                dupla.Pago,
                pergunta!.ConfirmadoEm != null));
        }

        // A RECUSA.
        //
        // `confirmo` é o que separa esta ação do GET de cima (mesma dupla GET/POST do
        // ExcluirConta) — e serve de cinto: POST sem a confirmação da tela não tira ninguém
        // do torneio.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecusarInscricao(int duplaId, bool confirmo)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;

            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var torneio = await _context.Torneios.FindAsync(torneioId);

            var pergunta = await _context.InscritosPorOutro
                .FirstOrDefaultAsync(p => p.DuplaId == duplaId && p.JogadorId == meuId);

            if (InscricaoDeOutraPessoa.MotivoParaNaoRecusar(dupla, torneio, pergunta, meuId) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction(nameof(Details), new { id = torneioId });
            }

            if (!confirmo)
            {
                TempData["Erro"] = "Confirme a recusa na tela.";
                return RedirectToAction(nameof(RecusarInscricao), new { duplaId });
            }

            var euMesmo = await _context.Jogadores.FindAsync(meuId);
            var quemInscreveuId = pergunta!.InscritoPorId;
            bool eraConfirmada = !dupla.EmListaDeEspera;
            bool eraPaga = dupla.Pago;
            int categoriaId = dupla.CategoriaId;

            // ⚠️ `SoEu` SEMPRE: recusar é dizer "eu não vou", nunca "nós não vamos". Quem
            // inscreveu continua com a vaga dele — a régua decide sozinha que a inscrição acaba
            // quando não sobra ninguém nela.
            var quemFica = await TirarDaInscricaoAsync(dupla, EscolhaDeQuemSai.SoEu, meuId);

            // ⚠️ A RECUSA É A QUINTA PORTA DE SAÍDA, e ela quase ficou de fora do histórico: o
            // pedido falava de "cancelar a inscrição", e recusar não se chama assim em lugar
            // nenhum da tela. Mas o efeito é o mesmo — alguém que estava inscrito deixou de
            // estar —, e um histórico que pula esta some justamente com quem nunca quis entrar.
            //
            // `abriuVaga` sai de `quemFica`: sobrou parceiro, a inscrição continua de pé e a
            // vaga não abriu; não sobrou ninguém, ela acabou.
            await RegistrarSaidaAsync(torneio!, categoriaId, meuId, null, meuId,
                MotivoDaSaida.Desistiu, observacao: null, eraPaga, abriuVaga: quemFica == null);

            var nome = euMesmo?.ComoChamar ?? "";

            if (quemFica is { } parceiro)
            {
                var (titulo, corpo) = TextoDeQuemFoiInscrito.ParaQuemFicou(nome, torneio!.Nome,
                    ficouSozinho: true);
                await AvisarAsync(new[] { parceiro }, titulo, corpo, torneioId);
            }

            // Quem inscreveu e NÃO está na dupla (o organizador que montou a inscrição) só
            // descobriria a recusa na hora de montar a chave — que é tarde pra chamar outra
            // pessoa. Quem está na dupla já foi avisado logo acima, e mandar dois avisos pra
            // mesma pessoa é como se ensina alguém a ignorar o canal.
            if (quemInscreveuId != quemFica && quemInscreveuId != meuId)
            {
                var (titulo, corpo) = TextoDeQuemFoiInscrito.ParaQuemInscreveu(nome, torneio!.Nome);
                await AvisarAsync(new[] { quemInscreveuId }, titulo, corpo, torneioId);
            }

            // A inscrição acabou de verdade: a vaga volta pra fila e o dinheiro que entrou por
            // ela vira assunto do organizador — as duas coisas que a desistência já faz.
            if (quemFica == null && eraConfirmada)
            {
                await PromoverDaListaDeEsperaAsync(categoriaId, torneio!);
            }

            TempData["Sucesso"] = quemFica == null
                ? "Pronto, você recusou a inscrição. Ela era só sua, então foi cancelada."
                : "Pronto, você recusou a inscrição. Quem te inscreveu continua no torneio e foi avisado.";
            return RedirectToAction(nameof(Details), new { id = torneioId });
        }

        // "ESTÁ CERTO, QUERO JOGAR". Não destrava nada — a inscrição já está de pé —, mas tira
        // a faixa das duas telas e registra que a pessoa soube.
        //
        // ⚠️ Não é um contrato: quem confirma e muda de ideia continua podendo recusar enquanto
        // as inscrições estiverem abertas. Prender alguém por ter clicado num botão de "ok"
        // seria transformar um aviso em compromisso.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarInscricao(int duplaId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;

            var pergunta = await _context.InscritosPorOutro
                .FirstOrDefaultAsync(p => p.DuplaId == duplaId && p.JogadorId == meuId);

            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            // Só o dono da pergunta responde a própria pergunta.
            if (pergunta == null) return Forbid();

            pergunta.ConfirmadoEm = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Combinado! Bom torneio.";
            return RedirectToAction(nameof(Details), new { id = dupla.Categoria.TorneioId });
        }

        // ── O EFEITO, NUM LUGAR SÓ ───────────────────────────────────────────────────────
        //
        // Tira `quemSaiId` da inscrição e devolve QUEM FICOU (null = a inscrição acabou).
        // Usada pela desistência e pela recusa: o efeito é o mesmo, os textos é que mudam — e
        // por isso esta função não avisa ninguém.
        private async Task<int?> TirarDaInscricaoAsync(Dupla dupla, EscolhaDeQuemSai escolha, int quemSaiId)
        {
            var quemFica = DesistenciaDeInscricao.QuemFica(dupla, quemSaiId);

            if (DesistenciaDeInscricao.Efeito(dupla, escolha) == EfeitoDaDesistencia.SoSaiQuemDesistiu)
            {
                // A vaga NÃO abre: o parceiro continua inscrito, agora sem dupla fechada. Ele
                // assume a cadeira de Jogador1 porque essa coluna não é anulável.
                dupla.Jogador1Id = quemFica!.Value;
                dupla.Jogador2Id = null;

                // A pergunta de quem saiu vai junto: respondida é respondida, e uma linha órfã
                // traria a faixa de volta pra quem não está mais na inscrição.
                var minha = await _context.InscritosPorOutro
                    .Where(p => p.DuplaId == dupla.Id && p.JogadorId == quemSaiId)
                    .ToListAsync();
                _context.InscritosPorOutro.RemoveRange(minha);
            }
            else
            {
                // ⚠️ As perguntas saem À MÃO, e não só pelo cascade do banco: o cascade existe
                // (ver DbPadelContext) e é ele que vale em produção, mas o EF InMemory dos
                // testes não apaga dependente que não está sendo rastreado — e um teste que
                // enxerga linha órfã onde produção não tem é um teste que mente nos dois
                // sentidos possíveis.
                var todas = await _context.InscritosPorOutro
                    .Where(p => p.DuplaId == dupla.Id)
                    .ToListAsync();
                _context.InscritosPorOutro.RemoveRange(todas);
                _context.Duplas.Remove(dupla);
            }

            await _context.SaveChangesAsync();
            return quemFica;
        }
    }
}
