using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class GruposController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly ISessaoGrupoService _sessaoGrupoService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<GruposController> _logger;

        public GruposController(DbPadelContext context, ISessaoGrupoService sessaoGrupoService,
            IPushNotificationService pushService, ILogger<GruposController> logger)
        {
            _context = context;
            _sessaoGrupoService = sessaoGrupoService;
            _pushService = pushService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = ObterUserId();

            var grupos = await _context.JogadoresGrupo
                .Include(jg => jg.GrupoPrivado)
                .Where(jg => jg.JogadorId == userId)
                .Select(jg => jg.GrupoPrivado)
                .ToListAsync();

            var idsGruposMembro = grupos.Select(g => g.Id).ToList();

            // Sessões onde o jogador foi convidado (avulso) mas ainda não é membro do grupo — ele
            // precisa conseguir achar e responder esse convite mesmo sem aparecer em "Meus Grupos".
            //
            // ⚠️ O convite fica aqui até o dia do jogo passar, RESPONDIDO OU NÃO. Filtrar por
            // "Pendente" fazia o jogo sumir da tela no instante em que a pessoa aceitava: ela não
            // é membro, então o grupo também não entra na lista de cima, e "Meus Grupos" ficava
            // vazia logo depois do "eu vou". Quem confirmou ainda precisa da tela pra ver quem
            // mais vai — e pra desmarcar quando não der.
            var hoje = DateTime.Today;
            var convites = await _context.ConfirmacoesSessao
                .Include(c => c.Sessao).ThenInclude(s => s.Grupo)
                .Where(c => c.JogadorId == userId && c.Avulso
                         && c.Sessao.DataHora >= hoje
                         && !idsGruposMembro.Contains(c.Sessao.GrupoId))
                .OrderBy(c => c.Sessao.DataHora)
                .ToListAsync();

            ViewBag.ConvitesDeJogo = convites;

            return View(grupos);
        }

        [HttpGet]
        public IActionResult Criar() => View();

        [HttpPost]
        public async Task<IActionResult> Criar(string nome)
        {
            var userId = ObterUserId();

            var grupo = new GrupoPrivado { Nome = nome, CodigoConvite = await GerarCodigoUnicoAsync(), AdministradorId = userId };
            _context.GruposPrivados.Add(grupo);
            await _context.SaveChangesAsync();

            _context.JogadoresGrupo.Add(new JogadorGrupo { JogadorId = userId, GrupoId = grupo.Id, PontuacaoInterna = 0 });
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalhes", new { id = grupo.Id });
        }

        // A tela de entrar por código — e também o destino do LINK de convite.
        //
        // O código vem pela URL, mas quem entra é o POST logo abaixo: convite não pode ser um
        // GET que já mete a pessoa no grupo. Link é coisa que o WhatsApp pré-visualiza, que
        // antivírus abre e que a pessoa toca sem querer — entrar tem que ser um ato dela.
        [HttpGet]
        public async Task<IActionResult> Entrar(string? codigo)
        {
            if (!string.IsNullOrWhiteSpace(codigo))
            {
                var convite = codigo.Trim().ToUpper();
                ViewBag.CodigoDoConvite = convite;

                // O nome do grupo na tela é o que faz o convite parecer convite, e não um
                // campo de código solto: "Entrar na Pinel Gravataí" responde sozinho.
                ViewBag.GrupoDoConvite = await _context.GruposPrivados
                    .Where(g => g.CodigoConvite == convite)
                    .Select(g => g.Nome)
                    .FirstOrDefaultAsync();
            }

            return View();
        }

        // Convida alguém pra PANELINHA (o grupo em si), pelo sistema.
        //
        // O convite pelo WhatsApp não passa por aqui: ele é só um link montado na tela, e é o
        // caminho de quem convida quem ainda NÃO tem conta. Este aqui é pro contrário — a
        // pessoa já está no Padelizou, e o aviso chega onde ela já olha.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConvidarParaGrupo(int grupoId, string? identificador)
        {
            var euId = ObterUserId();

            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo == null) return NotFound();

            // ⚠️ Só quem é DO grupo convida. Sem esta guarda, qualquer pessoa logada mandaria
            // convite em nome de uma panelinha que não é dela — spam com a nossa cara.
            bool souMembro = await _context.JogadoresGrupo
                .AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == euId);
            if (!souMembro) return Forbid();

            if (string.IsNullOrWhiteSpace(identificador))
            {
                TempData["Erro"] = "Digite o CPF ou o login de quem você quer convidar.";
                return RedirectToAction("Detalhes", new { id = grupoId });
            }

            // Mesma régua da entrada do sistema: acha por login OU por CPF, com ou sem
            // pontuação. Quem convida copia do jeito que tem na mão.
            var procurado = identificador.Trim();
            var soDigitos = new string(procurado.Where(char.IsDigit).ToArray());

            var convidado = await _context.Jogadores.FirstOrDefaultAsync(j =>
                j.Login == procurado || j.Cpf == procurado
                || (soDigitos.Length == 11 && j.Cpf == soDigitos));

            if (convidado == null)
            {
                TempData["Erro"] = $"Não achei ninguém com \"{procurado}\". "
                                 + "Confira o CPF/login — ou use o convite por WhatsApp, que serve pra quem ainda não tem conta.";
                return RedirectToAction("Detalhes", new { id = grupoId });
            }

            if (convidado.Id == euId)
            {
                TempData["Erro"] = "Você já está na panelinha.";
                return RedirectToAction("Detalhes", new { id = grupoId });
            }

            bool jaEstaDentro = await _context.JogadoresGrupo
                .AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == convidado.Id);
            if (jaEstaDentro)
            {
                TempData["Erro"] = $"{convidado.Nome} já está nesta panelinha.";
                return RedirectToAction("Detalhes", new { id = grupoId });
            }

            var quemConvidou = await _context.Jogadores.FindAsync(euId);
            var link = Url.Action("Entrar", "Grupos", new { codigo = grupo.CodigoConvite });

            await _pushService.EnviarParaJogadorAsync(
                convidado.Id,
                "Convite pra uma panelinha",
                $"{quemConvidou?.Nome ?? "Alguém"} te convidou pra entrar na \"{grupo.Nome}\".",
                link);

            TempData["Sucesso"] = $"Convite enviado pra {convidado.Nome}. "
                                + "Ele aparece nas notificações dele — e no celular, se tiver o app.";
            return RedirectToAction("Detalhes", new { id = grupoId });
        }

        // O método tem outro nome só porque o GET acima agora também recebe `codigo` (o link
        // de convite) — em C# os dois seriam a MESMA assinatura. A rota continua /Grupos/Entrar.
        [HttpPost]
        [ActionName("Entrar")]
        public async Task<IActionResult> EntrarNoGrupo(string codigo)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.CodigoConvite == codigo.Trim().ToUpper());

            if (grupo == null)
            {
                TempData["Erro"] = "Código inválido.";
                return RedirectToAction("Entrar");
            }

            var jaMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupo.Id && jg.JogadorId == userId);
            if (!jaMembro)
            {
                _context.JogadoresGrupo.Add(new JogadorGrupo { JogadorId = userId, GrupoId = grupo.Id, PontuacaoInterna = 0 });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Detalhes", new { id = grupo.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id, int? mes, int? ano)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == id && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            var grupo = await _context.GruposPrivados.Include(g => g.Clube).FirstOrDefaultAsync(g => g.Id == id);
            if (grupo == null) return NotFound();

            var ranking = await _context.JogadoresGrupo
                .Include(jg => jg.Jogador)
                .Where(jg => jg.GrupoId == id)
                .OrderByDescending(jg => jg.PontuacaoInterna)
                .ToListAsync();

            var mesConsulta = mes ?? DateTime.Today.Month;
            var anoConsulta = ano ?? DateTime.Today.Year;

            var jogosDoMes = await _context.JogosSemanais
                .Where(j => j.GrupoId == id && j.DataJogo.Month == mesConsulta && j.DataJogo.Year == anoConsulta)
                .ToListAsync();

            var pontosMes = new Dictionary<int, int>();
            foreach (var jogo in jogosDoMes)
            {
                AplicarPontos(pontosMes, jogo);
            }

            var jogosRecentes = await _context.JogosSemanais
                .Include(j => j.Dupla1Jogador1).Include(j => j.Dupla1Jogador2)
                .Include(j => j.Dupla2Jogador1).Include(j => j.Dupla2Jogador2)
                .Include(j => j.Clube)
                .Where(j => j.GrupoId == id)
                .OrderByDescending(j => j.DataJogo)
                .Take(15)
                .ToListAsync();

            ViewBag.Ranking = ranking;
            ViewBag.RankingMes = ranking
                .Select(r => new RankingMesItem { Jogador = r.Jogador, Pontos = pontosMes.GetValueOrDefault(r.JogadorId) })
                .OrderByDescending(x => x.Pontos)
                .ToList();
            ViewBag.MesConsulta = mesConsulta;
            ViewBag.AnoConsulta = anoConsulta;
            ViewBag.JogosRecentes = jogosRecentes;
            ViewBag.EhAdmin = grupo.AdministradorId == userId;

            return View(grupo);
        }

        [HttpGet]
        public async Task<IActionResult> RegistrarJogo(int grupoId)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            ViewBag.Membros = await _context.JogadoresGrupo
                .Include(jg => jg.Jogador)
                .Where(jg => jg.GrupoId == grupoId)
                .Select(jg => jg.Jogador)
                .ToListAsync();
            ViewBag.GrupoId = grupoId;

            // O local já vem escolhido no clube fixo do grupo — quem jogou fora do de sempre
            // troca ali mesmo, sem precisar mexer nas configurações da panelinha.
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.ClubeDoGrupoId = await _context.GruposPrivados
                .Where(g => g.Id == grupoId).Select(g => g.ClubeId).FirstOrDefaultAsync();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarJogo(
            int grupoId, DateTime dataJogo,
            int dupla1Jogador1Id, int dupla1Jogador2Id, int dupla2Jogador1Id, int dupla2Jogador2Id,
            int gamesDupla1, int gamesDupla2, int? clubeId)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            // Sem escolha na tela, cai no clube fixo do grupo: o jogo da panelinha é quase
            // sempre lá, e local vazio não vira ranking de clube nenhum.
            clubeId ??= await _context.GruposPrivados
                .Where(g => g.Id == grupoId).Select(g => g.ClubeId).FirstOrDefaultAsync();

            var jogo = new JogoSemanal
            {
                GrupoId = grupoId,
                DataJogo = dataJogo,
                ClubeId = clubeId,
                Dupla1Jogador1Id = dupla1Jogador1Id,
                Dupla1Jogador2Id = dupla1Jogador2Id,
                Dupla2Jogador1Id = dupla2Jogador1Id,
                Dupla2Jogador2Id = dupla2Jogador2Id,
                GamesDupla1 = gamesDupla1,
                GamesDupla2 = gamesDupla2,
                RegistradoPorId = userId
            };
            _context.JogosSemanais.Add(jogo);
            await _context.SaveChangesAsync();

            var pontos = new Dictionary<int, int>();
            AplicarPontos(pontos, jogo);
            foreach (var (jogadorId, pts) in pontos)
            {
                var registro = await _context.JogadoresGrupo.FirstOrDefaultAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == jogadorId);
                if (registro != null)
                {
                    registro.PontuacaoInterna += pts;
                }
            }
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Jogo registrado! Ranking atualizado.";
            return RedirectToAction("Detalhes", new { id = grupoId });
        }

        // ===================== JOGO DA SEMANA (roster/RSVP do horário fixo) =====================

        [HttpGet]
        public async Task<IActionResult> Semana(int grupoId, DateTime? data)
        {
            var userId = ObterUserId();

            var grupo = await _context.GruposPrivados
                .Include(g => g.Clube)
                .Include(g => g.CategoriaPadrao)
                .FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo == null) return NotFound();

            if (grupo.DiaSemanaFixo == null || grupo.HorarioFixo == null)
            {
                if (grupo.AdministradorId == userId)
                {
                    TempData["Erro"] = "Configure o dia e horário fixo do grupo antes de usar a tela da semana.";
                    return RedirectToAction("Configuracoes", new { id = grupoId });
                }
                return RedirectToAction("Detalhes", new { id = grupoId });
            }

            var sessao = await _sessaoGrupoService.ObterOuCriarSessaoAsync(grupo, data);

            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            var souConvidado = sessao.Confirmacoes.Any(c => c.JogadorId == userId);
            if (!souMembro && !souConvidado) return RedirectToAction("Index");

            var mensalidades = await _context.MensalidadesGrupo
                .Where(m => m.GrupoId == grupoId && m.Ano == sessao.DataHora.Year && m.Mes == sessao.DataHora.Month)
                .ToListAsync();

            var ranking = await _context.JogadoresGrupo
                .Include(jg => jg.Jogador)
                .Where(jg => jg.GrupoId == grupoId)
                .OrderByDescending(jg => jg.PontuacaoInterna)
                .ToListAsync();

            // DataJogo vem de um campo de data pura (meia-noite) — compara só a parte de data. Janela
            // vai do dia da sessão anterior (exclusivo — pertence à semana passada) até o dia desta
            // sessão (inclusivo), acompanhando a mesma cadência de 7 em 7 dias do jogo fixo.
            var fimSemana = sessao.DataHora.Date;
            var inicioSemana = fimSemana.AddDays(-7);
            var jogosDaSemana = await _context.JogosSemanais
                .Where(j => j.GrupoId == grupoId && j.DataJogo.Date > inicioSemana && j.DataJogo.Date <= fimSemana)
                .ToListAsync();
            var pontosSemana = new Dictionary<int, int>();
            foreach (var jogo in jogosDaSemana) AplicarPontos(pontosSemana, jogo);

            ViewBag.Grupo = grupo;
            ViewBag.EhAdmin = grupo.AdministradorId == userId;
            ViewBag.SouMembro = souMembro;
            ViewBag.Confirmados = sessao.Confirmacoes.Where(c => c.Status == "Confirmado").OrderBy(c => c.Jogador.Nome).ToList();
            ViewBag.NaoVao = sessao.Confirmacoes.Where(c => c.Status == "NaoVai").OrderBy(c => c.Jogador.Nome).ToList();
            ViewBag.Pendentes = sessao.Confirmacoes.Where(c => c.Status == "Pendente").OrderBy(c => c.Jogador.Nome).ToList();
            ViewBag.MinhaConfirmacao = sessao.Confirmacoes.FirstOrDefault(c => c.JogadorId == userId);
            ViewBag.Mensalidades = mensalidades;
            ViewBag.Ranking = ranking;
            ViewBag.RankingSemana = ranking
                .Select(r => new RankingMesItem { Jogador = r.Jogador, Pontos = pontosSemana.GetValueOrDefault(r.JogadorId) })
                .Where(x => x.Pontos > 0)
                .OrderByDescending(x => x.Pontos)
                .ToList();
            ViewBag.SemanaAnterior = sessao.DataHora.AddDays(-7);
            ViewBag.ProximaSemana = sessao.DataHora.AddDays(7);

            return View(sessao);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmarPresenca(int sessaoId, bool vou, string? lado)
        {
            var userId = ObterUserId();
            var sessao = await _context.SessoesGrupo.FirstOrDefaultAsync(s => s.Id == sessaoId);
            if (sessao == null) return NotFound();

            var confirmacao = await _context.ConfirmacoesSessao
                .FirstOrDefaultAsync(c => c.SessaoId == sessaoId && c.JogadorId == userId);

            if (confirmacao == null)
            {
                var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == sessao.GrupoId && jg.JogadorId == userId);
                if (!souMembro) return Forbid();

                var jogador = await _context.Jogadores.FindAsync(userId);
                confirmacao = new ConfirmacaoSessao { SessaoId = sessaoId, JogadorId = userId, Avulso = false, Lado = jogador?.LadoQuadra };
                _context.ConfirmacoesSessao.Add(confirmacao);
            }

            confirmacao.Status = vou ? "Confirmado" : "NaoVai";
            if (!string.IsNullOrWhiteSpace(lado)) confirmacao.Lado = lado;
            confirmacao.RespondidoEm = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = vou ? "Presença confirmada!" : "Ok, marcamos que você não vai dessa vez.";
            return RedirectToAction("Semana", new { grupoId = sessao.GrupoId, data = sessao.DataHora.ToString("s") });
        }

        // ===================== CONVIDAR JOGADORES DE FORA (link wa.me manual) =====================

        [HttpGet]
        public async Task<IActionResult> Convidar(int grupoId, DateTime? data)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.Include(g => g.Clube).FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo == null || grupo.AdministradorId != userId) return RedirectToAction("Index");

            if (grupo.ClubeId == null || grupo.CategoriaPadraoId == null || grupo.DiaSemanaFixo == null || grupo.HorarioFixo == null)
            {
                TempData["Erro"] = "Configure clube, categoria, dia e horário do grupo antes de convidar jogadores.";
                return RedirectToAction("Configuracoes", new { id = grupoId });
            }

            var sessao = await _sessaoGrupoService.ObterOuCriarSessaoAsync(grupo, data);

            var idsJaEnvolvidos = sessao.Confirmacoes.Select(c => c.JogadorId)
                .Append(grupo.AdministradorId)
                .ToList();

            var periodo = ObterPeriodo(sessao.DataHora);
            var diaSemana = (int)sessao.DataHora.DayOfWeek;

            var elegiveis = await _context.Jogadores
                .Where(j => !idsJaEnvolvidos.Contains(j.Id) && j.AceitaConvitesJogo && !string.IsNullOrEmpty(j.Celular))
                .Where(j => !_context.JogadorCategorias.Any(c => c.JogadorId == j.Id)
                         || _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == grupo.CategoriaPadraoId))
                .Where(j => !_context.JogadorClubes.Any(c => c.JogadorId == j.Id)
                         || _context.JogadorClubes.Any(c => c.JogadorId == j.Id && c.ClubeId == grupo.ClubeId))
                .Where(j => !_context.JogadorDiasHorarios.Any(d => d.JogadorId == j.Id)
                         || _context.JogadorDiasHorarios.Any(d => d.JogadorId == j.Id && d.DiaSemana == diaSemana && d.Periodo == periodo))
                .OrderBy(j => j.Nome)
                .ToListAsync();

            ViewBag.Grupo = grupo;
            ViewBag.SessaoId = sessao.Id;
            ViewBag.DataSessao = sessao.DataHora;

            return View(elegiveis);
        }

        [HttpPost]
        public async Task<IActionResult> ConvidarJogador(int sessaoId, int jogadorId)
        {
            var userId = ObterUserId();
            var sessao = await _context.SessoesGrupo
                .Include(s => s.Grupo).ThenInclude(g => g.Clube)
                .FirstOrDefaultAsync(s => s.Id == sessaoId);
            if (sessao == null || sessao.Grupo.AdministradorId != userId) return RedirectToAction("Index");

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            var confirmacao = await _context.ConfirmacoesSessao
                .FirstOrDefaultAsync(c => c.SessaoId == sessaoId && c.JogadorId == jogadorId);

            if (confirmacao == null)
            {
                _context.ConfirmacoesSessao.Add(new ConfirmacaoSessao
                {
                    SessaoId = sessaoId,
                    JogadorId = jogadorId,
                    Status = "Pendente",
                    Lado = jogador.LadoQuadra,
                    Avulso = true
                });
                await _context.SaveChangesAsync();
            }

            var grupo = sessao.Grupo;
            var infoValor = grupo.ValorAvulso.HasValue ? $" (R$ {grupo.ValorAvulso.Value:0.00} a diária)" : "";
            var mensagem = $"Oi {jogador.Nome}! Tô te chamando pro nosso jogo fixo em {(grupo.Clube?.Nome ?? "nosso clube")} " +
                           $"dia {sessao.DataHora:dd/MM 'às' HH:mm}{infoValor}. Bora?";

            // Push além do WhatsApp: quem tem o app instalado recebe o convite na hora e cai
            // direto na tela de responder. O WhatsApp continua sendo o canal principal.
            try
            {
                // ⚠️ SEM E-MAIL desde 09/08/2026: o WhatsApp já é o canal principal deste
                // convite (o texto acima é o que vai por lá), e o e-mail era uma terceira via
                // pra mesma frase, multiplicada pelo tamanho da panelinha.
                await _pushService.EnviarParaJogadorAsync(jogadorId,
                    "Te chamaram pra jogar!",
                    $"{grupo.Nome} · {sessao.DataHora:dd/MM 'às' HH:mm}{(grupo.Clube != null ? $" em {grupo.Clube.Nome}" : "")}",
                    Url.Action("Index", "Grupos"), AlcanceDoAviso.AppSemEmail);
            }
            catch (Exception ex)
            {
                // Push é acessório — falhar aqui não pode impedir o convite pelo WhatsApp.
                _logger.LogWarning(ex, "Falha ao enviar push de convite pro jogador {JogadorId}.", jogadorId);
            }

            TempData["WhatsAppLink"] = WhatsAppLinkHelper.GerarLink(jogador.Celular, mensagem);
            TempData["WhatsAppNome"] = jogador.Nome;

            return RedirectToAction("Convidar", new { grupoId = grupo.Id, data = sessao.DataHora.ToString("s") });
        }

        // ===================== CONFIGURAÇÕES DO GRUPO =====================

        [HttpGet]
        public async Task<IActionResult> Configuracoes(int id)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo == null || grupo.AdministradorId != userId) return RedirectToAction("Index");

            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();

            return View(grupo);
        }

        [HttpPost]
        public async Task<IActionResult> Configuracoes(
            int id, int? clubeId, int? categoriaPadraoId, int? diaSemanaFixo, string? horarioFixo,
            decimal? valorMensalidade, decimal? valorAvulso, int vagasMaximas, bool enviarLembrete24h)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo == null || grupo.AdministradorId != userId) return RedirectToAction("Index");

            grupo.ClubeId = clubeId;
            grupo.CategoriaPadraoId = categoriaPadraoId;
            grupo.DiaSemanaFixo = diaSemanaFixo;
            grupo.HorarioFixo = TimeSpan.TryParse(horarioFixo, out var horario) ? horario : null;
            grupo.ValorMensalidade = valorMensalidade;
            grupo.ValorAvulso = valorAvulso;
            grupo.VagasMaximas = vagasMaximas <= 0 ? 4 : vagasMaximas;
            grupo.EnviarLembrete24h = enviarLembrete24h;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Configurações do grupo atualizadas.";

            if (grupo.DiaSemanaFixo != null && grupo.HorarioFixo != null)
                return RedirectToAction("Semana", new { grupoId = id });

            return RedirectToAction("Detalhes", new { id });
        }

        // ===================== MENSALIDADE =====================

        [HttpPost]
        public async Task<IActionResult> MarcarPagamento(int grupoId, int jogadorId, int ano, int mes, bool pago, DateTime? data)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo == null || grupo.AdministradorId != userId) return RedirectToAction("Index");

            var mensalidade = await _context.MensalidadesGrupo
                .FirstOrDefaultAsync(m => m.GrupoId == grupoId && m.JogadorId == jogadorId && m.Ano == ano && m.Mes == mes);

            if (mensalidade == null)
            {
                mensalidade = new MensalidadeGrupo { GrupoId = grupoId, JogadorId = jogadorId, Ano = ano, Mes = mes };
                _context.MensalidadesGrupo.Add(mensalidade);
            }

            mensalidade.Pago = pago;
            mensalidade.DataPagamento = pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            return RedirectToAction("Semana", new { grupoId, data = data?.ToString("s") });
        }

        // Vitória = 3 pts, derrota = 1 pt (participação), empate = 2 pts pra cada lado.
        private static void AplicarPontos(Dictionary<int, int> pontos, JogoSemanal jogo)
        {
            int pontosDupla1, pontosDupla2;
            if (jogo.VencedorLado == 1) { pontosDupla1 = 3; pontosDupla2 = 1; }
            else if (jogo.VencedorLado == 2) { pontosDupla1 = 1; pontosDupla2 = 3; }
            else { pontosDupla1 = 2; pontosDupla2 = 2; }

            Somar(pontos, jogo.Dupla1Jogador1Id, pontosDupla1);
            Somar(pontos, jogo.Dupla1Jogador2Id, pontosDupla1);
            Somar(pontos, jogo.Dupla2Jogador1Id, pontosDupla2);
            Somar(pontos, jogo.Dupla2Jogador2Id, pontosDupla2);
        }

        private static void Somar(Dictionary<int, int> dict, int id, int pts)
        {
            dict[id] = dict.GetValueOrDefault(id) + pts;
        }

        private int ObterUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<string> GerarCodigoUnicoAsync()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // sem caracteres ambíguos (O/0, I/1)
            var rnd = new Random();
            string codigo;
            do
            {
                codigo = new string(Enumerable.Range(0, 6).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
            }
            while (await _context.GruposPrivados.AnyAsync(g => g.CodigoConvite == codigo));

            return codigo;
        }

        private static string ObterPeriodo(DateTime dataHora)
        {
            if (dataHora.Hour < 12) return "Manhã";
            if (dataHora.Hour < 18) return "Tarde";
            return "Noite";
        }
    }

    public class RankingMesItem
    {
        public Jogador Jogador { get; set; } = null!;
        public int Pontos { get; set; }
    }
}
