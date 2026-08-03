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
            string? chaveAcesso = null, string? formaPagamentoEscolhida = null)
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

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (torneio.Restrito && !string.Equals(chaveAcesso?.Trim(), torneio.ChaveAcesso, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Chave de acesso inválida. Confira com o organizador do torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (jogador == null)
            {
                jogador = new Jogador { Nome = nome, Cpf = cpf };
                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();
            }

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
                // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
            // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
            if (_pagamentos.PodeCobrar(torneio, recebedor) && torneio.PagamentoObrigatorioNaInscricao)
                {
                    var dadosInscricao = new DadosInscricaoTorneio(
                        torneioId, categoriaId, jogador.Id, null, false, false, false);

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

                _context.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoriaId, JogadorId = jogador.Id, EmListaDeEspera = emListaDeEspera });
                await _context.SaveChangesAsync();

                await NotificarSeguidoresDeInscricaoAsync(torneioId, new[] { jogador.Id });

                TempData["Sucesso"] = emListaDeEspera
                    ? "Vagas esgotadas — inscrição entrou na lista de espera. Se alguém desistir, é chamado na ordem de inscrição."
                    : "Inscrição individual confirmada!";
            }
            else
            {
                TempData["Sucesso"] = "Inscrição individual confirmada!";
            }
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

            bool eraConfirmada = !dupla.EmListaDeEspera;
            var removidos = new[] { dupla.Jogador1Id, dupla.Jogador2Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();

            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            // Quem foi tirado precisa saber ANTES do dia do jogo. Sem isso a pessoa aparecia
            // no clube e descobria na hora que não estava mais no torneio.
            await AvisarAsync(removidos, "Você saiu do torneio",
                $"O organizador removeu sua inscrição em {torneio.Nome}. Se foi engano, fale com ele.",
                torneioId);

            if (eraConfirmada) await PromoverDaListaDeEsperaAsync(dupla.CategoriaId, torneio);

            TempData["Sucesso"] = "Inscrito removido do torneio.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // ── O próprio inscrito desiste ────────────────────────────────────────────────────
        // Antes só o organizador tirava alguém, então desistir era mandar mensagem pra ele —
        // que mandava mensagem pro suporte. O jogador resolve sozinho, e só enquanto as
        // inscrições estão abertas (a regra mora em Services/DesistenciaDeInscricao).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Desistir(int duplaId)
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

            if (DesistenciaDeInscricao.Efeito(dupla) == EfeitoDaDesistencia.SoSaiQuemDesistiu)
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

            // Estava sozinho: a inscrição acaba e a vaga volta pra fila.
            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            if (eraConfirmada) await PromoverDaListaDeEsperaAsync(dupla.CategoriaId, torneio!);

            TempData["Sucesso"] = "Sua inscrição foi cancelada.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // Abriu vaga: promove quem está há mais tempo na lista de espera desta categoria (a
        // dupla de menor Id, já que a ordem de inscrição segue a ordem de criação) — e AVISA.
        //
        // Sem o aviso, ser promovido era um segredo entre o sistema e o banco: a dupla saía da
        // espera e só descobria olhando a página por conta própria. Quem entra na lista de
        // espera justamente não fica olhando.
        private async Task PromoverDaListaDeEsperaAsync(int categoriaId, Torneio torneio)
        {
            var proximaDaFila = await _context.Duplas
                .Where(d => d.CategoriaId == categoriaId && d.EmListaDeEspera)
                .OrderBy(d => d.Id)
                .FirstOrDefaultAsync();

            if (proximaDaFila == null) return;

            proximaDaFila.EmListaDeEspera = false;
            await _context.SaveChangesAsync();

            var promovidos = new[] { proximaDaFila.Jogador1Id, proximaDaFila.Jogador2Id }
                .Where(i => i != null).Select(i => i!.Value).ToList();

            await AvisarAsync(promovidos, "Abriu vaga — vocês estão dentro!",
                $"Alguém desistiu de {torneio.Nome} e vocês saíram da lista de espera. Boa sorte!",
                torneio.Id);
        }

        // Push falha calado (quem não instalou o app não recebe nada), então o aviso que
        // importa vai também por e-mail — é o que a maioria tem.
        private async Task AvisarAsync(IEnumerable<int> jogadorIds, string titulo, string corpo, int torneioId)
        {
            var url = Url.Action("Details", "Torneios", new { id = torneioId });

            foreach (var jogadorId in jogadorIds)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId, titulo, corpo, url);
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

            torneio.Status = "Chaves em Sorteio";
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

        // ---- A taxa dos 5% do torneio "por fora" (Services/TaxaDoTorneioExterno) ----

    }
}
