using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // Seguir o torneio: "me avise a cada inscrição nova nesta chave".
    //
    // É irmão do seguir-jogador e responde outra pergunta. Lá se acompanha uma PESSOA ("me
    // avise quando o Lucas entrar num torneio"); aqui se acompanha um TORNEIO — quem já se
    // inscreveu quer ver a chave encher pra saber contra quem vai jogar.
    //
    // Desde 20/08/2026 a inscrição já segue SOZINHA (AvisoDeInscricaoNoTorneio.SeguirAsync,
    // chamado nas três portas de inscrição). Estas ações viraram a porta de saída — e de
    // volta, pra quem desligou e mudou de ideia ou se inscreveu antes da regra existir.
    public partial class TorneiosController
    {
        // ⚠️ POST, nunca GET. Seguir grava, e coisa que grava por GET é ligada sem querer por
        // pré-carregamento de link e por varredor de página — o mesmo motivo pelo qual o
        // convite pra panelinha deixou de ser link.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeguirTorneio(int id)
        {
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            if (!await TorneioExisteAsync(id)) return NotFound();

            // O botão só aparece pra quem está inscrito, mas a REGRA mora aqui: tela esconde,
            // servidor recusa. Sem isto, seguir seria um POST solto que qualquer pessoa
            // logada alcança direto, e o alcance da rajada deixaria de ter limite.
            if (!await EstouInscritoAsync(id, meuId))
            {
                TempData["Erro"] = "Só quem está inscrito pode seguir este torneio.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Já seguir não pode virar segunda linha: a chave composta impediria de qualquer
            // jeito, mas estourar exceção num clique repetido é transformar impaciência em
            // tela de erro.
            var jaSigo = await _context.SeguidoresTorneio
                .AnyAsync(s => s.TorneioId == id && s.JogadorId == meuId);

            if (!jaSigo)
            {
                _context.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = id, JogadorId = meuId });
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Pronto! Você recebe um aviso a cada inscrição nova neste torneio.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeixarDeSeguirTorneio(int id)
        {
            var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var vinculo = await _context.SeguidoresTorneio
                .FirstOrDefaultAsync(s => s.TorneioId == id && s.JogadorId == meuId);

            if (vinculo != null)
            {
                _context.SeguidoresTorneio.Remove(vinculo);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Você não recebe mais aviso de inscrição neste torneio.";
            }

            // ⚠️ Deixar de seguir NÃO exige estar inscrito, de propósito: quem saiu do torneio
            // continua seguindo (o vínculo não cai junto) e precisa conseguir desligar. Exigir
            // inscrição aqui prenderia a pessoa no aviso sem porta de saída.
            return RedirectToAction(nameof(Details), new { id });
        }

        private Task<bool> TorneioExisteAsync(int torneioId) =>
            _context.Torneios.AnyAsync(t => t.Id == torneioId);

        // As DUAS portas de inscrição. Olhar só uma deixaria metade das pessoas de fora — e
        // sem erro nenhum, o que é a assinatura dos defeitos mais caros deste projeto.
        private async Task<bool> EstouInscritoAsync(int torneioId, int jogadorId) =>
            await _context.Duplas.AnyAsync(d => d.Categoria!.TorneioId == torneioId
                                             && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
            || await _context.InscricoesAmericanas.AnyAsync(i => i.Categoria!.TorneioId == torneioId
                                                              && i.JogadorId == jogadorId);
    }
}
