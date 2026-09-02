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
    // Inscrições: inscrever pelo balcão, pago/não pago, remover dupla e encerrar as inscrições.
    public partial class TorneiosController
    {
        // Inscrição individual (Torneio Americano) — achar-ou-criar Jogador por CPF, mesmo
        // padrão de DuplasController.Create, só que sem parceiro fixo.
        // Mesma regra da inscrição em dupla: quem inscreve precisa estar logado, quem é
        // inscrito não precisa ter conta (entra como pré-cadastro e assume depois, pelo CPF).
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> InscreverIndividual(int torneioId, int categoriaId, string nome, string cpf,
            string? chaveAcesso = null, string? formaPagamentoEscolhida = null,
            // "Pagar agora" ou "pagar depois", quando o torneio aceita as duas — ver
            // Services/QuandoPagarInscricao. Nulo vale como "depois".
            string? quandoPagar = null)
        {
            // Mesma limpeza de DuplasController.Create: CPF com máscara estoura a coluna de
            // 11 chars e derruba a página em vez de avisar o jogador.
            cpf = Documentos.SomenteDigitos(cpf);
            // Dígito verificador, não só 11 números — mesma régua da inscrição em dupla.
            if (!Documentos.CpfEhValido(cpf))
            {
                TempData["Erro"] = "CPF inválido — confira os números.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // varchar(100) recusa (não corta) o que passa do tamanho: sem isto, nome comprido
            // colado da agenda do celular derrubava a inscrição com erro 500.
            if (LimitesDeTexto.Problema(nome, LimitesDeTexto.NomeDeJogador, "O nome") is { } nomeLongo)
            {
                TempData["Erro"] = nomeLongo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            // ...e que pareça um nome (ver Services/NomeDePessoa).
            nome = NomeDePessoa.Arrumar(nome);
            if (NomeDePessoa.Problema(nome, "O nome") is { } nomeEstranho)
            {
                TempData["Erro"] = nomeEstranho;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Na categoria de TIMES quem cadastra é o organizador — jogador não se inscreve
            // nela. A tela nem a oferece; isto segura o POST montado à mão.
            if (categoria.DeTimes)
            {
                TempData["Erro"] = "Essa categoria é de times — os times são cadastrados pelo organizador.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Chave direta é convite, não inscrição: as duplas são montadas pelo organizador
            // (quase sempre remontando gente que já está inscrita nas categorias normais).
            if (categoria.ChaveDireta)
            {
                TempData["Erro"] = "Essa chave é montada pelo organizador — não dá pra se inscrever nela.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // A inscrição individual é SÓ do Americano individual. No Padrão e no Americano
            // de Duplas a inscrição é em dupla — a tela nem mostra este formulário, e um POST
            // montado à mão criaria uma inscrição que nenhum sorteio lê.
            if (torneio.Formato != "Americano")
            {
                TempData["Erro"] = "Neste torneio a inscrição é em dupla.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (torneio.Restrito && !string.Equals(chaveAcesso?.Trim(), torneio.ChaveAcesso, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Chave de acesso inválida. Confira com o organizador do torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Torneio OCULTO: quem não abre a página não se inscreve por ela. Gêmeo da trava
            // em DuplasController.Create — o organizador que inscreve alguém à mão passa.
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (jogador == null)
            {
                jogador = new Jogador { Nome = nome, Cpf = cpf };
                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();
            }

            // SEM TRAVA DE NÍVEL AQUI, E É DE PROPÓSITO (decisão do Felipe, 06/08/2026).
            //
            // A inscrição em dupla passa por duas regras de categoria — a do Ranking RS e a
            // dormente RestricaoCategoria (ver Services/ValidacaoPeloRankingRs e RANKING.md).
            // O Americano não passa por nenhuma: aqui o parceiro TROCA a cada rodada e todo
            // mundo joga com todo mundo, então misturar nível é o objetivo do formato, não o
            // defeito. Além disso o rodízio precisa de número fechado de gente pra fechar as
            // rodadas, e barrar alguém no dia quebraria a montagem inteira.
            //
            // Ou seja: a ausência das checagens abaixo não é esquecimento. Não "conserte".

            // Uma categoria por jogador, quando o organizador desligou as múltiplas.
            var bloqueioCategorias = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { jogador.Id });
            if (bloqueioCategorias != null)
            {
                TempData["Erro"] = bloqueioCategorias;
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool jaInscrito = await _context.InscricoesAmericanas
                .AnyAsync(i => i.CategoriaId == categoriaId && i.JogadorId == jogador.Id);
            if (!jaInscrito)
            {
                // Torneio pago com recebimento ativado? A inscrição ainda NÃO é criada: o
                // jogador vai pro checkout e ela nasce quando o webhook confirmar o pagamento
                // (PagamentoInscricaoService.EfetivarAsync).
                var recebedor = await _pagamentos.ObterRecebedorTorneioAsync(torneioId);
                bool podeCobrar = _pagamentos.PodeCobrar(torneio, recebedor);
                // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
                // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
                if (podeCobrar && torneio.PagamentoObrigatorioNaInscricao)
                {
                    var dadosInscricao = new DadosInscricaoTorneio(
                        torneioId, categoriaId, jogador.Id, null, false, false, false, false);

                    var checkout = await _pagamentos.IniciarCobrancaTorneioAsync(
                        torneio, recebedor!, jogador, "TorneioAmericano", dadosInscricao, formaPagamentoEscolhida);

                    if (checkout != null) return Redirect(checkout);

                    TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                // Vagas: mesma regra da inscrição em dupla (ver DuplasController) — se a
                // categoria ou o torneio já estão cheios, entra na lista de espera.
                bool emListaDeEspera = false;
                if (categoria.LimiteDuplas.HasValue)
                {
                    int naCategoria = await _context.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoriaId && !i.EmListaDeEspera);
                    emListaDeEspera = naCategoria >= categoria.LimiteDuplas.Value;
                }
                if (!emListaDeEspera && torneio.LimiteDuplasTotal.HasValue)
                {
                    int noTorneio = await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == torneioId && !i.EmListaDeEspera);
                    emListaDeEspera = noTorneio >= torneio.LimiteDuplasTotal.Value;
                }

                // ⚠️ ANTES de adicionar: depois, a própria inscrição apareceria na consulta e
                // a pessoa ganharia o desconto de segunda já na primeira categoria.
                bool jaEstavaNoTorneio = await QuemJaEstaNoTorneio.EstaAsync(_context, torneioId, jogador.Id);

                var inscricaoCriada = new InscricaoAmericana
                {
                    CategoriaId = categoriaId,
                    JogadorId = jogador.Id,
                    EmListaDeEspera = emListaDeEspera,
                    // Quanto esta inscrição custou — o número que os somatórios leem depois.
                    ValorInscricao = PrecoDaInscricao.PorPessoa(torneio, jaEstavaNoTorneio),
                };
                _context.InscricoesAmericanas.Add(inscricaoCriada);
                await _context.SaveChangesAsync();

                await NotificarSeguidoresDeInscricaoAsync(torneioId, new[] { jogador.Id });

                // O "Apitouuuu!" pra quem SEGUE ESTE TORNEIO — a mesma chamada da inscrição em
                // dupla (DuplasController), pelo mesmo serviço. ⚠️ São as duas portas: se um
                // dia esta linha sumir, o americano inteiro para de apitar sem nenhum erro.
                //
                // Lista de espera fica de fora aqui também: ainda não é vaga na chave.
                if (!emListaDeEspera)
                {
                    var categoriaDaInscricao = await _context.Categorias
                        .Where(c => c.Id == categoriaId)
                        .Select(c => c.Nome)
                        .FirstOrDefaultAsync() ?? "";

                    await _avisoDeInscricao.NotificarAsync(torneioId, categoriaDaInscricao,
                        new[] { jogador.Nome }, new[] { jogador.Id },
                        Url.Action("Details", "Torneios", new { id = torneioId }));
                }

                // Mesma escolha da inscrição em dupla: a cobrança só nasce se a pessoa disse
                // que vai pagar agora, e só DEPOIS da inscrição estar gravada.
                if (QuandoPagarInscricao.VaiPagarAgora(torneio, podeCobrar, quandoPagar) && !emListaDeEspera)
                {
                    var checkoutAgora = await _pagamentos.IniciarCobrancaDeInscricaoAsync(
                        torneio, recebedor!, jogador, inscricaoDeDupla: false, impedimentos: 0,
                        new DadosPagamentoDeInscricao(torneioId, null, inscricaoCriada.Id),
                        formaPagamentoEscolhida);

                    if (checkoutAgora != null) return Redirect(checkoutAgora);

                    TempData["Erro"] = "Inscrição confirmada, mas não deu pra abrir o pagamento agora. "
                        + "Use o botão \"Pagar agora\" na tela do torneio.";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                TempData["Sucesso"] = emListaDeEspera
                    ? "Vagas esgotadas — inscrição entrou na lista de espera. Se alguém desistir, é chamado na ordem de inscrição."
                    : "Inscrição individual confirmada!";
            }
            else
            {
                TempData["Sucesso"] = "Inscrição individual confirmada!";
            }

            // Mesmo momento da inscrição em dupla: acabou de entrar no torneio, e o que ela
            // espera agora (chaves, horário) chega por aviso.
            TempData[ConviteDeInstalarApp.ChaveTempData] = ConviteDeInstalarApp.InscricaoConfirmada;
            return RedirectToAction("Details", new { id = torneioId });
        }

        // A última palavra sobre quem pagou é sempre do organizador: muita inscrição é
        // acertada em dinheiro na quadra ou por Pix direto, e o site não tem como saber.
        // Vale nos dois sentidos — marcar e desmarcar.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // `voltarPara` existe porque esta ação é chamada de DOIS lugares: da lista de gestão
        // (aba Gerenciar) e da caderneta do Financeiro. Voltar sempre pro Details fazia o
        // organizador que está conferindo o Pix perder a lista a cada marcação.
        public async Task<IActionResult> AlternarPagamentoDupla(int duplaId, string? voltarPara = null)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            dupla.Pago = !dupla.Pago;
            dupla.PagoEm = dupla.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = dupla.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction(voltarPara == "Financeiro" ? "Financeiro" : "Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPagamentoAmericano(int inscricaoId, string? voltarPara = null)
        {
            var inscricao = await _context.InscricoesAmericanas
                .Include(i => i.Categoria).FirstOrDefaultAsync(i => i.Id == inscricaoId);
            if (inscricao == null) return NotFound();

            int torneioId = inscricao.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            inscricao.Pago = !inscricao.Pago;
            inscricao.PagoEm = inscricao.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = inscricao.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction(voltarPara == "Financeiro" ? "Financeiro" : "Details", new { id = torneioId });
        }

        // Aba "Gerenciar Torneio": remove um inscrito (só enquanto as inscrições estiverem abertas —
        // depois disso já pode existir Partida referenciando a dupla)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverDupla(int duplaId)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            if (torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "Só é possível remover inscritos enquanto as inscrições estiverem abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            await TirarDuplaDoTorneioAsync(dupla, torneio,
                $"O organizador removeu sua inscrição em {torneio.Nome}. Se foi engano, fale com ele.");

            TempData["Sucesso"] = "Inscrito removido do torneio.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // O MIOLO da remoção de um inscrito, sem tela: apagar, avisar quem saiu e chamar a
        // lista de espera.
        //
        // ⚠️ Existe separado porque DOIS botões removem inscrito — este, um por um, e o
        // "remover quem não pagou" do fechamento, em lote. Duas cópias significariam,
        // inevitavelmente, uma delas esquecendo de avisar a pessoa ou de promover a fila; e a
        // pessoa que não é avisada descobre no clube, no dia do jogo.
        //
        // A `mensagem` é do chamador: "o organizador removeu" e "não foi pago até o prazo" são
        // motivos diferentes, e quem recebe merece o motivo certo.
        private async Task TirarDuplaDoTorneioAsync(Dupla dupla, Torneio torneio, string mensagem)
        {
            bool eraConfirmada = !dupla.EmListaDeEspera;
            int categoriaId = dupla.CategoriaId;
            var removidos = new[] { dupla.Jogador1Id, dupla.Jogador2Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();

            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            // Quem foi tirado precisa saber ANTES do dia do jogo. Sem isso a pessoa aparecia
            // no clube e descobria na hora que não estava mais no torneio.
            await AvisarAsync(removidos, "Você saiu do torneio", mensagem, torneio.Id);

            if (eraConfirmada) await PromoverDaListaDeEsperaAsync(categoriaId, torneio);
        }

        // ── O próprio inscrito desiste ────────────────────────────────────────────────────
        // Antes só o organizador tirava alguém, então desistir era mandar mensagem pra ele —
        // que mandava mensagem pro suporte. O jogador resolve sozinho, e só enquanto as
        // inscrições estão abertas (a regra mora em Services/DesistenciaDeInscricao).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // `escolha` só é lida quando a dupla está completa. Ela cai em `SoEu` quando o formulário
        // não manda nada, e isso é de propósito: `SoEu` é a saída que NÃO tira a vaga de
        // ninguém, então um campo perdido no caminho nunca desinscreve o parceiro por acidente.
        public async Task<IActionResult> Desistir(int duplaId, EscolhaDeQuemSai escolha = EscolhaDeQuemSai.SoEu)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var meuId = ObterJogadorIdLogado() ?? 0;

            if (DesistenciaDeInscricao.MotivoParaNaoDesistir(dupla, torneio, meuId) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var euMesmo = await _context.Jogadores.FindAsync(meuId);
            var quemFica = DesistenciaDeInscricao.QuemFica(dupla, meuId);
            bool eraConfirmada = !dupla.EmListaDeEspera;

            if (DesistenciaDeInscricao.Efeito(dupla, escolha) == EfeitoDaDesistencia.SoSaiQuemDesistiu)
            {
                // A vaga NÃO abre: o parceiro continua inscrito, agora sem dupla fechada. Ele
                // assume a cadeira de Jogador1 porque essa coluna não é anulável.
                dupla.Jogador1Id = quemFica!.Value;
                dupla.Jogador2Id = null;
                await _context.SaveChangesAsync();

                await AvisarAsync(new[] { quemFica.Value }, "Seu parceiro desistiu",
                    $"{euMesmo?.ComoChamar ?? "Seu parceiro"} saiu de {torneio!.Nome}. Sua vaga continua sua — "
                    + "escolha outro parceiro antes do sorteio das chaves.", torneioId);

                TempData["Sucesso"] = "Você saiu da dupla. Seu parceiro segue inscrito e foi avisado.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Estava sozinho, ou os dois saem juntos: a inscrição acaba e a vaga volta pra fila.
            bool eraPaga = dupla.Pago;
            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            // O parceiro não clicou em nada e mesmo assim deixou de estar inscrito. Ele PRECISA
            // saber hoje, não no dia do jogo — é o mesmo motivo pelo qual o organizador avisa
            // quem ele remove (ver TirarDuplaDoTorneioAsync).
            if (quemFica is { } parceiro)
            {
                await AvisarAsync(new[] { parceiro }, "A inscrição da dupla foi cancelada",
                    $"{euMesmo?.ComoChamar ?? "Seu parceiro"} cancelou a inscrição de vocês em {torneio!.Nome}. "
                    + "Se foi engano, dá pra se inscrever de novo enquanto as inscrições estiverem abertas.",
                    torneioId);
            }

            await AvisarOrganizadorDeSaidaPagaAsync(eraPaga, torneio!, euMesmo);

            if (eraConfirmada) await PromoverDaListaDeEsperaAsync(dupla.CategoriaId, torneio!);

            TempData["Sucesso"] = eraPaga
                ? "Sua inscrição foi cancelada. Como ela estava paga, a devolução é com o organizador — ele já foi avisado."
                : "Sua inscrição foi cancelada.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // ── Trocar o impedimento de horário ───────────────────────────────────────────────
        // 🗣️ Felipe, 02/09/2026: "permita a pessoa alterar o impedimento, até o fechamento das
        // inscrições" e "sempre que um parceiro alterar o impedimento, crie um aviso para o
        // parceiro dele". A regra (quem pode, e o que acontece com o valor) mora em
        // Services/AlteracaoDeImpedimento.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarImpedimento(int duplaId, TurnoDoImpedimento turno)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var meuId = ObterJogadorIdLogado() ?? 0;

            if (AlteracaoDeImpedimento.MotivoParaNaoAlterar(dupla, torneio, meuId, turno) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var antes = AlteracaoDeImpedimento.TurnoAtual(dupla);
            if (antes == turno)
            {
                // Nada mudou. Sair aqui evita gravar autoria e acordar o parceiro por um clique
                // que não trocou coisa nenhuma.
                return RedirectToAction("Details", new { id = torneioId });
            }

            var diferenca = AlteracaoDeImpedimento.QuantoMudaOValor(dupla, torneio!, turno);
            AlteracaoDeImpedimento.Aplicar(dupla, torneio!, turno, meuId, DateTime.Now);
            await _context.SaveChangesAsync();

            // O parceiro não clicou em nada e o horário dele mudou junto — o impedimento é da
            // INSCRIÇÃO, não de quem marcou. Sem este aviso ele descobre no dia do jogo.
            var euMesmo = await _context.Jogadores.FindAsync(meuId);
            var parceiro = dupla.Jogador1Id == meuId ? dupla.Jogador2Id : dupla.Jogador1Id;
            if (parceiro is { } outro)
            {
                await AvisarAsync(new[] { outro }, "O impedimento da dupla mudou",
                    $"{euMesmo?.ComoChamar ?? "Seu parceiro"} trocou o impedimento de vocês em {torneio!.Nome}: "
                    + $"de \"{AlteracaoDeImpedimento.Rotulo(antes)}\" para \"{AlteracaoDeImpedimento.Rotulo(turno)}\". "
                    + "Se não era pra ser, dá pra trocar de volta enquanto as inscrições estiverem abertas.",
                    torneioId);
            }

            TempData["Sucesso"] = diferenca switch
            {
                > 0 => $"Impedimento alterado para \"{AlteracaoDeImpedimento.Rotulo(turno)}\". "
                     + $"Foram somados {diferenca:C} à sua inscrição.",
                < 0 => $"Impedimento retirado. Sua inscrição diminuiu {Math.Abs(diferenca):C}.",
                _ => $"Impedimento alterado para \"{AlteracaoDeImpedimento.Rotulo(turno)}\". O valor não muda.",
            };
            return RedirectToAction("Details", new { id = torneioId });
        }

        // ── O inscrito do Americano desiste ───────────────────────────────────────────────
        // Mesma porta do Desistir, pra quem se inscreveu num Torneio Americano. Ela existe
        // separada porque a inscrição de Americano é individual e vive em outra tabela — não
        // há parceiro, então também não há o que perguntar: sair é sempre a inscrição inteira.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DesistirDoAmericano(int inscricaoId)
        {
            var inscricao = await _context.InscricoesAmericanas
                .Include(i => i.Categoria)
                .FirstOrDefaultAsync(i => i.Id == inscricaoId);
            if (inscricao == null) return NotFound();

            int torneioId = inscricao.Categoria.TorneioId;
            var torneio = await _context.Torneios.FindAsync(torneioId);
            var meuId = ObterJogadorIdLogado() ?? 0;

            if (DesistenciaDeInscricao.MotivoParaNaoDesistirDoAmericano(inscricao, torneio, meuId) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var euMesmo = await _context.Jogadores.FindAsync(meuId);
            bool eraConfirmada = !inscricao.EmListaDeEspera;
            bool eraPaga = inscricao.Pago;
            int categoriaId = inscricao.CategoriaId;

            _context.InscricoesAmericanas.Remove(inscricao);
            await _context.SaveChangesAsync();

            await AvisarOrganizadorDeSaidaPagaAsync(eraPaga, torneio!, euMesmo);

            if (eraConfirmada) await PromoverDaListaDeEsperaAsync(categoriaId, torneio!);

            TempData["Sucesso"] = eraPaga
                ? "Sua inscrição foi cancelada. Como ela estava paga, a devolução é com o organizador — ele já foi avisado."
                : "Sua inscrição foi cancelada.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // ⚠️ O dinheiro NÃO volta sozinho, e isso é desenho, não esquecimento: estornar é botão
        // do organizador, na tela de pagamentos dele (ver ESTORNO.md). O buraco era outro — ele
        // não ficava sabendo que alguém pago tinha saído, então a devolução dependia de ele
        // reparar numa vaga a menos na lista. Este aviso é a ponte entre as duas metades.
        private async Task AvisarOrganizadorDeSaidaPagaAsync(bool eraPaga, Torneio torneio, Jogador? quemSaiu)
        {
            if (!eraPaga) return;

            var organizadores = await _context.TorneioOrganizadores
                .Where(o => o.TorneioId == torneio.Id)
                .Select(o => o.JogadorId)
                .ToListAsync();

            await AvisarAsync(organizadores, "Saiu do torneio uma inscrição PAGA",
                $"{quemSaiu?.ComoChamar ?? "Um inscrito"} cancelou a inscrição em {torneio.Nome}, e ela estava paga. "
                + "O estorno não é automático: se for o caso de devolver, faça em Pagamentos → Meus.",
                // ⚠️ `SoApp`, e não o WhatsApp — decisão do Felipe em 01/09/2026, revertendo a
                // minha. Este aviso passa nos três critérios do canal (pessoal, urgente,
                // acionável) e o volume é ridículo, mas a família de torneio SAIU do canal em
                // 21/08 e "só mais este" é exatamente como ela voltaria inteira. Quem decide o
                // que entra ali é ele; `TorneioNaoVaiProWhatsAppTests` guarda a porta.
                torneio.Id);
        }

        // Abriu vaga: promove quem está há mais tempo na lista de espera desta categoria (a
        // inscrição de menor Id, já que a ordem de inscrição segue a ordem de criação) — e AVISA.
        //
        // Sem o aviso, ser promovido era um segredo entre o sistema e o banco: a dupla saía da
        // espera e só descobria olhando a página por conta própria. Quem entra na lista de
        // espera justamente não fica olhando.
        //
        // ⚠️ CHECA InscricoesAmericanas TAMBÉM (achado de 21/08/2026). Até então só olhava
        // Duplas — numa categoria de Americano a fila de espera nunca era chamada: quem
        // esperava, esperava pra sempre, mesmo com vaga aberta. Uma categoria só tem um dos
        // dois tipos (o Formato do torneio decide), então checar Duplas primeiro e cair pra
        // InscricoesAmericanas quando não achar ninguém é seguro — nunca promove dos dois ao
        // mesmo tempo por engano.
        private async Task PromoverDaListaDeEsperaAsync(int categoriaId, Torneio torneio)
        {
            var proximaDaFila = await _context.Duplas
                .Where(d => d.CategoriaId == categoriaId && d.EmListaDeEspera)
                .OrderBy(d => d.Id)
                .FirstOrDefaultAsync();

            if (proximaDaFila != null)
            {
                proximaDaFila.EmListaDeEspera = false;
                await _context.SaveChangesAsync();

                var promovidos = new[] { proximaDaFila.Jogador1Id, proximaDaFila.Jogador2Id }
                    .Where(i => i != null).Select(i => i!.Value).ToList();

                await AvisarPromocaoAsync(promovidos, torneio);
                return;
            }

            var proximaAmericana = await _context.InscricoesAmericanas
                .Where(i => i.CategoriaId == categoriaId && i.EmListaDeEspera)
                .OrderBy(i => i.Id)
                .FirstOrDefaultAsync();

            if (proximaAmericana == null) return;

            proximaAmericana.EmListaDeEspera = false;
            await _context.SaveChangesAsync();

            await AvisarPromocaoAsync(new[] { proximaAmericana.JogadorId }, torneio);
        }

        // Mesmo evento que o da desistência com pagamento, e por isso o mesmo alcance: a
        // vaga abrir por estorno ou por desistência direta é diferença nossa, não de quem
        // estava na fila esperando. ⚠️ O gêmeo mora em PagamentoInscricaoService — mexeu
        // aqui, mexe lá.
        //
        // Fora do WhatsApp desde 21/08/2026, com o resto da família de torneio (ver
        // Services/EncerramentoDaPartida).
        private async Task AvisarPromocaoAsync(IEnumerable<int> promovidos, Torneio torneio) =>
            await AvisarAsync(promovidos, "Abriu vaga — vocês estão dentro!",
                $"Alguém desistiu de {torneio.Nome} e vocês saíram da lista de espera. Boa sorte!",
                torneio.Id, AlcanceDoAviso.SoApp);

        // Push falha calado (quem não instalou o app não recebe nada), então o aviso que
        // importa vai também por e-mail — é o que a maioria tem.
        private async Task AvisarAsync(IEnumerable<int> jogadorIds, string titulo, string corpo, int torneioId,
            AlcanceDoAviso alcance = AlcanceDoAviso.SoApp)
        {
            var url = Url.Action("Details", "Torneios", new { id = torneioId });

            foreach (var jogadorId in jogadorIds)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId, titulo, corpo, url, alcance);
                }
                catch (Exception ex)
                {
                    // Aviso é acessório: a inscrição já mudou, não pode falhar por causa disso.
                    _logger.LogWarning(ex, "Falha ao avisar o jogador {JogadorId}.", jogadorId);
                }
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EncerrarInscricoes(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);

            // Verifica se o torneio existe
            if (torneio == null) return NotFound();

            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // Torneio cancelado não volta a andar por aqui. A tela já não mostra o botão, mas
            // aba velha e POST feito à mão ressuscitariam o torneio em "Chaves em Sorteio" —
            // com os inscritos já avisados de que ele não vai acontecer. A recusa (e a frase)
            // moram em PortaDaInscricao, que é a mesma régua que a tela lê.
            if (PortaDaInscricao.PorQueNaoPodeFechar(torneio) is { } naoFecha)
            {
                TempData["Erro"] = naoFecha;
                return RedirectToAction("Details", new { id });
            }

            torneio.Status = PortaDaInscricao.Fechada;
            await _context.SaveChangesAsync();

            // Último momento em que dá pra resolver: quem está sem parceiro ainda pode fechar
            // a dupla antes do sorteio. Sem este aviso, a pessoa só descobria que ficou de
            // fora quando a chave saía — e aí não havia mais o que fazer.
            var semParceiro = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == id && d.Jogador2Id == null && !d.EmListaDeEspera)
                .Select(d => d.Jogador1Id)
                .ToListAsync();

            if (semParceiro.Count > 0)
            {
                await AvisarAsync(semParceiro, "Você ainda está sem parceiro",
                    $"As inscrições de {torneio.Nome} foram encerradas e sua dupla não está fechada. "
                    + "Sem parceiro, vocês ficam de fora do sorteio — defina alguém na página do torneio.",
                    torneio.Id);
            }

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // ---- "Pagar agora" de uma inscrição que já existe ----
        //
        // O par que faltava do torneio que GARANTE A VAGA e cobra depois
        // (`PagamentoObrigatorioNaInscricao == false`). Sem ele, a combinação "cobra pelo site
        // + a vaga não depende do pagamento" não levava a lugar nenhum: os dois únicos
        // caminhos que criam cobrança eram os de inscrição, então quem já estava dentro NUNCA
        // conseguia pagar pelo app — o torneio cobrava pelo site só no nome.
        //
        // Achado no NATA PADEL TOUR, quando o Felipe se inscreveu e não apareceu pagamento.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PagarInscricao(
            int torneioId, int? duplaId, int? inscricaoAmericanaId, string? formaPagamentoEscolhida = null)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (jogadorId <= 0) return Forbid();

            IActionResult Recusar(string motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            var recebedor = await _pagamentos.ObterRecebedorTorneioAsync(torneioId);
            if (!_pagamentos.PodeCobrar(torneio, recebedor))
                return Recusar("Este torneio não está cobrando pelo site.");

            // ⚠️ A trava de QUEM pode pagar O QUÊ. Sem ela, um id trocado na mão pagaria (e
            // marcaria como paga) a inscrição de outra pessoa — e o dinheiro sairia do bolso
            // de quem clicou.
            bool ehDupla = duplaId.HasValue;
            bool inscricaoDeDupla;
            int impedimentos;
            DadosPagamentoDeInscricao dados;

            if (ehDupla)
            {
                var dupla = await _context.Duplas
                    .Include(d => d.Categoria)
                    .FirstOrDefaultAsync(d => d.Id == duplaId!.Value);

                if (dupla == null || dupla.Categoria.TorneioId != torneioId)
                    return Recusar("Inscrição não encontrada neste torneio.");

                if (dupla.Jogador1Id != jogadorId && dupla.Jogador2Id != jogadorId)
                    return Recusar("Só quem está nesta inscrição pode pagá-la.");

                if (dupla.Pago) return Recusar("Esta inscrição já está paga.");

                inscricaoDeDupla = true;
                impedimentos = (dupla.ImpedimentoSextaNoite ? 1 : 0)
                             + (dupla.ImpedimentoSabadoManha ? 1 : 0)
                             + (dupla.ImpedimentoSabadoTarde ? 1 : 0);
                dados = new DadosPagamentoDeInscricao(torneioId, dupla.Id, null);
            }
            else if (inscricaoAmericanaId is int americanaId)
            {
                var inscricao = await _context.InscricoesAmericanas
                    .Include(i => i.Categoria)
                    .FirstOrDefaultAsync(i => i.Id == americanaId);

                if (inscricao == null || inscricao.Categoria.TorneioId != torneioId)
                    return Recusar("Inscrição não encontrada neste torneio.");

                if (inscricao.JogadorId != jogadorId)
                    return Recusar("Só quem está nesta inscrição pode pagá-la.");

                if (inscricao.Pago) return Recusar("Esta inscrição já está paga.");

                inscricaoDeDupla = false;
                impedimentos = 0;
                dados = new DadosPagamentoDeInscricao(torneioId, null, inscricao.Id);
            }
            else
            {
                return Recusar("Não sei qual inscrição pagar.");
            }

            var pagador = await _context.Jogadores.FindAsync(jogadorId);
            if (pagador == null) return Forbid();

            var checkout = await _pagamentos.IniciarCobrancaDeInscricaoAsync(
                torneio, recebedor!, pagador, inscricaoDeDupla, impedimentos, dados,
                formaPagamentoEscolhida);

            if (checkout != null) return Redirect(checkout);

            return Recusar("Não foi possível gerar a cobrança agora. Tente novamente em instantes.");
        }

        // ---- Reabrir as inscrições ----
        //
        // O par que faltava do EncerrarInscricoes, que era de mão única: um clique e nunca
        // mais. O organizador que fechou cedo demais — ou antes mesmo de anunciar — só tinha
        // a saída errada de apagar o torneio e criar outro, perdendo o link já compartilhado.
        //
        // Aconteceu com o NATA PADEL TOUR (id 22 de produção): parado em "Chaves em Sorteio"
        // com ninguém inscrito, sem caminho de volta.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReabrirInscricoes(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // ⚠️ "Já sorteou" é PARTIDA existindo, não o nome da fase: é a partida que a
            // jogadora vê na tela e que diz contra quem ela joga. A tela esconde o botão, mas
            // a recusa mora aqui porque POST feito à mão passaria por cima dela.
            bool jaSorteou = await _context.Partidas.AnyAsync(p => p.TorneioId == id);

            if (PortaDaInscricao.PorQueNaoPodeAbrir(torneio, jaSorteou) is { } naoAbre)
            {
                TempData["Erro"] = naoAbre;
                return RedirectToAction("Details", new { id });
            }

            torneio.Status = PortaDaInscricao.Aberta;
            await _context.SaveChangesAsync();

            // Sem aviso à base, de propósito: reabrir não é evento novo pra quem não estava
            // olhando, e o torneio já foi anunciado uma vez. Quem manda aviso pra base é a
            // APROVAÇÃO do torneio — mandar de novo aqui seria um segundo push pelo mesmo
            // evento, e cada clique de fechar/abrir viraria mais um.
            TempData["Sucesso"] = "Inscrições reabertas — o formulário voltou a aceitar gente.";
            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // ---- Cancelar o torneio ----
        //
        // Choveu, o clube perdeu a quadra, não deu gente. Sem este botão o organizador só
        // tinha saídas erradas: deixar o torneio pendurado em "Inscrições Abertas" pra sempre,
        // ou marcá-lo como finalizado — o que faria o sistema procurar campeão onde não houve
        // um jogo sequer. As regras moram em Services/CancelamentoDoTorneio.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarTorneio(int id, string? motivo = null)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (CancelamentoDoTorneio.MotivoParaNaoCancelar(torneio.Status) is { } recusa)
            {
                TempData["Erro"] = recusa;
                return RedirectToAction("Details", new { id });
            }

            // Quem avisar é lido ANTES de mudar o status: depois o torneio some das listagens,
            // e a consulta dos inscritos passaria a depender de uma tela que já não os mostra.
            var inscritos = await InscritosDoTorneioAsync(id);

            torneio.Status = CancelamentoDoTorneio.Status;
            torneio.MotivoCancelamento = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            torneio.CanceladoEm = DateTime.Now;
            await _context.SaveChangesAsync();

            // Cancelamento é o aviso mais caro de NÃO chegar: a pessoa sai de casa e vai pra
            // quadra à toa. Mesmo assim saiu do WhatsApp em 21/08/2026, com o resto da família
            // de torneio (ver Services/EncerramentoDaPartida) — o e-mail continua indo, e é ele
            // que alcança quem não instalou o app.
            await AvisarAsync(inscritos, "Torneio cancelado",
                CancelamentoDoTorneio.RecadoParaInscritos(torneio.Nome, torneio.MotivoCancelamento),
                torneio.Id, AlcanceDoAviso.SoApp);

            TempData["Sucesso"] = $"Torneio cancelado. {inscritos.Count} inscrito(s) avisado(s). "
                + "Ele sai da listagem, mas continua na sua lista de torneios — e quem pagou "
                + "aparece na aba de gestão, pra você devolver.";
            return RedirectToAction("Details", new { id });
        }

        // Todo mundo que está dentro do torneio, dos dois formatos: dupla (Padrão) e inscrição
        // individual (Americano). Distinct porque a mesma pessoa pode estar em mais de uma
        // categoria — e receber o mesmo aviso duas vezes é o tipo de detalhe que faz a pessoa
        // desligar as notificações.
        private async Task<List<int>> InscritosDoTorneioAsync(int torneioId)
        {
            // ⚠️ Traz os PARES e achata depois: `SelectMany` montando array não tem tradução
            // pra SQL, e o erro só aparece rodando (o compilador aceita numa boa).
            var paresDeDuplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId && d.NomeTime == null)
                .Select(d => new { d.Jogador1Id, d.Jogador2Id })
                .ToListAsync();

            var deDuplas = paresDeDuplas
                .SelectMany(p => new int?[] { p.Jogador1Id, p.Jogador2Id })
                .ToList();

            var deAmericano = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == torneioId)
                .Select(i => (int?)i.JogadorId)
                .ToListAsync();

            return deDuplas.Concat(deAmericano)
                .Where(i => i != null)
                .Select(i => i!.Value)
                .Distinct()
                .ToList();
        }

        // ---- A taxa dos 5% do torneio "por fora" (Services/TaxaDoTorneioExterno) ----

    }
}
