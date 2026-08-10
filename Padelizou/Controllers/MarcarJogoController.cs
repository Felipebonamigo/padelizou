using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Tela do jogador pra marcar horário de quadra num clube que ativou "Marcar Jogo pelo
    // Padelizou" — gestão de quadras/horários pelo dono/admin é HorarioMarcacaoController.
    [Authorize]
    public class MarcarJogoController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IHorarioMarcacaoService _horarioMarcacaoService;
        private readonly IPagamentoInscricaoService _pagamentos;

        public MarcarJogoController(DbPadelContext context, IHorarioMarcacaoService horarioMarcacaoService,
            IPagamentoInscricaoService pagamentos)
        {
            _context = context;
            _horarioMarcacaoService = horarioMarcacaoService;
            _pagamentos = pagamentos;
        }

        private int ObterJogadorIdLogado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var clubes = await _context.Clubes
                .Include(c => c.Cidade)
                .Where(c => c.MarcacaoHorariosAtiva)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return View(clubes);
        }

        [HttpGet]
        public async Task<IActionResult> Clube(int clubeId, DateTime? data)
        {
            var clube = await _context.Clubes.Include(c => c.Cidade).FirstOrDefaultAsync(c => c.Id == clubeId);
            if (clube == null || !clube.MarcacaoHorariosAtiva) return NotFound();

            var diaEscolhido = (data ?? DateTime.Today).Date;
            ViewBag.Clube = clube;
            ViewBag.Data = diaEscolhido;
            ViewBag.Slots = await _horarioMarcacaoService.ObterSlotsDisponiveisAsync(clubeId, diaEscolhido);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Marcar(int clubeId, int quadraClubeId, DateTime dataHora, int duracaoMinutos)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || !clube.MarcacaoHorariosAtiva) return NotFound();

            var meuId = ObterJogadorIdLogado();

            // Reconfere que ninguém marcou esse exato horário/quadra entre o jogador ver a
            // tela e clicar em marcar (checagem simples, sem lock — mesmo rigor já usado hoje
            // pra vaga de torneio).
            var jaMarcado = await _context.MarcacoesJogo.AnyAsync(m =>
                m.ClubeId == clubeId && m.QuadraClubeId == quadraClubeId &&
                m.DataHora == dataHora && m.Status == "Confirmada");

            if (jaMarcado)
            {
                TempData["Erro"] = "Esse horário acabou de ser marcado por outra pessoa.";
            }
            else
            {
                // O preço vem sempre da regra no banco, nunca do formulário — senão daria pra
                // adulterar o valor no navegador e pagar o que quisesse pela quadra.
                var slots = await _horarioMarcacaoService.ObterSlotsDisponiveisAsync(clubeId, dataHora.Date);
                var slot = slots.FirstOrDefault(s => s.QuadraClubeId == quadraClubeId && s.Inicio == dataHora);

                var dono = await _pagamentos.ObterDonoDoClubeAsync(clubeId);
                if (slot != null && _pagamentos.PodeCobrarQuadra(slot.Preco, dono))
                {
                    var jogador = await _context.Jogadores.FindAsync(meuId);
                    if (jogador != null)
                    {
                        // Igual a torneio e aula: a marcação só nasce quando o pagamento confirma.
                        var checkout = await _pagamentos.IniciarCobrancaQuadraAsync(
                            clube, dono!, jogador, slot.Preco!.Value,
                            new DadosMarcacaoJogo(clubeId, quadraClubeId, meuId, dataHora, duracaoMinutos));

                        if (checkout != null) return Redirect(checkout);

                        TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                        return RedirectToAction("Clube", new { clubeId, data = dataHora.Date });
                    }
                }

                var novaMarcacao = new MarcacaoJogo
                {
                    ClubeId = clubeId,
                    QuadraClubeId = quadraClubeId,
                    JogadorId = meuId,
                    DataHora = dataHora,
                    DuracaoMinutos = duracaoMinutos,
                    Status = "Confirmada",
                    // Sem cobrança online, o acerto é no clube — é o que faz o selo
                    // "paga lá" aparecer na agenda do dono.
                    PagaNoLocal = true,
                };
                _context.MarcacoesJogo.Add(novaMarcacao);
                await _context.SaveChangesAsync();
                return RedirectToAction("Confirmada", new { id = novaMarcacao.Id });
            }

            return RedirectToAction("Clube", new { clubeId, data = dataHora.Date });
        }

        // Vários horários de uma vez (2h seguidas, ou duas quadras pro grupo grande).
        // Cada item de slots é "quadraId|yyyy-MM-ddTHH:mm:ss" — a duração e o PREÇO saem
        // sempre do servidor (ObterSlotsDisponiveisAsync), nunca do formulário.
        //
        // Tudo-ou-nada de propósito: se um dos horários acabou de ser tomado, criar só o
        // resto entregaria outra coisa que não a escolhida — melhor recusar e deixar o
        // jogador remontar a seleção já vendo a grade atual.
        [HttpPost]
        public async Task<IActionResult> MarcarVarios(int clubeId, string[] slots)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || !clube.MarcacaoHorariosAtiva) return NotFound();

            var meuId = ObterJogadorIdLogado();

            // Teto de sanidade: ninguém reserva 9+ horários de uma vez — isso é robô ou erro.
            var pedidos = (slots ?? Array.Empty<string>()).Distinct().Take(8)
                .Select(s => s.Split('|'))
                .Where(p => p.Length == 2 && int.TryParse(p[0], out _) && DateTime.TryParse(p[1], out _))
                .Select(p => (QuadraId: int.Parse(p[0]), DataHora: DateTime.Parse(p[1])))
                .ToList();

            if (pedidos.Count == 0)
            {
                TempData["Erro"] = "Escolha pelo menos um horário.";
                return RedirectToAction("Clube", new { clubeId });
            }

            var dia = pedidos[0].DataHora.Date;

            // Recusa horário de outro dia misturado: a tela é por dia, e a cobrança única
            // descreve "N horários de dd/MM" — misturar datas faria a descrição mentir.
            if (pedidos.Any(p => p.DataHora.Date != dia))
            {
                TempData["Erro"] = "Escolha horários de um dia só por vez.";
                return RedirectToAction("Clube", new { clubeId });
            }

            var disponiveis = await _horarioMarcacaoService.ObterSlotsDisponiveisAsync(clubeId, dia);
            var escolhidos = new List<SlotMarcacao>();
            foreach (var p in pedidos)
            {
                var slot = disponiveis.FirstOrDefault(s => s.QuadraClubeId == p.QuadraId && s.Inicio == p.DataHora);
                if (slot == null)
                {
                    TempData["Erro"] = $"O horário das {p.DataHora:HH:mm} acabou de ser marcado por outra pessoa. Nenhuma reserva foi feita — escolha de novo.";
                    return RedirectToAction("Clube", new { clubeId, data = dia });
                }
                escolhidos.Add(slot);
            }

            var dadosVarios = new DadosMarcacaoJogoVarios(clubeId, meuId, escolhidos
                .Select(s => new SlotReservado(s.QuadraClubeId, s.Inicio, (int)(s.Fim - s.Inicio).TotalMinutes))
                .ToList());

            var precoTotal = escolhidos.Sum(s => s.Preco ?? 0m);
            var dono = await _pagamentos.ObterDonoDoClubeAsync(clubeId);
            if (precoTotal > 0 && _pagamentos.PodeCobrarQuadra(precoTotal, dono))
            {
                var jogador = await _context.Jogadores.FindAsync(meuId);
                if (jogador != null)
                {
                    // Uma cobrança só pro conjunto; as marcações nascem no webhook.
                    var checkout = await _pagamentos.IniciarCobrancaQuadrasAsync(
                        clube, dono!, jogador, precoTotal, dadosVarios);

                    if (checkout != null) return Redirect(checkout);

                    TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                    return RedirectToAction("Clube", new { clubeId, data = dia });
                }
            }

            MarcacaoJogo? primeira = null;
            foreach (var s in dadosVarios.Slots)
            {
                var m = new MarcacaoJogo
                {
                    ClubeId = clubeId,
                    QuadraClubeId = s.QuadraClubeId,
                    JogadorId = meuId,
                    DataHora = s.DataHora,
                    DuracaoMinutos = s.DuracaoMinutos,
                    Status = "Confirmada",
                    PagaNoLocal = true,
                };
                _context.MarcacoesJogo.Add(m);
                primeira ??= m;
            }
            await _context.SaveChangesAsync();

            if (dadosVarios.Slots.Count > 1)
            {
                TempData["Sucesso"] = $"{dadosVarios.Slots.Count} horários marcados!";
            }
            return RedirectToAction("Confirmada", new { id = primeira!.Id });
        }

        // Tela pós-marcação: pergunta se o jogador quer convidar alguém da lista antes de
        // seguir em frente — convite é opcional, a marcação já está feita de qualquer jeito.
        [HttpGet]
        public async Task<IActionResult> Confirmada(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var marcacao = await _context.MarcacoesJogo
                .Include(m => m.Clube)
                .Include(m => m.QuadraClube)
                .FirstOrDefaultAsync(m => m.Id == id && m.JogadorId == meuId);
            if (marcacao == null) return NotFound();

            return View(marcacao);
        }

        // ===================== CONVIDAR JOGADORES (link wa.me manual, mesmo padrão de Grupos) =====================

        [HttpGet]
        public async Task<IActionResult> Convidar(int marcacaoId, int? categoriaId, int? cidadeId)
        {
            var meuId = ObterJogadorIdLogado();
            var marcacao = await _context.MarcacoesJogo
                .Include(m => m.Clube)
                .Include(m => m.QuadraClube)
                .FirstOrDefaultAsync(m => m.Id == marcacaoId && m.JogadorId == meuId);
            if (marcacao == null) return NotFound();

            ViewBag.Marcacao = marcacao;
            ViewBag.Categorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Nome).ToListAsync();

            // Só cidade onde tem alguém marcado: esta tela existe pra ACHAR jogador, e cidade
            // sem ninguém é opção que só sabe devolver lista vazia.
            var cidadesComJogador = await _context.JogadorCidades
                .Select(jc => jc.Cidade)
                .Distinct()
                .ToListAsync();

            ViewBag.Cidades = CidadesSemRepetir.Agrupar(cidadesComJogador);
            ViewBag.CategoriaId = categoriaId;
            ViewBag.CidadeId = cidadeId;

            var jogadores = new List<Jogador>();

            if (categoriaId.HasValue || cidadeId.HasValue)
            {
                var query = _context.Jogadores.Where(j => j.Id != meuId && j.AceitaConvitesJogo && !string.IsNullOrEmpty(j.Celular));

                if (categoriaId.HasValue)
                    query = query.Where(j => _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == categoriaId));

                // Todos os ids da mesma cidade — ver CidadesSemRepetir.IdsDaMesma.
                if (cidadeId.HasValue)
                {
                    var ids = CidadesSemRepetir.IdsDaMesma(cidadeId.Value, cidadesComJogador);
                    query = query.Where(j => _context.JogadorCidades.Any(c => c.JogadorId == j.Id && ids.Contains(c.CidadeId)));
                }

                jogadores = await query.OrderBy(j => j.Nome).ToListAsync();
            }

            return View(jogadores);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarConvite(int marcacaoId, int jogadorId, int? categoriaId, int? cidadeId)
        {
            var meuId = ObterJogadorIdLogado();
            var marcacao = await _context.MarcacoesJogo
                .Include(m => m.Clube)
                .Include(m => m.QuadraClube)
                .FirstOrDefaultAsync(m => m.Id == marcacaoId && m.JogadorId == meuId);
            if (marcacao == null) return NotFound();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            var mensagem = $"Oi {jogador.Nome}! Marquei quadra em {marcacao.Clube.Nome} " +
                           $"dia {marcacao.DataHora:dd/MM 'às' HH:mm} ({marcacao.QuadraClube.Nome}). Bora jogar?";

            TempData["WhatsAppLink"] = WhatsAppLinkHelper.GerarLink(jogador.Celular, mensagem);
            TempData["WhatsAppNome"] = jogador.Nome;

            return RedirectToAction("Convidar", new { marcacaoId, categoriaId, cidadeId });
        }

        [HttpGet]
        public async Task<IActionResult> MinhasMarcacoes()
        {
            var meuId = ObterJogadorIdLogado();
            var marcacoes = await _context.MarcacoesJogo
                .Include(m => m.Clube)
                .Include(m => m.QuadraClube)
                .Where(m => m.JogadorId == meuId && m.Status == "Confirmada")
                .OrderByDescending(m => m.DataHora)
                .ToListAsync();

            return View(marcacoes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var marcacao = await _context.MarcacoesJogo.FirstOrDefaultAsync(m => m.Id == id && m.JogadorId == meuId);
            if (marcacao != null)
            {
                marcacao.Status = "Cancelada";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MinhasMarcacoes");
        }
    }
}
