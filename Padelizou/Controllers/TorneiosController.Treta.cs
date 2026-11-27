using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // A FINALÍSSIMA DTN E O TROFÉU (Treta, 25/11/2026) — só o HTTP; a regra mora em
    // Services/FinalissimaDtn. Ver TRETA.md.
    //
    // 🔒 Criar a Finalíssima é de quem organiza (EhOrganizadorAsync, sem régua nova). Registrar o
    // troféu é da dupla vencedora do jogo decisivo OU de quem organiza — o serviço confere quem
    // venceu; aqui só se descobre se é organizador.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarFinalissima(int id, int moduloId, int direitaDaPrimeira, int direitaDaSegunda)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var erro = await new FinalissimaDtn(_context)
                .CriarAsync(id, moduloId, direitaDaPrimeira, direitaDaSegunda, Robo);
            if (erro != null) TempData["Erro"] = erro;
            else TempData["Sucesso"] = "Finalíssima DTN criada.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarTrofeuDtn(int id, int categoriaId, int jogadorId)
        {
            var quem = ObterJogadorIdLogado() ?? 0;
            if (quem <= 0) return Forbid();
            bool ehOrganizador = await EhOrganizadorAsync(id, quem);

            var erro = await new FinalissimaDtn(_context)
                .RegistrarTrofeuAsync(id, categoriaId, jogadorId, quem, ehOrganizador);
            if (erro != null) TempData["Erro"] = erro;
            else TempData["Sucesso"] = "Troféu DTN registrado.";
            return RedirectToAction("Details", new { id });
        }
    
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarDtnDaCategoria(int categoriaId, int duplasNoDtnPorGrupo, int? parDaFinalissimaId)
        {
            var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId);
            if (categoria == null) return NotFound();
            if (!await EhOrganizadorAsync(categoria.TorneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // A DTN e a Finalíssima não têm DTN própria.
            if (categoria.PapelDaChave != null)
            {
                TempData["Erro"] = "Essa chave já é do DTN.";
                return RedirectToAction("Details", new { id = categoria.TorneioId });
            }

            // 0 desliga; mais de 2 por grupo não é um DTN, é outra chave inteira.
            categoria.DuplasNoDtnPorGrupo = Math.Clamp(duplasNoDtnPorGrupo, 0, 2);

            // O par só vale no mesmo torneio, entre módulos comuns, e nunca ela mesma.
            Categoria? par = parDaFinalissimaId is int parId && parId != categoria.Id
                ? await _context.Categorias.FirstOrDefaultAsync(c =>
                    c.Id == parId && c.TorneioId == categoria.TorneioId && c.PapelDaChave == null)
                : null;

            // Um módulo, um par: quem apontava pra esta ou pro par novo deixa de apontar — senão a
            // mesma categoria estaria em duas Finalíssimas (FinalissimaDtn lê os dois sentidos).
            var envolvidos = new List<int> { categoria.Id, par?.Id ?? 0 };
            foreach (var antiga in await _context.Categorias
                         .Where(c => c.TorneioId == categoria.TorneioId && c.ParDaFinalissimaId != null
                                  && (envolvidos.Contains(c.ParDaFinalissimaId.Value) || envolvidos.Contains(c.Id)))
                         .ToListAsync())
                antiga.ParDaFinalissimaId = null;

            categoria.ParDaFinalissimaId = par?.Id;
            if (par != null && par.DuplasNoDtnPorGrupo <= 0) par.DuplasNoDtnPorGrupo = categoria.DuplasNoDtnPorGrupo > 0 ? categoria.DuplasNoDtnPorGrupo : 1;

            await _context.SaveChangesAsync();

            // Com os grupos já sorteados, a categoria DTN nasce agora (vazia) — é o que dá à
            // prévia os botões de horário e quadra. Desligado, sai a que ainda está vazia.
            if (categoria.DuplasNoDtnPorGrupo > 0)
            {
                if (await _context.Set<GrupoTorneio>().AnyAsync(g => g.CategoriaId == categoria.Id))
                    await ChaveDtn.GarantirCategoriaAsync(_context, categoria);
            }
            else
            {
                await ChaveDtn.TirarCategoriaVaziaAsync(_context, categoria);
            }

            TempData["Sucesso"] = categoria.DuplasNoDtnPorGrupo == 0
                ? $"\"{categoria.Nome}\" ficou sem DTN."
                : $"\"{categoria.Nome}\": os {categoria.DuplasNoDtnPorGrupo} último(s) de cada grupo vão pro DTN"
                  + (par != null ? $", com Finalíssima contra \"{par.Nome}\"." : ".");
            return RedirectToAction("Details", "Torneios", new { id = categoria.TorneioId }, fragment: $"categoria-{categoria.Id}");
        }
    
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // Folga pros melhores 1ºs (Services/AberturaPelaCampanha). Diferente do desenho à mão, vale
        // DEPOIS da aprovação também: a regra só age quando os grupos fecham. Trava quando o
        // mata-mata da categoria já nasceu — aí a abertura já foi decidida.
        public async Task<IActionResult> AberturaPelaCampanhaDaCategoria(int id, int categoriaId, bool ligar)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId && c.TorneioId == id);
            if (categoria == null) return NotFound();

            bool mataMataNasceu = await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId
                && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));
            if (mataMataNasceu)
            {
                TempData["Erro"] = $"{categoria.Nome}: o mata-mata já começou — a abertura não muda mais.";
                return RedirectToAction("Details", new { id });
            }

            // Desligar devolve ao de sempre: nulo, e o robô grava o desenho por colocação.
            categoria.CruzamentoDoMataMata = ligar ? AberturaPelaCampanha.Marca : null;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = ligar
                ? $"{categoria.Nome}: as folgas vão pros melhores 1ºs, pela campanha, quando todos os grupos fecharem."
                : $"{categoria.Nome}: as folgas voltam a seguir o cruzamento por grupo.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // QUANTOS GRUPOS NO SORTEIO (Services/SorteioDosGrupos). Só antes do sorteio da categoria:
        // depois os grupos existem, e mudar o número não os refaz.
        public async Task<IActionResult> DefinirQuantidadeDeGrupos(int categoriaId, int? quantidade)
        {
            var categoria = await _context.Categorias.Include(c => c.GruposTorneio)
                .FirstOrDefaultAsync(c => c.Id == categoriaId);
            if (categoria == null) return NotFound();
            if (!await EhOrganizadorAsync(categoria.TorneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            IActionResult Voltar() => RedirectToAction("Details", "Torneios",
                new { id = categoria.TorneioId }, fragment: $"categoria-{categoria.Id}");

            if (categoria.DeTimes || categoria.PapelDaChave != null || categoria.ChaveDireta)
            {
                TempData["Erro"] = "Essa categoria não sorteia grupos por aqui.";
                return Voltar();
            }
            if (categoria.GruposTorneio.Count > 0)
            {
                TempData["Erro"] = $"{categoria.Nome}: os grupos já foram sorteados.";
                return Voltar();
            }
            if (quantidade is int q && (q < 1 || q > SorteioDosGrupos.Maximo))
            {
                TempData["Erro"] = $"Escolha de 1 a {SorteioDosGrupos.Maximo} grupos.";
                return Voltar();
            }

            categoria.QuantidadeGrupos = quantidade is > 0 ? quantidade : null;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = categoria.QuantidadeGrupos == null
                ? $"{categoria.Nome}: o sorteio volta a montar grupos de 3."
                : $"{categoria.Nome}: o sorteio vai dividir em {categoria.QuantidadeGrupos} grupo(s).";
            return Voltar();
        }
}
}
