using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // Criação e edição do torneio: formulário, capa, organizadores e o pacote de registro de resultados.
    public partial class TorneiosController
    {
        // 1. ABRE A TELA DE CRIAÇÃO (Carrega o Catálogo)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // A tela abre pra todo mundo, porque o AMERICANO é livre — qualquer pessoa
            // cadastrada cria o rodízio dela. O que depende de liberação é o torneio Oficial,
            // e a tela esconde esse formato de quem ainda não tem o perfil.
            //
            // Esconder na tela, e não recusar na porta: mandar embora quem só queria criar um
            // Americano seria fechar a porta de entrada do app na cara de quem chegou.
            var quemPede = await _context.Jogadores.FindAsync(ObterJogadorIdLogado() ?? 0);
            ViewBag.PodeCriarOficial = PermissaoDeOrganizador.PodeCriarOficial(quemPede);
            ViewBag.ComoPedirOPerfil = PermissaoDeOrganizador.ComoPedirOPerfil;
            // Pra tela saber se mostra o botão de pedir ou o "seu pedido está na fila".
            ViewBag.EstadoDoPedido = PermissaoDeOrganizador.EstadoDe(quemPede);

            // Busca as categorias do banco para montar os Checkboxes na tela. Só as ATIVAS:
            // categoria desligada continua valendo onde já foi usada, mas não é oferecida.
            var catalogo = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoCategorias = catalogo;
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

            // Pacote adicional de registro de resultados: some da tela quando o serviço está
            // desligado, pra não receber pedido que já se sabe que vai virar "sem equipe".
            ViewBag.RegistroHabilitado = _registro.Habilitado;
            ViewBag.RegistroQuadrasPorPessoa = _registro.QuadrasPorPessoa;
            ViewBag.RegistroPrecoPorJogo = _registro.PrecoPorJogo;
            ViewBag.RegistroValorMinimo = _registro.ValorMinimo;

            // Sem um Torneio no View(), asp-for não teria de onde tirar valor e os campos
            // obrigatórios de horário e duração nasceriam VAZIOS — o organizador teria que
            // adivinhar o que preencher. Assim ele começa com 8h-22h e 50 min e só ajusta.
            //
            // Sem conta conectada as opções "pelo site" não têm como funcionar — e até aqui
            // elas apareciam normalmente, com a de Pix já marcada. A tela precisa saber disso
            // ANTES de o organizador escolher, senão a descoberta vem tarde demais.
            var eu = await _context.Jogadores.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
            bool conectado = _pagamentos.PodeReceberOnline(eu);
            ViewBag.RecebimentoConectado = conectado;

            // A inscrição nasce em 150 em vez de 0: zero é um preço VÁLIDO (torneio gratuito),
            // então o campo em branco não avisava nada e um esquecimento virava torneio de
            // graça. O valor é um chute plausível pra dupla, e o campo se seleciona sozinho
            // ao receber o foco — quem digita substitui, não emenda dígito no que já estava.
            //
            // A forma de pagamento nasce no que ele CONSEGUE fazer. Quem é o marcado quem
            // decide é o asp-for pelo valor do modelo — pôr um `checked` fixo na tela deixaria
            // dois rádios marcados e o resultado dependeria da ordem do HTML.
            return View(new Torneio
            {
                PrecoInscricao = 150m,
                FormaPagamento = conectado ? "OnlinePix" : "Externo"
            });
        }

        // ── Pacote "nós registramos os resultados para você" ──────────────────────────────
        // O organizador contrata o Padelizou pra mandar gente lançar os jogos durante o
        // torneio. É SOLICITAÇÃO, não compra: pode não haver ninguém livre naquela data e
        // naquela cidade, e por isso o botão diz "verificar disponibilidade".

        private async Task<string?> CriarSolicitacaoRegistroAsync(Torneio torneio, string? observacoes)
        {
            var jaTemAberta = await _context.SolicitacoesRegistroResultados.AnyAsync(s =>
                s.TorneioId == torneio.Id &&
                (s.Status == SolicitacaoRegistroResultados.Solicitada ||
                 s.Status == SolicitacaoRegistroResultados.Confirmada));

            var problema = RegistroDeResultados.ProblemaParaSolicitar(
                _registro.Habilitado, jaTemAberta, torneio.DataInicio, DateTime.Today,
                _registro.AntecedenciaMinimaDias);

            if (problema != null) return problema;

            var dias = RegistroDeResultados.DiasDoTorneio(torneio.DataInicio, torneio.DataFim);
            var pessoas = RegistroDeResultados.PessoasSugeridas(
                torneio.QuantidadeQuadras, _registro.QuadrasPorPessoa);

            // Quantos jogos, pelas duplas JÁ inscritas em cada categoria. Num torneio recém
            // criado isso é zero, e aí o número fica nulo de propósito: melhor mostrar só a
            // regra do que um total que vai mudar a cada inscrição.
            var duplasPorCategoria = await _context.Categorias
                .Where(c => c.TorneioId == torneio.Id)
                .Select(c => c.Duplas.Count(d => !d.EmListaDeEspera))
                .ToListAsync();

            var jogos = RegistroDeResultados.JogosPrevistos(duplasPorCategoria);

            _context.SolicitacoesRegistroResultados.Add(new SolicitacaoRegistroResultados
            {
                TorneioId = torneio.Id,
                Status = SolicitacaoRegistroResultados.Solicitada,
                QuadrasNaSolicitacao = torneio.QuantidadeQuadras,
                DiasNaSolicitacao = dias,
                PessoasSugeridas = pessoas,
                JogosPrevistos = jogos > 0 ? jogos : null,
                PrecoPorJogoCotado = _registro.PrecoPorJogo,
                ValorMinimoCotado = _registro.ValorMinimo,
                Observacoes = string.IsNullOrWhiteSpace(observacoes) ? null : observacoes.Trim(),
                SolicitadoPorId = ObterJogadorIdLogado() ?? 0,
                SolicitadaEm = DateTime.Now,
            });

            await _context.SaveChangesAsync();

            // Avisa quem responde. Sem isto o pedido ficaria esperando alguém lembrar de
            // abrir o painel — e o organizador contando com uma equipe que ninguém viu pedir.
            try
            {
                var admins = await _context.Jogadores
                    .Where(j => (j.IsAdminGeral || j.IsAdminRaiz) && j.ExcluidoEm == null)
                    .ToListAsync();

                var url = Url.Action("RegistroResultados", "Admin");
                foreach (var admin in admins)
                {
                    await _pushService.EnviarParaJogadorAsync(admin.Id,
                        "Pedido de equipe para registrar resultados",
                        $"{torneio.Nome}: {pessoas} pessoa(s) por {dias} dia(s).", url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar admins do pedido de registro do torneio {TorneioId}.", torneio.Id);
            }

            return null;
        }

        // Pedir depois da criação: o organizador pode não ter decidido na hora, ou o torneio
        // pode ter crescido de 2 pra 6 quadras e virado outra história.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarRegistroResultados(int id, string? observacoes)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var problema = await CriarSolicitacaoRegistroAsync(torneio, observacoes);

            TempData[problema == null ? "Sucesso" : "Erro"] = problema
                ?? "Pedido enviado! Vamos verificar a disponibilidade e responder por aqui.";

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarRegistroResultados(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var solicitacao = await _context.SolicitacoesRegistroResultados
                .Where(s => s.TorneioId == id)
                .OrderByDescending(s => s.SolicitadaEm)
                .FirstOrDefaultAsync();

            if (solicitacao == null) return RedirectToAction("Details", new { id });

            var problema = RegistroDeResultados.ProblemaParaCancelar(solicitacao.Status);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Details", new { id });
            }

            solicitacao.Status = SolicitacaoRegistroResultados.Cancelada;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Pedido cancelado.";
            return RedirectToAction("Details", new { id });
        }

        // 2. RECEBE OS DADOS E SALVA O TORNEIO
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(Torneio torneio, int[] categoriasSelecionadas, int[]? organizadoresSelecionados, string[]? nomesQuadras, IFormFile? capa, Dictionary<int, int?>? limiteCategoria, string? novoClubeNome = null,
            bool querRegistroDeResultados = false, string? observacoesRegistro = null, string? chaveAcessoEscolhida = null,
            bool categoriaDeTimes = false, string? nomeCategoriaTimes = null,
            int? quantidadeTimes = null, int? quantidadeGruposTimes = null, int? classificadosPorGrupoTimes = null)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            // A mesma trava do GET, agora no servidor. A tela esconde o formato Oficial de quem
            // não pode, mas POST montado à mão não passa por tela nenhuma — e é justamente
            // quem quer lotar o sistema que não usa o formulário.
            //
            // ⚠️ A pergunta leva o FORMATO junto: o Americano é livre, o Oficial não.
            if (!PermissaoDeOrganizador.PodeCriarTorneio(
                    await _context.Jogadores.FindAsync(criadorId), torneio.Formato))
            {
                TempData["Erro"] = PermissaoDeOrganizador.ComoPedirOPerfil;
                return RedirectToAction(nameof(Index));
            }

            // ── As recusas vêm ANTES de qualquer gravação ────────────────────────────────
            // Inclusive antes de achar-ou-criar o clube: recusar depois deixaria um clube
            // novo no catálogo a cada tentativa recusada.
            async Task<IActionResult> Recusar(string motivo)
            {
                ViewBag.Erro = motivo;
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                // Sem isto, cada recusa apagava as categorias marcadas e o organizador
                // remarcava tudo de novo — oito cliques pra corrigir um campo de texto.
                ViewBag.CategoriasSelecionadas = categoriasSelecionadas ?? Array.Empty<int>();
                // Sem isto o formulário voltaria com as opções "pelo site" liberadas de novo,
                // e a recusa seguinte seria a mesma — o organizador em looping.
                bool conectado = _pagamentos.PodeReceberOnline(await _context.Jogadores.FindAsync(criadorId));
                ViewBag.RecebimentoConectado = conectado;

                // Rádio desativado não é enviado pelo navegador. Se o formulário voltasse com
                // "OnlinePix" marcado E desativado, o próximo envio viria SEM forma nenhuma e
                // o binder cairia no valor inicial do modelo — que é "OnlinePix" de novo. Ou
                // seja: recusa eterna, sem o organizador entender o que fazer.
                //
                // Corrigir só o objeto NÃO basta, e isso só apareceu testando no navegador: o
                // asp-for decide o marcado pelo ModelState (o que veio no POST), que ganha do
                // modelo. Tirar a chave de lá é o que faz a tela voltar em "Por fora".
                if (!conectado && torneio.CobraPeloSite)
                {
                    torneio.FormaPagamento = "Externo";
                    ModelState.Remove(nameof(Torneio.FormaPagamento));
                }

                return View(torneio);
            }

            // Nome é varchar(150) e o Postgres RECUSA o que passa — não corta. Quem cola a
            // descrição inteira do torneio no campo do nome perdia o formulário num erro 500.
            if (LimitesDeTexto.Problema(torneio.Nome, LimitesDeTexto.NomeDeTorneio, "O nome do torneio") is { } nomeLongo)
            {
                return await Recusar(nomeLongo);
            }

            // Preço negativo não estoura nada — e é justamente por isso que passava batido: a
            // cobrança some (PodeCobrar exige > 0) e o torneio fica anunciando "−R$ 50,00" pra
            // quem for se inscrever. Zero continua valendo: torneio de graça existe.
            if (torneio.PrecoInscricao < 0 || torneio.TaxaPorImpedimento < 0)
            {
                return await Recusar("Valor negativo não dá: use zero pra não cobrar nada.");
            }

            // "Pelo site" sem conta conectada era a armadilha mais cara que o sistema tinha:
            // o torneio nascia, as inscrições entravam e NENHUMA cobrança era criada, porque
            // PodeCobrar exige a conta. Nada disso aparecia — nem erro, nem aviso — e o
            // organizador só descobria ao ir atrás do dinheiro, com o torneio já rodando.
            // A tela também esconde as opções, mas a recusa precisa existir aqui: quem manda
            // o formulário na mão passaria direto pelo bloqueio do navegador.
            if (torneio.CobraPeloSite && torneio.PrecoInscricao > 0
                && _pagamentos.OQueFaltaParaReceber(await _context.Jogadores.FindAsync(criadorId)) is { } faltaConta)
            {
                return await Recusar($"{faltaConta} {ContaDeRecebimento.ComoResolver} " +
                    "Se preferir receber por fora, escolha essa opção em \"Como você vai receber as inscrições?\".");
            }

            // A chave escolhida é conferida antes de criar: recusar depois deixaria o torneio
            // no ar com uma chave que o organizador não pediu.
            if (torneio.Restrito && ChaveDeAcessoDoTorneio.ProblemaCom(chaveAcessoEscolhida) is { } problemaChave)
            {
                return await Recusar(problemaChave);
            }

            // Torneio sem categoria nenhuma é um torneio em que NINGUÉM consegue se inscrever:
            // o formulário de inscrição escolhe a categoria, e sem opção não há o que escolher.
            // Ele era aceito em silêncio — o organizador compartilhava o link e descobria pelo
            // primeiro jogador que tentou entrar e não conseguiu. A caixa das categorias fica
            // no fim de um formulário longo, então passar batido é o caso comum, não o raro.
            // Torneio SÓ de times é válido: nele quem monta a lista é o organizador.
            if ((categoriasSelecionadas == null || categoriasSelecionadas.Length == 0) && !categoriaDeTimes)
            {
                return await Recusar("Escolha pelo menos uma categoria — é nela que os jogadores se inscrevem.");
            }

            // O formato vem de um radio, mas POST montado à mão pode mandar qualquer texto —
            // e um formato desconhecido criaria um torneio meio-Padrão que nenhuma tela sabe
            // sortear nem encerrar.
            if (!FormatoDoTorneio.Existe(torneio.Formato))
            {
                return await Recusar("Escolha o formato do torneio.");
            }

            // "Vale ponto no Ranking Americano" só existe no Americano — a caixa nem aparece no
            // formato Padrão. Vindo marcada num Padrão (aba antiga, POST à mão), some em vez de
            // recusar: é campo que não se aplica, não erro do organizador. E deixar gravado
            // criaria um torneio de chave cobrando por um ranking em que ele nunca entra.
            if (!FormatoDoTorneio.EhAmericano(torneio.Formato))
            {
                torneio.PontuaNoRankingAmericano = false;
            }

            // Categoria de times: a estrutura precisa fechar um quadro de mata-mata ANTES de
            // criar qualquer coisa — recusar depois deixaria o torneio no ar pela metade.
            if (categoriaDeTimes)
            {
                if (torneio.Formato != "Padrao")
                {
                    return await Recusar("Categoria de times só existe no formato padrão (grupos + mata-mata).");
                }
                if (CategoriaDeTimes.ProblemaNaConfiguracao(
                        quantidadeTimes, quantidadeGruposTimes, classificadosPorGrupoTimes) is { } problemaTimes)
                {
                    return await Recusar($"Categoria de times: {problemaTimes}");
                }
            }

            // Mesmo nome, mesmo organizador, torneio ainda de pé: quase sempre é o botão
            // apertado duas vezes (ver Services/NomeDoTorneio). Dois torneios iguais dividem
            // as inscrições entre si e ninguém sabe em qual entrar.
            var meusTorneioIds = await _context.TorneioOrganizadores
                .Where(o => o.JogadorId == criadorId)
                .Select(o => o.TorneioId)
                .ToListAsync();
            var meusTorneios = await _context.Torneios
                .Where(t => meusTorneioIds.Contains(t.Id))
                .Select(t => new { t.Nome, t.Status })
                .ToListAsync();

            if (NomeDoTorneio.JaExisteEntre(
                    meusTorneios.Select(t => (t.Nome, (string?)t.Status)), torneio.Nome) is { } jaExiste)
            {
                return await Recusar($"Você já tem um torneio chamado \"{jaExiste}\" em andamento. " +
                    "Use outro nome, ou continue no que já existe — ele está na sua lista de torneios.");
            }

            // O clube pode ser escrito na hora: numa base nova não existe nenhum, e um select
            // obrigatório e vazio impediria de criar o primeiro torneio.
            var clubeNovo = await CatalogoLocais.AcharOuCriarClubeAsync(_context, novoClubeNome);
            if (clubeNovo != null) torneio.ClubeId = clubeNovo.Id;

            // Sem clube o insert estouraria na chave estrangeira, com erro 500 e o formulário
            // inteiro perdido. Melhor recusar aqui, explicando o que fazer.
            if (torneio.ClubeId <= 0)
            {
                return await Recusar("Escolha o clube responsável, ou escreva o nome dele no campo abaixo do seletor.");
            }

            // O local do torneio é o clube: pedir os dois era pedir o mesmo dado duas vezes.
            if (string.IsNullOrWhiteSpace(torneio.LocalTorneio))
            {
                torneio.LocalTorneio = clubeNovo?.Nome
                    ?? (await _context.Clubes.FindAsync(torneio.ClubeId))?.Nome;
            }

            // Validação de Segurança: Se for formato único, iguala todas as fases à Fase de Grupos
            if (torneio.FormatoUnico)
            {
                torneio.SetsFaseMataMata = torneio.SetsFaseGrupos;
                torneio.GamesFaseMataMata = torneio.GamesFaseGrupos;
                torneio.SetsFaseFinal = torneio.SetsFaseGrupos;
                torneio.GamesFaseFinal = torneio.GamesFaseGrupos;
            }

            // O Torneio nasce com Inscrições Abertas
            torneio.Status = "Inscrições Abertas";
            torneio.Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            // Escolhida pelo organizador quando ele digitou uma; sorteada quando deixou vazio.
            torneio.ChaveAcesso = torneio.Restrito ? ChaveDeAcessoDoTorneio.Definir(chaveAcessoEscolhida) : null;

            if (capa != null && capa.Length > 0)
            {
                var capaNova = await SalvarCapaAsync(capa);
                torneio.ImagemCapa = capaNova.Caminho;
                if (capaNova.DeuErro) TempData["ErroImagem"] = capaNova.Erro + " O torneio foi criado — dá pra pôr a capa depois, editando.";
            }

            // Salva o Torneio primeiro para gerar o ID dele
            _context.Torneios.Add(torneio);
            await _context.SaveChangesAsync();

            // Cria as quadras do torneio a partir da quantidade informada, usando o nome que o
            // organizador deu a cada uma (ou "Quadra A/B..." como fallback se deixou em branco).
            string alfabetoQuadras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int quantidadeQuadras = Math.Max(1, torneio.QuantidadeQuadras);
            for (int q = 0; q < quantidadeQuadras && q < alfabetoQuadras.Length; q++)
            {
                string? nomeInformado = nomesQuadras != null && q < nomesQuadras.Length ? nomesQuadras[q]?.Trim() : null;
                string nomeQuadra = string.IsNullOrWhiteSpace(nomeInformado) ? $"Quadra {alfabetoQuadras[q]}" : nomeInformado;
                _context.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nomeQuadra });
            }
            await _context.SaveChangesAsync();

            // Quem criou o torneio já entra como organizador dele (o criadorId foi lido lá em
            // cima, junto com a checagem de nome repetido).
            _context.TorneioOrganizadores.Add(new TorneioOrganizador
            {
                TorneioId = torneio.Id,
                JogadorId = criadorId,
                NivelAcesso = "Criador"
            });

            // Co-organizadores escolhidos por CPF/Login na tela de criação
            if (organizadoresSelecionados != null)
            {
                foreach (var jogadorId in organizadoresSelecionados.Distinct())
                {
                    if (jogadorId == criadorId) continue;
                    _context.TorneioOrganizadores.Add(new TorneioOrganizador
                    {
                        TorneioId = torneio.Id,
                        JogadorId = jogadorId,
                        NivelAcesso = "Organizador"
                    });
                }
            }
            await _context.SaveChangesAsync();

            // Pega as categorias que o organizador marcou e salva na tabela Categoria do Torneio
            var semParNoRanking = new List<string>();
            int comParNoRanking = 0;
            if (categoriasSelecionadas != null && categoriasSelecionadas.Length > 0)
            {
                foreach (var catId in categoriasSelecionadas)
                {
                    // Desligada entra na mesma vala do id que não existe: a tela não oferece,
                    // e o POST feito à mão não devolve a categoria pro ar por um atalho.
                    var catPadrao = await _context.CategoriasPadrao.FindAsync(catId);
                    if (catPadrao is { Ativa: true })
                    {
                        int? limite = limiteCategoria != null && limiteCategoria.TryGetValue(catId, out var l) && l is > 0 ? l : null;
                        var novaCategoria = new Categoria
                        {
                            TorneioId = torneio.Id,
                            Nome = catPadrao.Nome,
                            Codigo = catPadrao.Codigo,
                            LimiteDuplas = limite
                        };

                        // O de-para com o Ranking RS só é preenchido para quem PEDIU a conferência.
                        // O palpite é seguro aqui porque o nome vem do catálogo padrão, que sempre
                        // escreve o sexo ("2ª Categoria Masculina") — e é justamente o sexo que o
                        // Adivinhar exige pra casar (ver Services/CategoriaDoRankingRs).
                        //
                        // Aplicar o palpite calado seria errado, então ele NÃO é calado: a mensagem
                        // de sucesso diz quantas entraram, quais ficaram de fora, e onde revisar.
                        if (torneio.ValidarPeloRankingRs)
                        {
                            novaCategoria.RankingRsCategoriaId = CategoriaDoRankingRs.Adivinhar(catPadrao.Nome);
                            if (novaCategoria.RankingRsCategoriaId == null) semParNoRanking.Add(catPadrao.Nome);
                            else comParNoRanking++;
                        }

                        _context.Categorias.Add(novaCategoria);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Categoria de times (validada lá em cima, antes de qualquer gravação). Os times
            // em si são cadastrados depois, na tela de times do torneio.
            if (categoriaDeTimes)
            {
                _context.Categorias.Add(new Categoria
                {
                    TorneioId = torneio.Id,
                    Nome = string.IsNullOrWhiteSpace(nomeCategoriaTimes) ? "Times" : nomeCategoriaTimes.Trim(),
                    Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper(),
                    DeTimes = true,
                    QuantidadeTimes = quantidadeTimes,
                    QuantidadeGrupos = quantidadeGruposTimes,
                    ClassificadosPorGrupo = classificadosPorGrupoTimes,
                });
                await _context.SaveChangesAsync();
            }

            // Pacote "nós registramos os resultados": vira uma solicitação, não uma compra.
            if (querRegistroDeResultados)
            {
                var problema = await CriarSolicitacaoRegistroAsync(torneio, observacoesRegistro);
                TempData[problema == null ? "Sucesso" : "Erro"] = problema
                    ?? "Torneio criado! Recebemos seu pedido de equipe para registrar os resultados "
                     + "e vamos confirmar a disponibilidade em breve.";
            }

            // Conferência pelo Ranking RS: diz o que foi decidido por palpite, pra não ser
            // surpresa. Só entra quando o organizador marcou — e não sobrescreve a mensagem do
            // pacote de registro de resultados, que é sobre dinheiro e vale mais.
            if (torneio.ValidarPeloRankingRs && TempData["Sucesso"] == null && TempData["Erro"] == null)
            {
                var recado = $"Torneio criado! As inscrições vão ser conferidas no Ranking RS em "
                           + $"{comParNoRanking} categoria(s).";

                if (semParNoRanking.Count > 0)
                {
                    recado += $" Sem correspondência no ranking (não vão ser conferidas): "
                            + $"{string.Join(", ", semParNoRanking)}.";
                }

                TempData["Sucesso"] = recado + " Dá pra revisar categoria por categoria em Editar torneio.";
            }

            // ⚠️ O aviso "novo torneio aberto" NÃO sai daqui desde 07/08/2026 — ele sai da
            // APROVAÇÃO, no painel admin (AdminController.AprovarTorneio). Avisar na criação
            // entregaria à base inteira justamente o torneio que ninguém olhou ainda, que é o
            // contrário do que a aprovação existe pra fazer.
            //
            // Em compensação o organizador PRECISA saber que está esperando: sem esta frase
            // ele publica, não vê o torneio na listagem e conclui que quebrou. O recado vai
            // grudado no que já havia (Ranking RS, pacote de registro), nunca no lugar dele.
            var esperandoOk = " O torneio já está de pé e você pode compartilhar o link agora "
                + "mesmo — ele aparece na listagem e no aviso pra galera assim que a gente "
                + "conferir, o que costuma ser rápido.";

            TempData["Sucesso"] = TempData["Sucesso"] is string jaDito
                ? jaDito + esperandoOk
                : "Torneio criado!" + esperandoOk;

            // Avisa quem aprova. Sem isto o pedido ficaria esperando alguém lembrar de abrir o
            // painel — e o organizador esperando um OK que ninguém sabe que precisa dar.
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

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // Autocomplete pra achar quem vai organizar o torneio. Usa a mesma busca do resto
        // do sistema (nome, apelido ou CPF completo, sem diferenciar maiúsculas) — antes
        // aqui era CPF parcial + login exato em maiúsculas, uma terceira regra própria.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BuscarJogadorParaOrganizador(string termo)
        {
            if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3) return Json(Array.Empty<object>());

            var achados = await BuscaJogador.BuscarAsync(_context, termo, limite: 8);

            return Json(achados.Select(j => new
            {
                j.Id,
                j.Nome,
                j.FotoPerfil,
                j.Login,
                apelido = j.Apelido ?? "",
                // A tela mostra uma linha só: o apelido quando existir, senão o nome.
                exibicao = j.ComoChamar,
            }));
        }

        // Preenche sozinho os dados do jogador na tela de inscrição quando o CPF digitado já
        // existe. Exige CPF completo de propósito: com busca parcial daria pra varrer a base
        // e colher nome/celular/cidade de terceiros.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BuscarJogadorPorCpf(string cpf, int? categoriaId = null)
        {
            cpf = Documentos.SomenteDigitos(cpf);

            // CPF que não fecha o dígito verificador é dito na hora. Antes ele caía no mesmo
            // "não encontrado" de um CPF novo e legítimo, e a tela respondia "preencha os
            // dados abaixo" — convidando a pessoa a cadastrar alguém com CPF inventado, que é
            // exatamente como nasceu o jogador chamado "." no torneio real.
            if (!Documentos.CpfEhValido(cpf))
            {
                return Json(new { encontrado = false, cpfInvalido = Documentos.CpfTemFormatoValido(cpf) });
            }

            var jogador = await _context.Jogadores
                .Where(j => j.Cpf == cpf)
                .Select(j => new { j.Id, j.Nome, j.Apelido, j.Celular, j.Cidade, j.Estado })
                .FirstOrDefaultAsync();

            if (jogador == null) return Json(new { encontrado = false });

            // A categoria mais FORTE que a pessoa marcou no próprio cadastro (Preferências).
            // Serve pro aviso da inscrição: quem se diz 4ª e entra na 5ª ouve a pergunta antes
            // de confirmar. Não é trava — a trava por histórico é outra (RestricaoCategoria).
            var declaradas = await _context.JogadorCategorias
                .Where(c => c.JogadorId == jogador.Id)
                .Select(c => c.CategoriaPadrao.Nome)
                .ToListAsync();

            var maisForte = declaradas
                .OrderByDescending(EstatisticasService.OrdemCategoria)
                .FirstOrDefault();

            // Ele já está inscrito NESTA categoria? A tela avisa na hora em que o CPF é
            // digitado — antes valia o silêncio, e a pessoa só descobria depois de já existir
            // uma inscrição repetida. Sozinho vira oferta de juntar; com dupla fechada, um
            // não. Ver Services/InscricaoRepetida.
            var repetidas = categoriaId is int cat
                ? await InscricaoRepetida.ProcurarAsync(_context, cat, new[] { jogador.Id })
                : new List<InscricaoRepetida.Achado>();

            var solo = InscricaoRepetida.QuePodemSerJuntadas(repetidas).FirstOrDefault();
            var fechada = repetidas.FirstOrDefault(r => r.Situacao == InscricaoRepetida.Situacao.ComParceiro);

            return Json(new
            {
                encontrado = true,
                id = jogador.Id,
                nome = jogador.Nome,
                // A tela mostra "achamos: Fulano" — o apelido confirma que é quem se pensa.
                apelido = jogador.Apelido ?? "",
                celular = jogador.Celular ?? "",
                cidade = jogador.Cidade ?? "",
                estado = jogador.Estado ?? "",
                categoriaDeclarada = maisForte ?? "",
                forcaDeclarada = EstatisticasService.OrdemCategoria(maisForte),
                jaInscritoSozinho = solo != null,
                jaInscritoComParceiro = fechada != null,
                parceiroAtual = fechada?.NomeDoParceiro ?? "",
            });
        }

        // Adiciona um co-organizador já cadastrado (achado por CPF/Login) na aba "Gerenciar Torneio"
        // ── Categorias de um torneio já publicado ─────────────────────────────────────────
        // Na criação a escolha é livre (ninguém inscrito). Depois, cada categoria pode ter
        // gente dentro — as regras de quando dá pra tirar moram em Services/CategoriasDoTorneio.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarCategoria(int torneioId, int categoriaPadraoId, int? limiteDuplas)
        {
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // `Ativa: false` é categoria tirada de circulação — vale onde já está, mas não
            // entra em torneio novo, nem por aqui.
            var catPadrao = await _context.CategoriasPadrao.FindAsync(categoriaPadraoId);
            if (catPadrao is not { Ativa: true })
            {
                TempData["Erro"] = "Categoria inválida.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Casa pelo NOME, que é o que o jogador vê na tela: duas linhas com o mesmo nome
            // dividiriam os inscritos entre si e ninguém saberia em qual entrar.
            bool jaTem = await _context.Categorias
                .AnyAsync(c => c.TorneioId == torneioId && c.Nome == catPadrao.Nome);

            if (jaTem)
            {
                TempData["Erro"] = $"O torneio já tem a categoria \"{catPadrao.Nome}\".";
                return RedirectToAction("Details", new { id = torneioId });
            }

            _context.Categorias.Add(new Categoria
            {
                TorneioId = torneioId,
                Nome = catPadrao.Nome,
                Codigo = catPadrao.Codigo,
                LimiteDuplas = limiteDuplas is > 0 ? limiteDuplas : null,
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Categoria \"{catPadrao.Nome}\" adicionada.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverCategoria(int categoriaId)
        {
            var categoria = await _context.Categorias
                .Include(c => c.Duplas)
                .Include(c => c.Torneio)
                .FirstOrDefaultAsync(c => c.Id == categoriaId);
            if (categoria == null) return NotFound();

            int torneioId = categoria.TorneioId;
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var motivo = CategoriasDoTorneio.MotivoParaNaoRemover(categoria, categoria.Torneio);
            if (motivo == null)
            {
                var quantasSobram = await _context.Categorias.CountAsync(c => c.TorneioId == torneioId) - 1;
                motivo = CategoriasDoTorneio.MotivoParaNaoFicarSemNenhuma(quantasSobram);
            }

            if (motivo != null)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var nome = categoria.Nome;
            _context.Categorias.Remove(categoria);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Categoria \"{nome}\" removida.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // O limite de vagas muda sozinho, sem precisar tirar e pôr a categoria de volta —
        // "abriu mais uma quadra, cabem mais 4 duplas" é o ajuste mais comum do dia.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarLimiteCategoria(int categoriaId, int? limiteDuplas)
        {
            var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId);
            if (categoria == null) return NotFound();

            if (!await EhOrganizadorAsync(categoria.TorneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            categoria.LimiteDuplas = limiteDuplas is > 0 ? limiteDuplas : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = categoria.LimiteDuplas == null
                ? $"\"{categoria.Nome}\" ficou sem limite de vagas."
                : $"\"{categoria.Nome}\" agora aceita {categoria.LimiteDuplas} dupla(s).";
            return RedirectToAction("Details", new { id = categoria.TorneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarOrganizador(int torneioId, int jogadorId)
        {
            var chamadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, chamadorId)) return Forbid();

            if (!await EhOrganizadorAsync(torneioId, jogadorId))
            {
                _context.TorneioOrganizadores.Add(new TorneioOrganizador
                {
                    TorneioId = torneioId,
                    JogadorId = jogadorId,
                    NivelAcesso = "Organizador"
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = torneioId });
        }
        // Aba "Gerenciar Torneio": edita os dados do torneio já criado (inclusive trocar a capa)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            // `localTorneio` não é mais lido: o local do torneio É o clube (ver abaixo). Fica na
            // assinatura só pra aba aberta antes deste deploy não estourar por parâmetro extra.
            int id, string nome, string? localTorneio, DateTime? dataInicio, decimal precoInscricao, int clubeId,
            int quantidadeQuadras, string[]? nomesQuadras,
            bool permiteImpedimentos, bool permiteImpedimentoSextaNoite, bool permiteImpedimentoSabadoManha, bool permiteImpedimentoSabadoTarde,
            string? restricaoCategoria,
            IFormFile? capa,
            // Opcionais com valor padrão: assim um formulário antigo (aba aberta antes deste
            // deploy) continua salvando o resto em vez de estourar por parâmetro faltando.
            string? chavePixOrganizador = null, string? recadoAosInscritos = null,
            DateTime? previsaoEncerramentoInscricoes = null, DateTime? previsaoChaveamento = null,
            bool usaCheckIn = false,
            bool restrito = false, string? chaveAcessoEscolhida = null, bool oculto = false,
            // Formato das partidas. Nulo = aba antiga (sem os campos) ou campo em branco:
            // nesse caso o que está gravado FICA, em vez de virar zero e deixar a Mesa sem
            // limite nenhum.
            int? setsFaseGrupos = null, int? gamesFaseGrupos = null,
            int? setsFaseMataMata = null, int? gamesFaseMataMata = null,
            int? setsFaseFinal = null, int? gamesFaseFinal = null,
            int? tempoPrevistoPartidaMinutos = null, bool semHorarioPrevisto = false,
            // Validação pelo Ranking RS. As duas listas andam em par, posição a posição:
            // rankingCategoriaId[i] é a categoria DAQUI e rankingRsId[i] é a do ranking
            // (0 = "não validar esta"). Par de arrays, e não dicionário, porque é o que o
            // binder do ASP.NET monta a partir de campos repetidos num formulário comum.
            bool validarPeloRankingRs = false,
            int[]? rankingCategoriaId = null, int[]? rankingRsId = null)
        {
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (LimitesDeTexto.Problema(nome, LimitesDeTexto.NomeDeTorneio, "O nome do torneio") is { } nomeLongo)
            {
                TempData["Erro"] = nomeLongo;
                return RedirectToAction("Details", new { id });
            }

            if (precoInscricao < 0)
            {
                TempData["Erro"] = "Valor negativo não dá: use zero pra não cobrar nada.";
                return RedirectToAction("Details", new { id });
            }

            // A chave é conferida ANTES de qualquer gravação: recusar depois deixaria o
            // torneio salvo pela metade, com o resto dos campos já alterados.
            if (restrito && ChaveDeAcessoDoTorneio.ProblemaCom(chaveAcessoEscolhida) is { } problemaChave)
            {
                TempData["Erro"] = problemaChave;
                return RedirectToAction("Details", new { id });
            }

            // Formato das partidas. Zero ou negativo é recusado ANTES de gravar: com zero
            // games a Mesa de Controle não deixaria marcar nem um ponto, e o organizador só
            // descobriria com a quadra ocupada.
            var formatoPedido = new[] {
                setsFaseGrupos, gamesFaseGrupos, setsFaseMataMata,
                gamesFaseMataMata, setsFaseFinal, gamesFaseFinal };
            if (formatoPedido.Any(v => v is <= 0))
            {
                TempData["Erro"] = "Sets e games precisam ser pelo menos 1.";
                return RedirectToAction("Details", new { id });
            }

            // Duração zerada faria a grade marcar todos os jogos no mesmo minuto. Quem não
            // quer horário nenhum tem a chave própria pra isso, logo abaixo.
            if (tempoPrevistoPartidaMinutos is <= 0)
            {
                TempData["Erro"] = "A duração de cada jogo precisa ser de pelo menos 1 minuto. "
                                 + "Pra jogar sem hora marcada, use \"Sem horário previsto\".";
                return RedirectToAction("Details", new { id });
            }

            torneio.Nome = nome;
            torneio.DataInicio = dataInicio;
            torneio.PrecoInscricao = precoInscricao;
            torneio.ClubeId = clubeId;

            // Nulo = campo não veio (aba antiga em cache) — mantém o que já estava gravado.
            torneio.SetsFaseGrupos = setsFaseGrupos ?? torneio.SetsFaseGrupos;
            torneio.GamesFaseGrupos = gamesFaseGrupos ?? torneio.GamesFaseGrupos;
            torneio.SetsFaseMataMata = setsFaseMataMata ?? torneio.SetsFaseMataMata;
            torneio.GamesFaseMataMata = gamesFaseMataMata ?? torneio.GamesFaseMataMata;
            torneio.SetsFaseFinal = setsFaseFinal ?? torneio.SetsFaseFinal;
            torneio.GamesFaseFinal = gamesFaseFinal ?? torneio.GamesFaseFinal;
            torneio.TempoPrevistoPartidaMinutos = tempoPrevistoPartidaMinutos ?? torneio.TempoPrevistoPartidaMinutos;
            torneio.SemHorarioPrevisto = semHorarioPrevisto;

            // O local É o clube. A tela de edição pedia os dois — o mesmo dado duas vezes — e
            // eles podiam divergir: o cabeçalho do torneio mostra o LocalTorneio, então dava
            // pra anunciar um lugar e ter o torneio marcado em outro. O nome do clube manda.
            //
            // O parâmetro `localTorneio` continua na assinatura só pra aba antiga em cache não
            // estourar; o valor dela é ignorado de propósito.
            var clubeEscolhido = await _context.Clubes.FindAsync(clubeId);
            if (clubeEscolhido != null) torneio.LocalTorneio = clubeEscolhido.Nome;
            torneio.QuantidadeQuadras = quantidadeQuadras;
            torneio.PermiteImpedimentos = permiteImpedimentos;
            torneio.RestricaoCategoria = string.IsNullOrEmpty(restricaoCategoria) ? "Livre" : restricaoCategoria;
            torneio.ValidarPeloRankingRs = validarPeloRankingRs;
            await SalvarDeParaDoRankingAsync(id, rankingCategoriaId, rankingRsId);
            torneio.PermiteImpedimentoSextaNoite = permiteImpedimentoSextaNoite;
            torneio.PermiteImpedimentoSabadoManha = permiteImpedimentoSabadoManha;
            torneio.PermiteImpedimentoSabadoTarde = permiteImpedimentoSabadoTarde;

            // Campo em branco vira null, não string vazia: é a diferença entre "o organizador
            // não pôs recado" e "pôs um recado vazio", e a tela decide o que mostrar por isso.
            torneio.ChavePixOrganizador = string.IsNullOrWhiteSpace(chavePixOrganizador) ? null : chavePixOrganizador.Trim();
            torneio.RecadoAosInscritos = string.IsNullOrWhiteSpace(recadoAosInscritos) ? null : recadoAosInscritos.Trim();
            torneio.PrevisaoEncerramentoInscricoes = previsaoEncerramentoInscricoes;
            torneio.PrevisaoChaveamento = previsaoChaveamento;
            // Caixa desmarcada não vai no POST, então o `false` do parâmetro é o "desliguei".
            torneio.UsaCheckIn = usaCheckIn;

            // ---- Torneio restrito (chave de acesso), agora editável ----
            // Só se decidia isso na criação, e "esqueci de marcar restrito" obrigava a criar
            // outro torneio e reinscrever todo mundo.
            //
            // A regra delicada é o campo VAZIO com o torneio já restrito: significa "não quis
            // mexer na chave", e não "apaga a chave". Trocar a chave sozinho aqui derrubaria
            // quem já recebeu a antiga no grupo do WhatsApp.
            if (restrito)
            {
                torneio.Restrito = true;
                var chaveDigitada = ChaveDeAcessoDoTorneio.Normalizar(chaveAcessoEscolhida);
                if (chaveDigitada != null) torneio.ChaveAcesso = chaveDigitada;
                else if (string.IsNullOrWhiteSpace(torneio.ChaveAcesso)) torneio.ChaveAcesso = ChaveDeAcessoDoTorneio.Sortear();
            }
            else
            {
                // Deixou de ser restrito: a chave sai junto. Guardá-la seria manter uma senha
                // que não tranca nada e que voltaria a valer sozinha se ele religasse.
                torneio.Restrito = false;
                torneio.ChaveAcesso = null;
            }

            // "Sumir da listagem" mora dentro do restrito, igual à criação: torneio aberto e
            // invisível é torneio que ninguém acha pra se inscrever.
            torneio.Oculto = restrito && oculto;

            if (capa != null && capa.Length > 0)
            {
                // Falhou o processamento: fica a capa antiga. Zerar aqui apagaria a arte do
                // torneio por causa de um envio que não deu certo.
                var capaSalva = await SalvarCapaAsync(capa);
                if (capaSalva.Salvou) torneio.ImagemCapa = capaSalva.Caminho;
                else if (capaSalva.DeuErro) TempData["ErroImagem"] = capaSalva.Erro;
            }

            // Reconcilia a lista de Quadras com a nova quantidade/nomes (por posição)
            var quadrasAtuais = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            string alfabetoQuadras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int quantidade = Math.Max(1, quantidadeQuadras);
            for (int i = 0; i < quantidade && i < alfabetoQuadras.Length; i++)
            {
                string? nomeInformado = nomesQuadras != null && i < nomesQuadras.Length ? nomesQuadras[i]?.Trim() : null;
                string nomeQuadra = string.IsNullOrWhiteSpace(nomeInformado) ? $"Quadra {alfabetoQuadras[i]}" : nomeInformado;
                if (i < quadrasAtuais.Count)
                {
                    quadrasAtuais[i].Nome = nomeQuadra;
                }
                else
                {
                    _context.Quadras.Add(new Quadra { TorneioId = id, Nome = nomeQuadra });
                }
            }
            if (quadrasAtuais.Count > quantidade)
            {
                _context.Quadras.RemoveRange(quadrasAtuais.Skip(quantidade));
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Dados do torneio atualizados!";
            return RedirectToAction("Details", new { id });
        }

        // PEDIR A LIBERAÇÃO DO TORNEIO OFICIAL.
        //
        // Até aqui a recusa mandava a pessoa "falar com a gente pelo WhatsApp". Funciona, e
        // vaza gente: quem decidiu criar um torneio às 23h de um domingo não abre conversa com
        // desconhecido — fecha o app. Um toque transforma a recusa numa fila, e a fila é o que
        // o admin consegue resolver em dois minutos no dia seguinte.
        //
        // ⚠️ Não mexe em permissão nenhuma: só registra que a pessoa quer. Quem libera é o
        // admin raiz, na tela dele (AdminController.Organizadores).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SolicitarPerfilDeOrganizador(string? motivo, string? voltarPara = null)
        {
            var jogador = await _context.Jogadores.FindAsync(ObterJogadorIdLogado() ?? 0);
            if (jogador == null) return Forbid();

            if (PermissaoDeOrganizador.MotivoParaNaoPedir(jogador) is { } recusa)
            {
                TempData["Erro"] = recusa;
                return VoltarDoPedido(voltarPara);
            }

            jogador.SolicitouOrganizadorEm = DateTime.Now;
            // Corta em vez de recusar: o motivo é OPCIONAL e ninguém pode perder o pedido
            // porque escreveu demais — a coluna aceita 500 e o excedente não faz falta.
            var texto = motivo?.Trim();
            jogador.MotivoDoPedidoDeOrganizador = string.IsNullOrEmpty(texto)
                ? null
                : texto.Length > 500 ? texto[..500] : texto;

            // Pedido novo limpa a recusa antiga: um "não" de meses atrás não pode fazer o
            // pedido de hoje nascer marcado como já negado na tela do admin.
            jogador.PedidoDeOrganizadorRecusadoEm = null;
            await _context.SaveChangesAsync();

            // O admin precisa saber que tem gente esperando — senão a fila só é descoberta
            // quando alguém lembra de abrir a tela. Enfileira e segue: a entrega sai por fora
            // da requisição (Services/FilaDeAvisos).
            try
            {
                var admins = await _context.Jogadores
                    .Where(j => j.IsAdminRaiz && j.ExcluidoEm == null)
                    .Select(j => j.Id)
                    .ToListAsync();

                foreach (var adminId in admins)
                {
                    await _pushService.EnviarParaJogadorAsync(adminId,
                        "Alguém quer criar torneios",
                        $"{NomeBonito.Formatar(jogador.Nome)} pediu liberação pro torneio Oficial.",
                        Url.Action("Organizadores", "Admin"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao avisar os admins do pedido de organizador de {JogadorId}.", jogador.Id);
            }

            TempData["Sucesso"] = TextoDoPedidoDeOrganizador
                .Frase(PermissaoDeOrganizador.EstadoDoPedido.Esperando);
            return VoltarDoPedido(voltarPara);
        }

        // Lista fechada de destinos: `voltarPara` vem do formulário, e campo de formulário
        // nunca vira redirecionamento pra qualquer lugar.
        private IActionResult VoltarDoPedido(string? voltarPara) =>
            voltarPara == "Create"
                ? RedirectToAction(nameof(Create))
                : RedirectToAction(nameof(Index));
    }
}
