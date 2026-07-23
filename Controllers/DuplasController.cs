using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    public class DuplasController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEstatisticasService _estatisticas;
        private readonly IEmailService _emailService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<DuplasController> _logger;

        public DuplasController(DbPadelContext context, IEstatisticasService estatisticas,
            IEmailService emailService, IPushNotificationService pushService, ILogger<DuplasController> logger)
        {
            _context = context;
            _estatisticas = estatisticas;
            _emailService = emailService;
            _pushService = pushService;
            _logger = logger;
        }

        // Notifica quem segue algum dos dois jogadores recém-inscritos e tem
        // NotificarSeguidosTorneio marcado — mesma lógica do gancho equivalente em
        // TorneiosController.InscreverIndividual, duplicada aqui de propósito (mesmo padrão
        // de helper pequeno duplicado por controller já usado no resto do app).
        private async Task NotificarSeguidoresDeInscricaoAsync(int torneioId, IEnumerable<int> jogadoresInscritos)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return;

            var jogadores = await _context.Jogadores
                .Where(j => jogadoresInscritos.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            var seguidores = await _context.SeguidoresJogador
                .Include(s => s.Seguidor)
                .Where(s => jogadoresInscritos.Contains(s.SeguidoId) && s.Seguidor.NotificarSeguidosTorneio)
                .ToListAsync();

            var url = Url.Action("Details", "Torneios", new { id = torneioId });

            foreach (var grupo in seguidores.GroupBy(s => s.SeguidorId))
            {
                var seguidor = grupo.First().Seguidor;
                var nomesQueSigo = grupo.Select(s => jogadores.TryGetValue(s.SeguidoId, out var nome) ? nome : "").Where(n => n != "");
                var titulo = "Alguém que você segue se inscreveu num torneio";
                var corpo = $"{string.Join(" e ", nomesQueSigo)} se inscreveu em {torneio.Nome}.";

                if (seguidor.NotificarEmail && !string.IsNullOrWhiteSpace(seguidor.Email))
                {
                    try
                    {
                        await _emailService.EnviarAsync(seguidor.Email!, seguidor.Nome, titulo, $"<p>{corpo}</p>");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao enviar e-mail de seguidor pro torneio {TorneioId}, jogador {JogadorId}", torneioId, seguidor.Id);
                    }
                }

                try
                {
                    await _pushService.EnviarParaJogadorAsync(seguidor.Id, titulo, corpo, url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de seguidor pro torneio {TorneioId}, jogador {JogadorId}", torneioId, seguidor.Id);
                }
            }
        }

        // Abre a tela de Inscrição
        public IActionResult Create()
        {
            return View();
        }

        // Recebe os dados do formulário
        [HttpPost]
        public async Task<IActionResult> Create(
            int torneioId, int categoriaId,
            string nome1, string cpf1, string? celular1, string? cidade1, string? estado1,
            string nome2, string cpf2, string? celular2, string? cidade2, string? estado2,
            bool impSextaNoite, bool impSabadoManha, bool impSabadoTarde,
            bool ignorarBloqueio = false, string? chaveAcesso = null)
        {
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            if (torneio.Restrito && !string.Equals(chaveAcesso?.Trim(), torneio.ChaveAcesso, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Chave de acesso inválida. Confira com o organizador do torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // 1. Verifica se os JOGADORES já existem (por CPF) — ainda NÃO cria ninguém,
            //    porque a regra anti-sandbagging precisa checar o histórico antes.
            var jogador1 = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf1);
            var jogador2 = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf2);

            // 2. REGRA ANTI-SANDBAGGING: quem comprovou nível numa categoria mais forte
            //    não pode se inscrever numa mais fraca. O organizador logado pode liberar.
            if (!string.IsNullOrEmpty(torneio.RestricaoCategoria) && torneio.RestricaoCategoria != "Livre")
            {
                bool liberado = ignorarBloqueio && await UsuarioEhOrganizadorAsync(torneioId);
                if (!liberado)
                {
                    var erro = await MotivoBloqueioCategoriaAsync(categoria.Nome, jogador1, jogador2, torneio.RestricaoCategoria);
                    if (erro != null)
                    {
                        TempData["Erro"] = erro;
                        return RedirectToAction("Details", "Torneios", new { id = torneioId });
                    }
                }
            }

            // 3. Agora sim, cria os jogadores que não existiam e completa o cadastro.
            if (jogador1 == null)
            {
                jogador1 = new Jogador { Nome = nome1, Cpf = cpf1 };
                _context.Jogadores.Add(jogador1);
            }
            jogador1.Celular = string.IsNullOrWhiteSpace(jogador1.Celular) ? celular1?.Trim() : jogador1.Celular;
            jogador1.Cidade = string.IsNullOrWhiteSpace(jogador1.Cidade) ? cidade1?.Trim() : jogador1.Cidade;
            jogador1.Estado = string.IsNullOrWhiteSpace(jogador1.Estado) ? estado1?.Trim() : jogador1.Estado;

            if (jogador2 == null)
            {
                jogador2 = new Jogador { Nome = nome2, Cpf = cpf2 };
                _context.Jogadores.Add(jogador2);
            }
            jogador2.Celular = string.IsNullOrWhiteSpace(jogador2.Celular) ? celular2?.Trim() : jogador2.Celular;
            jogador2.Cidade = string.IsNullOrWhiteSpace(jogador2.Cidade) ? cidade2?.Trim() : jogador2.Cidade;
            jogador2.Estado = string.IsNullOrWhiteSpace(jogador2.Estado) ? estado2?.Trim() : jogador2.Estado;

            // Salva os jogadores (se forem novos) para gerar os IDs que usaremos na dupla
            await _context.SaveChangesAsync();

            // 4. Vagas: se a categoria ou o torneio já bateram no limite, a dupla entra
            //    na lista de espera em vez de ser bloqueada — pode ser promovida depois
            //    se alguém desistir (ver TorneiosController.RemoverDupla).
            bool emListaDeEspera = await CategoriaOuTorneioEstaCheioAsync(categoria, torneio);

            // 5. Monta a DUPLA e vincula à Categoria
            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = jogador1.Id,
                Jogador2Id = jogador2.Id,
                ImpedimentoSextaNoite = impSextaNoite,
                ImpedimentoSabadoManha = impSabadoManha,
                ImpedimentoSabadoTarde = impSabadoTarde,
                EmListaDeEspera = emListaDeEspera
            };

            _context.Duplas.Add(dupla);
            await _context.SaveChangesAsync(); // Inscrição finalizada!

            await NotificarSeguidoresDeInscricaoAsync(torneioId, new[] { jogador1.Id, jogador2.Id });

            TempData["Sucesso"] = emListaDeEspera
                ? "Vagas esgotadas — sua dupla entrou na lista de espera. Se alguém desistir, vocês são chamados na ordem de inscrição."
                : "Inscrição confirmada com sucesso!";
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }

        // Monta a mensagem de bloqueio se algum dos jogadores já comprovou nível (conforme o
        // gatilho do torneio) numa categoria mais forte que a escolhida. null = ninguém impedido.
        private async Task<string?> MotivoBloqueioCategoriaAsync(string categoriaAlvo, Jogador? j1, Jogador? j2, string modo)
        {
            int ordemAlvo = EstatisticasService.OrdemCategoria(categoriaAlvo);
            if (ordemAlvo == 0) return null; // categoria sem tier reconhecido não trava

            var niveis = await _estatisticas.ObterNiveisComprovadosAsync(modo);
            var impedidos = new List<string>();

            foreach (var j in new[] { j1, j2 })
            {
                if (j == null) continue;
                if (niveis.TryGetValue(j.Id, out var nivel) && nivel.Ordem > ordemAlvo)
                {
                    string comoComprovou = EstatisticasService.RotuloComprovacao(nivel.MelhorFase);
                    impedidos.Add($"{j.Nome} ({comoComprovou} na {nivel.Categoria})");
                }
            }

            if (impedidos.Count == 0) return null;

            return $"Não é possível inscrever nesta categoria: {string.Join(" e ", impedidos)}. "
                 + $"Esse nível já comprovado impede jogar uma categoria mais fraca. "
                 + $"Peça ao organizador para liberar a inscrição, se for o caso.";
        }

        // Checa se a categoria ou o torneio (somando todas as categorias) já bateram no
        // limite de duplas confirmadas (fora da lista de espera). Null = sem limite configurado.
        private async Task<bool> CategoriaOuTorneioEstaCheioAsync(Categoria categoria, Torneio torneio)
        {
            if (categoria.LimiteDuplas.HasValue)
            {
                int naCategoria = await _context.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && !d.EmListaDeEspera);
                if (naCategoria >= categoria.LimiteDuplas.Value) return true;
            }

            if (torneio.LimiteDuplasTotal.HasValue)
            {
                int noTorneio = await _context.Duplas.CountAsync(d => d.Categoria.TorneioId == torneio.Id && !d.EmListaDeEspera);
                if (noTorneio >= torneio.LimiteDuplasTotal.Value) return true;
            }

            return false;
        }

        // O usuário logado é organizador deste torneio? (usado para liberar o bloqueio)
        private async Task<bool> UsuarioEhOrganizadorAsync(int torneioId)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var jogadorId)) return false;
            return await _context.TorneioOrganizadores
                .AnyAsync(o => o.TorneioId == torneioId && o.JogadorId == jogadorId);
        }
    }
}
