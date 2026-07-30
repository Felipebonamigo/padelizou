using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // Dia de jogo, bastidores: financeiro, relatÃ³rio, check-in e comunicado em massa.
    public partial class TorneiosController
    {
        // Arrecadado, pendente e estornado por categoria, numa tela só. Antes o
        // organizador tinha que cruzar Pagamentos/Meus com a lista de inscritos.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Financeiro(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var pagamentos = await _context.Pagamentos
                .Include(p => p.Jogador)
                .Where(p => p.TorneioId == id)
                .ToListAsync();

            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == id)
                .Select(d => new { d.CategoriaId, d.EmListaDeEspera })
                .ToListAsync();

            var americanos = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == id)
                .Select(i => new { i.CategoriaId, i.EmListaDeEspera })
                .ToListAsync();

            var confirmados = pagamentos.Where(p => p.Status == "Confirmado").ToList();
            var pendentes = pagamentos.Where(p => p.Status == "Pendente").ToList();
            var estornados = pagamentos.Where(p => p.Status == "Estornado").ToList();

            var vm = new FinanceiroTorneioVM
            {
                Torneio = torneio,
                Arrecadado = confirmados.Sum(p => p.Valor),
                Pendente = pendentes.Sum(p => p.Valor),
                Estornado = estornados.Sum(p => p.Valor),
                TaxaPlataforma = confirmados.Sum(p => p.Comissao),
                Inscritos = duplas.Count + americanos.Count,
                Pagantes = confirmados.Select(p => p.JogadorId).Distinct().Count(),
            };

            // Quebra por categoria. O pagamento guarda a categoria dentro do JSON de
            // DadosInscricao, então o vínculo confiável é pela inscrição já criada
            // (ReferenciaId) — pagamento pendente ainda não tem inscrição.
            var categoriaPorDupla = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == id)
                .ToDictionaryAsync(d => d.Id, d => d.CategoriaId);

            var categoriaPorAmericano = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == id)
                .ToDictionaryAsync(i => i.Id, i => i.CategoriaId);

            int? CategoriaDo(Pagamento p)
            {
                if (p.ReferenciaId == null) return null;
                if (p.Tipo == "TorneioDupla" && categoriaPorDupla.TryGetValue(p.ReferenciaId.Value, out var c1)) return c1;
                if (p.Tipo == "TorneioAmericano" && categoriaPorAmericano.TryGetValue(p.ReferenciaId.Value, out var c2)) return c2;
                return null;
            }

            vm.PorCategoria = torneio.Categorias.Select(c => new FinanceiroCategoriaVM
            {
                Categoria = c.Nome,
                Inscritos = duplas.Count(d => d.CategoriaId == c.Id && !d.EmListaDeEspera)
                          + americanos.Count(a => a.CategoriaId == c.Id && !a.EmListaDeEspera),
                ListaDeEspera = duplas.Count(d => d.CategoriaId == c.Id && d.EmListaDeEspera)
                              + americanos.Count(a => a.CategoriaId == c.Id && a.EmListaDeEspera),
                Arrecadado = confirmados.Where(p => CategoriaDo(p) == c.Id).Sum(p => p.Valor),
                Estornado = estornados.Where(p => CategoriaDo(p) == c.Id).Sum(p => p.Valor),
                // Pendente não tem inscrição ainda, então não dá pra atribuir categoria —
                // aparece só no total e na lista de "aguardando pagamento" abaixo.
                Pendente = 0,
            })
            .OrderBy(c => c.Categoria)
            .ToList();

            vm.Pendentes = pendentes
                .OrderBy(p => p.ExpiraEm ?? p.CriadoEm)
                .Select(p => new PagamentoPendenteVM
                {
                    Jogador = p.Jogador.Nome,
                    Celular = p.Jogador.Celular,
                    Categoria = "—",
                    Valor = p.Valor,
                    CriadoEm = p.CriadoEm,
                    ExpiraEm = p.ExpiraEm,
                    LinkCobranca = p.InvoiceUrl,
                })
                .ToList();

            return View(vm);
        }

        // ===================== RELATÓRIO PÓS-TORNEIO =====================

        // Fechamento pra prestar contas ao patrocinador: pódio por categoria, público e
        // financeiro. A tela é feita pra imprimir/salvar em PDF pelo próprio navegador
        // (Ctrl+P), sem depender de biblioteca de PDF no servidor.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Relatorio(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var duplas = await _context.Duplas
                .Include(d => d.Jogador1).Include(d => d.Jogador2).Include(d => d.Categoria)
                .Where(d => d.Categoria.TorneioId == id)
                .ToListAsync();

            var partidas = await _context.Partidas
                .Where(p => p.TorneioId == id)
                .Select(p => new { p.Status })
                .ToListAsync();

            var pagamentos = await _context.Pagamentos
                .Where(p => p.TorneioId == id && p.Status == "Confirmado")
                .ToListAsync();

            string Nomes(Dupla d) => d.Jogador2 != null
                ? $"{d.Jogador1.Nome} / {d.Jogador2.Nome}"
                : d.Jogador1.Nome;

            var jogadores = new HashSet<int>();
            foreach (var d in duplas)
            {
                jogadores.Add(d.Jogador1Id);
                if (d.Jogador2Id != null) jogadores.Add(d.Jogador2Id.Value);
            }

            var vm = new RelatorioTorneioVM
            {
                Torneio = torneio,
                TotalDuplas = duplas.Count,
                TotalJogadores = jogadores.Count,
                TotalCategorias = torneio.Categorias.Count,
                TotalPartidas = partidas.Count,
                PartidasFinalizadas = partidas.Count(p => p.Status == "Finalizada"),
                Arrecadado = pagamentos.Sum(p => p.Valor),
                TaxaPlataforma = pagamentos.Sum(p => p.Comissao),
                JogadoresAlcancados = jogadores.Count,
                Podios = torneio.Categorias.Select(c =>
                {
                    var daCategoria = duplas.Where(d => d.CategoriaId == c.Id).ToList();
                    return new PodioCategoriaVM
                    {
                        Categoria = c.Nome,
                        Duplas = daCategoria.Count,
                        Campea = daCategoria.FirstOrDefault(d => d.UltimaFase == "Campeao") is { } camp ? Nomes(camp) : null,
                        Vice = daCategoria.FirstOrDefault(d => d.UltimaFase == "Final") is { } vice ? Nomes(vice) : null,
                        Semifinalistas = daCategoria.Where(d => d.UltimaFase == "Semifinal").Select(Nomes).ToList(),
                    };
                })
                .OrderBy(p => p.Categoria)
                .ToList(),
            };

            return View(vm);
        }

        // ===================== CHECK-IN NO DIA =====================

        // Lista de presença do torneio: quem já chegou, quem falta. Evita descobrir o
        // W.O. só na hora de chamar o jogo.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckIn(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null) return NotFound();
            return View(torneio);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarcarCheckIn(int duplaId, bool presente)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);

            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            dupla.CheckInEm = presente ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            return RedirectToAction("CheckIn", new { id = torneioId });
        }

        // ===================== COMUNICADO EM MASSA =====================

        // Um clique avisa todo mundo do torneio. É o que hoje o organizador faz na mão,
        // em cinco grupos de WhatsApp diferentes.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Comunicar(int id, string mensagem, int? categoriaId)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (string.IsNullOrWhiteSpace(mensagem))
            {
                TempData["Erro"] = "Escreva a mensagem antes de enviar.";
                return RedirectToAction("Details", new { id });
            }

            // Todo mundo inscrito: duplas (os dois nomes) + americano.
            var duplas = _context.Duplas.Where(d => d.Categoria.TorneioId == id);
            var americanos = _context.InscricoesAmericanas.Where(i => i.Categoria.TorneioId == id);

            if (categoriaId != null)
            {
                duplas = duplas.Where(d => d.CategoriaId == categoriaId);
                americanos = americanos.Where(i => i.CategoriaId == categoriaId);
            }

            var ids = new HashSet<int>();
            foreach (var d in await duplas.Select(d => new { d.Jogador1Id, d.Jogador2Id }).ToListAsync())
            {
                ids.Add(d.Jogador1Id);
                if (d.Jogador2Id != null) ids.Add(d.Jogador2Id.Value);
            }
            foreach (var jid in await americanos.Select(i => i.JogadorId).ToListAsync()) ids.Add(jid);

            var url = Url.Action("Details", "Torneios", new { id });
            int enviados = 0;

            foreach (var jogadorId in ids)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId, torneio.Nome, mensagem.Trim(), url);
                    enviados++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha no comunicado do torneio {TorneioId} pro jogador {JogadorId}", id, jogadorId);
                }
            }

            TempData["Sucesso"] = $"Comunicado enviado para {enviados} de {ids.Count} inscrito(s). " +
                                  "Quem não tem o app instalado não recebe push.";
            return RedirectToAction("Details", new { id });
        }

    }
}
