using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Controllers
{
    // "POR QUE ESSA VAGA ABRIU" — a lista de quem saiu do torneio (Felipe, 25/09/2026).
    //
    // Irmã da NaoPagos: mesma régua de acesso, mesma página de gestão. A diferença é que aquela
    // pergunta "quem ainda não pagou?" (e deixa agir), e esta só conta o que já aconteceu.
    public partial class TorneiosController
    {
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Desistencias(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // ⚠️ REGRA 0: a lista diz quem desistiu e quanto tinha pago. É informação do
            // organizador sobre os inscritos DELE — a mesma régua de EhOrganizadorAsync que
            // guarda a NaoPagos e o resto da gestão.
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            var saidas = await _context.SaidasDoTorneio
                .Where(s => s.TorneioId == id)
                .OrderByDescending(s => s.SaiuEm)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Torneio = torneio;
            return View(await MontarAsync(saidas));
        }

        // ⚠️ OS NOMES SAEM DE UMA CONSULTA SÓ, e não de um Include por linha: o histórico não
        // tem FK pra Jogador de propósito (ver Models/SaidaDoTorneio), então navegação não
        // existe aqui — e uma consulta por linha seria N+1 numa tela que cresce com o torneio.
        private async Task<IReadOnlyList<SaidaNaTela>> MontarAsync(List<SaidaDoTorneio> saidas)
        {
            if (saidas.Count == 0) return Array.Empty<SaidaNaTela>();

            var ids = saidas
                .SelectMany(s => new[] { (int?)s.Jogador1Id, s.Jogador2Id, s.QuemPediuId })
                .Where(i => i != null).Select(i => i!.Value).Distinct().ToList();

            var nomes = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .Select(j => new { j.Id, j.Nome })
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            var categorias = await _context.Categorias
                .Where(c => saidas.Select(s => s.CategoriaId).Contains(c.Id))
                .Select(c => new { c.Id, c.Nome })
                .ToDictionaryAsync(c => c.Id, c => c.Nome);

            // ⚠️ Conta encerrada vira "Conta encerrada", e não linha vazia. O histórico guarda
            // id e resolve o nome agora — é o preço, decidido de propósito, de não ter uma
            // segunda cópia de dado pessoal que ninguém lembra de limpar.
            string Nome(int? id) => id is int i && nomes.TryGetValue(i, out var n) ? n : "Conta encerrada";

            return saidas.Select(s => new SaidaNaTela(
                s.SaiuEm,
                s.Jogador2Id is int segundo ? $"{Nome(s.Jogador1Id)} e {Nome(segundo)}" : Nome(s.Jogador1Id),
                categorias.GetValueOrDefault(s.CategoriaId) ?? "Categoria removida",
                RotuloDoMotivo(s.Motivo),
                s.QuemPediuId == null ? null : Nome(s.QuemPediuId),
                s.Observacao,
                s.EstavaPaga,
                s.AbriuVaga)).ToList();
        }

        // ⚠️ O `_ =>` não existe aqui de propósito: motivo novo no enum tem que quebrar o build,
        // e não aparecer na tela com o rótulo do vizinho — foi o que quase aconteceu com a
        // cortesia em AvisosDoPlanoDoProfessor.Titulo.
        private static string RotuloDoMotivo(MotivoDaSaida motivo) => motivo switch
        {
            MotivoDaSaida.Desistiu => "Desistiu",
            MotivoDaSaida.RemovidoPeloOrganizador => "O organizador removeu",
            MotivoDaSaida.NaoPagou => "Não pagou no prazo",
            _ => throw new ArgumentOutOfRangeException(nameof(motivo)),
        };
    }
}
