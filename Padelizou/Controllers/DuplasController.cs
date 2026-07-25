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
        [HttpPost]
        public async Task<IActionResult> Create(
            int torneioId, int categoriaId,
            string nome1, string cpf1, string? celular1, string? cidade1, string? estado1,
            string? nome2, string? cpf2, string? celular2, string? cidade2, string? estado2,
            bool impSextaNoite, bool impSabadoManha, bool impSabadoTarde,
            bool semParceiro = false, bool ignorarBloqueio = false, string? chaveAcesso = null)
        {
            // A coluna CPF tem 11 chars: se vier "111.444.777-35" do formulário, o INSERT
            // estoura com "value too long" e o jogador só vê a página de erro. A tela pede
            // "apenas números", mas isso não impede quem digita com máscara ou cola de outro
            // lugar — então a limpeza é feita aqui, no servidor.
            cpf1 = Documentos.SomenteDigitos(cpf1);
            cpf2 = Documentos.SomenteDigitos(cpf2 ?? "");
            celular1 = Documentos.SomenteDigitosOuNulo(celular1);
            celular2 = Documentos.SomenteDigitosOuNulo(celular2);

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
            if (_pagamentos.PodeCobrar(torneio, recebedor))
            {
                // SemParceiro marca que isto é uma DUPLA aberta, não um americano — os dois
                // chegam aqui com Jogador2Id nulo (ver DadosInscricaoTorneio).
                var dadosInscricao = new DadosInscricaoTorneio(
                    torneioId, categoriaId, jogador1.Id, jogador2?.Id,
                    impSextaNoite, impSabadoManha, impSabadoTarde, SemParceiro: semParceiro);

                var checkout = await _pagamentos.IniciarCobrancaTorneioAsync(
                    torneio, recebedor!, jogador1, "TorneioDupla", dadosInscricao);

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

            // O novo parceiro não pode já estar em outra dupla desta MESMA categoria.
            bool jaNaCategoria = await _context.Duplas.AnyAsync(d => d.CategoriaId == dupla.CategoriaId
                && d.Id != dupla.Id
                && (d.Jogador1Id == novo.Id || d.Jogador2Id == novo.Id));
            if (jaNaCategoria)
            {
                TempData["Erro"] = $"{novo.Nome} já está inscrito nesta categoria com outra dupla.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // ...nem violar a regra de uma categoria por jogador (ignorando esta categoria,
            // onde a dupla já está inscrita).
            var bloqueio = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { novo.Id }, ignorarCategoriaId: dupla.CategoriaId);
            if (bloqueio != null)
            {
                TempData["Erro"] = bloqueio;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // Anti-sandbagging: o novo parceiro precisa poder jogar nesta categoria.
            if (!string.IsNullOrEmpty(torneio.RestricaoCategoria) && torneio.RestricaoCategoria != "Livre")
            {
                var erro = await MotivoBloqueioCategoriaAsync(dupla.Categoria.Nome, novo, null, torneio.RestricaoCategoria);
                if (erro != null)
                {
                    TempData["Erro"] = erro;
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
                }
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
        private async Task NotificarInscricaoConfirmadaAsync(
            Torneio torneio, string categoriaNome, IEnumerable<int> jogadorIds, bool emListaDeEspera)
        {
            var url = Url.Action("Details", "Torneios", new { id = torneio.Id });

            foreach (var jogadorId in jogadorIds)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId,
                        emListaDeEspera ? "Você entrou na lista de espera" : "Inscrição confirmada!",
                        emListaDeEspera
                            ? $"{torneio.Nome} · {categoriaNome} estava lotado. Se alguém desistir, vocês são chamados."
                            : $"{torneio.Nome} · {categoriaNome}. Boa sorte!",
                        url);
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
