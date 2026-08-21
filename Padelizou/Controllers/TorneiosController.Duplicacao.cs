using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // DUPLICAR TORNEIO: o botão "criar a próxima edição" de quem faz o mesmo torneio todo mês.
    //
    // O que copia e o que não copia mora em Services/DuplicacaoDeTorneio, com um teste de
    // reflexão prendendo a decisão. Aqui entra só o que é HTTP e o que precisa do banco:
    // quem pode, o nome livre, as filhas (categorias, quadras, preferências, organizadores)
    // e o arquivo da capa.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Duplicar(int id)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var original = await _context.Torneios.FindAsync(id);
            if (original == null) return NotFound();

            // Só organizador DESTE torneio duplica (pedido do Felipe, 12/08/2026) — de
            // propósito SEM a chave-mestra do admin: duplicar cria um torneio em nome de
            // quem clicou, e admin socorrendo organizador não deve virar dono de edição.
            var souDesteTorneio = await _context.TorneioOrganizadores
                .AnyAsync(o => o.TorneioId == id && o.JogadorId == jogadorId);
            if (!souDesteTorneio)
            {
                TempData["Erro"] = "Só quem organiza este torneio pode criar a próxima edição.";
                return RedirectToAction("Details", new { id });
            }

            // A mesma porta do Create: o perfil de organizador continua valendo, e a pergunta
            // leva o formato junto (o Americano é livre, o Oficial não).
            var duplicador = await _context.Jogadores.FindAsync(jogadorId);
            if (!PermissaoDeOrganizador.PodeCriarTorneio(duplicador, original.Formato))
            {
                TempData["Erro"] = PermissaoDeOrganizador.ComoPedirOPerfil;
                return RedirectToAction("Details", new { id });
            }

            var novo = DuplicacaoDeTorneio.CopiarConfiguracao(original);
            novo.Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            // Torneio restrito ganha chave NOVA: a da edição passada já circulou por grupo
            // de WhatsApp — copiá-la deixaria a edição nova aberta pra quem guardou a antiga.
            novo.ChaveAcesso = novo.Restrito ? ChaveDeAcessoDoTorneio.Sortear() : null;

            // O nome do original quando está livre (edição anterior finalizada libera); um
            // número quando ainda não está — a mesma régua do botão apertado duas vezes.
            var meusTorneioIds = await _context.TorneioOrganizadores
                .Where(o => o.JogadorId == jogadorId)
                .Select(o => o.TorneioId)
                .ToListAsync();
            var meusTorneios = await _context.Torneios
                .Where(t => meusTorneioIds.Contains(t.Id))
                .Select(t => new { t.Nome, t.Status })
                .ToListAsync();
            novo.Nome = DuplicacaoDeTorneio.NomeParaEdicaoNova(
                original.Nome, meusTorneios.Select(t => (t.Nome, (string?)t.Status)));

            // "Pelo site" exige a conta de quem RECEBE — e quem recebe agora é quem duplicou.
            // Sem isso, um co-organizador sem conta duplicaria um torneio pago e as inscrições
            // entrariam sem nenhuma cobrança nascer, que é a armadilha mais cara que o sistema
            // já teve. Cai pra "por fora" avisando, em vez de recusar: o resto da cópia vale.
            string avisoPagamento = "";
            if (novo.CobraPeloSite && novo.PrecoInscricao > 0
                && _pagamentos.OQueFaltaParaReceber(duplicador) != null)
            {
                novo.FormaPagamento = FormaDePagamentoDoTorneio.Externo;
                avisoPagamento = " As inscrições vieram como \"recebo por fora\": pra cobrar "
                    + "pelo site é preciso conectar sua conta de recebimento.";
            }

            // A capa: copia o ARQUIVO, não o caminho. Duas edições apontando pro mesmo arquivo
            // é um vínculo invisível — a primeira que trocasse de capa mudaria a da outra.
            novo.ImagemCapa = CopiarArquivoDeCapa(original.ImagemCapa);

            _context.Torneios.Add(novo);
            await _context.SaveChangesAsync();

            // ── As filhas ────────────────────────────────────────────────────────────────

            // Quadras, na ordem em que nasceram: é a posição que a preferência por categoria
            // usa pra apontar.
            var quadrasOriginais = await _context.Quadras
                .Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            var quadraNovaDaAntiga = new Dictionary<int, Quadra>();
            foreach (var q in quadrasOriginais)
            {
                // O CLUBE DA QUADRA viaja junto: a 2ª edição do torneio acontece nos mesmos
                // lugares. Sem isto, duplicar um torneio de duas sedes devolveria um torneio de
                // uma sede só — com os NOMES de quadra ainda dizendo "Nata" e "Batata", que é a
                // pior forma de errar: parece certo na tela e a grade mistura tudo.
                var quadraNova = new Quadra { TorneioId = novo.Id, Nome = q.Nome, ClubeId = q.ClubeId };
                _context.Quadras.Add(quadraNova);
                quadraNovaDaAntiga[q.Id] = quadraNova;
            }

            // Categorias com a configuração que é estrutura. Ficam de fora GruposAmericano e
            // PassamPorGrupo: eles são escolhidos NA HORA do sorteio, quando o organizador já
            // sabe quantos se inscreveram — copiá-los travaria a divisão da edição nova no
            // tamanho da antiga.
            var categoriasOriginais = await _context.Categorias
                .Where(c => c.TorneioId == id).OrderBy(c => c.Id).ToListAsync();
            var categoriaNovaDaAntiga = new Dictionary<int, Categoria>();
            foreach (var c in categoriasOriginais)
            {
                var categoriaNova = new Categoria
                {
                    TorneioId = novo.Id,
                    Nome = c.Nome,
                    // Categoria de catálogo compartilha o código entre torneios; a de times
                    // ganhou um código próprio no Create, então ganha outro aqui também.
                    Codigo = c.DeTimes ? Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper() : c.Codigo,
                    LimiteDuplas = c.LimiteDuplas,
                    RankingRsCategoriaId = c.RankingRsCategoriaId,
                    DeTimes = c.DeTimes,
                    QuantidadeTimes = c.QuantidadeTimes,
                    QuantidadeGrupos = c.QuantidadeGrupos,
                    ClassificadosPorGrupo = c.ClassificadosPorGrupo,
                    // Em que clube a categoria joga — o par do `ClubeId` da quadra logo acima.
                    // Os dois viajam ou nenhum viaja: metade da divisão é pior que nenhuma.
                    ClubeId = c.ClubeId,
                };
                _context.Categorias.Add(categoriaNova);
                categoriaNovaDaAntiga[c.Id] = categoriaNova;
            }
            await _context.SaveChangesAsync();

            // A quadra preferida de cada categoria, remapeada pros ids novos.
            var preferencias = await _context.QuadrasDaCategoria
                .Where(p => categoriasOriginais.Select(c => c.Id).Contains(p.CategoriaId))
                .ToListAsync();
            foreach (var p in preferencias)
            {
                if (!categoriaNovaDaAntiga.TryGetValue(p.CategoriaId, out var categoriaNova)) continue;
                if (!quadraNovaDaAntiga.TryGetValue(p.QuadraId, out var quadraNova)) continue;
                _context.QuadrasDaCategoria.Add(new QuadraDaCategoria
                {
                    CategoriaId = categoriaNova.Id,
                    QuadraId = quadraNova.Id,
                });
            }

            // A equipe viaja junto — mas quem duplicou vira o CRIADOR da edição nova (é quem
            // recebe, quando se cobra pelo site), e os demais entram como organizadores.
            var equipeOriginal = await _context.TorneioOrganizadores
                .Where(o => o.TorneioId == id).ToListAsync();
            _context.TorneioOrganizadores.Add(new TorneioOrganizador
            {
                TorneioId = novo.Id,
                JogadorId = jogadorId,
                NivelAcesso = "Criador",
            });
            foreach (var o in equipeOriginal.Where(o => o.JogadorId != jogadorId))
            {
                _context.TorneioOrganizadores.Add(new TorneioOrganizador
                {
                    TorneioId = novo.Id,
                    JogadorId = o.JogadorId,
                    NivelAcesso = "Organizador",
                });
            }
            await _context.SaveChangesAsync();

            // A edição nova volta pra fila de aprovação, como qualquer torneio novo.
            await AvisarAdminsDeTorneioEsperandoAsync(novo);

            TempData["Sucesso"] = $"Edição nova criada a partir de \"{original.Nome}\"! "
                + "As datas ficaram em branco e as inscrições vieram FECHADAS — revise em "
                + "\"Editar torneio\", marque as datas e abra as inscrições quando quiser."
                + avisoPagamento;

            return RedirectToAction("Details", new { id = novo.Id });
        }

        // Avisa quem aprova que entrou torneio na fila. Push é acessório: falhar não pode
        // desfazer o torneio que acabou de nascer, por isso o try/catch engole e loga.
        private async Task AvisarAdminsDeTorneioEsperandoAsync(Torneio torneio)
        {
            try
            {
                var admins = await _context.Jogadores
                    .Where(j => (j.IsAdminGeral || j.IsAdminRaiz) && j.ExcluidoEm == null)
                    .Select(j => j.Id)
                    .ToListAsync();

                var urlFila = Url.Action("TorneiosParaAprovar", "Admin");
                foreach (var adminId in admins)
                {
                    await _pushService.EnviarParaJogadorAsync(adminId,
                        "Torneio esperando aprovação", torneio.Nome, urlFila);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar admins do torneio {TorneioId} esperando aprovação.", torneio.Id);
            }
        }

        // Copia o arquivo físico da capa pra um nome novo e devolve o caminho relativo — ou
        // null quando não há capa (ou o arquivo sumiu do disco, que não pode travar a cópia).
        private string? CopiarArquivoDeCapa(string? caminhoRelativo)
        {
            if (string.IsNullOrWhiteSpace(caminhoRelativo)) return null;

            try
            {
                // A mesma régua de caminho das imagens enviadas: só /uploads, sem escapar dela.
                var origem = ImagemEnviada.CaminhoFisico(_env.WebRootPath, caminhoRelativo);
                if (origem == null || !System.IO.File.Exists(origem)) return null;

                var nomeNovo = ImagemEnviada.NomeDeArquivo();
                var destino = Path.Combine(Path.GetDirectoryName(origem)!, nomeNovo);
                System.IO.File.Copy(origem, destino);

                var pasta = Path.GetDirectoryName(caminhoRelativo.Replace('\\', '/'))!
                    .Replace('\\', '/');
                return $"{pasta}/{nomeNovo}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao copiar a capa {Caminho} na duplicação.", caminhoRelativo);
                return null;
            }
        }
    }
}
