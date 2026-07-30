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
            // Busca todas as categorias do banco para montar os Checkboxes na tela
            var catalogo = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
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
            return View(new Torneio());
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
            bool querRegistroDeResultados = false, string? observacoesRegistro = null)
        {
            // O clube pode ser escrito na hora: numa base nova não existe nenhum, e um select
            // obrigatório e vazio impediria de criar o primeiro torneio.
            var clubeNovo = await CatalogoLocais.AcharOuCriarClubeAsync(_context, novoClubeNome);
            if (clubeNovo != null) torneio.ClubeId = clubeNovo.Id;

            // Sem clube o insert estouraria na chave estrangeira, com erro 500 e o formulário
            // inteiro perdido. Melhor recusar aqui, explicando o que fazer.
            if (torneio.ClubeId <= 0)
            {
                ViewBag.Erro = "Escolha o clube responsável, ou escreva o nome dele no campo abaixo do seletor.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                return View(torneio);
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
            torneio.ChaveAcesso = torneio.Restrito ? GerarChaveAcesso() : null;

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

            // Quem criou o torneio já entra como organizador dele
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
            if (categoriasSelecionadas != null && categoriasSelecionadas.Length > 0)
            {
                foreach (var catId in categoriasSelecionadas)
                {
                    var catPadrao = await _context.CategoriasPadrao.FindAsync(catId);
                    if (catPadrao != null)
                    {
                        int? limite = limiteCategoria != null && limiteCategoria.TryGetValue(catId, out var l) && l is > 0 ? l : null;
                        var novaCategoria = new Categoria
                        {
                            TorneioId = torneio.Id,
                            Nome = catPadrao.Nome,
                            Codigo = catPadrao.Codigo,
                            LimiteDuplas = limite
                        };
                        _context.Categorias.Add(novaCategoria);
                    }
                }
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

            // Avisa quem tem NotificarTorneiosAbertos marcado que um torneio novo abriu.
            var elegiveis = await _context.Jogadores.Where(j => j.NotificarTorneiosAbertos).ToListAsync();
            var urlTorneio = Url.Action("Details", "Torneios", new { id = torneio.Id });

            foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
            {
                try
                {
                    await _emailService.EnviarAsync(jogador.Email!, jogador.Nome,
                        "Novo torneio aberto - Padelizou",
                        $"<p>Olá {jogador.Nome},</p><p>Um novo torneio acabou de abrir: <strong>{torneio.Nome}</strong>.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de torneio aberto {TorneioId} para jogador {JogadorId}", torneio.Id, jogador.Id);
                }
            }

            foreach (var jogador in elegiveis)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogador.Id, "Novo torneio aberto", torneio.Nome, urlTorneio);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de torneio aberto {TorneioId} para jogador {JogadorId}", torneio.Id, jogador.Id);
                }
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
        public async Task<IActionResult> BuscarJogadorPorCpf(string cpf)
        {
            cpf = Documentos.SomenteDigitos(cpf);
            if (!Documentos.CpfTemFormatoValido(cpf)) return Json(new { encontrado = false });

            var jogador = await _context.Jogadores
                .Where(j => j.Cpf == cpf)
                .Select(j => new { j.Nome, j.Apelido, j.Celular, j.Cidade, j.Estado })
                .FirstOrDefaultAsync();

            if (jogador == null) return Json(new { encontrado = false });

            return Json(new
            {
                encontrado = true,
                nome = jogador.Nome,
                // A tela mostra "achamos: Fulano" — o apelido confirma que é quem se pensa.
                apelido = jogador.Apelido ?? "",
                celular = jogador.Celular ?? "",
                cidade = jogador.Cidade ?? "",
                estado = jogador.Estado ?? ""
            });
        }

        // Adiciona um co-organizador já cadastrado (achado por CPF/Login) na aba "Gerenciar Torneio"
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
            int id, string nome, string? localTorneio, DateTime? dataInicio, decimal precoInscricao, int clubeId,
            int quantidadeQuadras, string[]? nomesQuadras,
            bool permiteImpedimentos, bool permiteImpedimentoSextaNoite, bool permiteImpedimentoSabadoManha, bool permiteImpedimentoSabadoTarde,
            string? restricaoCategoria,
            IFormFile? capa)
        {
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.Nome = nome;
            torneio.LocalTorneio = localTorneio;
            torneio.DataInicio = dataInicio;
            torneio.PrecoInscricao = precoInscricao;
            torneio.ClubeId = clubeId;
            torneio.QuantidadeQuadras = quantidadeQuadras;
            torneio.PermiteImpedimentos = permiteImpedimentos;
            torneio.RestricaoCategoria = string.IsNullOrEmpty(restricaoCategoria) ? "Livre" : restricaoCategoria;
            torneio.PermiteImpedimentoSextaNoite = permiteImpedimentoSextaNoite;
            torneio.PermiteImpedimentoSabadoManha = permiteImpedimentoSabadoManha;
            torneio.PermiteImpedimentoSabadoTarde = permiteImpedimentoSabadoTarde;

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

    }
}
