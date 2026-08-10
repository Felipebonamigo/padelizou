using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;
// A ação PixDireto() abaixo esconde a classe de regras de mesmo nome — o alias desfaz o empate.
using RegrasDoPix = Padelizou.Services.PixDireto;

namespace padelizou.Controllers
{
    // Painel do administrador: hoje só gerencia donos de clube e a lista de administradores do
    // sistema — fundação pra futuras telas administrativas reaproveitarem o mesmo gate.
    [Authorize]
    // `partial`: o acerto do Ranking Americano mora em AdminController.RankingAmericano.cs.
    // Mesmo caminho já usado no TorneiosController — o arquivo cresce por assunto, não por
    // acumulação.
    public partial class AdminController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IConfiguration _configuration;
        private readonly RegistroResultadosSettings _registro;
        private readonly RankingRsSettings _rankingRs;
        private readonly ILogger<AdminController>? _logger;

        public AdminController(DbPadelContext context, IPushNotificationService pushNotificationService,
            IConfiguration configuration,
            Microsoft.Extensions.Options.IOptions<RegistroResultadosSettings> registro,
            // Opcional como o logger: o preço por inscrito tem padrão no próprio settings, e
            // os testes que só exercitam outras ações não precisam montá-lo.
            Microsoft.Extensions.Options.IOptions<RankingRsSettings>? rankingRs = null,
            ILogger<AdminController>? logger = null)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _configuration = configuration;
            _registro = registro.Value;
            _rankingRs = rankingRs?.Value ?? new RankingRsSettings();
            _logger = logger;
        }

        // Qualquer administrador (raiz ou nomeado) — usado pelas ações que administradores
        // nomeados também podem fazer, como atribuir dono de clube.
        //
        // ⚠️ E o ASSISTENTE DO SISTEMA entra aqui, mas SÓ NO GET. Ele enxerga o painel inteiro
        // e não muda nada (pedido do Felipe: o Foka acompanha tudo, só o Felipe edita).
        //
        // A trava é o VERBO, e isso não é atalho: foi conferido, uma a uma, que as 23
        // gravações dos controllers de admin estão TODAS sob [HttpPost] e nenhuma sob
        // [HttpGet]. Travar aqui, num lugar só, é o que evita ter que lembrar do assistente em
        // cada uma das ~62 telas — e esquecer uma custa "ele não vê", nunca "ele editou".
        //
        // Se algum dia uma ação de GET passar a gravar, ela quebra esta premissa: o teste
        // TodaGravacaoDoAdminEstaSobPostTests existe pra falhar nesse dia.
        private async Task<Jogador?> ObterJogadorAdminAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return null;

            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador == null) return null;

            if (PoderesNoSistema.PodeEditarTudo(jogador)) return jogador;

            bool sóLendo = HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method);
            return sóLendo && PoderesNoSistema.PodeOlharTudo(jogador) ? jogador : null;
        }

        // Só o administrador raiz — usado só pra gerenciar quem é IsAdminGeral.
        private async Task<Jogador?> ObterJogadorAdminRaizAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return null;

            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && jogador.IsAdminRaiz ? jogador : null;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            [FromServices] PortaoDeAcesso portao,
            [FromServices] Microsoft.Extensions.Options.IOptions<AcessoAntecipadoSettings> acessoAntecipado)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.EhRaiz = admin.IsAdminRaiz;
            ViewBag.PortaoLigado = portao.EstaHabilitado(acessoAntecipado.Value);
            ViewBag.PortaoDecididoAqui = portao.DecididoPeloAdmin;

            // Quantos estão no WhatsApp sem nunca terem pedido. O card só aparece enquanto
            // houver alguém — feito o acerto, ele some sozinho e não vira enfeite permanente.
            ViewBag.NoWhatsAppSemTerPedido = await ConsentimentoDoWhatsApp
                .NoCanalSemTerPedido(_context).CountAsync();

            return View();
        }

        // ── Pedidos de "nós registramos os resultados para você" ──────────────────────────
        // O organizador pede a equipe; aqui é onde a gente olha se dá e responde. O valor
        // sai SÓ nesta resposta: antes de saber quem vai e de onde vem, qualquer preço na
        // tela do organizador seria chute virando promessa.

        [HttpGet]
        public async Task<IActionResult> RegistroResultados()
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedidos = await _context.SolicitacoesRegistroResultados
                .Include(s => s.Torneio)
                .OrderBy(s => s.Status == SolicitacaoRegistroResultados.Solicitada ? 0 : 1)
                .ThenBy(s => s.Torneio.DataInicio)
                .ToListAsync();

            // Nome de quem pediu, numa consulta só.
            var ids = pedidos.Select(p => p.SolicitadoPorId).Distinct().ToList();
            ViewBag.Solicitantes = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            ViewBag.Config = _registro;
            return View(pedidos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderRegistroResultados(
            int id, bool temDisponibilidade, int? pessoas, decimal? valor, string? resposta)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedido = await _context.SolicitacoesRegistroResultados
                .Include(s => s.Torneio)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (pedido == null) return NotFound();

            var problema = RegistroDeResultados.ProblemaParaResponder(pedido.Status);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("RegistroResultados");
            }

            if (temDisponibilidade && (valor == null || valor <= 0))
            {
                TempData["Erro"] = "Informe o valor combinado para confirmar o pedido.";
                return RedirectToAction("RegistroResultados");
            }

            pedido.Status = temDisponibilidade
                ? SolicitacaoRegistroResultados.Confirmada
                : SolicitacaoRegistroResultados.SemDisponibilidade;
            pedido.PessoasConfirmadas = temDisponibilidade ? (pessoas ?? pedido.PessoasSugeridas) : null;
            pedido.ValorCombinado = temDisponibilidade ? valor : null;
            pedido.Resposta = string.IsNullOrWhiteSpace(resposta) ? null : resposta.Trim();
            pedido.RespondidaEm = DateTime.Now;
            pedido.RespondidaPorId = admin.Id;

            await _context.SaveChangesAsync();

            // O organizador está esperando esta resposta pra decidir se arruma alguém por
            // conta própria. Deixá-la só na tela seria fazê-lo descobrir tarde demais.
            try
            {
                await _pushNotificationService.EnviarParaJogadorAsync(pedido.SolicitadoPorId,
                    temDisponibilidade ? "Temos equipe para o seu torneio!" : "Sem equipe disponível",
                    temDisponibilidade
                        ? $"{pedido.Torneio.Nome}: {pedido.PessoasConfirmadas} pessoa(s), R$ {pedido.ValorCombinado:N2}."
                        : $"{pedido.Torneio.Nome}: não conseguimos equipe para essa data.",
                    Url.Action("Details", "Torneios", new { id = pedido.TorneioId }));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Falha ao avisar o organizador da resposta do pedido {PedidoId}.", pedido.Id);
            }

            TempData["Sucesso"] = temDisponibilidade ? "Pedido confirmado." : "Pedido marcado como sem disponibilidade.";
            return RedirectToAction("RegistroResultados");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConcluirRegistroResultados(int id)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedido = await _context.SolicitacoesRegistroResultados.FindAsync(id);
            if (pedido == null) return NotFound();

            if (pedido.Status != SolicitacaoRegistroResultados.Confirmada)
            {
                TempData["Erro"] = "Só dá pra concluir um pedido confirmado.";
                return RedirectToAction("RegistroResultados");
            }

            pedido.Status = SolicitacaoRegistroResultados.Concluida;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Pedido concluído.";
            return RedirectToAction("RegistroResultados");
        }

        [HttpGet]
        public async Task<IActionResult> Clubes()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var clubes = await _context.Clubes
                .Include(c => c.Dono)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return View(clubes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtribuirDono(int clubeId, int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return NotFound();

            clube.DonoId = jogadorId == 0 ? null : jogadorId;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = jogadorId == 0 ? "Dono removido." : "Dono do clube atualizado.";
            return RedirectToAction("Clubes");
        }

        [HttpGet]
        public async Task<IActionResult> Administradores()
        {
            if (await ObterJogadorAdminRaizAsync() == null) return RedirectToAction("Perfil", "Auth");

            var administradores = await _context.Jogadores
                .Where(j => j.IsAdminGeral)
                .OrderBy(j => j.Nome)
                .ToListAsync();

            // Assistentes do sistema: veem tudo, não mudam nada. Lista separada porque são
            // poderes diferentes — juntar as duas na mesma tabela faria parecer que existe um
            // "administrador mais fraco", e não é isso: o assistente não edita NADA.
            ViewBag.Assistentes = await _context.Jogadores
                .Where(j => j.IsAssistente)
                .OrderBy(j => j.Nome)
                .ToListAsync();

            return View(administradores);
        }

        // ── Assistente do sistema ──────────────────────────────────────────────────────────
        // Só o RAIZ concede, igual ao administrador: quem pode ver o sistema inteiro é decisão
        // do dono. Ver Services/PoderesNoSistema.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAssistente(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.IsAssistente = true;
            await _context.SaveChangesAsync();

            // ⚠️ O crachá (claim) só é reescrito quando a pessoa entra de novo: quem estiver
            // logado agora continua sem enxergar o painel até sair e voltar.
            TempData["Sucesso"] = $"{NomeBonito.Formatar(jogador.Nome)} agora é assistente do sistema — "
                + "vê tudo, não altera nada. Ele precisa sair e entrar de novo pra valer.";
            return RedirectToAction("Administradores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAssistente(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador != null)
            {
                jogador.IsAssistente = false;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Assistente removido. Ele sai do painel no próximo login.";
            return RedirectToAction("Administradores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAdministrador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.IsAdminGeral = true;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{jogador.Nome} agora é administrador do sistema.";
            return RedirectToAction("Administradores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAdministrador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador != null)
            {
                jogador.IsAdminGeral = false;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Administrador removido.";
            return RedirectToAction("Administradores");
        }

        // Os erros não tratados de produção — o que o CapturaDeErro registrou. O aviso por
        // push/e-mail diz QUE quebrou; esta tela diz ONDE e POR QUÊ, sem precisar de ssh.
        [HttpGet]
        public async Task<IActionResult> Erros()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var erros = await _context.ErrosDoSistema
                .OrderByDescending(e => e.QuandoEm)
                .Take(100)
                .ToListAsync();

            // Nome de quem estava logado quando estourou. Consulta separada (não navegação)
            // porque ErroDoSistema não tem FK de propósito — ver o comentário no modelo.
            var ids = erros.Where(e => e.JogadorId != null).Select(e => e.JogadorId!.Value).Distinct().ToList();
            ViewBag.NomesDosJogadores = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            ViewBag.Ultimas24h = erros.Count(e => e.QuandoEm >= DateTime.Now.AddHours(-24));

            return View(erros);
        }

        // Estoura de propósito, pra provar que o vigia funciona. Mesma razão do teste de aviso
        // que já existe aqui do lado: alarme que só se manifesta no dia do desastre é alarme
        // que ninguém sabe se está ligado — e no que não se testa, não se confia.
        //
        // O caminho é o de verdade, inteiro: estoura sem tratamento, o CapturaDeErro registra,
        // o admin recebe push e e-mail, e a pessoa vê a mesma tela amiga que um usuário veria.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestarVigiaDeErros()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            throw new InvalidOperationException(
                "Erro de teste disparado do painel admin — se você está lendo isto no registro, o vigia está funcionando.");
        }

        // O que os jogadores acharam do site. Chega tudo invisível; publicar é decisão de
        // admin, uma a uma, depois de ler.
        [HttpGet]
        public async Task<IActionResult> Feedbacks()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedbacks = await _context.FeedbacksSite
                .Include(f => f.Jogador)
                .OrderByDescending(f => f.CriadoEm)
                .ToListAsync();

            ViewBag.Nps = RegrasDeFeedback.Nps(feedbacks.Select(f => f.Nota));
            ViewBag.NaoLidos = feedbacks.Count(f => !f.Lido);
            ViewBag.Publicados = feedbacks.Count(f => f.Exibir);

            return View(feedbacks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarExibicaoFeedback(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedback = await _context.FeedbacksSite.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.Exibir = !feedback.Exibir;
            feedback.ExibidoEm = feedback.Exibir ? DateTime.Now : null;
            feedback.Lido = true;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = feedback.Exibir
                ? "Publicado — agora aparece na página inicial."
                : "Tirado do ar.";
            return RedirectToAction("Feedbacks");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarFeedbackLido(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedback = await _context.FeedbacksSite.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.Lido = !feedback.Lido;
            await _context.SaveChangesAsync();

            return RedirectToAction("Feedbacks");
        }

        // ── Comentários denunciados ────────────────────────────────────────────────────
        // A fila fica aqui, não num e-mail: pra decidir é preciso ver o texto ao lado de
        // quem escreveu, em que perfil e quem denunciou — contexto que só a tela dá.
        // As duas saídas são deliberadamente as únicas: apagar o comentário ou mantê-lo
        // (limpando o carimbo). Não existe "banir autor" aqui — punição de conta é outra
        // decisão, tomada com mais calma que um clique numa fila.

        [HttpGet]
        public async Task<IActionResult> Denuncias()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var comentarios = await _context.ComentariosPerfil
                .Include(c => c.Autor)
                .Include(c => c.Perfil)
                .Where(c => c.DenunciadoEm != null)
                .OrderBy(c => c.DenunciadoEm)
                .ToListAsync();

            // Nome de quem denunciou, numa consulta só (DenunciadoPorId não tem FK/nav
            // de propósito — ver ComentarioPerfil). Quem excluiu a conta sai como null
            // e a tela mostra "conta excluída".
            var ids = comentarios.Where(c => c.DenunciadoPorId != null)
                .Select(c => c.DenunciadoPorId!.Value).Distinct().ToList();
            ViewBag.Denunciantes = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            return View(comentarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApagarComentarioDenunciado(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var comentario = await _context.ComentariosPerfil.FindAsync(id);
            if (comentario != null)
            {
                _context.ComentariosPerfil.Remove(comentario);
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Comentário apagado.";
            return RedirectToAction("Denuncias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManterComentarioDenunciado(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var comentario = await _context.ComentariosPerfil.FindAsync(id);
            if (comentario != null)
            {
                comentario.DenunciadoEm = null;
                comentario.DenunciadoPorId = null;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Comentário mantido — saiu da fila.";
            return RedirectToAction("Denuncias");
        }

        // Métricas de uso: os números que dizem se o sistema está crescendo e quanto a
        // plataforma já faturou no ano (controle do teto do MEI). CriadoEm nulo = registro
        // anterior a 25/07/2026 (antes da coluna existir) — entra nos totais, não nas séries.
        [HttpGet]
        public async Task<IActionResult> Metricas(string? agrupar = null)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var agora = DateTime.Now;
            var ha7 = agora.AddDays(-7);
            var ha30 = agora.AddDays(-30);
            var inicioAno = new DateTime(agora.Year, 1, 1);

            // Dia, semana ou mês. Semana continua sendo o padrão — é o que a tela sempre
            // mostrou, e link antigo (sem o parâmetro) tem que continuar caindo nela.
            var agrupamento = FaixasDeMetricas.Normalizar(agrupar);
            var faixas = FaixasDeMetricas.Series(agrupamento, agora);
            var inicioSerie = faixas[0];

            var vm = new MetricasAdminVM
            {
                Agrupamento = agrupamento,
                TotalJogadores = await _context.Jogadores.CountAsync(),
                JogadoresNovos7 = await _context.Jogadores.CountAsync(j => j.CriadoEm >= ha7),
                JogadoresNovos30 = await _context.Jogadores.CountAsync(j => j.CriadoEm >= ha30),

                InscricoesNovas7 = await _context.Duplas.CountAsync(d => d.CriadoEm >= ha7)
                    + await _context.InscricoesAmericanas.CountAsync(i => i.CriadoEm >= ha7),
                InscricoesNovas30 = await _context.Duplas.CountAsync(d => d.CriadoEm >= ha30)
                    + await _context.InscricoesAmericanas.CountAsync(i => i.CriadoEm >= ha30),

                PagamentosConfirmados30 = await _context.Pagamentos
                    .CountAsync(p => p.Status == "Confirmado" && p.ConfirmadoEm >= ha30),
                ValorConfirmado30 = await _context.Pagamentos
                    .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= ha30)
                    .SumAsync(p => (decimal?)p.Valor) ?? 0,

                JogadoresComApp = await _context.PushSubscriptionsJogador
                    .Select(s => s.JogadorId).Distinct().CountAsync(),
                TorneiosTotal = await _context.Torneios.CountAsync(),
                TorneiosAtivos = await _context.Torneios.CountAsync(t => t.Status != "Finalizado"),

                ComissaoAno = await _context.Pagamentos
                    .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= inicioAno)
                    .SumAsync(p => (decimal?)p.Comissao) ?? 0,
                TetoMei = _configuration.GetValue<decimal?>("Mei:TetoAnual") ?? 81000m,
            };

            // Estado do backup fora do servidor, lido do mesmo carimbo que o vigia usa. Vem
            // pro painel pra não depender só do e-mail semanal — que em 07/08/2026 esbarrou na
            // cota do Gmail e não chegou. Ambiente sem backup (dev) não mostra nada.
            var carimboDoBackup = _configuration["Backup:ArquivoDeUltimoSucesso"];
            vm.VigiaDeBackupLigado = !string.IsNullOrWhiteSpace(carimboDoBackup);
            if (vm.VigiaDeBackupLigado)
            {
                vm.UltimoBackupFora = VigiaDoBackup.LerUltimoSucesso(carimboDoBackup!);
            }

            // A série — poucas linhas por fatia, agrupar em memória é suficiente.
            var cadastros = await _context.Jogadores
                .Where(j => j.CriadoEm >= inicioSerie)
                .Select(j => j.CriadoEm!.Value).ToListAsync();
            var inscricoes = (await _context.Duplas
                    .Where(d => d.CriadoEm >= inicioSerie)
                    .Select(d => d.CriadoEm!.Value).ToListAsync())
                .Concat(await _context.InscricoesAmericanas
                    .Where(i => i.CriadoEm >= inicioSerie)
                    .Select(i => i.CriadoEm!.Value).ToListAsync())
                .ToList();
            var pagamentos = await _context.Pagamentos
                .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= inicioSerie)
                .Select(p => new { Data = p.ConfirmadoEm!.Value, p.Valor }).ToListAsync();

            foreach (var inicio in faixas)
            {
                var fim = FaixasDeMetricas.ProximaFaixa(agrupamento, inicio);
                vm.Faixas.Add(new FaixaMetricaVM
                {
                    Inicio = inicio,
                    Rotulo = FaixasDeMetricas.Rotulo(agrupamento, inicio),
                    Cadastros = cadastros.Count(d => d >= inicio && d < fim),
                    Inscricoes = inscricoes.Count(d => d >= inicio && d < fim),
                    Pagamentos = pagamentos.Count(p => p.Data >= inicio && p.Data < fim),
                    Valor = pagamentos.Where(p => p.Data >= inicio && p.Data < fim).Sum(p => p.Valor),
                });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarNotificacaoTeste()
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var quantidade = await _pushNotificationService.EnviarParaTodosInscritosAsync(
                "Padelizou",
                "Notificação de teste — se você recebeu isso, o app instalado no seu celular está funcionando certinho.");

            TempData["Sucesso"] = quantidade > 0
                ? $"Notificação de teste enviada pra {quantidade} jogador(es) com o app instalado."
                : "Ninguém tem o app instalado com notificações ativas ainda.";
            return RedirectToAction("Index");
        }

        // Tira do WhatsApp quem nunca pediu pra estar lá, e convida essa gente a voltar pelos
        // canais que não correm risco (push e e-mail). Ver Services/ConsentimentoDoWhatsApp
        // pro porquê disto ser a causa raiz da restrição, e não mais um ajuste de volume.
        //
        // Admin RAIZ: mexe na preferência de dezenas de pessoas de uma vez e manda mensagem
        // pra todas elas — não é botão de admin nomeado.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesfazerOptInDoWhatsApp([FromServices] DesfazerOptInHerdado acao)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var r = await acao.RodarAsync();

            TempData["Sucesso"] = r.Desmarcados == 0
                ? "Ninguém está no WhatsApp sem ter pedido — não havia o que desfazer."
                : $"{r.Desmarcados} pessoa(s) saíram do WhatsApp e foram convidadas a voltar "
                  + "pelo app e por e-mail. Quem quiser marca de novo nas preferências.";

            return RedirectToAction("Index");
        }

        // Teste dirigido: escolhe UMA pessoa (pelo login ou e-mail) e quais canais tentar.
        // O botão de cima manda pra todo mundo com o app e serve pra outra coisa; este aqui
        // é pra conferir a corrente inteira sem incomodar ninguém além de quem você escolheu.
        [HttpGet]
        public async Task<IActionResult> NotificacaoTeste()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            return View(new TesteDeNotificacaoVM());
        }

        // A tela tem DOIS tempos, e o `acao` diz em qual estamos. Achar e mandar viraram passos
        // separados porque, juntos, o admin só descobria pra quem tinha mandado depois de já
        // ter mandado — e num teste isso é tarde.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificacaoTeste(string? identificador, bool porPush = false,
            bool porWhatsApp = false, string? mensagem = null, int? jogadorId = null, string? acao = null)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            // Os canais só existem como caixinha no PASSO 2 — o formulário de procurar não tem
            // nenhuma. Lendo `porPush`/`porWhatsApp` sempre do POST, o passo 1 confundia "não
            // veio no formulário" com "o admin desmarcou": a tela de envio nascia com os dois
            // canais APAGADOS (contra o padrão do VM), e o primeiro clique em Enviar batia em
            // "escolha pelo menos um canal" — parecia que o teste tinha falhado.
            // No passo de envio vale o que ele marcou, inclusive desmarcar.
            var noPassoDeEnvio = acao == "enviar";

            var vm = new TesteDeNotificacaoVM
            {
                Identificador = identificador,
                PorPush = noPassoDeEnvio ? porPush : true,
                PorWhatsApp = noPassoDeEnvio ? porWhatsApp : true,
                Mensagem = mensagem,
            };

            // --- Passo 1: achar e mostrar quem achou ---------------------------------------
            if (acao != "enviar")
            {
                if (jogadorId is int escolhidoNaLista)
                {
                    // Veio da lista de desambiguação: a pessoa foi escolhida a dedo, não se
                    // procura de novo (o nome casaria com os mesmos e voltaria pra lista).
                    vm.Selecionado = await _context.Jogadores.FindAsync(escolhidoNaLista);
                    if (vm.Selecionado == null) vm.Erro = "Essa pessoa não existe mais.";
                    return View(vm);
                }

                // Login, e-mail, nome, apelido ou CPF — o que o admin tiver na mão.
                var achados = await BuscaJogador.ParaAcaoAdministrativaAsync(_context, identificador);

                if (achados.Count == 0)
                {
                    vm.Erro = "Não achei ninguém com esse login, e-mail, nome, apelido ou CPF.";
                    return View(vm);
                }

                // Achou um só? Já fica escolhido — mas APARECE na tela antes de qualquer envio,
                // que é o ponto: o admin confere que é a pessoa certa com o botão ainda parado.
                if (achados.Count == 1) vm.Selecionado = achados[0];
                else vm.Candidatos = achados;

                return View(vm);
            }

            // --- Passo 2: mandar -----------------------------------------------------------
            var alvo = jogadorId is int escolhido
                ? await _context.Jogadores.FindAsync(escolhido)
                : null;

            if (alvo == null)
            {
                vm.Erro = "Escolha a pessoa antes de mandar.";
                return View(vm);
            }

            if (!porPush && !porWhatsApp)
            {
                vm.Selecionado = alvo;
                vm.Erro = "Escolha pelo menos um canal — sem isso o teste não testa nada.";
                return View(vm);
            }

            var texto = string.IsNullOrWhiteSpace(mensagem)
                ? "Notificação de teste — se você recebeu isso, o aviso do Padelizou está chegando certinho."
                : mensagem.Trim();

            vm.Alvo = alvo;
            // Continua escolhido depois de mandar: quem testa quase nunca testa uma vez só —
            // troca o canal, troca o texto, manda de novo na MESMA pessoa.
            vm.Selecionado = alvo;
            vm.Resultado = await _pushNotificationService.EnviarTesteAsync(
                alvo.Id, porPush, porWhatsApp, "Padelizou", texto, "/");

            return View(vm);
        }

        // Refaz as imagens antigas no padrão novo (o ImagemEnviada cuida das que chegam daqui
        // pra frente). Idempotente: rodar de novo pula tudo que já está no tamanho certo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtimizarImagens([FromServices] OtimizacaoDeImagens otimizacao)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var r = await otimizacao.RodarAsync();

            if (r.Otimizadas == 0)
            {
                TempData["Sucesso"] = r.ComProblema > 0
                    ? $"Nenhuma imagem precisou mudar. {r.ComProblema} não puderam ser lidas."
                    : "Nenhuma imagem precisou mudar — está tudo já no tamanho certo.";
            }
            else
            {
                var texto = $"{r.Otimizadas} imagem(ns) otimizada(s): "
                          + $"{OtimizacaoDeImagens.Resultado.EmMegas(r.BytesAntes)} viraram "
                          + $"{OtimizacaoDeImagens.Resultado.EmMegas(r.BytesDepois)} "
                          + $"({OtimizacaoDeImagens.Resultado.EmMegas(r.BytesEconomizados)} a menos).";

                if (r.ComProblema > 0) texto += $" {r.ComProblema} não puderam ser lidas.";

                TempData["Sucesso"] = texto;
            }

            return RedirectToAction("Index");
        }

        // Replay do Padelímetro: zera e reconstrói o nível de TODO MUNDO a partir das
        // partidas finalizadas, em ordem cronológica (RANKING.md). É o botão de "mudou a
        // regra" e o de "algum jogo se perdeu do extrato" — determinístico, rodar duas
        // vezes dá no mesmo lugar.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalcularPadelimetro([FromServices] IPadelimetroService padelimetro)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var partidas = await padelimetro.RecalcularTudoAsync();
            var comNivel = await _context.Jogadores.CountAsync(j => j.Padelimetro != null);

            TempData["Sucesso"] = partidas == 0
                ? "Nenhuma partida contável — ninguém ganhou Padelímetro ainda."
                : $"Padelímetro recalculado: {partidas} partida(s) contada(s), {comNivel} jogador(es) com nível.";

            return RedirectToAction("Index");
        }

        // O PORTÃO DE ACESSO ANTECIPADO, ligado e desligado daqui.
        //
        // Desligado, o site fica aberto: qualquer pessoa entra e se cadastra sem a senha
        // compartilhada. É o botão do LANÇAMENTO — e ele existe porque a alternativa era ssh
        // no servidor mais restart do serviço, o que não se faz do celular às 20h de um
        // sábado. O estado fica no banco e sobrevive a deploy (ver Services/PortaoDeAcesso).
        //
        // ⚠️ Só o admin RAIZ. Desligar o portão é o ato mais público que este painel tem —
        // abre o cadastro pro mundo inteiro —, e administrador nomeado entra pra ajudar na
        // operação do dia a dia, não pra decidir a hora do lançamento.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPortao([FromServices] PortaoDeAcesso portao, bool habilitar)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            await portao.DefinirAsync(_context, habilitar, admin.Id);

            TempData["Sucesso"] = habilitar
                ? "Portão LIGADO: o site volta a pedir a senha de acesso antecipado a quem não tem conta."
                : "Portão DESLIGADO: o site está aberto — qualquer pessoa entra e se cadastra sem senha.";

            _logger?.LogWarning("Portão de acesso antecipado {Estado} pelo admin {AdminId}.",
                habilitar ? "LIGADO" : "DESLIGADO", admin.Id);

            return RedirectToAction("Index");
        }

        // ── Pix direto: configuração da chave + fila de conferência ───────────────────────
        // O dinheiro da mensalidade e da taxa do externo caindo direto na conta do Padelizou,
        // sem gateway. O QR não avisa quando é pago, então a confirmação é MANUAL: o admin
        // olha o extrato do banco e dá baixa aqui (ver Services/PixDireto).

        [HttpGet]
        public async Task<IActionResult> PixDireto()
        {
            // Raiz, como tudo que é dinheiro DO PADELIZOU: dar baixa exige olhar o extrato
            // do banco, e o extrato é do dono.
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var dados = await ChavePixDoPadelizou.LerAsync(_context);
            ViewBag.Chave = dados?.Chave;
            ViewBag.Nome = dados?.Nome;
            ViewBag.Cidade = dados?.Cidade;

            // A fila inteira de cobranças Pix abertas — quem declarou primeiro no topo, e as
            // que ninguém declarou por último (o Pix pode ter caído sem clique nenhum).
            var fila = await _context.Pagamentos
                .Include(p => p.Jogador)
                .Where(p => p.MetodoPagamento == RegrasDoPix.Metodo
                    && (p.Status == "Pendente" || p.Status == RegrasDoPix.AguardandoConfirmacao))
                .OrderBy(p => p.Status == "Pendente" ? 1 : 0)
                .ThenBy(p => p.CriadoEm)
                .ToListAsync();

            // Nome dos torneios da taxa do externo, numa consulta só.
            var torneioIds = fila.Where(p => p.TorneioId != null).Select(p => p.TorneioId!.Value).Distinct().ToList();
            ViewBag.Torneios = await _context.Torneios
                .Where(t => torneioIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.Nome);

            return View(fila);
        }

        // ⚠️ Só o admin RAIZ, como o portão: a chave decide PRA ONDE VAI O DINHEIRO. Um admin
        // nomeado que pudesse trocá-la poderia apontar todo Pix pra conta dele.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigurarPix(string? chave, string? nome, string? cidade)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var problema = ChavePixDoPadelizou.Problema(chave, nome, cidade);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("PixDireto");
            }

            await ChavePixDoPadelizou.GravarAsync(_context, chave!, nome!, cidade!, admin.Id);

            _logger?.LogWarning("Chave Pix do recebimento direto alterada pelo admin {AdminId}.", admin.Id);
            TempData["Sucesso"] = "Chave Pix gravada. As próximas cobranças por Pix já saem pra ela.";
            return RedirectToAction("PixDireto");
        }

        // A baixa: o admin conferiu que o Pix caiu no extrato. É o webhook humano — daqui em
        // diante o fluxo é o mesmo do gateway (EfetivarAsync), inclusive as notificações.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPix(int id,
            [FromServices] IPagamentoInscricaoService pagamentos)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var pagamento = await _context.Pagamentos.FindAsync(id);
            var problema = RegrasDoPix.ProblemaParaConfirmar(pagamento);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("PixDireto");
            }

            pagamento!.Status = "Confirmado";
            pagamento.ConfirmadoEm = DateTime.Now;
            pagamento.ConfirmadoPorJogadorId = admin.Id;
            await _context.SaveChangesAsync();

            await pagamentos.EfetivarAsync(pagamento);

            _logger?.LogInformation("Pix direto {Id} confirmado pelo admin {AdminId}.", pagamento.Id, admin.Id);
            TempData["Sucesso"] = $"Pagamento #{pagamento.Id} confirmado — {pagamento.Valor:C} registrado.";
            return RedirectToAction("PixDireto");
        }

        // O Pix não apareceu no extrato: a cobrança sai da fila sem virar dinheiro. Quem pagou
        // de verdade e foi recusado por engano gera outra cobrança e avisa de novo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecusarPix(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var pagamento = await _context.Pagamentos.FindAsync(id);
            var problema = RegrasDoPix.ProblemaParaRecusar(pagamento);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("PixDireto");
            }

            pagamento!.Status = "Cancelado";
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Pix direto {Id} recusado pelo admin {AdminId}.", pagamento.Id, admin.Id);
            TempData["Sucesso"] = $"Cobrança #{pagamento.Id} cancelada.";
            return RedirectToAction("PixDireto");
        }

        // ── O caixa do Padelizou ──────────────────────────────────────────────────────────
        // Recebido por fora × pelo gateway × o que saiu — e os dois formulários de lançar à
        // mão (pagamento por fora e despesa). Regras em Services/FinanceiroDoPadelizou.
        // ⚠️ Só o admin RAIZ: este é o caixa do dono, não uma tela de operação.

        [HttpGet]
        public async Task<IActionResult> Financeiro(string? periodo)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var agora = DateTime.Now;
            var periodoEscolhido = (periodo ?? "").Trim().ToLower() switch
            {
                "mes" => "mes", "ano" => "ano", _ => "sempre"
            };
            DateTime? de = periodoEscolhido switch
            {
                "mes" => new DateTime(agora.Year, agora.Month, 1),
                "ano" => new DateTime(agora.Year, 1, 1),
                _ => null,
            };

            // O corte é pela data em que o dinheiro ENTROU (ConfirmadoEm) — pendente não é
            // caixa. Nas despesas, pela data informada no lançamento.
            var confirmados = await _context.Pagamentos
                .Where(p => p.Status == "Confirmado" && (de == null || p.ConfirmadoEm >= de))
                .ToListAsync();
            var despesas = await _context.DespesasRegistradas
                .Where(d => de == null || d.Data >= de)
                .OrderByDescending(d => d.Data)
                .ToListAsync();

            ViewBag.Periodo = periodoEscolhido;
            ViewBag.Resumo = FinanceiroDoPadelizou.Somar(confirmados, despesas);
            ViewBag.Despesas = despesas;

            // As últimas entradas, mais recentes primeiro, pro admin conferir o que somou.
            var entradas = confirmados.OrderByDescending(p => p.ConfirmadoEm ?? p.CriadoEm).Take(50).ToList();
            ViewBag.Entradas = entradas;
            var jogadorIds = entradas.Select(p => p.JogadorId).Distinct().ToList();
            ViewBag.Pagadores = await _context.Jogadores
                .Where(j => jogadorIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            // Pro formulário da taxa: os torneios "por fora" com taxa ainda em aberto.
            ViewBag.TorneiosComTaxaAberta = await _context.Torneios
                .Where(t => t.FormaPagamento == "Externo" && t.TaxaExternoPagaEm == null)
                .OrderByDescending(t => t.DataInicio)
                .Select(t => new { t.Id, t.Nome, t.Codigo })
                .ToListAsync();

            return View();
        }

        // Dinheiro que entrou POR FORA de tudo (Pix combinado no WhatsApp, dinheiro na mão):
        // o registro cria o Pagamento já confirmado e EFETIVA — estende a assinatura ou
        // libera as chaves, com as mesmas notificações do caminho normal.
        // ⚠️ Não tem desfazer automático: o efetivar dispara coisas que não voltam sozinhas.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarPagamento(string? tipo, string? pagador,
            int? torneioId, decimal valor, DateTime? data,
            [FromServices] IPagamentoInscricaoService pagamentos)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var problema = FinanceiroDoPadelizou.ProblemaParaRegistrar(tipo, valor);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Financeiro");
            }

            Jogador? quemPagou;
            object dados;
            int? torneioDoPagamento = null;
            string descricaoDoSucesso;

            if (tipo == RegrasDoPix.TipoAssinatura)
            {
                var chave = (pagador ?? "").Trim();
                var soDigitos = new string(chave.Where(char.IsDigit).ToArray());
                // Mesma consulta da entrada: login e e-mail vivem no mesmo espaço de nomes,
                // então procurar num só campo acharia metade das pessoas.
                quemPagou = await _context.Jogadores.FirstOrDefaultAsync(j =>
                    j.Login == chave || j.Email == chave || (soDigitos != "" && j.Cpf == soDigitos));

                problema = FinanceiroDoPadelizou.ProblemaComOPagador(tipo, quemPagou);
                if (problema != null)
                {
                    TempData["Erro"] = problema;
                    return RedirectToAction("Financeiro");
                }

                dados = new DadosAssinaturaProfessor(quemPagou!.Id);
                descricaoDoSucesso = $"mensalidade de {quemPagou.Nome}";
            }
            else
            {
                var torneio = torneioId == null ? null : await _context.Torneios.FindAsync(torneioId.Value);
                problema = FinanceiroDoPadelizou.ProblemaComOTorneio(torneio);
                if (problema != null)
                {
                    TempData["Erro"] = problema;
                    return RedirectToAction("Financeiro");
                }

                // Quem "paga" no registro é quem recebeu a cobrança de verdade: o criador do
                // torneio — é no extrato DELE que este recibo aparece.
                quemPagou = await pagamentos.ObterRecebedorTorneioAsync(torneio!.Id) ?? admin;
                dados = new DadosTaxaExterno(torneio.Id);
                torneioDoPagamento = torneio.Id;
                descricaoDoSucesso = $"taxa de {torneio.Nome}";
            }

            var pagamento = new Pagamento
            {
                Tipo = tipo!,
                TorneioId = torneioDoPagamento,
                JogadorId = quemPagou!.Id,
                RecebedorId = null,
                Valor = valor,
                ValorRepasse = 0m,
                Comissao = valor,
                MetodoPagamento = FinanceiroDoPadelizou.MetodoManual,
                DadosInscricao = System.Text.Json.JsonSerializer.Serialize(dados),
                Status = "Confirmado",
                // Data de trás vale: o registro costuma vir depois do dinheiro.
                ConfirmadoEm = data ?? DateTime.Now,
                ConfirmadoPorJogadorId = admin.Id,
            };
            _context.Pagamentos.Add(pagamento);
            await _context.SaveChangesAsync();

            await pagamentos.EfetivarAsync(pagamento);

            _logger?.LogInformation("Pagamento manual {Id} ({Tipo}, {Valor}) registrado pelo admin {AdminId}.",
                pagamento.Id, pagamento.Tipo, valor, admin.Id);
            TempData["Sucesso"] = $"Registrado: {valor:C} — {descricaoDoSucesso}.";
            return RedirectToAction("Financeiro");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarDespesa(string? descricao, decimal valor, DateTime? data)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var problema = FinanceiroDoPadelizou.ProblemaComADespesa(descricao, valor);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Financeiro");
            }

            _context.DespesasRegistradas.Add(new DespesaRegistrada
            {
                Descricao = descricao!.Trim(),
                Valor = valor,
                Data = data ?? DateTime.Now,
                RegistradoPorJogadorId = admin.Id,
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Despesa de {valor:C} lançada.";
            return RedirectToAction("Financeiro");
        }

        // Despesa é só uma linha de caderneta — apagar não desfaz nada além dela mesma, por
        // isso aqui PODE, ao contrário do pagamento registrado.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApagarDespesa(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var despesa = await _context.DespesasRegistradas.FindAsync(id);
            if (despesa == null) return NotFound();

            _context.DespesasRegistradas.Remove(despesa);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Despesa \"{despesa.Descricao}\" apagada.";
            return RedirectToAction("Financeiro");
        }

        // ── O que devemos ao Ranking RS ───────────────────────────────────────────────────
        // R$ 1 por INSCRITO, e inscrito é PESSOA: quem joga duas categorias custa um real.
        // A regra inteira mora em Services/AcertoComORankingRs — inclusive por que ela NÃO
        // é a mesma da taxa dos 5%.

        // As inscrições de um torneio, já achatadas em "pessoa × categoria".
        private async Task<List<AcertoComORankingRs.Inscrito>> InscritosDoRankingRsAsync(int torneioId)
        {
            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId).ToListAsync();
            var americanas = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == torneioId).ToListAsync();

            var categorias = await _context.Categorias
                .Where(c => c.TorneioId == torneioId)
                .ToDictionaryAsync(c => c.Id, c => c.Nome);

            // Os nomes numa consulta só. Vale a pena carregar antes de achatar: sem isso cada
            // linha da tela de detalhe viraria uma ida ao banco.
            var jogadorIds = duplas.SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
                .Where(i => i != null).Select(i => i!.Value)
                .Concat(americanas.Select(i => i.JogadorId))
                .Distinct().ToList();
            var jogadores = await _context.Jogadores
                .Where(j => jogadorIds.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            return AcertoComORankingRs.Achatar(duplas, americanas, categorias, jogadores);
        }

        [HttpGet]
        public async Task<IActionResult> RankingRs()
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var torneios = await _context.Torneios
                .Where(t => t.ValidarPeloRankingRs)
                .OrderByDescending(t => t.DataInicio)
                .ToListAsync();

            var acertos = await _context.AcertosRankingRs
                .ToDictionaryAsync(a => a.TorneioId, a => a);

            // Uma linha por torneio, com a contagem de PESSOAS já resolvida.
            var linhas = new List<AcertoComORankingRs.LinhaDoTorneio>();
            foreach (var t in torneios)
            {
                int pessoas = AcertoComORankingRs.Cobradas(await InscritosDoRankingRsAsync(t.Id)).Count;
                acertos.TryGetValue(t.Id, out var acerto);

                linhas.Add(new AcertoComORankingRs.LinhaDoTorneio(
                    t, pessoas,
                    AcertoComORankingRs.Valor(pessoas, _rankingRs.CustoPorInscrito),
                    acerto,
                    // Com inscrição aberta o número ainda se mexe — a tela avisa em vez de
                    // fingir que o valor está fechado.
                    InscricoesAbertas: t.Status == "Inscrições Abertas"));
            }

            ViewBag.Linhas = linhas;
            ViewBag.CustoPorInscrito = _rankingRs.CustoPorInscrito;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RankingRsTorneio(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var cobradas = AcertoComORankingRs.Cobradas(await InscritosDoRankingRsAsync(id));

            ViewBag.Cobradas = cobradas;
            ViewBag.CustoPorInscrito = _rankingRs.CustoPorInscrito;
            ViewBag.Valor = AcertoComORankingRs.Valor(cobradas.Count, _rankingRs.CustoPorInscrito);
            ViewBag.Acerto = await _context.AcertosRankingRs.FirstOrDefaultAsync(a => a.TorneioId == id);
            return View(torneio);
        }

        // "Já paguei eles por este torneio": grava a FOTOGRAFIA (quantas pessoas, quanto deu)
        // e lança a despesa no caixa, pra o Financeiro não ficar devendo essa saída.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarAcertoRankingRs(int id)
        {
            var admin = await ObterJogadorAdminRaizAsync();
            if (admin == null) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            var jaAcertado = await _context.AcertosRankingRs.FirstOrDefaultAsync(a => a.TorneioId == id);
            var cobradas = torneio == null
                ? new List<AcertoComORankingRs.PessoaCobrada>()
                : AcertoComORankingRs.Cobradas(await InscritosDoRankingRsAsync(id));

            var problema = AcertoComORankingRs.ProblemaParaAcertar(torneio, cobradas.Count, jaAcertado);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("RankingRs");
            }

            var valor = AcertoComORankingRs.Valor(cobradas.Count, _rankingRs.CustoPorInscrito);

            var despesa = new DespesaRegistrada
            {
                Descricao = $"{MarcaDoRanking.Nome} — {torneio!.Nome} ({cobradas.Count} inscritos)",
                Valor = valor,
                Data = DateTime.Now,
                RegistradoPorJogadorId = admin.Id,
            };
            _context.DespesasRegistradas.Add(despesa);
            await _context.SaveChangesAsync();

            _context.AcertosRankingRs.Add(new AcertoRankingRs
            {
                TorneioId = torneio.Id,
                PessoasCobradas = cobradas.Count,
                Valor = valor,
                AcertadoEm = DateTime.Now,
                RegistradoPorJogadorId = admin.Id,
                DespesaId = despesa.Id,
            });
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Acerto com o Ranking RS do torneio {TorneioId}: {Pessoas} pessoas, {Valor}.",
                torneio.Id, cobradas.Count, valor);
            TempData["Sucesso"] = $"Acerto registrado: {valor:C} por {cobradas.Count} inscritos. A despesa entrou no caixa.";
            return RedirectToAction("RankingRs");
        }
    }
}
