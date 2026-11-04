using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // CAMISETA DE BRINDE (04/11/2026) — só o HTTP; as regras moram em Services/CamisetaDeBrinde.
    //
    // ⚠️ BOTÃO PRÓPRIO, e não mais campos no formulão do Editar, pelo mesmo motivo do
    // AlternarVisibilidade: aquele salva 40 campos de uma vez, e ligar o brinde AVISA todos os
    // inscritos — esse efeito não pode sair de um "Salvar Alterações" de quem só mexeu no preço.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ConfigurarCamisetas(int id, bool ofereceCamiseta, string[]? tamanhos,
            DateTime? escolhaAte)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (!ofereceCamiseta)
            {
                // Desligar NÃO apaga as escolhas: desligou por engano e religou, ninguém precisa
                // escolher de novo. Elas só somem da tela enquanto o brinde estiver desligado.
                torneio.OfereceCamiseta = false;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Camiseta de brinde desligada.";
                return RedirectToAction("Details", new { id });
            }

            var codigos = CamisetaDeBrinde.Normalizar(tamanhos);
            if (codigos == null)
            {
                TempData["Erro"] = "Marque pelo menos um tamanho de camiseta.";
                return RedirectToAction("Details", new { id });
            }

            var acabouDeLigar = !torneio.OfereceCamiseta;
            torneio.OfereceCamiseta = true;
            torneio.TamanhosDeCamiseta = codigos;
            torneio.CamisetaEscolhaAte = escolhaAte?.Date;
            await _context.SaveChangesAsync();

            var avisados = 0;
            if (acabouDeLigar)
            {
                // 🗣️ Felipe: quem já se inscreveu é avisado pra escolher. Só ao LIGAR — trocar um
                // tamanho ou a data não é notícia nova pra todo mundo.
                //
                // ⚠️ AppSemEmail (push + caixa de entrada), como o "Novo torneio aberto": é aviso
                // de massa, e um e-mail por inscrito estouraria a cota do Gmail (EMAIL.md).
                var url = Url.Action("Details", "Torneios", new { id });
                var prazo = torneio.CamisetaEscolhaAte is DateTime ate ? $" até {ate:dd/MM}" : "";
                foreach (var jogadorId in await CamisetaDeBrinde.InscritosAsync(_context, id))
                {
                    try
                    {
                        await _pushService.EnviarParaJogadorAsync(jogadorId,
                            "Escolha o tamanho da sua camiseta",
                            $"\"{torneio.Nome}\" vai ter camiseta de brinde. Escolha seu tamanho{prazo} na página do torneio.",
                            url, AlcanceDoAviso.AppSemEmail);
                        avisados++;
                    }
                    catch (Exception ex)
                    {
                        // Acessório: falhar um aviso não desfaz a configuração nem cala os outros.
                        _logger.LogWarning(ex, "Falha ao avisar o jogador {JogadorId} da camiseta do torneio {TorneioId}.",
                            jogadorId, id);
                    }
                }
            }

            TempData["Sucesso"] = "Camiseta de brinde salva."
                + (avisados > 0 ? $" {avisados} inscrito(s) foram avisados pra escolher o tamanho." : "");
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EscolherCamiseta(int id, string? tamanho)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (ObterJogadorIdLogado() is not int jogadorId
                || !await CamisetaDeBrinde.EstaInscritoAsync(_context, id, jogadorId))
                return Forbid();

            if (!CamisetaDeBrinde.AceitaEscolha(torneio, DateTime.Now))
            {
                TempData["Erro"] = torneio.OfereceCamiseta
                    ? "O prazo pra escolher a camiseta já acabou. Fale com o organizador."
                    : "Este torneio não tem camiseta de brinde.";
                return RedirectToAction("Details", new { id });
            }

            var atual = await _context.CamisetasDosAtletas.FindAsync(id, jogadorId);

            // Opcional: "nenhuma" tira a escolha.
            if (string.IsNullOrWhiteSpace(tamanho))
            {
                if (atual != null)
                {
                    _context.CamisetasDosAtletas.Remove(atual);
                    await _context.SaveChangesAsync();
                }
                TempData["Sucesso"] = "Pronto: você ficou sem camiseta neste torneio.";
                return RedirectToAction("Details", new { id });
            }

            if (!CamisetaDeBrinde.Oferecidos(torneio).Contains(tamanho))
            {
                TempData["Erro"] = "Esse tamanho não está disponível neste torneio.";
                return RedirectToAction("Details", new { id });
            }

            if (atual == null)
                _context.CamisetasDosAtletas.Add(new CamisetaDoAtleta { TorneioId = id, JogadorId = jogadorId, Tamanho = tamanho });
            else
            {
                atual.Tamanho = tamanho;
                atual.EscolhidaEm = DateTime.Now;
            }
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Camiseta {CamisetaDeBrinde.NomeDo(tamanho)} anotada.";
            return RedirectToAction("Details", new { id });
        }
    }
}
