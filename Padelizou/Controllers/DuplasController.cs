using Microsoft.AspNetCore.Authorization;
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
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly ILogger<DuplasController> _logger;

        public DuplasController(DbPadelContext context, IEstatisticasService estatisticas,
            IEmailService emailService, IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, ILogger<DuplasController> logger)
        {
            _context = context;
            _estatisticas = estatisticas;
            _emailService = emailService;
            _pushService = pushService;
            _pagamentos = pagamentos;
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

        // Recebe os dados do formulário de inscrição em dupla, que vive em
        // Views/Torneios/Details.cshtml (não há GET aqui: /Duplas/Create sozinho
        // não teria o torneioId e a inscrição falharia).
        // Quem INSCREVE precisa estar logado; o PARCEIRO não precisa ter conta. Antes a
        // inscrição era aberta a qualquer visitante que soubesse a senha do portão, e o portão
        // não identifica ninguém — dava pra criar cadastro com CPF de terceiro sem deixar
        // rastro de quem fez. Agora existe autor: é dele o aviso "Fulano inscreveu você" e é
        // ele quem responde pelo que digitou.
        //
        // O parceiro continua entrando como PRÉ-CADASTRO (Jogador sem senha, achado por CPF).
        // Quando ele se cadastrar depois, o próprio CPF reencontra esta linha e ele assume a
        // conta com o histórico junto — ver AuthController.Cadastro.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(
            int torneioId, int categoriaId,
            string nome1, string cpf1, string? celular1, string? cidade1, string? estado1,
            string? nome2, string? cpf2, string? celular2, string? cidade2, string? estado2,
            bool impSextaNoite, bool impSabadoManha, bool impSabadoTarde,
            bool semParceiro = false, bool ignorarBloqueio = false, string? chaveAcesso = null,
            // Forma que o jogador declarou no checkout. Só é perguntada quando o organizador
            // abriu todas as formas — é ela que decide a taxa (ver CobrancaDoTorneio).
            string? formaPagamentoEscolhida = null)
        {
            // A coluna CPF tem 11 chars: se vier "111.444.777-35" do formulário, o INSERT
            // estoura com "value too long" e o jogador só vê a página de erro. A tela pede
            // "apenas números", mas isso não impede quem digita com máscara ou cola de outro
            // lugar — então a limpeza é feita aqui, no servidor.
            cpf1 = Documentos.SomenteDigitos(cpf1);
            cpf2 = Documentos.SomenteDigitos(cpf2 ?? "");
            celular1 = Documentos.SomenteDigitosOuNulo(celular1);
            celular2 = Documentos.SomenteDigitosOuNulo(celular2);

            // No máximo UM impedimento (ver Services/ImpedimentoUnico). A tela já não deixa
            // marcar dois, mas página em cache e POST feito à mão não passam pela tela — e
            // dupla sem turno nenhum disponível trava o chaveamento inteiro.
            (impSextaNoite, impSabadoManha, impSabadoTarde) =
                ImpedimentoUnico.Apenas(impSextaNoite, impSabadoManha, impSabadoTarde);

            // Marcou "ainda não tenho parceiro"? Então tudo do jogador 2 é ignorado — mesmo
            // que o formulário tenha mandado algo preenchido antes de o check ser marcado.
            if (semParceiro)
            {
                nome2 = null; cpf2 = ""; celular2 = null; cidade2 = null; estado2 = null;
            }

            if (cpf1.Length != 11 || (!semParceiro && cpf2.Length != 11))
            {
                TempData["Erro"] = "CPF inválido — informe os 11 números, sem pontos ou traço.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // Nome é varchar(100) e o Postgres RECUSA o que passa disso — não corta. Nome
            // colado da agenda do celular (com apelido, empresa e tudo) estourava a inscrição
            // com erro 500 no lugar de um aviso.
            var nomeLongo = LimitesDeTexto.Problema(nome1, LimitesDeTexto.NomeDeJogador, "O nome do jogador 1")
                            ?? (semParceiro ? null : LimitesDeTexto.Problema(nome2, LimitesDeTexto.NomeDeJogador, "O nome do jogador 2"));
            if (nomeLongo != null)
            {
                TempData["Erro"] = nomeLongo;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // Categoria de TIMES não aceita inscrição de dupla: quem cadastra time é o
            // organizador. A tela nem oferece a opção; isto segura o POST montado à mão.
            if (categoria.DeTimes)
            {
                TempData["Erro"] = "Essa categoria é de times — os times são cadastrados pelo organizador.";
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
            var jogador2 = semParceiro ? null : await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf2);

            if (!semParceiro && cpf1 == cpf2)
            {
                TempData["Erro"] = "Os dois CPFs são iguais — informe o parceiro certo ou marque \"ainda não tenho parceiro\".";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

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

            // 2b. Uma categoria por jogador, quando o organizador desligou as múltiplas.
            //     Só checa quem já existe: jogador novo obviamente não está inscrito.
            var idsExistentes = new[] { jogador1?.Id, jogador2?.Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();
            var bloqueioCategorias = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, idsExistentes);
            if (bloqueioCategorias != null)
            {
                TempData["Erro"] = bloqueioCategorias;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
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

            if (!semParceiro)
            {
                if (jogador2 == null)
                {
                    jogador2 = new Jogador { Nome = nome2!, Cpf = cpf2 };
                    _context.Jogadores.Add(jogador2);
                }
                jogador2.Celular = string.IsNullOrWhiteSpace(jogador2.Celular) ? celular2?.Trim() : jogador2.Celular;
                jogador2.Cidade = string.IsNullOrWhiteSpace(jogador2.Cidade) ? cidade2?.Trim() : jogador2.Cidade;
                jogador2.Estado = string.IsNullOrWhiteSpace(jogador2.Estado) ? estado2?.Trim() : jogador2.Estado;
            }

            // Salva os jogadores (se forem novos) para gerar os IDs que usaremos na dupla
            await _context.SaveChangesAsync();

            // 4. Torneio pago com recebimento ativado? Então a dupla ainda NÃO é criada: o
            //    jogador vai pro checkout e a inscrição nasce quando o webhook confirmar o
            //    pagamento (PagamentoInscricaoService.EfetivarAsync).
            var recebedor = await _pagamentos.ObterRecebedorTorneioAsync(torneioId);
            // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
            // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
            if (_pagamentos.PodeCobrar(torneio, recebedor) && torneio.PagamentoObrigatorioNaInscricao)
            {
                // SemParceiro marca que isto é uma DUPLA aberta, não um americano — os dois
                // chegam aqui com Jogador2Id nulo (ver DadosInscricaoTorneio).
                var dadosInscricao = new DadosInscricaoTorneio(
                    torneioId, categoriaId, jogador1.Id, jogador2?.Id,
                    impSextaNoite, impSabadoManha, impSabadoTarde, SemParceiro: semParceiro);

                var checkout = await _pagamentos.IniciarCobrancaTorneioAsync(
                    torneio, recebedor!, jogador1, "TorneioDupla", dadosInscricao, formaPagamentoEscolhida);

                if (checkout != null) return Redirect(checkout);

                TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // 5. Vagas: se a categoria ou o torneio já bateram no limite, a dupla entra
            //    na lista de espera em vez de ser bloqueada — pode ser promovida depois
            //    se alguém desistir (ver TorneiosController.RemoverDupla).
            bool emListaDeEspera = await CategoriaOuTorneioEstaCheioAsync(categoria, torneio);

            // 6. Monta a DUPLA e vincula à Categoria
            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = jogador1.Id,
                Jogador2Id = jogador2?.Id,   // nulo = ainda procurando parceiro
                ImpedimentoSextaNoite = impSextaNoite,
                ImpedimentoSabadoManha = impSabadoManha,
                ImpedimentoSabadoTarde = impSabadoTarde,
                EmListaDeEspera = emListaDeEspera
            };

            _context.Duplas.Add(dupla);
            await _context.SaveChangesAsync(); // Inscrição finalizada!

            var inscritos = jogador2 == null
                ? new[] { jogador1.Id }
                : new[] { jogador1.Id, jogador2.Id };

            await NotificarSeguidoresDeInscricaoAsync(torneioId, inscritos);
            await NotificarInscricaoConfirmadaAsync(torneio, categoria.Nome, inscritos, emListaDeEspera);

            TempData["Sucesso"] = emListaDeEspera
                ? "Vagas esgotadas — sua inscrição entrou na lista de espera. Se alguém desistir, você é chamado na ordem de inscrição."
                : jogador2 == null
                    ? "Inscrição confirmada! Você está sem parceiro — defina o parceiro pela tela do torneio quando encontrar alguém."
                    : "Inscrição confirmada com sucesso!";
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }

        // Define ou troca o parceiro de uma inscrição já feita. Qualquer um dos dois
        // integrantes pode fazer isso (e o organizador também), a qualquer momento — quem
        // sai é avisado, quem entra também.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> TrocarParceiro(int duplaId, string cpfNovoParceiro, string? nomeNovoParceiro)
        {
            var jogadorLogadoId = ObterJogadorIdLogado();
            if (jogadorLogadoId == null) return Forbid();

            var dupla = await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .FirstOrDefaultAsync(d => d.Id == duplaId);

            if (dupla == null) return NotFound();

            var torneio = dupla.Categoria.Torneio;
            int torneioId = torneio.Id;

            // Só quem está na dupla ou organiza o torneio pode mexer.
            bool ehDaDupla = dupla.Jogador1Id == jogadorLogadoId || dupla.Jogador2Id == jogadorLogadoId;
            if (!ehDaDupla && !await UsuarioEhOrganizadorAsync(torneioId)) return Forbid();

            // Depois do sorteio a dupla já está numa chave — trocar aí bagunçaria os jogos.
            if (torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "O parceiro só pode ser alterado enquanto as inscrições estão abertas. Fale com o organizador.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var cpf = Documentos.SomenteDigitos(cpfNovoParceiro ?? "");
            if (cpf.Length != 11)
            {
                TempData["Erro"] = "CPF inválido — informe os 11 números do novo parceiro.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            if (cpf == dupla.Jogador1.Cpf)
            {
                TempData["Erro"] = "O parceiro não pode ser você mesmo.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var novo = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (novo == null)
            {
                if (string.IsNullOrWhiteSpace(nomeNovoParceiro))
                {
                    TempData["Erro"] = "Esse CPF ainda não tem cadastro — informe também o nome do parceiro.";
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
                }
                novo = new Jogador { Nome = nomeNovoParceiro.Trim(), Cpf = cpf };
                _context.Jogadores.Add(novo);
                await _context.SaveChangesAsync();
            }

            if (novo.Id == dupla.Jogador2Id)
            {
                TempData["Sucesso"] = $"{novo.Nome} já é o parceiro desta inscrição.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var impedimento = await MotivoParaNaoSerParceiroAsync(dupla, torneio, novo);
            if (impedimento != null)
            {
                TempData["Erro"] = impedimento;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var antigo = dupla.Jogador2;
            dupla.Jogador2Id = novo.Id;
            await _context.SaveChangesAsync();

            await AvisarTrocaDeParceiroAsync(dupla, torneio, antigo, novo);

            TempData["Sucesso"] = antigo == null
                ? $"Parceiro definido: {novo.Nome}. Sua dupla está completa!"
                : $"Parceiro alterado de {antigo.Nome} para {novo.Nome}.";
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }

        // As regras que impedem alguém de entrar nesta dupla, num lugar só: valem tanto pra
        // quem é escolhido pelo CPF (TrocarParceiro) quanto pra quem aceita um convite.
        // Separar as duas cópias deixaria o caminho do convite mais frouxo que o outro — e
        // é justamente o caminho aberto por link, o que qualquer um alcança.
        // Devolve a mensagem do impedimento, ou null quando pode entrar.
        private async Task<string?> MotivoParaNaoSerParceiroAsync(Dupla dupla, Torneio torneio, Jogador candidato)
        {
            // Não pode já estar em outra dupla desta MESMA categoria.
            bool jaNaCategoria = await _context.Duplas.AnyAsync(d => d.CategoriaId == dupla.CategoriaId
                && d.Id != dupla.Id
                && (d.Jogador1Id == candidato.Id || d.Jogador2Id == candidato.Id));
            if (jaNaCategoria)
            {
                return $"{candidato.Nome} já está inscrito nesta categoria com outra dupla.";
            }

            // ...nem violar a regra de uma categoria por jogador (ignorando esta categoria,
            // onde a dupla já está inscrita).
            var bloqueio = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { candidato.Id }, ignorarCategoriaId: dupla.CategoriaId);
            if (bloqueio != null) return bloqueio;

            // Anti-sandbagging: o parceiro precisa poder jogar nesta categoria.
            if (!string.IsNullOrEmpty(torneio.RestricaoCategoria) && torneio.RestricaoCategoria != "Livre")
            {
                return await MotivoBloqueioCategoriaAsync(
                    dupla.Categoria.Nome, candidato, null, torneio.RestricaoCategoria);
            }

            return null;
        }

        // ── Convite de parceiro ────────────────────────────────────────────────────────
        // O link que fecha a dupla sem ninguém digitar o CPF do outro. Regras em
        // Services/ConviteDeParceiro.

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GerarConvite(int duplaId)
        {
            var jogadorLogadoId = ObterJogadorIdLogado();
            if (jogadorLogadoId == null) return Forbid();

            var dupla = await _context.Duplas
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            var torneio = dupla.Categoria.Torneio;

            bool ehDaDupla = dupla.Jogador1Id == jogadorLogadoId || dupla.Jogador2Id == jogadorLogadoId;
            if (!ehDaDupla && !await UsuarioEhOrganizadorAsync(torneio.Id)) return Forbid();

            if (dupla.Jogador2Id != null)
            {
                TempData["Erro"] = "Essa dupla já está completa.";
                return RedirectToAction("Details", "Torneios", new { id = torneio.Id });
            }

            if (torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio já foram encerradas.";
                return RedirectToAction("Details", "Torneios", new { id = torneio.Id });
            }

            // Gerar de novo TROCA o token: o link antigo para de valer. É o que se espera de
            // "gerar convite" quando o primeiro foi mandado pra pessoa errada.
            dupla.ConviteToken = ConviteDeParceiro.NovoToken();
            dupla.ConviteCriadoEm = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["ConviteDuplaId"] = dupla.Id;
            TempData["ConviteLink"] = Url.Action(nameof(Convite), "Duplas",
                new { token = dupla.ConviteToken }, Request.Scheme);
            return RedirectToAction("Details", "Torneios", new { id = torneio.Id });
        }

        // A tela que quem recebeu o link abre. Exige login: é ele quem vai virar parceiro,
        // e a conta dele é que diz quem ele é (o convite não pergunta CPF de ninguém).
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Convite(string? token)
        {
            var dupla = string.IsNullOrWhiteSpace(token) ? null : await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .FirstOrDefaultAsync(d => d.ConviteToken == token);

            var torneio = dupla?.Categoria.Torneio;

            if (!ConviteDeParceiro.Valido(dupla, torneio?.Status, token))
            {
                ViewBag.Erro = ConviteDeParceiro.MotivoDeNaoValer(dupla, torneio?.Status);
                return View("ConviteInvalido");
            }

            var jogadorLogadoId = ObterJogadorIdLogado();

            // Convidar a si mesmo não fecha dupla nenhuma — melhor dizer isso do que
            // deixar aceitar e recusar depois com "o parceiro não pode ser você mesmo".
            ViewBag.SouEuMesmo = jogadorLogadoId == dupla!.Jogador1Id;
            ViewBag.Token = token;

            // O impedimento é mostrado JÁ na tela do convite: descobrir só no clique de
            // aceitar ("você já está nesta categoria") é descobrir tarde.
            if (jogadorLogadoId != null && !(bool)ViewBag.SouEuMesmo)
            {
                var eu = await _context.Jogadores.FindAsync(jogadorLogadoId.Value);
                if (eu != null)
                {
                    ViewBag.Impedimento = await MotivoParaNaoSerParceiroAsync(dupla, torneio!, eu);
                }
            }

            return View(dupla);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AceitarConvite(string? token)
        {
            var jogadorLogadoId = ObterJogadorIdLogado();
            if (jogadorLogadoId == null) return Forbid();

            var dupla = string.IsNullOrWhiteSpace(token) ? null : await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .FirstOrDefaultAsync(d => d.ConviteToken == token);

            var torneio = dupla?.Categoria.Torneio;

            // A validade é conferida DE NOVO aqui, não só na tela: entre abrir o convite e
            // clicar em aceitar, outra pessoa pode ter aceitado o mesmo link.
            if (!ConviteDeParceiro.Valido(dupla, torneio?.Status, token))
            {
                ViewBag.Erro = ConviteDeParceiro.MotivoDeNaoValer(dupla, torneio?.Status);
                return View("ConviteInvalido");
            }

            if (jogadorLogadoId == dupla!.Jogador1Id)
            {
                TempData["Erro"] = "Você não pode ser parceiro de si mesmo.";
                return RedirectToAction(nameof(Convite), new { token });
            }

            var eu = await _context.Jogadores.FindAsync(jogadorLogadoId.Value);
            if (eu == null) return Forbid();

            var impedimento = await MotivoParaNaoSerParceiroAsync(dupla, torneio!, eu);
            if (impedimento != null)
            {
                TempData["Erro"] = impedimento;
                return RedirectToAction(nameof(Convite), new { token });
            }

            dupla.Jogador2Id = eu.Id;
            // Token usado não volta a valer: sem isto, o mesmo link fecharia a dupla de novo
            // se o parceiro saísse depois.
            dupla.ConviteToken = null;
            dupla.ConviteCriadoEm = null;
            await _context.SaveChangesAsync();

            await AvisarTrocaDeParceiroAsync(dupla, torneio!, null, eu);

            TempData["Sucesso"] = $"Pronto! Você é parceiro de {dupla.Jogador1.Nome} em {torneio!.Nome}.";
            return RedirectToAction("Details", "Torneios", new { id = torneio.Id });
        }

        // Quem saiu precisa saber que saiu; quem entrou, que entrou. Push é acessório:
        // a troca já foi gravada e não pode falhar por causa de notificação.
        private async Task AvisarTrocaDeParceiroAsync(Dupla dupla, Torneio torneio, Jogador? antigo, Jogador novo)
        {
            try
            {
                var url = Url.Action("Details", "Torneios", new { id = torneio.Id });

                if (antigo != null)
                {
                    await _pushService.EnviarParaJogadorAsync(antigo.Id,
                        "Você saiu de uma dupla",
                        $"{dupla.Jogador1.Nome} trocou de parceiro em {torneio.Nome}.", url);
                }

                await _pushService.EnviarParaJogadorAsync(novo.Id,
                    "Você entrou numa dupla!",
                    $"{dupla.Jogador1.Nome} te escolheu como parceiro em {torneio.Nome} · {dupla.Categoria.Nome}.", url);

                await _pushService.EnviarParaJogadorAsync(dupla.Jogador1Id,
                    "Dupla atualizada",
                    $"Seu parceiro em {torneio.Nome} agora é {novo.Nome}.", url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao notificar troca de parceiro da dupla {DuplaId}.", dupla.Id);
            }
        }

        // Avisa a própria dupla que está dentro. Quem paga recebe o mesmo aviso pelo
        // PagamentoInscricaoService, quando a cobrança confirma.
        // Quando alguém inscreve OUTRA pessoa, o aviso dela precisa dizer quem foi: "inscrição
        // confirmada" sozinho deixa a pessoa achando que ela mesma se inscreveu, e quem só
        // descobre pela chave sorteada já perdeu o prazo de reclamar. É o mesmo push de sempre,
        // com o autor no texto — mandar um segundo aviso só pra isso viraria barulho.
        private async Task NotificarInscricaoConfirmadaAsync(
            Torneio torneio, string categoriaNome, IEnumerable<int> jogadorIds, bool emListaDeEspera)
        {
            var url = Url.Action("Details", "Torneios", new { id = torneio.Id });

            var autorId = ObterJogadorIdLogado();
            var autorNome = autorId == null
                ? null
                : (await _context.Jogadores.FindAsync(autorId.Value))?.ComoChamar;

            foreach (var jogadorId in jogadorIds)
            {
                bool inscritoPorOutro = autorId != null && jogadorId != autorId.Value
                                        && !string.IsNullOrWhiteSpace(autorNome);

                var titulo = emListaDeEspera
                    ? "Você entrou na lista de espera"
                    : inscritoPorOutro ? $"{autorNome} inscreveu você" : "Inscrição confirmada!";

                var corpo = emListaDeEspera
                    ? $"{torneio.Nome} · {categoriaNome} estava lotado. Se alguém desistir, vocês são chamados."
                    : $"{torneio.Nome} · {categoriaNome}. Boa sorte!";

                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId, titulo, corpo, url);
                }
                catch (Exception ex)
                {
                    // Push é acessório — a inscrição já foi salva, não pode falhar por isso.
                    _logger.LogWarning(ex, "Falha ao notificar inscrição do jogador {JogadorId}.", jogadorId);
                }
            }
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

        private int? ObterJogadorIdLogado()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : null;
        }

        // O usuário logado manda neste torneio? (usado para liberar o bloqueio de categoria e
        // pra trocar parceiro de qualquer dupla). Organizador do torneio ou admin do
        // Padelizou — mesma régua do TorneiosController.EhOrganizadorAsync.
        private async Task<bool> UsuarioEhOrganizadorAsync(int torneioId)
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var jogadorId) || jogadorId <= 0) return false;

            if (await _context.TorneioOrganizadores
                    .AnyAsync(o => o.TorneioId == torneioId && o.JogadorId == jogadorId))
                return true;

            return await _context.Jogadores
                .AnyAsync(j => j.Id == jogadorId && (j.IsAdminRaiz || j.IsAdminGeral));
        }
    }
}
