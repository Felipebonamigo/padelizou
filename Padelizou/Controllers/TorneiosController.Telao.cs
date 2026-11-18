using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    public partial class TorneiosController
    {
        // O TELÃO — a tela pra TV do clube (18/11/2026). Ver Services/TelaoDoTorneio.
        //
        // ⚠️ PÚBLICA E SÓ DE LEITURA, com as MESMAS portas da página Jogos: torneio oculto e chave
        // esperando aprovação não vão pra TV. Aqui a chave pendente é NotFound, e não o redirect da
        // Jogos: na TV ninguém lê o aviso, e o "Details" ficaria preso na tela como se fosse o telão.
        //
        // `parcial` devolve só o miolo: a página se atualiza sozinha buscando ele a cada 20 s
        // (wwwroot/js/telao.js), sem recarregar — navegador de TV piscando a tela branca a cada
        // atualização é o que faz o dono do clube desligar a TV.
        [HttpGet]
        public async Task<IActionResult> Telao(int id, int? clube, bool parcial = false)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();
            if (torneio.Status == AprovacaoDeChaves.Pendente) return NotFound();

            var partidas = await _context.Partidas
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == id)
                .ToListAsync();

            var sedes = await SedesAsync(id);
            var quadro = TelaoDoTorneio.Montar(partidas, sedes, clube);

            ViewBag.Torneio = torneio;
            ViewBag.Sedes = sedes;
            ViewBag.Clube = clube;
            ViewBag.NomeDoClube = clube is int c ? sedes.NomeDoClube(c) : null;
            ViewBag.NumeroNaFase = ReservasDeHorario.NumeroNaFase(partidas);
            ViewBag.Palpites = quadro.AoVivo.Count == 0
                ? new Dictionary<int, Padelizou.ViewModels.PalpiteResumoVM>()
                : await _palpites.ObterResumosAsync(quadro.AoVivo.Select(p => p.Id), null);

            if (parcial) return PartialView("_TelaoConteudo", quadro);

            // O QR leva pra página do torneio — quem está no clube abre no celular e acompanha.
            var endereco = Url.Action("Details", "Torneios", new { id }, Request?.Scheme ?? "https")
                           ?? $"https://padelizou.com.br/Torneios/Details/{id}";
            ViewBag.QrDoTorneio = "data:image/png;base64," + Convert.ToBase64String(QrDoTelao(endereco));

            return View(quadro);
        }

        private static byte[] QrDoTelao(string conteudo)
        {
            using var gerador = new QRCoder.QRCodeGenerator();
            using var codigo = gerador.CreateQrCode(conteudo, QRCoder.QRCodeGenerator.ECCLevel.M);
            return new QRCoder.PngByteQRCode(codigo).GetGraphic(pixelsPerModule: 8);
        }
    }
}
