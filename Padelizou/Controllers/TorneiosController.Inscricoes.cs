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

        // Aba "Gerenciar Torneio": muda a dupla de categoria — pra quem inscreveu na errada e
        // devia ter ido noutra, ou trocou de ideia antes do sorteio. Sem isto, a única saída
        // era RemoverDupla seguido de inscrever de novo à mão, perdendo Pago/PagoEm no meio.
        //
        // Pedido do Felipe (06/09/2026): "Crie a opção também, do organizador trocar a dupla
        // de categoria".
        //
        // ⚠️ MESMA JANELA DO SORTEIO de sempre: uma vez que existe Partida, tem gente vendo
        // contra quem joga e em qual grupo — mudar a categoria por baixo desfaria isso
        // silenciosamente. `jaSorteou` é a MESMA régua de ReabrirInscricoes/DesfazerSorteio/
        // DesfazerRodadasAmericano, e não a de RemoverDupla (que trava em "Inscrições
        // Abertas" — mais estrita do que precisa: aqui a janela vai até o sorteio de verdade).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrocarCategoriaDupla(int duplaId, int novaCategoriaId)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            // ⚠️ TEM QUE SER DO MESMO TORNEIO. A tela só lista as categorias de quem organiza
            // esta página, mas um POST feito à mão poderia mandar o id de uma categoria de
            // OUTRO torneio (até de outro organizador) — sem esta checagem a dupla mudaria de
            // torneio inteiro, não só de categoria.
            var novaCategoria = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == novaCategoriaId && c.TorneioId == torneioId);
            if (novaCategoria == null) return NotFound();

            if (novaCategoria.Id == dupla.CategoriaId)
            {
                TempData["Erro"] = "Esta dupla já está nesta categoria.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool jaSorteou = await _context.Partidas.AnyAsync(p => p.TorneioId == torneioId);
            if (jaSorteou)
            {
                TempData["Erro"] = "As chaves já foram sorteadas — não dá pra trocar de categoria agora.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // Categoria de times e de chave direta são cadastradas pelo ORGANIZADOR (jogador
            // não se inscreve) e a Dupla ali carrega suposições diferentes (NomeTime, ou a
            // mesma pessoa inscrita duas vezes no torneio) — misturar com uma categoria comum
            // bagunçaria as duas. A troca só circula dentro do mesmo "tipo" de categoria.
            if (dupla.Categoria.DeTimes != novaCategoria.DeTimes || dupla.Categoria.ChaveDireta != novaCategoria.ChaveDireta)
            {
                TempData["Erro"] = "Não dá pra trocar entre categoria de times/chave direta e categoria comum.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // A regra de sexo da categoria de DESTINO não some na troca — mesma checagem que
            // a inscrição normal usa (DuplasController.Create). Times não têm Jogador2 de
            // verdade (é o organizador cadastrando o nome do time), então a checagem não se
            // aplica — e o guard acima já garante que só entra times⇄times.
            if (!novaCategoria.DeTimes
                && SexoDoJogador.MotivoParaNaoEntrar(novaCategoria.Nome, dupla.Jogador1!, dupla.Jogador2) is { } motivoSexo)
            {
                TempData["Erro"] = motivoSexo;
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool estavaConfirmada = !dupla.EmListaDeEspera;
            int categoriaAntigaId = dupla.CategoriaId;

            // ⚠️ CONFERE A VAGA ANTES DE TOCAR NA DUPLA. Ela ainda pertence à categoria
            // ANTIGA neste ponto — a checagem não corre risco nenhum de se contar a si mesma
            // na categoria de destino, tracked ou não. Só depois disso a dupla muda de fato.
            bool novaEstaCheia = await CategoriaEstaCheiaAsync(novaCategoria);

            dupla.CategoriaId = novaCategoria.Id;
            // Recalcula do zero contra a categoria de DESTINO — mesma régua de
            // DuplasController.CategoriaOuTorneioEstaCheioAsync, só a metade da CATEGORIA: o
            // total do TORNEIO não muda numa troca dentro do mesmo torneio, então não há por
            // que conferir `LimiteDuplasTotal` de novo aqui.
            dupla.EmListaDeEspera = novaEstaCheia;
            await _context.SaveChangesAsync();

            // Sair CONFIRMADA da categoria antiga libera uma vaga de verdade lá — a fila de
            // espera DAQUELA categoria avança sozinha, mesmo comportamento de
            // RemoverDupla/Desistir (TirarDuplaDoTorneioAsync). Quem já estava na lista de
            // espera não tirava vaga de ninguém, então sair dali não move a fila de ninguém.
            if (estavaConfirmada) await PromoverDaListaDeEsperaAsync(categoriaAntigaId, torneio);

            TempData["Sucesso"] = dupla.EmListaDeEspera
                ? $"Movida pra {novaCategoria.Nome} — na lista de espera, a categoria está cheia."
                : $"Movida pra {novaCategoria.Nome}.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // Mesma régua de DuplasController.CategoriaOuTorneioEstaCheioAsync (e do gêmeo em
        // PagamentoInscricaoService), só a metade da CATEGORIA — a troca não muda o total do
        // torneio, então `LimiteDuplasTotal` não entra aqui.
        private async Task<bool> CategoriaEstaCheiaAsync(Categoria categoria)
        {
            if (!categoria.LimiteDuplas.HasValue) return false;

            int naCategoria = await _context.Duplas.CountAsync(d => d.CategoriaId == categoria.Id && !d.EmListaDeEspera);
            return naCategoria >= categoria.LimiteDuplas.Value;
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

        // ── O ORGANIZADOR troca o impedimento de outra pessoa (aba Pagamentos) ────────────
        // Pedido do Felipe (07/09/2026): na aba de gerenciar pagamentos, "o organizador pode
        // enxergar quem solicitou impedimento e pra qual horário" e "permite ele editar esse
        // impedimento". A regra mora em AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar
        // — diferente da do próprio jogador: aqui não há checagem de dono (é OUTRA pessoa, de
        // propósito), a janela vai até o sorteio (não só "Inscrições Abertas"), e uma dupla já
        // paga PODE ter o impedimento trocado — o ajuste do dinheiro fica manual, do lado dele.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarImpedimentoOrganizador(int duplaId, TurnoDoImpedimento turno)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, meuId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            bool jaSorteou = await _context.Partidas.AnyAsync(p => p.TorneioId == torneioId);
            if (AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar(dupla, torneio, jaSorteou) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", "Torneios", new { id = torneioId }, "pagamentos");
            }

            var antes = AlteracaoDeImpedimento.TurnoAtual(dupla);
            if (antes == turno)
            {
                return RedirectToAction("Details", "Torneios", new { id = torneioId }, "pagamentos");
            }

            var diferenca = AlteracaoDeImpedimento.QuantoMudaOValor(dupla, torneio, turno);
            AlteracaoDeImpedimento.Aplicar(dupla, torneio, turno, meuId, DateTime.Now);
            await _context.SaveChangesAsync();

            // Diferente do jogador (que avisa só o PARCEIRO, porque ele mesmo já sabe): aqui
            // foi o organizador quem mexeu por fora, então os DOIS da dupla precisam saber.
            var jogadoresDaDupla = new[] { dupla.Jogador1Id, dupla.Jogador2Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();
            if (jogadoresDaDupla.Count > 0)
            {
                await AvisarAsync(jogadoresDaDupla, "O organizador mudou seu impedimento",
                    $"O organizador de {torneio.Nome} trocou o impedimento de vocês: "
                    + $"de \"{AlteracaoDeImpedimento.Rotulo(antes)}\" para \"{AlteracaoDeImpedimento.Rotulo(turno)}\".",
                    torneioId);
            }

            // ⚠️ NENHUM AJUSTE DE DINHEIRO AUTOMÁTICO — decisão do Felipe (07/09/2026): ele
            // pode trocar o impedimento mesmo de quem já pagou, mas o valor não se cobra nem
            // se estorna sozinho. A mensagem é o lembrete: ele acerta manualmente, como já faz
            // com o botão de marcar pago e com o estorno (ESTORNO.md).
            TempData["Sucesso"] = diferenca switch
            {
                > 0 => $"Impedimento alterado para \"{AlteracaoDeImpedimento.Rotulo(turno)}\". "
                     + $"A inscrição passa a valer {diferenca:C} a mais"
                     + (dupla.Pago ? " — já está paga, ajuste o valor recebido." : "."),
                < 0 => $"Impedimento alterado. A inscrição passa a valer {Math.Abs(diferenca):C} a menos"
                     + (dupla.Pago ? " — já está paga, veja se cabe estorno." : "."),
                _ => $"Impedimento alterado para \"{AlteracaoDeImpedimento.Rotulo(turno)}\". O valor não muda.",
            };
            return RedirectToAction("Details", "Torneios", new { id = torneioId }, "pagamentos");
        }

        // ── SEM ELIMINATÓRIA NO SÁBADO À NOITE, POR CATEGORIA (sub-aba "Eliminatórias") ───
        // 🗣️ Pedido do Felipe (08/09/2026): "colocar por categoria, se vai ter jogos de
        // eliminatórias no sabado a noite ainda ou não. por exemplo, a 5a categoria feminina
        // nao pode ter jogo sabado a noite, ai passaria para domingo de manha".
        //
        // Mora neste arquivo, e não junto do sorteio, porque é a outra metade da MESMA aba que
        // o `AlterarImpedimentoOrganizador` logo acima serve — quem procurar "o que a aba
        // Pagamentos e impedimentos faz" acha as duas ações lado a lado. A régua da janela está
        // em Services/EliminatoriaNoSabado; quem a aplica é GradeDeJogos.Encaixar.
        //
        // ⚠️ SEM JANELA DE SORTEIO, ao contrário do impedimento: ligar/desligar isto não mexe em
        // jogo nenhum sozinho, só muda o que a PRÓXIMA montagem de grade vai respeitar. O
        // organizador que já sorteou e mudar de ideia aperta "Refazer grade", que é o botão que
        // existe justamente pra isso.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarEliminatoriaNoSabado(int categoriaId, bool permitir)
        {
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null) return NotFound();

            // ⚠️ A CHECAGEM DE DONO É SOBRE O TORNEIO DA CATEGORIA, lido do banco — nunca sobre
            // um torneioId que venha no formulário. Sem isso, quem organiza o torneio A mexeria
            // na categoria do torneio B só trocando o id no POST.
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(categoria.TorneioId, meuId)) return Forbid();

            categoria.EliminatoriaNoSabadoANoite = permitir;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = permitir
                ? $"{categoria.Nome}: as eliminatórias podem entrar no sábado à noite."
                : $"{categoria.Nome}: sem eliminatória no sábado à noite — o que não couber até "
                  + "as 18h cai no dia seguinte. Se as chaves já saíram, use \"Refazer grade\".";

            return RedirectToAction("Details", "Torneios", new { id = categoria.TorneioId }, "pagamentos");
        }

        // ── O LOCAL EXTERNO ALUGADO (sub-aba "Quadras e sedes") ───────────────────────────
        // 🗣️ Felipe, 08/09/2026: "esse do ER por exemplo, como colocou muita dupla, ele terá q
        // locar um local externo ao dele [...] vai ter q por quantos jogos vão para la, ou quais
        // horarios, quais categorias, temos que pensar nisso, e aonde colocar".
        //
        // ⚠️ METADE DISSO JÁ EXISTIA (21/08): quais clubes, qual quadra em qual clube, e a
        // categoria PRESA a um clube continuam em "Gerenciar Torneio", no formulário de edição.
        // O que nasce aqui é o que só se sabe na hora de gerar as chaves: a JANELA do lugar
        // alugado, quem PODE transbordar pra lá, e o "só um jogo por dupla lá".
        //
        // "Quantos jogos vão pra lá" NÃO virou campo: é `quadras × rodadas da janela`, e a tela
        // mostra a conta (SedesDoTorneio.JogosQueCabemNaJanela). Dois campos pra mesma
        // informação discordariam, e ninguém saberia qual mandou.

        // A janela vale pra TODAS as quadras daquele clube — é o lugar que está alugado das 8h
        // às 12h, não uma quadra dele. Nulos nos dois campos devolvem a quadra pro expediente
        // inteiro do torneio.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarJanelaDaSede(int torneioId, int? clubeId,
            DateTime? de, DateTime? ate)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, meuId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            // ⚠️ O FILTRO É POR TORNEIO **E** POR CLUBE. Sem o TorneioId, o mesmo clube alugado
            // por dois torneios no mesmo fim de semana teria a janela de um escrita no outro.
            var quadras = await _context.Quadras
                .Where(q => q.TorneioId == torneioId && q.ClubeId == clubeId)
                .ToListAsync();

            if (quadras.Count == 0)
            {
                TempData["Erro"] = "Esse clube não tem quadra nenhuma neste torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId }, "pagamentos");
            }

            foreach (var quadra in quadras)
            {
                quadra.DisponivelDe = de;
                quadra.DisponivelAte = ate;
            }
            await _context.SaveChangesAsync();

            var cabem = SedesDoTorneio.JogosQueCabemNaJanela(
                quadras.Count, de, ate, torneio.TempoPrevistoPartidaMinutos);

            TempData["Sucesso"] = cabem is int quantos
                ? $"{quadras.Count} quadra(s) disponíveis de {de:dd/MM HH:mm} a {ate:dd/MM HH:mm} — "
                  + $"cabem cerca de {quantos} jogos. Se as chaves já saíram, use \"Refazer grade\"."
                : $"{quadras.Count} quadra(s) voltaram a valer o expediente inteiro do torneio.";

            return RedirectToAction("Details", "Torneios", new { id = torneioId }, "pagamentos");
        }

        // ── ONDE CADA QUADRA FICA, E ONDE CADA CATEGORIA JOGA ────────────────────────────
        // 🗣️ Felipe, 08/09/2026: "move o editor de sedes pra aba nova".
        //
        // ⚠️ ISTO SAIU DO `Editar` (TorneiosController.Criacao) E VEIO PRA CÁ. Lá ele vivia
        // dentro do formulário gigante de gestão, com a marca `sedesInformadas` — que existia
        // só pra que um POST sem os campos não apagasse as sedes. Tela própria não precisa de
        // marca: quem manda este POST está mexendo em sede, e ninguém mais escreve nessas
        // colunas.
        //
        // ⚠️ E A QUADRA PASSOU A SER ENDEREÇADA POR **Id**, não por posição. No formulário
        // antigo o 3º campo de nome andava em par com o 3º select de clube, e isso só era
        // seguro porque os dois viajavam juntos. Separados, duas abas abertas fariam as
        // posições discordarem e o clube da quadra 3 iria parar na quadra 4 — calado. O
        // formato é o mesmo "id:clube" que a categoria já usava.
        //
        // ⚠️ A LISTA É A VERDADE INTEIRA: quadra que não vier no POST volta pro clube do
        // torneio, e categoria que não vier fica solta. A tela manda uma linha por quadra e uma
        // por categoria, então "não veio" quer dizer "é de casa" — não "não mexi". É o mesmo
        // desenho da preferência de quadra (regravar tudo em vez de comparar e ajustar), e é o
        // que faz a tela ser a verdade.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarSedesDoTorneio(int id, string[]? clubesQuadras,
            string[]? clubesCategorias, int? minutosParaTrocarDeClube)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, meuId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var quadras = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            var categorias = await _context.Categorias.Where(c => c.TorneioId == id).ToListAsync();

            // Os clubes que ESTE POST pode citar: os do catálogo, mais o clube do torneio. Um
            // id que não esteja aqui é descartado pela leitura, não recusado — mesma régua do
            // formulário antigo.
            var permitidos = (await _context.Clubes.Select(c => c.Id).ToListAsync()).ToHashSet();

            var porQuadra = SedesDoTorneio.LerClubePorQuadra(clubesQuadras, permitidos);
            var porCategoria = SedesDoTorneio.LerClubePorCategoria(clubesCategorias, permitidos);

            // ⚠️ SÓ AS CATEGORIAS DESTE TORNEIO. O valor vem do navegador, e aqui o filtro é
            // CARGA, não zelo: `clubeDaCategoria.Values` alimenta a validação logo abaixo, e uma
            // categoria de OUTRO torneio apontada pra um clube sem quadra aqui faria a recusa
            // disparar — o organizador não conseguiria salvar as próprias sedes por causa de um
            // valor que não é dele. Travado em
            // SedesNaAbaDeQuadrasTests.Categoria_de_outro_torneio_nao_bloqueia_o_salvamento_daqui.
            var idsDeCategoria = categorias.Select(c => c.Id).ToHashSet();
            var clubeDaCategoria = porCategoria.Where(p => idsDeCategoria.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value);

            // ⚠️ A QUADRA NÃO PRECISA DO MESMO FILTRO, e não é descuido: os dois laços abaixo
            // percorrem `quadras`, que já é só o deste torneio, e consultam o mapa POR Id. Um id
            // de fora simplesmente nunca casa. Um filtro aqui seria uma segunda tranca que
            // nenhum teste consegue distinguir de não existir — e guarda que não dá pra
            // falsificar é a que some no próximo refactor sem ninguém notar.
            var clubeDaQuadra = porQuadra;

            // A MESMA recusa do formulário antigo, e ela vem ANTES de gravar qualquer coisa:
            // meio salvo é pior que nada salvo. Ver Services/SedesDoTorneio.MotivoParaNaoSalvar.
            var clubeDeCadaQuadra = quadras.Select(q =>
                clubeDaQuadra.TryGetValue(q.Id, out var clube) ? clube
                : torneio.ClubeId > 0 ? torneio.ClubeId : (int?)null);

            if (SedesDoTorneio.MotivoParaNaoSalvar(clubeDeCadaQuadra, clubeDaCategoria.Values) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return RedirectToAction("Details", "Torneios", new { id }, "pagamentos");
            }

            foreach (var quadra in quadras)
            {
                // Nulo = "no clube do torneio". É o que TODA quadra de torneio de uma sede só é,
                // e é por isso que voltar pra uma sede só não precisa de conversão nenhuma.
                quadra.ClubeId = clubeDaQuadra.TryGetValue(quadra.Id, out var clube)
                                 && clube != torneio.ClubeId
                    ? clube
                    : null;
            }

            foreach (var categoria in categorias)
            {
                categoria.ClubeId = clubeDaCategoria.TryGetValue(categoria.Id, out var sede) ? sede : null;
            }

            // Negativo vira zero, e zero desliga a folga de propósito — o organizador que tem as
            // duas sedes na mesma rua não quer buraco nenhum na grade. Nulo = campo ausente, e
            // aí o que está gravado FICA.
            if (minutosParaTrocarDeClube is { } folga)
                torneio.MinutosParaTrocarDeClube = Math.Max(0, folga);

            await _context.SaveChangesAsync();

            var sedesAgora = quadras.Where(q => q.ClubeId != null).Select(q => q.ClubeId).Distinct().Count();
            TempData["Sucesso"] = sedesAgora == 0
                ? "Todas as quadras voltaram pro clube do torneio."
                : $"Sedes salvas. Vale a partir do próximo sorteio (ou do \"Refazer grade\") — "
                  + "jogo que já tem quadra não muda de lugar sozinho.";

            return RedirectToAction("Details", "Torneios", new { id }, "pagamentos");
        }

        // Esta categoria pode transbordar pro local externo? Não confundir com o clube FIXO da
        // categoria (Gerenciar Torneio), que é trava dura: aqui é a régua mole do Er — a sede
        // principal enche e o que sobra vai pro alugado.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarTransbordoDaCategoria(int categoriaId, bool permitir)
        {
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null) return NotFound();

            // Dono conferido pelo torneio DA CATEGORIA, lido do banco — nunca por um id que
            // venha no formulário. Mesma régua de AlterarEliminatoriaNoSabado.
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(categoria.TorneioId, meuId)) return Forbid();

            categoria.PodeJogarNaSedeExtra = permitir;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = permitir
                ? $"{categoria.Nome} pode jogar no local externo quando a sede principal encher."
                : $"{categoria.Nome} joga só na sede principal.";

            return RedirectToAction("Details", "Torneios", new { id = categoria.TorneioId }, "pagamentos");
        }

        // 🗣️ Felipe, 08/09/2026: "o Er também me falou, que eles não querem q a dupla jogue os 2
        // jogos la, que jogue apenas um, para que ele possa jogar no clube dele também".
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlterarEvitarDoisJogosNaSedeExtra(int id, bool evitar)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, meuId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.EvitarDoisJogosNaSedeExtra = evitar;
            await _context.SaveChangesAsync();

            // ⚠️ A mensagem diz "evita", e não "garante", de propósito: a regra CEDE quando
            // respeitá-la deixaria a quadra do lugar alugado parada. Prometer garantia numa
            // regra mole é como o organizador descobre a exceção no dia do jogo.
            TempData["Sucesso"] = evitar
                ? "A grade vai evitar mandar os 2 jogos da mesma dupla pro local externo — "
                  + "cede só se não houver outro jogo pra pôr na vaga."
                : "A dupla pode ter os 2 jogos no local externo.";

            return RedirectToAction("Details", "Torneios", new { id }, "pagamentos");
        }

        // ── RELATÓRIO EM CSV: nome, telefone, pago e impedimento (aba Pagamentos) ──────────
        // Pedido do Felipe (07/09/2026): "crie um botão com um relatório em excel, com nome
        // completo, telefone, se pagou ou não, se tem impedimento e quando". Uma linha por
        // DUPLA — mesmo recorte que a própria aba já mostra em tela, só que pra baixar.
        //
        // ⚠️ ATRÁS DE PodeVerDinheiro, e não de PodeGerenciar: é o TELEFONE que muda a régua.
        // Hoje só quem vê dinheiro tem acesso ao número de qualquer jogador — o link "Cobrar"
        // o usa por baixo pra montar o link do WhatsApp, mas nunca IMPRIME o dígito em tela pra
        // quem não vê dinheiro (mesma régua de AbaPagamentosNaPaginaDoTorneioTests). Um
        // relatório com telefone em texto puro pra qualquer ajudante exporia mais do que a
        // própria tela já expõe hoje.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> RelatorioDuplasCsv(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await PodeVerDinheiroAsync(id, meuId)) return Forbid();

            var duplas = await _context.Duplas
                .Include(d => d.Categoria)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.TorneioId == id && d.NomeTime == null)
                .OrderBy(d => d.Categoria.Nome).ThenBy(d => d.Jogador1.Nome)
                .ToListAsync();

            // Mesmo formato de PagamentosController.ExportarCsv: ponto e vírgula (o Excel
            // brasileiro abre certo de primeira) e BOM UTF-8 (sem ele, acento vira lixo).
            //
            // ⚠️ JOGADOR 1 e JOGADOR 2 EM COLUNAS SEPARADAS, cada um com seu próprio telefone —
            // não "Fulano & Sicrano" numa coluna só. O Felipe mandou a planilha que a Camila já
            // usa na mão: Jogador 1/Fone 1/Jogador 2/Fone 2, pra poder ligar pra qualquer um
            // dos dois direto da linha, sem abrir outra tela.
            static string Campo(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Categoria;Jogador 1;Fone 1;Jogador 2;Fone 2;Pago;Impedimento;Impedimento alterado em");
            foreach (var dupla in duplas)
            {
                var turno = AlteracaoDeImpedimento.TurnoAtual(dupla);
                // "Quando" só faz sentido junto de um impedimento ATUAL — uma dupla que teve
                // impedimento marcado e depois voltou pra "Nenhum" ainda carrega o registro
                // histórico da última troca, e mostrar aqui confundiria "tem impedimento hoje"
                // com "mexeu no impedimento algum dia".
                var quando = turno == TurnoDoImpedimento.Nenhum || dupla.ImpedimentoAlteradoEm == null
                    ? ""
                    : dupla.ImpedimentoAlteradoEm.Value.ToString("dd/MM/yyyy HH:mm");
                // NULO = ainda procurando parceiro (Dupla.Jogador2Id) — mesmo estado que a
                // planilha da Camila já marca à mão; uma célula vazia sem dizer o motivo
                // pareceria um dado perdido, não uma inscrição incompleta de verdade.
                sb.AppendLine(string.Join(";",
                    Campo(dupla.Categoria.Nome),
                    Campo(dupla.Jogador1.NomeNaTela),
                    Campo(WhatsAppLinkHelper.Formatar(dupla.Jogador1.Celular)),
                    Campo(dupla.Jogador2?.NomeNaTela ?? "Procurando parceiro"),
                    Campo(WhatsAppLinkHelper.Formatar(dupla.Jogador2?.Celular)),
                    torneio.PrecoInscricao > 0 ? (dupla.Pago ? "Sim" : "Não") : "-",
                    Campo(AlteracaoDeImpedimento.Rotulo(turno)),
                    quando));
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"duplas-{torneio.Codigo}-{DateTime.Now:yyyyMMdd}.csv");
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

            TempData["Sucesso"] = "Inscrições encerradas. O próximo passo é sortear os grupos e gerar as chaves.";

            // ⚠️ A HASH NÃO É ENFEITE: é ela que decide em qual aba a Details abre.
            //
            // Encerrar troca o status pra "Chaves em Sorteio", e é isso que faz `torneioComecou`
            // virar true na view. A aba Inscritos perde o `active`, a aba Jogos assume — e neste
            // exato momento ela está vazia por definição, porque o sorteio ainda não rodou. O
            // organizador acabava de fechar as inscrições e caía num "Nenhum jogo agendado",
            // com o botão de sortear a um clique dali sem nada dizendo isso.
            //
            // `#admin` é o painel de controle, que só existe pra quem gerencia (ViewBag.PodeGerenciar).
            // Quando quem chega não pode vê-lo, o script de abas não acha o alvo e sai calado —
            // a página abre na aba padrão, sem erro nenhum.
            return RedirectToAction("Details", "Torneios", new { id = torneio.Id }, fragment: "admin");
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
