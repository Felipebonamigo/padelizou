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
                // A data é só o DIA, e uma panelinha lança 5 jogos na mesma noite: ordenar só
                // por ela deixa o desempate com o banco, e a ordem das linhas muda de um F5
                // pro outro. O Id cresce com o lançamento, então ele é o critério que fecha.
                .OrderByDescending(j => j.DataJogo)
                .ThenByDescending(j => j.Id)
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
            // A TELA NÃO REPETE A REGRA. Quem responde quem pode corrigir é o mesmo
            // `PodeMexerNoJogo` que guarda o POST — se a condição vivesse também na view, uma
            // das duas cópias mudaria sozinha e o botão apareceria pra quem o servidor recusa.
            ViewBag.JogosQuePodeMexer = jogosRecentes
                .Where(j => PodeMexerNoJogo(grupo, j, userId))
                .Select(j => j.Id)
                .ToHashSet();
            ViewBag.EhAdmin = grupo.AdministradorId == userId;

            return View(grupo);
        }

        [HttpGet]
        public async Task<IActionResult> RegistrarJogo(int grupoId, DateTime? data)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            ViewBag.Membros = await ParticipantesParaResultadoAsync(grupoId);
            ViewBag.Convidados = await IdsDeConvidadosAsync(grupoId);
            ViewBag.GrupoId = grupoId;

            // Quem chega pela tela da Semana já está OLHANDO uma data — o formulário abre nela.
            // Sem isso ele abre sempre em "hoje", e quem lança na quarta o jogo de terça grava o
            // dia errado. ⚠️ Isso não é cosmético: o ranking DA SEMANA é fatiado por data
            // (`DataJogo > início && <= fim`), então um dia a mais joga a partida pra semana
            // seguinte — ela some do quadro sem erro nenhum, e some do lugar certo também.
            ViewBag.DataSugerida = (data ?? DateTime.Today).Date;

            // Guardado pra devolver a pessoa de onde ela veio: quem clicou na tela da semana
            // não quer ser cuspido no ranking geral depois de salvar.
            ViewBag.VoltarParaSemana = data;

            // O local já vem escolhido no clube fixo do grupo — quem jogou fora do de sempre
            // troca ali mesmo, sem precisar mexer nas configurações da panelinha.
            ViewBag.CatalogoClubes = await _context.Clubes.ParaEscolher().ToListAsync();
            // ⚠️ SÓ O ID. O nome do clube ia junto pra escrever a linha de resumo no servidor,
            // e era ele que congelava ali enquanto o seletor abaixo já mostrava outro local —
            // a mesma contradição das duas datas. Quem desenha o resumo agora é o JS, lendo a
            // opção escolhida no próprio seletor, que é o que vai ser gravado.
            ViewBag.ClubeDoGrupoId = await _context.GruposPrivados
                .Where(g => g.Id == grupoId).Select(g => g.ClubeId).FirstOrDefaultAsync();

            // QUEM CONFIRMOU naquela data — só pra DESTACAR, nunca pra filtrar.
            //
            // ⚠️ A distinção é o pedido de um usuário e está certa: quem jogou e esqueceu de
            // confirmar no app tem que aparecer na lista igual, senão o jogo não consegue ser
            // lançado por causa de um RSVP. A lista de escolha continua sendo todo mundo
            // (ParticipantesParaResultadoAsync); confirmar só empurra pra cima.
            var diaDoJogo = ViewBag.DataSugerida as DateTime? ?? DateTime.Today;
            ViewBag.Confirmados = (await _context.ConfirmacoesSessao
                .Where(c => c.Sessao.GrupoId == grupoId
                         && c.Sessao.DataHora.Date == diaDoJogo.Date
                         && c.Status == "Confirmado")
                .Select(c => c.JogadorId)
                .ToListAsync())
                .ToHashSet();

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarJogo(
            int grupoId, DateTime dataJogo,
            int dupla1Jogador1Id, int dupla1Jogador2Id, int dupla2Jogador1Id, int dupla2Jogador2Id,
            int vencedorLado, int? gamesDupla1, int? gamesDupla2, int? clubeId, DateTime? voltarParaSemana = null)
        {
            var userId = ObterUserId();
            var souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            // O vencedor é o fato; o placar é detalhe opcional. A régua é a mesma do editar —
            // ver Services/ResultadoDoJogoSemanal, e o porquê de ela existir separada.
            if (ResultadoDoJogoSemanal.MotivoParaNaoSalvar(vencedorLado, gamesDupla1, gamesDupla2) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("RegistrarJogo", new { grupoId, data = dataJogo });
            }

            // O SENTINELA DO FORMULÁRIO VIRA NULO AQUI, e só aqui. Ver Services/ConvidadoNoJogo:
            // -1 é "convidado sem nome"; o ZERO que o binder produz pra campo faltando continua
            // sendo zero, e continua caindo no porteiro logo abaixo.
            var escolhidos = new[] { dupla1Jogador1Id, dupla1Jogador2Id, dupla2Jogador1Id, dupla2Jogador2Id }
                .Select(ConvidadoNoJogo.DoFio)
                .ToArray();

            if (ConvidadoNoJogo.MotivoParaNaoSalvar(escolhidos[0], escolhidos[1], escolhidos[2], escolhidos[3]) is { } convidadosDemais)
            {
                TempData["Erro"] = convidadosDemais;
                return RedirectToAction("RegistrarJogo", new { grupoId, data = voltarParaSemana });
            }

            // A LISTA DA TELA NÃO É A TRAVA. Ela some de vista, o POST não: sem conferir aqui,
            // um id qualquer entraria no placar do grupo — e no recálculo do ranking — por um
            // formulário montado à mão.
            //
            // ⚠️ Nulo não se confere contra lista nenhuma — é a vaga de quem o sistema não
            // conhece, e conferir "o convidado é do grupo?" não quer dizer nada. Quem TEM id
            // continua tendo que ser membro ou convidado, e o zero do binder continua sendo
            // recusado aqui: é o que separa "veio um convidado" de "o formulário chegou pela
            // metade".
            var podemJogar = (await ParticipantesParaResultadoAsync(grupoId)).Select(j => j.Id).ToHashSet();
            if (escolhidos.Any(id => id != null && !podemJogar.Contains(id.Value)))
            {
                TempData["Erro"] = "Um dos jogadores escolhidos não é da panelinha nem foi convidado pra um jogo dela.";
                // A recusa devolve o formulário no MESMO contexto — voltar pro "hoje" faria a
                // pessoa perder a data que ela estava lançando junto com o erro.
                return RedirectToAction("RegistrarJogo", new { grupoId, data = voltarParaSemana });
            }

            // Sem escolha na tela, cai no clube fixo do grupo: o jogo da panelinha é quase
            // sempre lá, e local vazio não vira ranking de clube nenhum.
            clubeId ??= await _context.GruposPrivados
                .Where(g => g.Id == grupoId).Select(g => g.ClubeId).FirstOrDefaultAsync();

            var jogo = new JogoSemanal
            {
                GrupoId = grupoId,
                DataJogo = dataJogo,
                ClubeId = clubeId,
                // ⚠️ `escolhidos`, NÃO os parâmetros crus: estes ainda valem -1 pro convidado, e
                // `int` cabe em `int?` por conversão implícita — o compilador não avisaria nada
                // e o banco receberia -1, que viola a FK. Fail-closed, mas caro de descobrir.
                Dupla1Jogador1Id = escolhidos[0],
                Dupla1Jogador2Id = escolhidos[1],
                Dupla2Jogador1Id = escolhidos[2],
                Dupla2Jogador2Id = escolhidos[3],
                RegistradoPorId = userId
            };
            ResultadoDoJogoSemanal.Aplicar(jogo, vencedorLado, gamesDupla1, gamesDupla2);
            _context.JogosSemanais.Add(jogo);
            await _context.SaveChangesAsync();

            await RecalcularPontuacaoAsync(grupoId);

            TempData["Sucesso"] = "Jogo registrado! Ranking atualizado.";

            // Volta pra onde a pessoa estava. Quem clicou na tela da semana quer ver o ranking
            // DAQUELA semana mudar — ser cuspido no ranking geral esconde justamente o efeito
            // do que ela acabou de fazer.
            return voltarParaSemana != null
                ? RedirectToAction("Semana", new { grupoId, data = voltarParaSemana.Value.ToString("s") })
                : RedirectToAction("Detalhes", new { id = grupoId });
        }

        // ===================== CORRIGIR UM JOGO JÁ LANÇADO =====================

        [HttpGet]
        public async Task<IActionResult> EditarJogo(int id)
        {
            var userId = ObterUserId();

            var jogo = await _context.JogosSemanais.FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == jogo.GrupoId);
            if (grupo == null) return NotFound();
            if (!PodeMexerNoJogo(grupo, jogo, userId)) return RedirectToAction("Detalhes", new { id = jogo.GrupoId });

            var membros = await ParticipantesParaResultadoAsync(jogo.GrupoId);

            // ⚠️ QUEM JOGOU MAS NÃO É MAIS MEMBRO PRECISA ESTAR NA LISTA. Sem isso o <select>
            // abre sem a opção da pessoa, o navegador seleciona o primeiro nome da lista e
            // salvar a correção de UM PLACAR trocaria calado quem jogou a partida.
            //
            // ⚠️ A VAGA DO CONVIDADO (nulo) SAI ANTES DA CONTA. Sem o `id != null`, o nulo entra
            // em `faltantes` e a consulta vira `WHERE Id IN (…, NULL)` — que no Postgres não casa
            // nada e não dá erro: some calada, e ninguém descobre pelo teste (o InMemory nem faz
            // SQL). Convidado não tem linha em Jogador pra buscar; é essa a questão.
            var idsNoJogo = new[] { jogo.Dupla1Jogador1Id, jogo.Dupla1Jogador2Id, jogo.Dupla2Jogador1Id, jogo.Dupla2Jogador2Id };
            var faltantes = idsNoJogo
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Where(id => membros.All(m => m.Id != id))
                .Distinct()
                .ToList();
            if (faltantes.Count > 0)
            {
                membros.AddRange(await _context.Jogadores.Where(j => faltantes.Contains(j.Id)).ToListAsync());
            }

            ViewBag.Membros = membros.OrderBy(m => m.Nome).ToList();
            ViewBag.Convidados = await IdsDeConvidadosAsync(jogo.GrupoId);
            ViewBag.CatalogoClubes = await _context.Clubes.ParaEscolher().ToListAsync();
            ViewBag.NomeDoGrupo = grupo.Nome;

            return View(jogo);
        }

        [HttpPost]
        public async Task<IActionResult> EditarJogo(
            int id, DateTime dataJogo,
            int dupla1Jogador1Id, int dupla1Jogador2Id, int dupla2Jogador1Id, int dupla2Jogador2Id,
            int vencedorLado, int? gamesDupla1, int? gamesDupla2, int? clubeId)
        {
            var userId = ObterUserId();

            var jogo = await _context.JogosSemanais.FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == jogo.GrupoId);
            if (grupo == null) return NotFound();
            if (!PodeMexerNoJogo(grupo, jogo, userId)) return RedirectToAction("Detalhes", new { id = jogo.GrupoId });

            // ⚠️ A MESMA régua do registrar, e ela é obrigatória AQUI de um jeito que não era
            // antes: enquanto o vencedor saía de conta, corrigir o placar acertava o vencedor
            // de graça. Agora ele é campo — um editar que só escrevesse os games deixaria o
            // vencedor gravado apontando pro lado antigo, e o ranking seguiria o vencedor.
            if (ResultadoDoJogoSemanal.MotivoParaNaoSalvar(vencedorLado, gamesDupla1, gamesDupla2) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("EditarJogo", new { id });
            }

            // ⚠️ O TETO DE CONVIDADOS É CONFERIDO AQUI TAMBÉM, e não por simetria decorativa:
            // esta é a SEGUNDA cópia da régua. Ensinar só o registrar faria o Corrigir recusar
            // exatamente o jogo que o Registrar acabou de aceitar. O número mora num lugar só —
            // Services/ConvidadoNoJogo.
            var escolhidos = new[] { dupla1Jogador1Id, dupla1Jogador2Id, dupla2Jogador1Id, dupla2Jogador2Id }
                .Select(ConvidadoNoJogo.DoFio)
                .ToArray();

            if (ConvidadoNoJogo.MotivoParaNaoSalvar(escolhidos[0], escolhidos[1], escolhidos[2], escolhidos[3]) is { } convidadosDemais)
            {
                TempData["Erro"] = convidadosDemais;
                return RedirectToAction("EditarJogo", new { id });
            }

            // Mesma conferência do registro — mais quem JÁ ESTAVA no jogo. Sem essa segunda
            // parte, corrigir só o placar de uma partida antiga seria recusado porque um dos
            // quatro saiu da panelinha desde então.
            var podemJogar = (await ParticipantesParaResultadoAsync(jogo.GrupoId)).Select(j => j.Id).ToHashSet();
            foreach (var jaEstava in new[] { jogo.Dupla1Jogador1Id, jogo.Dupla1Jogador2Id, jogo.Dupla2Jogador1Id, jogo.Dupla2Jogador2Id })
            {
                // A vaga do convidado não vira item de lista: não há id pra liberar.
                if (jaEstava != null) podemJogar.Add(jaEstava.Value);
            }
            if (escolhidos.Any(id => id != null && !podemJogar.Contains(id.Value)))
            {
                TempData["Erro"] = "Um dos jogadores escolhidos não é da panelinha nem foi convidado pra um jogo dela.";
                return RedirectToAction("EditarJogo", new { id });
            }

            // O GRUPO DO JOGO NÃO SE MEXE. Trocar de grupo levaria os pontos junto pra outro
            // ranking, e o recálculo só passa no grupo que veio no formulário — o de origem
            // ficaria com o fantasma que este método existe pra evitar.
            jogo.DataJogo = dataJogo;
            jogo.ClubeId = clubeId;
            // `escolhidos`, e não os parâmetros crus — ver o mesmo cuidado no POST do registrar.
            jogo.Dupla1Jogador1Id = escolhidos[0];
            jogo.Dupla1Jogador2Id = escolhidos[1];
            jogo.Dupla2Jogador1Id = escolhidos[2];
            jogo.Dupla2Jogador2Id = escolhidos[3];
            ResultadoDoJogoSemanal.Aplicar(jogo, vencedorLado, gamesDupla1, gamesDupla2);
            await _context.SaveChangesAsync();

            await RecalcularPontuacaoAsync(jogo.GrupoId);

            TempData["Sucesso"] = "Jogo corrigido! Ranking refeito.";
            return RedirectToAction("Detalhes", new { id = jogo.GrupoId });
        }

        [HttpPost]
        public async Task<IActionResult> ApagarJogo(int id)
        {
            var userId = ObterUserId();

            var jogo = await _context.JogosSemanais.FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == jogo.GrupoId);
            if (grupo == null) return NotFound();
            if (!PodeMexerNoJogo(grupo, jogo, userId)) return RedirectToAction("Detalhes", new { id = jogo.GrupoId });

            var grupoId = jogo.GrupoId;
            _context.JogosSemanais.Remove(jogo);
            await _context.SaveChangesAsync();

            // Depois do Remove, senão o jogo apagado ainda entra na conta.
            await RecalcularPontuacaoAsync(grupoId);

            TempData["Sucesso"] = "Jogo apagado! Ranking refeito.";
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

            // ⚠️ A CONTA É FEITA AQUI, e não pedindo a sessão de novo, PORQUE PEDIR CRIA. Um
            // `ObterOuCriarSessaoAsync(grupo, null)` só pra saber a data faria toda visita ao
            // histórico gravar a sessão da semana corrente de lambuja — com as confirmações
            // "Pendente" de todo o grupo junto, e o lembrete de 24h em cima delas.
            //
            // É a MESMA conta que a tela faz quando ninguém pede data, e por isso o botão não
            // leva `data` nenhuma: quem responde qual é a semana atual continua sendo um só.
            var semanaAtual = SessaoGrupoService.ProximaOcorrencia(grupo.DiaSemanaFixo.Value, grupo.HorarioFixo.Value);
            ViewBag.EstouNaSemanaAtual = sessao.DataHora == semanaAtual;

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

        // ===================== SORTEAR AS DUPLAS DA SEMANA =====================
        //
        // "Para que as duplas não sejam sempre as mesmas" (Felipe, 18/08/2026). A regra e o
        // porquê de cada decisão moram em Services/SorteioDeDuplas; aqui só se junta o que o
        // motor precisa: quem confirmou, de que lado cada um joga e quem já jogou com quem.
        //
        // Só o admin do grupo sorteia. Não é hierarquia: o resultado vale pra todos, e dois
        // membros clicando alternado virariam duplas trocando a cada F5.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SortearDuplas(int sessaoId, bool respeitarLado = true)
        {
            var userId = ObterUserId();

            var sessao = await _context.SessoesGrupo
                .Include(s => s.Grupo)
                .Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
                .FirstOrDefaultAsync(s => s.Id == sessaoId);
            if (sessao == null) return NotFound();
            if (sessao.Grupo.AdministradorId != userId) return Forbid();

            var voltarPara = new { grupoId = sessao.GrupoId, data = sessao.DataHora.ToString("s") };

            // ⚠️ `LadoNaQuadra.Efetivo`, e não `c.Lado` cru: a confirmação nasce copiando o lado
            // do perfil, mas quem foi convidado de fora — ou é membro de antes de o campo
            // existir — vem com nulo. Nulo aqui não é "tanto faz": é "não escolheu nesta tela",
            // e aí vale o perfil. Mesma régua da inscrição de torneio.
            var candidatos = sessao.Confirmacoes
                .Where(c => c.Status == "Confirmado")
                .Select(c => new SorteioDeDuplas.Candidato(
                    c.JogadorId,
                    c.Jogador.ComoChamar,
                    LadoNaQuadra.Efetivo(c.Lado, c.Jogador.LadoQuadra)))
                .ToList();

            if (candidatos.Count < 2)
            {
                TempData["Erro"] = "Precisa de pelo menos 2 confirmados pra sortear as duplas.";
                return RedirectToAction("Semana", voltarPara);
            }

            // O HISTÓRICO DE PARCERIAS — é ele que faz "não repetir" querer dizer alguma coisa.
            //
            // ⚠️ Janela de 8 semanas, e não o grupo inteiro desde sempre: em panelinha antiga
            // todo mundo já jogou com todo mundo, os custos empatam e o histórico deixa de
            // separar qualquer coisa. Oito semanas é o passado que as pessoas ainda sentem
            // como "de novo esses dois juntos".
            var desde = sessao.DataHora.Date.AddDays(-56);
            var jogos = await _context.JogosSemanais
                .Where(j => j.GrupoId == sessao.GrupoId
                         && j.DataJogo.Date >= desde && j.DataJogo.Date <= sessao.DataHora.Date)
                .Select(j => new { j.Dupla1Jogador1Id, j.Dupla1Jogador2Id, j.Dupla2Jogador1Id, j.Dupla2Jogador2Id })
                .ToListAsync();

            var vezesJuntos = new Dictionary<(int, int), int>();
            foreach (var j in jogos)
            {
                foreach (var (a, b) in new[]
                {
                    (j.Dupla1Jogador1Id, j.Dupla1Jogador2Id),
                    (j.Dupla2Jogador1Id, j.Dupla2Jogador2Id),
                })
                {
                    // ⚠️ DUPLA COM CONVIDADO NÃO ENTRA NO HISTÓRICO DE PARCERIAS — descartar, e
                    // NUNCA `?? 0`. Com zero, todo par com convidado viraria (0, Fulano) e dois
                    // convidados virariam o par (0, 0): o motor de "não repetir dupla" passaria a
                    // fugir de parcerias que nunca existiram, uma vez por convidado dentro da
                    // janela de 8 semanas. E o efeito é INVISÍVEL — isto é um sorteio, ninguém
                    // consegue provar na tela que ele saiu errado.
                    if (a == null || b == null) continue;

                    var par = SorteioDeDuplas.Chave(a.Value, b.Value);
                    vezesJuntos[par] = vezesJuntos.GetValueOrDefault(par) + 1;
                }
            }

            var resultado = SorteioDeDuplas.Sortear(candidatos, vezesJuntos, Random.Shared, respeitarLado);

            // O sorteio NÃO grava nada: ele é uma sugestão pra mesa, e o organizador pode
            // clicar de novo se a quadra pedir outra coisa. Vai por TempData pra sobreviver ao
            // redirect (o POST-redirect-GET do resto do site) e sumir na navegação seguinte —
            // que é o tempo de vida certo pra um sorteio.
            TempData["SorteioDeDuplas"] = System.Text.Json.JsonSerializer.Serialize(resultado);

            return RedirectToAction("Semana", voltarPara);
        }

        // ===================== CONVIDAR JOGADORES DE FORA (link wa.me manual) =====================

        [HttpGet]
        public async Task<IActionResult> Convidar(int grupoId, DateTime? data, string? busca)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.Include(g => g.Clube).FirstOrDefaultAsync(g => g.Id == grupoId);
            if (grupo == null) return RedirectToAction("Index");

            // ⚠️ QUALQUER MEMBRO CONVIDA, não só quem administra (13/08/2026). É a mesma régua
            // do `ConvidarParaGrupo` aqui de cima: quem é DO grupo chama gente pro jogo do
            // grupo. Faltar um pro quarteto às 19h de terça é problema de quem vai jogar, e
            // depender do administrador aparecer é exatamente como o jogo não acontece.
            bool souMembro = await _context.JogadoresGrupo.AnyAsync(jg => jg.GrupoId == grupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            if (grupo.ClubeId == null || grupo.CategoriaPadraoId == null || grupo.DiaSemanaFixo == null || grupo.HorarioFixo == null)
            {
                // Membro comum não abre as Configurações: mandá-lo pra lá seria empurrá-lo
                // contra uma porta trancada, com a tarefa na mão de quem não pode cumpri-la.
                bool euConfiguro = grupo.AdministradorId == userId;
                TempData["Erro"] = euConfiguro
                    ? "Configure clube, categoria, dia e horário do grupo antes de convidar jogadores."
                    : "Esta panelinha ainda não tem clube, categoria e dia/horário definidos — quem administra precisa fazer isso antes de sair convite.";
                return euConfiguro
                    ? RedirectToAction("Configuracoes", new { id = grupoId })
                    : RedirectToAction("Detalhes", new { id = grupoId });
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

            // ⚠️ A BUSCA POR CPF/LOGIN É EXATA, E ISSO É A REGRA, NÃO PREGUIÇA. Um "começa
            // com" viraria caça-níquel de CPF: digitar 111 e ir vendo nomes de gente real
            // aparecer é varredura da base inteira, uma tecla por vez. Assim só acha quem já
            // sabe o CPF ou o login INTEIRO de alguém — que é o caso de quem está com a
            // pessoa do lado tentando fechar o quarteto.
            //
            // 🔒 E O CPF NÃO VAI PRA TELA — nem no texto, nem em atributo escondido. O filtro
            // que roda no navegador enxerga só nome e login, que já estão à vista de todos.
            var procurado = (busca ?? "").Trim();
            if (procurado.Length > 0)
            {
                var soDigitos = new string(procurado.Where(char.IsDigit).ToArray());
                var achado = await _context.Jogadores.FirstOrDefaultAsync(j =>
                    (j.Login == procurado || j.Cpf == procurado || (soDigitos.Length == 11 && j.Cpf == soDigitos))
                    && !idsJaEnvolvidos.Contains(j.Id)
                    // Quem desligou convites não é achável por aqui: a chave é dele, e um
                    // caminho lateral que a ignora esvazia a chave.
                    && j.AceitaConvitesJogo);

                if (achado != null && elegiveis.All(e => e.Id != achado.Id))
                {
                    // Entra no topo mesmo furando os filtros de categoria/clube/horário: os
                    // filtros existem pra SUGERIR bem, e quem digitou o CPF inteiro não está
                    // pedindo sugestão — está apontando pra uma pessoa específica.
                    elegiveis.Insert(0, achado);
                    ViewBag.AchadoPelaBusca = achado.Id;
                }
                else if (achado == null)
                {
                    ViewBag.NaoAcheiPeloIdentificador = procurado;
                }
            }

            ViewBag.Grupo = grupo;
            ViewBag.SessaoId = sessao.Id;
            ViewBag.DataSessao = sessao.DataHora;
            ViewBag.Busca = procurado;
            ViewBag.SouOAdministrador = grupo.AdministradorId == userId;

            return View(elegiveis);
        }

        [HttpPost]
        public async Task<IActionResult> ConvidarJogador(int sessaoId, int jogadorId)
        {
            var userId = ObterUserId();
            var sessao = await _context.SessoesGrupo
                .Include(s => s.Grupo).ThenInclude(g => g.Clube)
                .FirstOrDefaultAsync(s => s.Id == sessaoId);
            if (sessao == null) return RedirectToAction("Index");

            // Mesma abertura do GET: convida quem é do grupo, não só quem administra.
            bool souMembro = await _context.JogadoresGrupo
                .AnyAsync(jg => jg.GrupoId == sessao.GrupoId && jg.JogadorId == userId);
            if (!souMembro) return RedirectToAction("Index");

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            // A trava mora AQUI, não só na lista da tela: a lista some da vista, o POST não.
            if (!jogador.AceitaConvitesJogo)
            {
                TempData["Erro"] = $"{jogador.ComoChamar} desligou os convites pra jogo no perfil dele.";
                return RedirectToAction("Convidar", new { grupoId = sessao.GrupoId, data = sessao.DataHora.ToString("s") });
            }

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

            // ⚠️ O CONVITE JÁ ESTÁ GRAVADO A ESTA ALTURA — o WhatsApp é o recado, não o
            // convite. Quem foi achado pelo CPF pode não ter celular cadastrado, e montar um
            // wa.me com número vazio abriria uma conversa com ninguém: parece que o recado
            // foi, e não foi. Sem número, a pessoa já está na lista de "Aguardando resposta"
            // e recebe pelo app.
            if (WhatsAppLinkHelper.NumeroValido(jogador.Celular))
            {
                TempData["WhatsAppLink"] = WhatsAppLinkHelper.GerarLink(jogador.Celular, mensagem);
                TempData["WhatsAppNome"] = jogador.Nome;
            }
            else
            {
                TempData["Sucesso"] = $"{jogador.ComoChamar} entrou na lista de convidados e recebeu o aviso pelo app. "
                                    + "Ele não tem WhatsApp cadastrado aqui, então o recado por lá fica com você.";
            }

            return RedirectToAction("Convidar", new { grupoId = grupo.Id, data = sessao.DataHora.ToString("s") });
        }

        // ===================== CONFIGURAÇÕES DO GRUPO =====================

        [HttpGet]
        public async Task<IActionResult> Configuracoes(int id)
        {
            var userId = ObterUserId();
            var grupo = await _context.GruposPrivados.FirstOrDefaultAsync(g => g.Id == id);
            if (grupo == null || grupo.AdministradorId != userId) return RedirectToAction("Index");

            ViewBag.CatalogoClubes = await _context.Clubes.ParaEscolher().ToListAsync();
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
        // A conta em si mora em Services/PontuacaoDaPanelinha — é a MESMA usada pra refazer o
        // ranking gravado, e ter uma cópia aqui é como o placar geral se descolaria da lista.
        // Quem pode aparecer num placar da panelinha: os membros MAIS os convidados de
        // qualquer sessão do grupo. Uma pergunta, um lugar — a lista de Registrar, a de
        // Corrigir e as duas conferências de POST bebem daqui.
        //
        // ⚠️ CONVIDADO ENTRA SEM TER ACEITADO O CONVITE (13/08/2026, a pedido do Felipe).
        // O placar é lançado DEPOIS do jogo, e a essa altura o "aceitar" não decide mais
        // nada: a pessoa já jogou. Exigir o aceite deixaria o resultado da noite preso a um
        // botão que ninguém mais vai apertar — e o placar simplesmente não seria lançado.
        //
        // ⚠️ E O CONVIDADO NÃO ENTRA NO RANKING DO GRUPO, o que é de propósito e não esquecimento:
        // `RecalcularPontuacaoAsync` escreve em `JogadorGrupo`, que só existe pra membro. Os
        // pontos DELE se perdem; os dos outros três saem certos. Convidado é quem tapa buraco
        // numa noite — subir no ranking interno por isso passaria na frente de mensalista.
        //
        // ⚠️ DESDE 20/08/2026 EXISTEM DOIS SENTIDOS DE "CONVIDADO" NESTA TELA, e confundi-los é
        // fácil: (1) o AVULSO COM CONTA, que é o desta lista — `ConfirmacaoSessao.Avulso`,
        // aparece nos botões, é rastreável, tem perfil; e (2) a VAGA SEM NOME, que é NULO no
        // JogoSemanal e não aparece em lista nenhuma, porque não é ninguém. Este método responde
        // só pelo primeiro. O segundo mora em Services/ConvidadoNoJogo e não passa por aqui.
        private async Task<List<Jogador>> ParticipantesParaResultadoAsync(int grupoId)
        {
            var membros = await _context.JogadoresGrupo
                .Where(jg => jg.GrupoId == grupoId)
                .Select(jg => jg.Jogador)
                .ToListAsync();

            var convidados = await _context.ConfirmacoesSessao
                .Where(c => c.Sessao.GrupoId == grupoId && c.Avulso)
                .Select(c => c.Jogador)
                .ToListAsync();

            return membros.Concat(convidados)
                .GroupBy(j => j.Id)
                .Select(g => g.First())
                .OrderBy(j => j.Nome)
                .ToList();
        }

        // Os ids que vieram como convidado, pra tela poder marcá-los sem repetir a consulta.
        private async Task<HashSet<int>> IdsDeConvidadosAsync(int grupoId) =>
            (await _context.ConfirmacoesSessao
                .Where(c => c.Sessao.GrupoId == grupoId && c.Avulso)
                .Select(c => c.JogadorId)
                .ToListAsync())
            .ToHashSet();

        private static void AplicarPontos(Dictionary<int, int> pontos, JogoSemanal jogo) =>
            PontuacaoDaPanelinha.Aplicar(pontos, jogo);

        // Refaz `JogadorGrupo.PontuacaoInterna` do grupo inteiro a partir dos jogos que existem
        // AGORA. Chamado por registrar, editar e apagar — os três, sempre.
        //
        // ⚠️ É REFAZER, não somar a diferença. Somar a diferença exige que o total de antes
        // estivesse certo; refazer não exige nada e ainda conserta o que já estava torto. E o
        // zero é obrigatório: quem saiu de todos os jogos do grupo precisa CAIR pra 0, e um
        // laço que só escreve quem aparece no dicionário deixaria o número velho parado.
        private async Task RecalcularPontuacaoAsync(int grupoId)
        {
            var jogos = await _context.JogosSemanais.Where(j => j.GrupoId == grupoId).ToListAsync();
            var totais = PontuacaoDaPanelinha.Totais(jogos);

            var membros = await _context.JogadoresGrupo.Where(jg => jg.GrupoId == grupoId).ToListAsync();
            foreach (var membro in membros)
            {
                membro.PontuacaoInterna = totais.GetValueOrDefault(membro.JogadorId);
            }
            await _context.SaveChangesAsync();
        }

        // Quem pode mexer num jogo já lançado: quem administra a panelinha, quem registrou
        // aquele jogo e OS QUATRO QUE ESTAVAM NA QUADRA. O placar errado quase sempre é
        // digitação de quem lançou, e obrigar a chamar o administrador pra corrigir um 6x4
        // faria a correção não acontecer.
        //
        // ⚠️ Quem jogou entra na regra porque a versão estreita (só admin + quem lançou) foi
        // pro ar e travou na hora: numa panelinha de verdade o jogo é lançado por quem pegou
        // o celular primeiro, e o erro fica preso até essa pessoa aparecer. Quem estava na
        // partida sabe o placar, e o ranking é interno ao grupo — o administrador desfaz.
        //
        // O administrador do sistema entra como chave-mestra, igual ao resto do app
        // (`PodeEditarTudo` vira a credencial `IsAdmin`; ver Services/PoderesNoSistema).
        //
        // ⚠️ NÃO MUDA COM O CONVIDADO SEM NOME (20/08/2026), e não é esquecimento: `int? == int`
        // dá `false` no nulo, que é o certo — convidado não tem conta pra ser `userId`. E o jogo
        // nunca fica órfão de quem pode corrigi-lo: o teto de 2 convidados
        // (Services/ConvidadoNoJogo) garante SEMPRE dois identificados na quadra, mais quem
        // registrou (`RegistradoPorId` continua obrigatório e é sempre membro) e o administrador.
        private bool PodeMexerNoJogo(GrupoPrivado grupo, JogoSemanal jogo, int userId) =>
            grupo.AdministradorId == userId
            || jogo.RegistradoPorId == userId
            || jogo.Dupla1Jogador1Id == userId || jogo.Dupla1Jogador2Id == userId
            || jogo.Dupla2Jogador1Id == userId || jogo.Dupla2Jogador2Id == userId
            || User.FindFirstValue("IsAdmin") == "true";

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
