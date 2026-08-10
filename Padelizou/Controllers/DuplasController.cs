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
        // Sem IEmailService de propósito: e-mail daqui sai pela FilaDeAvisos, junto do push
        // (EnviarParaJogadorAsync enfileira os dois canais). O SMTP inline foi o que deixou a
        // inscrição lenta a ponto de gente clicar três vezes no evento de teste.
        private readonly IPushNotificationService _pushService;
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly ValidacaoPeloRankingRs _rankingRs;
        private readonly ILogger<DuplasController> _logger;

        public DuplasController(DbPadelContext context, IEstatisticasService estatisticas,
            IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, ValidacaoPeloRankingRs rankingRs,
            ILogger<DuplasController> logger)
        {
            _context = context;
            _estatisticas = estatisticas;
            _pushService = pushService;
            _pagamentos = pagamentos;
            _rankingRs = rankingRs;
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

            // Só ENFILEIRA: a FilaDeAvisos entrega por fora da requisição. O e-mail inline que
            // morava aqui saía em dobro (a fila já cobre o canal) e foi um dos motivos de a
            // inscrição demorar a ponto de gente clicar três vezes.
            //
            // ⚠️ SEM E-MAIL desde 09/08/2026 (decisão do Felipe, cortando volume depois de a
            // cota do Gmail estourar): que um amigo se inscreveu é bilhete social — bom de ver,
            // não pede resposta, não tem hora pra ser lido. E é exatamente o tipo de e-mail que
            // faz a pessoa marcar o remetente como lixo, e aí ela perde junto o aviso de que a
            // chave saiu. Push e caixa de entrada seguem iguais; ali o aviso não custa cota.
            //
            // ⚠️ A cópia gêmea deste gancho está em TorneiosController — mudar aqui só resolve
            // metade, porque a inscrição individual passa pela outra.
            foreach (var grupo in seguidores.GroupBy(s => s.SeguidorId))
            {
                var seguidor = grupo.First().Seguidor;
                var nomesQueSigo = grupo.Select(s => jogadores.TryGetValue(s.SeguidoId, out var nome) ? nome : "").Where(n => n != "");
                var titulo = "Alguém que você segue se inscreveu num torneio";
                var corpo = $"{string.Join(" e ", nomesQueSigo)} se inscreveu em {torneio.Nome}.";

                await _pushService.EnviarParaJogadorAsync(seguidor.Id, titulo, corpo, url,
                    AlcanceDoAviso.AppSemEmail);
            }
        }

        // Quantos impedimentos esta inscrição tem. Hoje é sempre 0 ou 1 (ver
        // Services/ImpedimentoUnico), mas a conta é a soma mesmo assim: o dia em que o
        // organizador puder marcar dois, o preço acompanha sozinho.
        private static int ImpedimentosDa(Dupla dupla) =>
            (dupla.ImpedimentoQuintaNoite ? 1 : 0)
            + (dupla.ImpedimentoSextaNoite ? 1 : 0)
            + (dupla.ImpedimentoSabadoManha ? 1 : 0)
            + (dupla.ImpedimentoSabadoTarde ? 1 : 0);

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
            bool impQuintaNoite, bool impSextaNoite, bool impSabadoManha, bool impSabadoTarde,
            bool semParceiro = false, bool ignorarBloqueio = false, string? chaveAcesso = null,
            // "Ele já está inscrito sozinho — pode juntar." Vem marcado na tela, depois de o
            // aviso aparecer (ver Services/InscricaoRepetida). Sem isto, o servidor recusa e
            // repete a pergunta, porque juntar APAGA a inscrição do outro.
            bool juntarComInscricaoSolo = false,
            // Forma que o jogador declarou no checkout. Só é perguntada quando o organizador
            // abriu todas as formas — é ela que decide a taxa (ver CobrancaDoTorneio).
            string? formaPagamentoEscolhida = null,
            // "Pagar agora" ou "pagar depois", quando o torneio aceita as duas — ver
            // Services/QuandoPagarInscricao. Nulo vale como "depois", que é o lado seguro:
            // nenhuma cobrança nasce sem alguém ter pedido.
            string? quandoPagar = null,
            // Parceiro escolhido pelo NOME, na lista de sugestões — quem já tem conta é
            // achado por aqui, e ninguém precisa saber o CPF dele pra inscrever a dupla.
            int? jogador2Id = null)
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
            (impQuintaNoite, impSextaNoite, impSabadoManha, impSabadoTarde) =
                ImpedimentoUnico.Apenas(impQuintaNoite, impSextaNoite, impSabadoManha, impSabadoTarde);

            // PARCEIRO ESCOLHIDO PELA LISTA DE NOMES. O CPF de terceiro não sai do servidor
            // (a busca por nome devolve só Id, nome e foto — Services/BuscaJogador), então
            // quem chega por aqui traz o Id e o resto vem do cadastro dele.
            //
            // Preencher cpf2/nome2 aqui, e não mais adiante, é de propósito: daqui pra baixo
            // tudo continua sendo a mesma inscrição por CPF de sempre — a checagem de CPF
            // válido, a de inscrição repetida, o "juntar com a inscrição solo". Um caminho
            // paralelo teria que repetir as quatro, e é assim que duas telas divergem.
            if (jogador2Id is int idParceiro && !semParceiro)
            {
                var escolhido = await _context.Jogadores
                    .Where(j => j.Id == idParceiro)
                    .Select(j => new { j.Cpf, j.Nome })
                    .FirstOrDefaultAsync();

                if (escolhido == null)
                {
                    TempData["Erro"] = "Não encontrei esse parceiro. Escolha de novo na lista ou informe o CPF.";
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
                }

                cpf2 = escolhido.Cpf;
                nome2 = escolhido.Nome;
            }

            // Marcou "ainda não tenho parceiro"? Então tudo do jogador 2 é ignorado — mesmo
            // que o formulário tenha mandado algo preenchido antes de o check ser marcado.
            if (semParceiro)
            {
                nome2 = null; cpf2 = ""; celular2 = null; cidade2 = null; estado2 = null;
            }

            // CPF com dígito verificador, não só 11 números. Foi por aqui que entrou no
            // torneio real um jogador chamado "." com um CPF inventado — e CPF errado é pior
            // do que parece: é por ele que o parceiro sem conta assume o próprio cadastro
            // depois. Errado, o histórico fica preso num fantasma.
            if (!Documentos.CpfEhValido(cpf1) || (!semParceiro && !Documentos.CpfEhValido(cpf2)))
            {
                TempData["Erro"] = "CPF inválido — confira os números. "
                    + (semParceiro ? "" : "Se for o do parceiro e você não tiver, marque \"ainda não tenho parceiro\" e feche a dupla depois pelo link de convite.");
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // Nome que pareça nome. O `required` do HTML aceita um ponto, e foi exatamente um
            // ponto que alguém digitou pra passar do campo do parceiro que não sabia preencher.
            nome1 = NomeDePessoa.Arrumar(nome1);
            nome2 = semParceiro ? null : NomeDePessoa.Arrumar(nome2);

            var nomeEstranho = NomeDePessoa.Problema(nome1, "O nome do jogador 1")
                               ?? (semParceiro ? null : NomeDePessoa.Problema(nome2, "O nome do parceiro"));
            if (nomeEstranho != null)
            {
                TempData["Erro"] = nomeEstranho;
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

            // 2a. A MESMA pergunta, feita pro Ranking RS (mundodoatleta.com.br), quando o
            //     organizador ligou a validação neste torneio. A regra acima olha só o
            //     histórico dentro do Padelizou; esta olha o ranking gaúcho, onde a pessoa
            //     pode ter pontuado numa categoria mais forte sem nunca ter jogado aqui.
            //
            //     ⚠️ Reprovar aqui NÃO é a palavra final: a recusa vira linha em
            //     BloqueioDoRanking e o organizador decide depois se ela fica de pé. E ranking
            //     fora do ar nunca vira recusa — ver Services/ValidacaoPeloRankingRs.
            if (!(ignorarBloqueio && await UsuarioEhOrganizadorAsync(torneioId)))
            {
                var pessoas = new List<ValidacaoPeloRankingRs.Pessoa> { new(nome1, cpf1) };
                if (!semParceiro) pessoas.Add(new(nome2!, cpf2));

                var barrado = await _rankingRs.MotivoDeRecusaAsync(torneio, categoria, pessoas);
                if (barrado != null)
                {
                    TempData["Erro"] = barrado;
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
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

            // 2b². MISTA e CASAIS são de um homem e uma mulher (Felipe, 08/08/2026).
            //
            //      Vem DEPOIS do achar-ou-criar dos jogadores porque precisa dos dois objetos
            //      — inclusive do parceiro que acabou de nascer como pré-cadastro, que é
            //      justamente quem costuma estar sem o dado.
            //
            //      A recusa por "não informou" e a por "mesmo sexo" são frases diferentes de
            //      propósito: uma pede uma ação, a outra explica um impedimento. Ver
            //      Services/SexoDoJogador.
            if (SexoDoJogador.MotivoParaNaoEntrar(categoria.Nome, jogador1!, jogador2) is { } motivoSexo)
            {
                TempData["Erro"] = motivoSexo;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // 2c. O MESMO jogador duas vezes na mesma categoria. Aconteceu de verdade: o
            //     Otávio ficou como parceiro de um e, logo abaixo, sozinho procurando
            //     parceiro — duas vagas pra uma pessoa só, e uma dupla fantasma no sorteio.
            //
            //     Quando a inscrição que já existe é SOLO, ela é justamente a que esta veio
            //     substituir: o certo é oferecer juntar, não dar um "não" seco. A regra e as
            //     frases moram em Services/InscricaoRepetida.
            var repetidas = await InscricaoRepetida.ProcurarAsync(_context, categoriaId, idsExistentes);

            var recusa = InscricaoRepetida.MotivoParaRecusar(repetidas);
            if (recusa != null)
            {
                TempData["Erro"] = recusa;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var juntaveis = InscricaoRepetida.QuePodemSerJuntadas(repetidas);
            if (juntaveis.Count > 0 && !juntarComInscricaoSolo)
            {
                // Sem a confirmação não se apaga nada: quem chegou por aba velha (ou por POST
                // montado à mão) tem que ver a pergunta antes.
                TempData["Erro"] = InscricaoRepetida.PerguntaParaJuntar(juntaveis)
                    + " Marque \"juntar com a inscrição que já existe\" na tela de inscrição e envie de novo.";
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
            bool podeCobrar = _pagamentos.PodeCobrar(torneio, recebedor);
            // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
            // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
            if (podeCobrar && torneio.PagamentoObrigatorioNaInscricao)
            {
                // Juntar com a inscrição solo NÃO vale aqui. A dupla só nasce quando o
                // webhook confirma o pagamento, e apagar a inscrição do outro agora deixaria
                // ele sem nada caso o checkout fosse abandonado — que é o desfecho mais
                // comum de um checkout.
                if (juntaveis.Count > 0)
                {
                    TempData["Erro"] = InscricaoRepetida.PerguntaParaJuntar(juntaveis)
                        + " Como este torneio cobra pelo site, primeiro desista da inscrição "
                        + "sozinha e depois faça a inscrição da dupla — assim ninguém fica sem "
                        + "vaga se o pagamento não for concluído.";
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
                }

                // SemParceiro marca que isto é uma DUPLA aberta, não um americano — os dois
                // chegam aqui com Jogador2Id nulo (ver DadosInscricaoTorneio).
                var dadosInscricao = new DadosInscricaoTorneio(
                    torneioId, categoriaId, jogador1.Id, jogador2?.Id,
                    impQuintaNoite, impSextaNoite, impSabadoManha, impSabadoTarde,
                    SemParceiro: semParceiro);

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
            // ⚠️ QUEM JÁ ESTAVA no torneio se pergunta ANTES de adicionar esta inscrição:
            // depois, a própria pessoa apareceria na consulta e ganharia o desconto de segunda
            // inscrição já na primeira.
            var jaNoTorneio = await QuemJaEstaNoTorneio.DentreAsync(
                _context, torneioId, new[] { jogador1.Id, jogador2?.Id ?? 0 });

            // ⚠️ SÓ ENTRA NA CONTA QUEM ESTÁ NA INSCRIÇÃO (Felipe, 08/08/2026). Dupla ainda
            // "procurando parceiro" custa UMA pessoa: cobrar duas seria cobrar por alguém que
            // não foi definido — a mesma régua que TaxaDoTorneioExterno já usava na base da
            // taxa. Quando o parceiro entrar, o valor é recalculado (ver TrocarParceiro).
            //
            // Quem repete no torneio paga o preço de segunda; o parceiro que ainda não existe
            // não entra aqui de jeito nenhum.
            var quemPaga = jogador2 == null
                ? new[] { jaNoTorneio.Contains(jogador1.Id) }
                : new[] { jaNoTorneio.Contains(jogador1.Id), jaNoTorneio.Contains(jogador2.Id) };

            var dupla = new Dupla
            {
                CategoriaId = categoriaId,
                Jogador1Id = jogador1.Id,
                Jogador2Id = jogador2?.Id,   // nulo = ainda procurando parceiro
                ImpedimentoQuintaNoite = impQuintaNoite,
                ImpedimentoSextaNoite = impSextaNoite,
                ImpedimentoSabadoManha = impSabadoManha,
                ImpedimentoSabadoTarde = impSabadoTarde,
                EmListaDeEspera = emListaDeEspera,
                // Quanto ESTA inscrição custa, gravado agora: é o número que os somatórios de
                // dinheiro leem depois, e o único que sabe quem pagou o preço de segunda.
                ValorInscricao = PrecoDaInscricao.Total(
                    torneio, quemPaga,
                    (impSextaNoite ? 1 : 0) + (impSabadoManha ? 1 : 0) + (impSabadoTarde ? 1 : 0)),
            };

            _context.Duplas.Add(dupla);

            // As inscrições sozinhas que esta veio substituir saem JUNTO, no mesmo
            // SaveChanges: apagar antes deixaria a pessoa sem vaga nenhuma se a criação
            // falhasse no meio.
            if (juntaveis.Count > 0)
            {
                var idsParaSair = juntaveis.Select(j => j.DuplaId).Distinct().ToList();
                var solosParaSair = await _context.Duplas.Where(d => idsParaSair.Contains(d.Id)).ToListAsync();
                _context.Duplas.RemoveRange(solosParaSair);
            }

            await _context.SaveChangesAsync(); // Inscrição finalizada!

            var inscritos = jogador2 == null
                ? new[] { jogador1.Id }
                : new[] { jogador1.Id, jogador2.Id };

            await NotificarSeguidoresDeInscricaoAsync(torneioId, inscritos);
            await NotificarInscricaoConfirmadaAsync(torneio, categoria.Nome, inscritos, emListaDeEspera);

            // ── "QUERO PAGAR AGORA" ──────────────────────────────────────────────────────────
            // A cobrança nasce AQUI, e não lá em cima: a inscrição já está gravada, então um
            // checkout abandonado deixa a pessoa inscrita e devendo — não a apaga. É a diferença
            // inteira entre este caminho e o "só confirmo depois de pago".
            //
            // ⚠️ Lista de espera não gera cobrança: a vaga ainda não é dela, e cobrar por uma
            // vaga que talvez não exista é o pior desfecho possível — daria trabalho de estorno
            // pro organizador e sensação de golpe pro jogador.
            if (QuandoPagarInscricao.VaiPagarAgora(torneio, podeCobrar, quandoPagar) && !emListaDeEspera)
            {
                var checkoutAgora = await _pagamentos.IniciarCobrancaDeInscricaoAsync(
                    torneio, recebedor!, jogador1, inscricaoDeDupla: true,
                    impedimentos: (impSextaNoite ? 1 : 0) + (impSabadoManha ? 1 : 0) + (impSabadoTarde ? 1 : 0),
                    new DadosPagamentoDeInscricao(torneioId, dupla.Id, null),
                    formaPagamentoEscolhida);

                if (checkoutAgora != null) return Redirect(checkoutAgora);

                // Falhou o gateway, mas a INSCRIÇÃO está feita — e é isso que a mensagem
                // precisa deixar claro, senão a pessoa tenta se inscrever de novo.
                TempData["Erro"] = "Inscrição confirmada, mas não deu pra abrir o pagamento agora. "
                    + "Use o botão \"Pagar agora\" na tela do torneio.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var juntadas = juntaveis.Count > 0
                ? $" {(juntaveis.Count > 1 ? "As inscrições sozinhas saíram" : "A inscrição sozinha saiu")} — agora é uma só."
                : "";

            TempData["Sucesso"] = (emListaDeEspera
                ? "Vagas esgotadas — sua inscrição entrou na lista de espera. Se alguém desistir, você é chamado na ordem de inscrição."
                : jogador2 == null
                    ? "Inscrição confirmada! Você está sem parceiro — defina o parceiro pela tela do torneio quando encontrar alguém."
                    : "Inscrição confirmada com sucesso!") + juntadas;
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }

        // Define ou troca o parceiro de uma inscrição já feita. Qualquer um dos dois
        // integrantes pode fazer isso (e o organizador também), a qualquer momento — quem
        // sai é avisado, quem entra também.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> TrocarParceiro(int duplaId, string cpfNovoParceiro, string? nomeNovoParceiro,
            // "Ele já está inscrito sozinho — pode juntar." Marcado na tela depois do aviso.
            bool juntarComInscricaoSolo = false)
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

            // Time não tem parceiro. E aqui a recusa não é formalidade: o time é gravado como
            // Dupla com Jogador1Id = ORGANIZADOR (ver TorneiosController.Times), então nada
            // aqui estouraria — a troca simplesmente penduraria um jogador na linha do time,
            // sem erro nenhum, quebrando a premissa de que nenhuma regra de jogador enxerga
            // essa linha. Quem monta a lista de times é o organizador, na tela de times.
            if (dupla.EhTime)
            {
                TempData["Erro"] = "Time não tem parceiro — a lista de times se altera em \"Gerenciar times e estrutura\".";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

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
            if (!Documentos.CpfEhValido(cpf))
            {
                TempData["Erro"] = "CPF inválido — confira os números do novo parceiro.";
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

                // Mesma régua da inscrição: nome que pareça nome (ver Services/NomeDePessoa).
                var nomeArrumado = NomeDePessoa.Arrumar(nomeNovoParceiro);
                var problemaNoNome = NomeDePessoa.Problema(nomeArrumado, "O nome do parceiro");
                if (problemaNoNome != null)
                {
                    TempData["Erro"] = problemaNoNome;
                    return RedirectToAction("Details", "Torneios", new { id = torneioId });
                }

                novo = new Jogador { Nome = nomeArrumado, Cpf = cpf };
                _context.Jogadores.Add(novo);
                await _context.SaveChangesAsync();
            }

            if (novo.Id == dupla.Jogador2Id)
            {
                TempData["Sucesso"] = $"{novo.Nome} já é o parceiro desta inscrição.";
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            var impedimento = await MotivoParaNaoSerParceiroAsync(dupla, torneio, novo, juntarComInscricaoSolo);
            if (impedimento != null)
            {
                TempData["Erro"] = impedimento;
                return RedirectToAction("Details", "Torneios", new { id = torneioId });
            }

            // A inscrição sozinha dele, se existir, sai junto: é ela que esta troca veio
            // substituir, e deixá-la de pé é o defeito que se está corrigindo.
            var absorvidas = juntarComInscricaoSolo
                ? InscricaoRepetida.QuePodemSerJuntadas(
                    await InscricaoRepetida.ProcurarAsync(_context, dupla.CategoriaId, new[] { novo.Id }, ignorarDuplaId: dupla.Id))
                : new List<InscricaoRepetida.Achado>();

            var antigo = dupla.Jogador2;

            // ⚠️ A conta do parceiro é feita ANTES de gravá-lo na dupla. Depois, esta própria
            // inscrição já o coloca "no torneio", e ele ganharia o preço de segunda inscrição
            // por causa de si mesmo.
            var precisaCobrarOSegundo = antigo == null;
            var segundoRepete = precisaCobrarOSegundo
                && await QuemJaEstaNoTorneio.EstaAsync(_context, torneio.Id, novo.Id);

            dupla.Jogador2Id = novo.Id;

            // A dupla estava SOZINHA e ganhou o segundo nome: o valor da inscrição passa a
            // contar duas pessoas — com TETO, porque quem se inscreveu antes de 08/08/2026 já
            // pagou pelos dois. Ver PrecoDaInscricao.AoEntrarOParceiro.
            //
            // ⚠️ ISTO NÃO COBRA NINGUÉM: só corrige o número que os somatórios de dinheiro, a
            // devolução e a base da taxa leem. A diferença é acertada com o organizador.
            if (precisaCobrarOSegundo && dupla.ValorInscricao is decimal valorAntes)
            {
                dupla.ValorInscricao = PrecoDaInscricao.AoEntrarOParceiro(
                    torneio, valorAntes, segundoRepete, ImpedimentosDa(dupla));
            }

            if (absorvidas.Count > 0)
            {
                var ids = absorvidas.Select(a => a.DuplaId).Distinct().ToList();
                _context.Duplas.RemoveRange(await _context.Duplas.Where(d => ids.Contains(d.Id)).ToListAsync());
            }

            await _context.SaveChangesAsync();

            await AvisarTrocaDeParceiroAsync(dupla, torneio, antigo, novo);

            var juntou = absorvidas.Count > 0 ? " A inscrição sozinha dele saiu — agora é uma só." : "";
            TempData["Sucesso"] = (antigo == null
                ? $"Parceiro definido: {novo.Nome}. Sua dupla está completa!"
                : $"Parceiro alterado de {antigo.Nome} para {novo.Nome}.") + juntou;
            return RedirectToAction("Details", "Torneios", new { id = torneioId });
        }

        // As regras que impedem alguém de entrar nesta dupla, num lugar só: valem tanto pra
        // quem é escolhido pelo CPF (TrocarParceiro) quanto pra quem aceita um convite.
        // Separar as duas cópias deixaria o caminho do convite mais frouxo que o outro — e
        // é justamente o caminho aberto por link, o que qualquer um alcança.
        // Devolve a mensagem do impedimento, ou null quando pode entrar.
        private async Task<string?> MotivoParaNaoSerParceiroAsync(
            Dupla dupla, Torneio torneio, Jogador candidato, bool juntarComInscricaoSolo = false)
        {
            // Não pode já estar em outra dupla desta MESMA categoria. Mas "já está inscrito"
            // tem dois sabores muito diferentes (ver Services/InscricaoRepetida): com dupla
            // fechada é um não; SOZINHO é justamente a inscrição que esta troca vem
            // substituir, e aí a resposta certa é perguntar se junta.
            var repetidas = await InscricaoRepetida.ProcurarAsync(
                _context, dupla.CategoriaId, new[] { candidato.Id }, ignorarDuplaId: dupla.Id);

            var recusa = InscricaoRepetida.MotivoParaRecusar(repetidas);
            if (recusa != null) return recusa;

            var juntaveis = InscricaoRepetida.QuePodemSerJuntadas(repetidas);
            if (juntaveis.Count > 0 && !juntarComInscricaoSolo)
            {
                return InscricaoRepetida.PerguntaParaJuntar(juntaveis)
                     + " Marque \"juntar com a inscrição que já existe\" e confirme de novo.";
            }

            // ...nem violar a regra de uma categoria por jogador (ignorando esta categoria,
            // onde a dupla já está inscrita).
            var bloqueio = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { candidato.Id }, ignorarCategoriaId: dupla.CategoriaId);
            if (bloqueio != null) return bloqueio;

            // Mista e Casais são de um homem e uma mulher — e ESTA é a porta que a checagem
            // não podia deixar de fora: sem ela, bastava entrar sozinho na categoria e trocar
            // o parceiro depois pra furar a regra inteira. É o mesmo motivo que já traz o
            // Ranking RS pra cá.
            //
            // O par é o candidato com quem FICA na dupla — quando o titular é o próprio
            // candidato (troca do parceiro 2), quem fica é o Jogador1.
            var quemFica = dupla.Jogador1Id == candidato.Id ? dupla.Jogador2 : dupla.Jogador1;
            if (SexoDoJogador.MotivoParaNaoEntrar(dupla.Categoria.Nome, candidato, quemFica) is { } motivoSexo)
                return motivoSexo;

            // Anti-sandbagging pelo histórico DENTRO do Padelizou: o parceiro precisa poder
            // jogar nesta categoria.
            if (!string.IsNullOrEmpty(torneio.RestricaoCategoria) && torneio.RestricaoCategoria != "Livre")
            {
                var bloqueioHistorico = await MotivoBloqueioCategoriaAsync(
                    dupla.Categoria.Nome, candidato, null, torneio.RestricaoCategoria);
                if (bloqueioHistorico != null) return bloqueioHistorico;
            }

            // ...e anti-sandbagging pelo RANKING RS, que é a outra metade da mesma pergunta e
            // olha pra fora do Padelizou. As duas convivem: passar numa não isenta da outra.
            // Trocar de parceiro é uma das DUAS portas que inscrevem gente numa categoria — a
            // outra é o Create. Se esta checagem existisse só lá, bastava entrar sozinho e
            // trocar o parceiro depois pra furar a regra inteira.
            return await _rankingRs.MotivoDeRecusaAsync(torneio, dupla.Categoria,
                new[] { new ValidacaoPeloRankingRs.Pessoa(candidato.Nome, candidato.Cpf) });
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

            // Mesma razão de TrocarParceiro: time não tem parceiro, e o link geraria um
            // convite pra entrar numa linha que é de time.
            if (dupla.EhTime)
            {
                TempData["Erro"] = "Time não tem parceiro — a lista de times se altera em \"Gerenciar times e estrutura\".";
                return RedirectToAction("Details", "Torneios", new { id = torneio.Id });
            }

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

            // Mesma conta da troca por CPF, e pelo mesmo motivo: perguntada ANTES de gravar,
            // senão esta inscrição faria a pessoa ganhar o preço de segunda por causa de si
            // mesma. Ver TrocarParceiro.
            var euRepito = await QuemJaEstaNoTorneio.EstaAsync(_context, torneio!.Id, eu.Id);

            dupla.Jogador2Id = eu.Id;

            // A dupla estava sozinha e fechou: o valor passa a contar duas pessoas, com o mesmo
            // TETO da troca por CPF — o caminho é outro, a regra é a mesma, e é exatamente por
            // isso que ela mora no PrecoDaInscricao e não aqui.
            if (dupla.ValorInscricao is decimal valorAntesDoConvite)
            {
                dupla.ValorInscricao = PrecoDaInscricao.AoEntrarOParceiro(
                    torneio, valorAntesDoConvite, euRepito, ImpedimentosDa(dupla));
            }

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
