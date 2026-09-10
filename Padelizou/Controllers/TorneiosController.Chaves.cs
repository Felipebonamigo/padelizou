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
    // O sorteio: grupos, grade de horários, previsão e o mata-mata automático.
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GerarChaves(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null || torneio.Status != "Chaves em Sorteio") return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // A condição do "por fora", escrita na criação, vale aqui: sem a taxa paga (ou
            // negociada), o sorteio não sai. Esconder o botão não basta — POST montado à mão
            // tem que esbarrar na mesma parede.
            if (await TaxaExternoImpedeChavesAsync(torneio))
            {
                TempData["Erro"] = "As chaves são liberadas depois do pagamento da taxa do Padelizou.";
                return RedirectToAction("TaxaPlataforma", new { id });
            }

            // Pontos reais de todo mundo inscrito, numa consulta só. Antes isto usava
            // Jogador.PontuacaoGlobal, campo morto que é sempre 0 — na prática os cabeças de
            // chave saíam na ordem de inscrição, e não por ranking.
            var idsInscritos = torneio.Categorias
                .SelectMany(c => c.Duplas)
                .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
                .Where(id => id != null).Select(id => id!.Value)
                .ToList();
            var pontosPorJogador = await _estatisticas.ObterPontosPorJogadorAsync(idsInscritos);

            // Categoria de TIMES: a estrutura prometida na criação precisa fechar com os
            // times que EXISTEM — validado antes de gravar qualquer grupo, senão uma
            // categoria recusada no meio deixaria as anteriores sorteadas pela metade.
            foreach (var categoriaDeTimes in torneio.Categorias.Where(c => c.DeTimes))
            {
                int timesCadastrados = categoriaDeTimes.Duplas.Count(d => d.EhTime && !d.EmListaDeEspera);
                if (timesCadastrados < 2) continue;   // sem gente suficiente, fica fora — como as comuns

                if (CategoriaDeTimes.ProblemaNoSorteio(timesCadastrados,
                        categoriaDeTimes.QuantidadeGrupos, categoriaDeTimes.ClassificadosPorGrupo) is { } problemaTimes)
                {
                    TempData["Erro"] = problemaTimes;
                    return RedirectToAction("Details", new { id });
                }
            }

            // A grade é UMA só pro torneio inteiro. Antes cada categoria recomeçava do
            // horário de início, então três categorias marcavam jogos no mesmo horário nas
            // mesmas quadras. Os horários são atribuídos depois, com todos os jogos na mão.
            // Os jogos de TIMES entram na mesma lista — dividem as mesmas quadras e os
            // mesmos horários, exatamente como se fossem jogadores jogando.
            var jogosPraAgendar = new List<Partida>();

            foreach (var categoria in torneio.Categorias)
            {
                // ---- Ramo de TIMES: grupos definidos pelo organizador, sorteio aleatório ----
                // (time não tem ranking de pontos — cabeça de chave aqui seria loteria fingida)
                if (categoria.DeTimes)
                {
                    var times = categoria.Duplas.Where(d => d.EhTime && !d.EmListaDeEspera).ToList();
                    if (times.Count < 2) continue;

                    var embaralhados = times.OrderBy(_ => Guid.NewGuid()).ToList();
                    var gruposDeTimes = CategoriaDeTimes.Distribuir(embaralhados, categoria.QuantidadeGrupos!.Value);

                    var gruposDeTimesCriados = new List<GrupoTorneio>();
                    for (int i = 0; i < gruposDeTimes.Count; i++)
                    {
                        var novoGrupo = new GrupoTorneio { CategoriaId = categoria.Id, Nome = $"Grupo {(char)('A' + i)}" };
                        _context.Add(novoGrupo);
                        gruposDeTimesCriados.Add(novoGrupo);
                    }
                    await _context.SaveChangesAsync();

                    for (int i = 0; i < gruposDeTimes.Count; i++)
                    {
                        char letra = (char)('A' + i);
                        foreach (var time in gruposDeTimes[i])
                        {
                            time.GrupoTorneioId = gruposDeTimesCriados[i].Id;
                            time.Grupo = letra.ToString();
                        }

                        // Todos contra todos dentro do grupo — a mesma Partida das duplas.
                        for (int a = 0; a < gruposDeTimes[i].Count; a++)
                        {
                            for (int b = a + 1; b < gruposDeTimes[i].Count; b++)
                            {
                                jogosPraAgendar.Add(new Partida
                                {
                                    TorneioId = torneio.Id,
                                    CategoriaId = categoria.Id,
                                    Dupla1Id = gruposDeTimes[i][a].Id,
                                    Dupla2Id = gruposDeTimes[i][b].Id,
                                    Fase = $"Grupo {letra}",
                                    Status = "Agendada",
                                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                                });
                            }
                        }
                    }
                    continue;   // o caminho de duplas abaixo não vale pra times
                }
                // Quem entra no sorteio: todo mundo com vaga confirmada. Desde 09/09/2026 a
                // inscrição SEM PARCEIRO entra também, ocupando a vaga dela com a segunda posição
                // em aberto (ver Services/ForaDoSorteio) — só a lista de espera fica fora.
                var duplas = categoria.Duplas.Where(d => !ForaDoSorteio.FicaDeFora(d)).ToList();

                // CORREÇÃO DA REGRA DE OURO:
                // O mínimo para ter jogo não é 3, é 2 duplas (Para uma chave final direta)!
                if (duplas.Count < 2) continue;

                // ---- Ramo de CHAVE DIRETA: mata-mata puro, sem grupo nenhum ----
                // Sorteio limpo, sem cabeça de chave: as duplas aqui são remontadas
                // misturando categorias, então ninguém tem campanha comparável — semear por
                // ranking seria dar ares de critério a um chute.
                if (categoria.ChaveDireta)
                {
                    if (ChaveamentoMataMata.ProblemaNaChaveDireta(duplas.Count) is { } problemaChave)
                    {
                        TempData["Erro"] = $"{categoria.Nome}: {problemaChave}";
                        return RedirectToAction("Details", new { id });
                    }

                    var sorteadas = duplas.OrderBy(_ => Guid.NewGuid()).Select(d => d.Id).ToList();
                    var primeiraRodada = ChaveamentoMataMata.MontarChaveDireta(sorteadas);

                    jogosPraAgendar.AddRange(primeiraRodada.Confrontos.Select(confronto => new Partida
                    {
                        TorneioId = torneio.Id,
                        CategoriaId = categoria.Id,
                        Dupla1Id = confronto.Dupla1Id,
                        Dupla2Id = confronto.Dupla2Id,
                        Fase = primeiraRodada.Fase,
                        Status = "Agendada",
                        Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                    }));

                    // Quem pegou bye não ganha partida agora — é a AUSÊNCIA de partida que
                    // o robô lê depois pra somá-lo aos vencedores da primeira rodada.
                    continue;
                }

                // ORDENAÇÃO PELO RANKING (Define os Cabeças de Chave)
                //
                // ⚠️ O `ThenBy(Guid)` É O SORTEIO, e sem ele NÃO HAVIA SORTEIO NENHUM aqui
                // (09/09/2026 — Felipe: "por que que toda vez q eu gero o sorteio, esta vindo
                // igual, o chaveamento, os horarios dos jogos e tudo mais?"). Daqui pra baixo
                // tudo é função PURA desta lista: os grupos de 2 do resto, o zigue-zague, a
                // letra do grupo, os confrontos — e a grade de horários por tabela, já que ela
                // nasce da lista de jogos e de `AberturaDaGrade`, que é data fixa.
                //
                // E `OrderByDescending` do LINQ é ordenação ESTÁVEL: o empate preservava a
                // ordem de carga do EF, então com quase todo mundo em 0 ponto a chave saía na
                // ordem de INSCRIÇÃO, igual a cada clique. Os `OrderBy(Guid.NewGuid())` que
                // sorteiam de verdade só existiam nos ramos de times e de chave direta.
                //
                // ⚠️ SORTEIA O DESEMPATE, NÃO A ORDEM INTEIRA: quem tem ranking continua
                // semeado por ranking, que é o que impede dois favoritos no mesmo grupo.
                // ⚠️ O SEGUNDO JOGADOR PODE NÃO EXISTIR (09/09/2026): dupla inscrita sozinha
                // entra na chave com a vaga em aberto. Aqui morava `d.Jogador2Id!.Value`, e o
                // `!` só calava o compilador — no primeiro sorteio com inscrição sozinha isso
                // era `InvalidOperationException` e um 500 na cara do organizador, antes de
                // gravar grupo nenhum. Quem tem meia dupla soma meia semeadura, que é o certo:
                // o ranking dela é só o do jogador que existe.
                var duplasOrdenadas = duplas
                    .OrderByDescending(d => pontosPorJogador.GetValueOrDefault(d.Jogador1Id)
                                          + (d.Jogador2Id is int parceiro
                                              ? pontosPorJogador.GetValueOrDefault(parceiro)
                                              : 0))
                    .ThenBy(_ => Guid.NewGuid())
                    .ToList();

                // O normal dos torneios é fechar em grupos de 3 duplas. Quando o total não é
                // múltiplo de 3, os melhores rankeados resolvem em grupo(s) de 2 (chave direta),
                // e o restante (sempre múltiplo de 3 depois disso) fecha em grupos de 3 normalmente:
                //   - sobra 2 (ex: 14 duplas): 1º x 2º vira um grupo de 2 só, o resto (12) fecha em 4 grupos de 3.
                //   - sobra 1 (ex: 13 duplas): os 4 melhores viram 2 grupos de 2 (1º x 4º e 2º x 3º),
                //     o resto (9) fecha em 3 grupos de 3.
                int n = duplasOrdenadas.Count;
                var gruposDeDuplas = new List<List<Dupla>>();

                if (n < 3)
                {
                    gruposDeDuplas.Add(duplasOrdenadas); // 2 duplas: só dá pra ter a chave direta 1x2
                }
                else
                {
                    int resto = n % 3;
                    List<Dupla> restantes;

                    if (resto == 1)
                    {
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[0], duplasOrdenadas[3] });
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[1], duplasOrdenadas[2] });
                        restantes = duplasOrdenadas.Skip(4).ToList();
                    }
                    else if (resto == 2)
                    {
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[0], duplasOrdenadas[1] });
                        restantes = duplasOrdenadas.Skip(2).ToList();
                    }
                    else
                    {
                        restantes = duplasOrdenadas;
                    }

                    int numGruposDeTres = restantes.Count / 3;
                    if (numGruposDeTres > 0)
                    {
                        var bucket = new List<Dupla>[numGruposDeTres];
                        for (int i = 0; i < numGruposDeTres; i++) bucket[i] = new List<Dupla>();

                        // DISTRIBUIÇÃO POR FAIXAS (1 cabeça de chave forte/médio/fraco por grupo).
                        //
                        // A lista chega ordenada do melhor pro pior e é fatiada em FAIXAS de
                        // `numGruposDeTres`. A primeira faixa abre os grupos na ordem (o 1º do
                        // ranking abre o Grupo A); TODA faixa seguinte entra INVERTIDA, então o
                        // grupo do cabeça mais forte recebe o PIOR de cada faixa.
                        //
                        // ⚠️ ISTO NÃO É A SERPENTINA CLÁSSICA, e a diferença foi pedida pelo
                        // Felipe (09/09/2026): "Grupo A (1º do ranking, 9º do ranking e 6º) /
                        // Grupo B (2º, 8º, 5º) / Grupo C (3º, 7º, 4º)". A serpentina (A→C, C→A,
                        // A→C) recomeçava em A na terceira faixa e dava Grupo A = 1º, 6º, 7º
                        // contra Grupo C = 3º, 4º, 9º — somando as colocações, o grupo do LÍDER
                        // saía o mais forte (14) e o do 3º cabeça o mais fraco (16). Ser cabeça
                        // de chave PUNIA. O equilíbrio aqui é o mesmo (as somas continuam
                        // 14/15/16), só que agora a favor de quem se classificou melhor, que é
                        // a convenção de todo torneio semeado.
                        for (int posicao = 0; posicao < restantes.Count; posicao++)
                        {
                            int dentroDaFaixa = posicao % numGruposDeTres;
                            int grupoIndex = posicao < numGruposDeTres
                                ? dentroDaFaixa                                 // 1ª faixa: A, B, C…
                                : numGruposDeTres - 1 - dentroDaFaixa;          // as demais: …C, B, A
                            bucket[grupoIndex].Add(restantes[posicao]);
                        }
                        gruposDeDuplas.AddRange(bucket);
                    }
                }

                var gruposCriados = new List<GrupoTorneio>();
                for (int i = 0; i < gruposDeDuplas.Count; i++)
                {
                    char letra = (char)('A' + i);
                    var novoGrupo = new GrupoTorneio { CategoriaId = categoria.Id, Nome = $"Grupo {letra}" };
                    _context.Add(novoGrupo);
                    gruposCriados.Add(novoGrupo);
                }
                await _context.SaveChangesAsync();

                // Vincula as duplas aos grupos E gera os jogos. Antes, este passo só setava o
                // GrupoTorneioId: as duplas ficavam com Grupo(str) nulo e NENHUMA partida era
                // criada, então o torneio travava em "Fase de Grupos" sem jogos pra registrar e
                // sem como avançar pro mata-mata (que agrupa por dupla.Grupo).
                for (int i = 0; i < gruposDeDuplas.Count; i++)
                {
                    char letra = (char)('A' + i);
                    var duplasDoGrupo = gruposDeDuplas[i];

                    foreach (var dupla in duplasDoGrupo)
                    {
                        dupla.GrupoTorneioId = gruposCriados[i].Id;
                        dupla.Grupo = letra.ToString();
                    }

                    // Todos contra todos dentro do grupo.
                    for (int a = 0; a < duplasDoGrupo.Count; a++)
                    {
                        for (int b = a + 1; b < duplasDoGrupo.Count; b++)
                        {
                            jogosPraAgendar.Add(new Partida
                            {
                                TorneioId = torneio.Id,
                                CategoriaId = categoria.Id,
                                Dupla1Id = duplasDoGrupo[a].Id,
                                Dupla2Id = duplasDoGrupo[b].Id,
                                Fase = $"Grupo {letra}",
                                Status = "Agendada",
                                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                            });
                        }
                    }
                }
            }

            // ⚠️ A ordem da fila é o CAMINHO CRÍTICO. Ver LevasDaGrade: a chave direta abre o
            // torneio, os grupos vêm em seguida, e o mata-mata que sai dos grupos fica pra
            // segunda leva.
            //
            // As fases seguintes (oitavas, semi, final) nem passam por aqui: nascem depois,
            // pelo robô, que as agenda emendadas nas vagas livres a partir do fim da fase que
            // as alimenta.
            jogosPraAgendar = OrdemDaFila(jogosPraAgendar, torneio.QuantidadeQuadras);

            // Os horários. A regra mora em EncaixarNasLevas, compartilhada com o "Refazer
            // grade" — duas cópias divergiriam e o torneio teria duas grades.
            //
            // ⚠️ O TORNEIO "POR ORDEM" PASSA POR AQUI TAMBÉM desde 09/09/2026 (pedido do
            // Felipe: "mesmo que seja por ordem os jogos, tem q ter o horario"). Antes ele
            // pulava a grade inteira e saía com tudo nulo. Agora ele calcula igual e só apaga
            // a QUADRA no fim — ver Services/OrdemDeLiberacao, que explica por que a quadra
            // precisa ser distribuída antes de ser apagada.
            var sedesDoTorneio = await SedesAsync(torneio.Id);

            EncaixarNasLevas(torneio, jogosPraAgendar,
                OcupantesPorDupla(torneio), await QuadrasDoTorneioAsync(torneio.Id),
                quadrasPorCategoria: await QuadrasPreferidasAsync(torneio.Id),
                janelas: JanelasDeImpedimento.PorDupla(torneio),
                sedes: sedesDoTorneio,
                concentracao: ConcentracaoDeJogos.De(torneio),
                noiteDeSabado: EliminatoriaNoSabado.PorCategoria(torneio));

            // ⚠️ "SE NÃO COUBER, TEM Q AVISAR POR QUE NAO COUBE" (Felipe, 09/09/2026). O aviso de
            // prazo já existia ANTES do sorteio (PrevisaoGradeVM.EstouraOPrazo, "Passa do dia
            // 13/09") e dizia só QUE passou. Agora ele também sai DEPOIS, que é quando o organizador
            // olha, e traz a causa — ver Services/PorQueNaoCoube.
            // ⚠️ O REPARO DEPOIS DO ENCAIXE, e antes de a quadra sumir. O guloso não volta atrás
            // (ver Services/ReparoDaGrade): quando as vagas apertam ele marca o jogo dentro do
            // impedimento, sem saber que dez vagas atrás havia um jogo livre que caberia ali.
            ReparoDaGrade.Reparar(torneio, jogosPraAgendar,
                torneio.Categorias.SelectMany(c => c.Duplas).ToList(), sedesDoTorneio);

            await AvisarSeNaoCoubeAsync(torneio, jogosPraAgendar, sedesDoTorneio);

            // O clube ANTES de a quadra ir embora — ver OrdemDeLiberacao.CarimbarOClube.
            OrdemDeLiberacao.CarimbarOClube(torneio, jogosPraAgendar, sedesDoTorneio);
            OrdemDeLiberacao.ApagarAsQuadras(torneio, jogosPraAgendar);

            _context.Partidas.AddRange(jogosPraAgendar);

            // Sorteado e gravado, mas ainda não é público — fica esperando quem aprova (ver
            // Services/AprovacaoDeChaves). O aviso "as chaves saíram" só sai daqui a pouco, em
            // AprovarChaves.
            torneio.Status = AprovacaoDeChaves.Pendente;
            await _context.SaveChangesAsync();

            return ParaAsChaves(torneio.Id);
        }

        // ── CANCELAR QUEM FICOU SEM PARCEIRO, NA HORA DE SORTEAR ────────────────────────────
        // 🗣️ Felipe, 09/09/2026: hoje quem fica sem parceiro só é excluído do sorteio em
        // silêncio (ForaDoSorteio) — o alerta acima do botão já avisa, mas não dá nenhuma
        // decisão de verdade pro organizador. Pedido: "avise que tem um sozinho e pergunta se
        // ele entra igual ou não". Entrar como está não é possível — dupla sem o segundo nome
        // não é um time, não joga mata-mata —, então a decisão vira: sortear sem essa pessoa
        // (não fazer nada, é o padrão de sempre) ou cancelar a inscrição dela agora e devolver
        // o dinheiro, se ela pagou.
        //
        // ⚠️ SÓ DUPLA INCOMPLETA, E SÓ NESTA JANELA. `RemoverDupla` já cobre "Inscrições
        // Abertas" pra qualquer inscrito; esta ação não duplica aquela — cobre exatamente o
        // buraco que ela deixa (não dá pra remover depois que as inscrições fecham).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarSemParceiro(int duplaId)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .Include(d => d.Jogador1)
                .FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            if (torneio.Status != "Chaves em Sorteio")
            {
                TempData["Erro"] = "Só dá pra cancelar por aqui na hora de sortear as chaves.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            // ⚠️ TIME NÃO PASSA POR AQUI, e a checagem tem que vir ANTES da de `Completa`:
            // `Dupla.Completa` é `Jogador2Id != null`, e TODO time tem esse campo nulo — então
            // um time entrava por esta porta como se fosse inscrição sozinha. A tela nunca
            // desenha o botão (ForaDoSorteio.ComVagaEmAberto exclui time), mas um POST montado à
            // mão apagaria a linha do time e ainda mandaria "Você saiu do torneio" pro
            // Jogador1Id dela — que é o próprio organizador. A régua irmã escrita no mesmo dia
            // (JanelaDoParceiro) já tinha essa guarda; esta nasceu sem.
            if (dupla.EhTime)
            {
                TempData["Erro"] = "Time não se cancela por aqui — use \"Gerenciar times e estrutura\".";
                return RedirectToAction("Details", new { id = torneioId });
            }

            if (dupla.Completa)
            {
                TempData["Erro"] = "Esta dupla já tem os dois parceiros — cancele pela lista de inscritos.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            string nome = dupla.Jogador1.ComoChamar;
            string? avisoDoDinheiro = null;

            // Achar a cobrança desta inscrição: o vínculo nasce só quando o pagamento confirma
            // (EfetivarTorneioAsync/EfetivarPagamentoDeInscricaoAsync gravam ReferenciaId =
            // dupla.Id nesse momento — antes disso ela nem existiria pra aparecer aqui).
            if (dupla.Pago)
            {
                var pagamento = await CobrancaDaDupla.AtivaDe(_context, dupla.Id)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (pagamento != null && !string.IsNullOrWhiteSpace(pagamento.AsaasPaymentId))
                {
                    if (!await _pagamentos.EstornarTotalAsync(pagamento))
                    {
                        TempData["Erro"] = "O gateway recusou o estorno. Tente novamente em instantes.";
                        return RedirectToAction("Details", new { id = torneioId });
                    }
                }
                else
                {
                    // Pago por fora (dinheiro, Pix direto) ou marcado na mão — não há cobrança
                    // real pra pedir devolução ao gateway. Mesmo caso de
                    // PagamentosController.Estornar quando a cobrança não tem AsaasPaymentId.
                    avisoDoDinheiro = " Não tinha cobrança no gateway pra estornar — combine a devolução com ele por fora.";
                }
            }

            // ⚠️ A FATURA ABERTA MORRE COM A INSCRIÇÃO. O bloco do estorno acima só roda quando
            // `dupla.Pago` — mas no torneio que "garante a vaga e cobra depois" a inscrição NÃO
            // paga tem uma cobrança pendente viva no gateway, com link válido até o prazo. Sem
            // isto ela sobrevivia ao cancelamento: o jogador pagava depois de já ter sido
            // removido, o dinheiro entrava, a dupla não existia mais, e o
            // EfetivarPagamentoDeInscricaoAsync caía no LogError que pede devolução à mão.
            //
            // `EstornarTotalAsync` já sabe tratar cobrança Pendente: ali ela é CANCELADA no
            // gateway (o link morre), sem movimentar dinheiro nenhum.
            if (!dupla.Pago
                && await FaturaAbertaDaInscricaoAsync(torneioId, dupla.Id) is { } faturaAberta
                && !string.IsNullOrWhiteSpace(faturaAberta.AsaasPaymentId))
            {
                if (!await _pagamentos.EstornarTotalAsync(faturaAberta))
                {
                    // Não dá pra seguir e apagar a inscrição: o link continuaria valendo e
                    // ninguém mais teria tela pra matá-lo depois que a dupla sumir.
                    TempData["Erro"] = "Não consegui cancelar a cobrança em aberto dessa inscrição. "
                        + "Tente de novo em instantes — remover a inscrição com a fatura de pé deixaria "
                        + "ele pagando por uma vaga que não existe mais.";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                avisoDoDinheiro = " A cobrança em aberto dele foi cancelada.";
            }

            // ⚠️ QUEM CHAMOU NO MURAL PRECISA SABER, E A CASCATA NÃO AVISA NINGUÉM. Apagar a
            // dupla leva junto os ChamadosDoMural dela (FK Cascade, ver DbPadelContext) — e é
            // justamente a inscrição SOZINHA que acumula chamado. Sem isto, quem se candidatou
            // fica esperando resposta de uma vaga que não existe mais e não procura outra: o
            // mesmo silêncio que o Felipe mandou cortar em 17/08/2026 ("quem chamou fica
            // ESPERANDO"). O caminho gêmeo já faz isso — ver FecharDuplaComAsync, que lê os ids
            // ANTES do RemoveRange pelo mesmo motivo.
            //
            // Texto próprio: aqui ninguém recusou ninguém e a vaga não foi preenchida — a
            // inscrição deixou de existir. As duas frases prontas do mural diriam algo falso.
            var candidatosDoMural = await _context.ChamadosDoMural
                .Where(c => c.DuplaId == dupla.Id)
                .Select(c => c.CandidatoId)
                .ToListAsync();

            await TirarDuplaDoTorneioAsync(dupla, torneio,
                $"O organizador cancelou sua inscrição em {torneio.Nome} porque você ficou sem parceiro até "
                + "o sorteio das chaves."
                + (avisoDoDinheiro == null && dupla.Pago ? " O valor pago foi estornado." : ""));

            if (candidatosDoMural.Count > 0)
            {
                await AvisarAsync(candidatosDoMural, "A inscrição saiu do torneio",
                    $"A inscrição de {nome} em {torneio.Nome} foi cancelada, então o seu pedido pra "
                    + "fechar dupla não tem mais resposta. Tem outras inscrições procurando parceiro "
                    + "por lá — toque pra ver.", torneio.Id);
            }

            TempData["Sucesso"] = $"Inscrição de {nome} cancelada."
                + (avisoDoDinheiro ?? (dupla.Pago ? " Valor estornado." : ""));
            return RedirectToAction("Details", new { id = torneioId });
        }

        // De quem é cada fatura pendente do "pagar depois"? Só o JSON sabe — ver
        // CobrancaDaDupla.PendentesDoPagarDepois pro porquê de a consulta parar no filtro grosso.
        private async Task<Pagamento?> FaturaAbertaDaInscricaoAsync(int torneioId, int duplaId)
        {
            foreach (var pagamento in await CobrancaDaDupla.PendentesDoPagarDepois(_context, torneioId).ToListAsync())
            {
                try
                {
                    var dados = System.Text.Json.JsonSerializer
                        .Deserialize<DadosPagamentoDeInscricao>(pagamento.DadosInscricao!);
                    if (dados?.DuplaId == duplaId) return pagamento;
                }
                catch (System.Text.Json.JsonException ex)
                {
                    // Mesmo tratamento do PagamentoInscricaoService.Desserializar: uma linha
                    // estragada não pode derrubar o cancelamento das outras — mas ela FICA no log,
                    // porque é a única pista de que existe fatura sem dono identificável.
                    _logger.LogError(ex, "DadosInscricao inválidos no pagamento {Id} — não dá pra saber "
                        + "de qual inscrição é essa cobrança em aberto.", pagamento.Id);
                }
            }

            return null;
        }

        // ── CONFERIR A GRADE ──────────────────────────────────────────────────────────────
        // 🗣️ Felipe, 09/09/2026: "faz esse botão e sobe".
        //
        // Nasceu de um beco: ele pediu duas vezes que eu conferisse a grade do torneio dele em
        // `dev`, e a sessão da web não alcança o `dev`. A saída não é pedir print — é virar a
        // auditoria em tela, pra ele apertar e ver, em qualquer torneio, sem depender de mim.
        //
        // ⚠️ SÓ LÊ. Não remarca nada, não grava nada: quem muda a grade é o "Refazer grade", ao
        // lado. Uma tela de conferência que conserta sozinha tira do organizador a decisão de
        // aceitar ou não o que cedeu.
        //
        // A régua mora em Services/AuditoriaDaGrade, e é a MESMA que o teste de regressão usa.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ConferirGrade(int id)
        {
            // Com os JOGADORES: sem eles a dupla vira "Dupla 589" e a pessoa vira "alguém" — e o
            // organizador não tem como mexer na mão no que não sabe quem é (10/09/2026).
            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            // A tela mostra a grade inteira e nome de jogador: é de quem organiza. Mesma régua
            // do "Refazer grade", que é o botão vizinho.
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var jogos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
            var duplas = torneio.Categorias.SelectMany(c => c.Duplas).ToList();

            ViewBag.Torneio = torneio;
            ViewBag.TotalDeJogos = jogos.Count;

            // ⚠️ O "POR QUÊ", e não só o "quais" (09/09/2026). 🗣️ Felipe, num print do dev: *"como
            // que tem jogo dia 15, no torneio do er? se termina dia 13? […] por que esse erro?"*.
            // A régua já existia e só falava no sorteio — que é um instante que passa. A pergunta
            // nasce DEPOIS, olhando a grade, e é aqui que ele olha.
            var sedesDaConferencia = await SedesAsync(id);

            var porQueNaoCoube = PorQueNaoCoube.Analisar(torneio,
                await _context.Quadras.Where(q => q.TorneioId == id).ToListAsync(),
                sedesDaConferencia,
                jogos.Count,
                jogos.Where(j => j.HorarioPrevisto != null).Max(j => j.HorarioPrevisto));

            // ⚠️ O BURACO NA GRADE VEM PRIMEIRO, e ele NÃO depende de `DataFim` (09/09/2026).
            // 🗣️ *"refiz a grade, continua com jogo dia 15, 16, do nada ele pula do dia 12 p dia
            // 15"*. O aviso de prazo fica mudo quando o campo não foi preenchido — e foi
            // exatamente aí que eu tinha pendurado o único aviso. Dois dias vazios no meio de um
            // torneio são anômalos com ou sem prazo declarado.
            porQueNaoCoube.InsertRange(0, PorQueNaoCoube.BuracosNaGrade(torneio, sedesDaConferencia, jogos));

            ViewBag.PorQueNaoCoube = porQueNaoCoube;

            return View(AuditoriaDaGrade.Conferir(torneio, jogos, duplas, sedesDaConferencia));
        }

        // Pra onde o organizador vai depois de sortear: a aba "Chaves e Grupos", que é a tela
        // do que ele acabou de criar.
        //
        // ⚠️ A HASH NÃO É ENFEITE: é ela que decide em qual aba a Details abre (o script no fim
        // da view). Sem ela a página recarregava na aba em que estava — o organizador apertava
        // o botão que é o clímax da montagem do torneio e não via nada acontecer.
        //
        // Existe como método porque o GerarChaves tem DUAS saídas de sucesso: o torneio "por
        // ordem de liberação" sai antes do encaixe na grade, o que tem horário sai depois.
        // Duas linhas soltas divergiriam no dia em que uma delas mudasse.
        private IActionResult ParaAsChaves(int torneioId) =>
            RedirectToAction("Details", "Torneios", new { id = torneioId }, fragment: "grupos");

        // A APROVAÇÃO das chaves: o passo entre "sorteado" e "público", pedido pelo Felipe.
        // Ninguém fora de quem organiza/administra vê nada até aqui — nem os inscritos (ver
        // TorneiosController.cs, ação Jogos, e Details.cshtml, aba Chaves e Grupos).
        //
        // `avisarJogadores` é a caixinha da tela (10/09/2026), e ela existe por causa do
        // `RecolherChaves` logo abaixo: podendo voltar pra aprovação, aprovar deixou de ser uma
        // vez só, e a rajada "as chaves saíram" repetiria a cada volta pra base inteira do torneio.
        //
        // ⚠️ NULO NÃO É "NÃO": é "ninguém disse", e cai no carimbo — avisa só se nunca avisou.
        // O formulário SEMPRE manda a escolha (o `<input type="hidden">` ao lado da caixinha
        // garante o `false` quando ela está desmarcada), então o nulo só alcança quem chamar sem
        // dizer nada: POST feito à mão, ou aba velha com o formulário de antes desta mudança. Pra
        // esse, o certo é o comportamento seguro — e aqui o seguro é NÃO repetir a rajada. Mesmo
        // raciocínio do `refazerHorarios` do TrocarDuplasDeGrupo, com o default no outro sentido
        // porque lá o seguro era a garantia vendida, e aqui é o silêncio.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AprovarChaves(int id, bool? avisarJogadores = null)
        {
            var torneio = await _context.Torneios.FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();
            if (torneio.Status != AprovacaoDeChaves.Pendente)
            {
                TempData["Erro"] = "Este torneio não tem chave esperando aprovação.";
                return RedirectToAction("Details", new { id });
            }
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            bool avisar = avisarJogadores ?? torneio.ChavesAvisadasEm == null;

            torneio.Status = "Fase de Grupos";

            // O carimbo vai junto do status, numa gravação só: é ele que faz a próxima aprovação
            // saber que a rajada já saiu uma vez. Ele marca o DISPARO — a entrega em si é por
            // fila e best-effort, e sempre foi (ver AvisarChavesPublicadasAsync).
            if (avisar) torneio.ChavesAvisadasEm = DateTime.Now;

            await _context.SaveChangesAsync();

            // O aviso "as chaves saíram" nasce AQUI, não no sorteio — é agora que elas ficam
            // de verdade visíveis pra quem joga.
            if (avisar)
            {
                var jogos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
                await AvisarChavesPublicadasAsync(torneio, jogos);
            }

            TempData["Sucesso"] = avisar
                ? "Chaves aprovadas — já estão visíveis pra todo mundo, e os jogadores foram avisados."
                : "Chaves aprovadas — já estão visíveis pra todo mundo. Ninguém foi avisado de novo.";
            return RedirectToAction("Details", new { id });
        }

        // RECOLHE a chave publicada: `Fase de Grupos` → `Chaves em Aprovação`, com o sorteio
        // INTEIRO de pé. É o desfazer da aprovação, e não do sorteio.
        //
        // 🗣️ Felipe, 10/09/2026: *"permita recolocar o torneio em fase fechada, ou já tem isso?"*
        // Não tinha — depois de aprovar, o status só andava pra frente. As três saídas que
        // existiam resolvem outra coisa: `DesfazerSorteio` APAGA grupos e jogos (e fecha assim
        // que se aprova), `ReabrirInscricoes` recusa com partida existindo, e
        // `AlternarVisibilidade` some da listagem mas deixa quem já está inscrito vendo a página.
        //
        // ⚠️ NÃO MEXE NO `ChavesAvisadasEm`, de propósito: é exatamente o que ele lembra. Limpar
        // aqui faria a re-aprovação achar que nunca avisou e mandar a segunda rajada.
        //
        // ⚠️ E NÃO DESFAZ O QUE JÁ SAIU. O push entregue e o evento que já caiu na agenda de quem
        // usa o ICS não voltam. Recolher esconde daqui pra frente; a tela diz isso antes do clique.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecolherChaves(int id)
        {
            var torneio = await _context.Torneios.FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            // A autorização vem ANTES da checagem de estado, ao contrário do `AprovarChaves` logo
            // acima: quem não organiza não precisa aprender em que fase o torneio está.
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            bool jaSaiuDoPapel = await _context.Partidas
                .Where(p => p.TorneioId == id)
                .AnyAsync(AprovacaoDeChaves.JaSaiuDoPapel);

            if (AprovacaoDeChaves.PorQueNaoPodeRecolher(torneio, jaSaiuDoPapel) is { } naoRecolhe)
            {
                TempData["Erro"] = naoRecolhe;
                return RedirectToAction("Details", new { id });
            }

            torneio.Status = AprovacaoDeChaves.Pendente;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Chaves recolhidas — voltaram a aparecer só pra você e pros outros "
                                + "organizadores. Os jogos e os horários continuam como estavam.";
            return RedirectToAction("Details", new { id });
        }

        // DESFAZ o sorteio: apaga os grupos e os jogos gerados e devolve o torneio pra
        // "Chaves em Sorteio", pronto pra sortear de novo. Só existe ENQUANTO a chave está
        // esperando aprovação — depois de aprovada ela é pública, tem gente vendo contra quem
        // joga, e desfazer viraria "reorganizar o torneio por baixo de quem já se organizou"
        // (mesmo raciocínio de PortaDaInscricao.PorQueNaoPodeAbrir).
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DesfazerSorteio(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.GruposTorneio)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();
            if (torneio.Status != AprovacaoDeChaves.Pendente)
            {
                TempData["Erro"] = "Este torneio não tem chave pendente de aprovação pra desfazer.";
                return RedirectToAction("Details", new { id });
            }
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var jogos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();

            // Ninguém deveria ter jogo em andamento ou finalizado numa chave que nem foi
            // aprovada ainda — mas se algo escapou por outra porta, desfazer apagaria placar
            // de verdade. Melhor recusar do que arriscar.
            if (jogos.Any(j => j.Status != "Agendada"))
            {
                TempData["Erro"] = "Já tem jogo em andamento ou finalizado — não dá pra desfazer o sorteio.";
                return RedirectToAction("Details", new { id });
            }

            // Solta as duplas dos grupos ANTES de apagar os grupos — a FK não deixa apagar
            // GrupoTorneio com Dupla ainda apontando pra ele.
            var duplas = torneio.Categorias.SelectMany(c => c.Duplas).Where(d => d.GrupoTorneioId != null);
            foreach (var dupla in duplas)
            {
                dupla.GrupoTorneioId = null;
                dupla.Grupo = null;
            }

            var grupos = torneio.Categorias.SelectMany(c => c.GruposTorneio).ToList();
            _context.RemoveRange(grupos);
            _context.Partidas.RemoveRange(jogos);

            // As reservas de horário das eliminatórias previstas (Models/ReservaDeHorario) eram
            // desta grade; sem a grade, sobreviver seria valer pra um sorteio que ainda não existe.
            _context.ReservasDeHorario.RemoveRange(await ReservasDeHorario.DoTorneio(_context, id).ToListAsync());

            torneio.Status = "Chaves em Sorteio";

            // ⚠️ QUEM DEVOLVE AS CHAVES DEVOLVE A DÍVIDA (09/09/2026, desenho aprovado pelo
            // Felipe). No torneio "por fora" a trava do sorteio é o mecanismo de cobrança
            // inteiro (Services/TaxaDoTorneioExterno): o organizador pega FIADO pra sortear, e o
            // carimbo destrava a chave. Desfazer apagava as partidas e devolvia o status, mas
            // não o carimbo — e daí saíam duas coisas, sendo a segunda dinheiro:
            //
            //   1. o painel seguia cobrando por uma chave que já tinha voltado (foi o print);
            //   2. `ChavesLiberadas` responde `true` enquanto o carimbo existir, então a trava
            //      ficava desligada PRA SEMPRE: dava pra desfazer, reabrir inscrições, entrar
            //      mais gente e sortear de novo sem a taxa ser apresentada nenhuma vez — com a
            //      dívida registrada valendo a de um torneio menor, já que
            //      `TaxaDoTorneioExterno.Valor` calcula sobre a lista do momento.
            //
            // Pago e negociado não se desfazem: um é dinheiro que entrou, o outro é o Padelizou
            // tendo aberto mão. É o que `FiadoEmAberto` separa.
            bool devolveuOFiado = TaxaDoTorneioExterno.FiadoEmAberto(torneio);
            if (devolveuOFiado) torneio.TaxaExternoAdiadaEm = null;

            await _context.SaveChangesAsync();

            // Os admins levaram um push quando o fiado foi tirado (AvisarAdminsDoFiadoAsync).
            // Sem a baixa, quem viu a dívida nascer continuaria cobrando um torneio que não
            // deve mais — e a lista do financeiro mudaria sozinha, sem ninguém saber por quê.
            if (devolveuOFiado) await AvisarAdminsDoFiadoDesfeitoAsync(torneio);

            TempData["Sucesso"] = devolveuOFiado
                ? "Sorteio desfeito — pode sortear de novo quando quiser. A taxa do Padelizou "
                  + "voltou a ficar pendente: você escolhe de novo na hora de sortear."
                : "Sorteio desfeito — pode sortear de novo quando quiser.";
            return RedirectToAction("Details", new { id });
        }

        // TROCAR DUAS DUPLAS DE GRUPO, em cima do sorteio que acabou de sair. Pedido do Felipe
        // (09/09/2026): "permita também, que o organizador, troque a dupla de lugar no grupo, e
        // ao trocar, verifique os horarios com impedimentos novamente, se nao vai atrapalhar
        // algum".
        //
        // É o ajuste fino que faltava entre "aceitar a chave como saiu" e "Desfazer sorteio":
        // com o sorteio passando a sortear de verdade (mesmo dia), torrar 63 duplas pra mover
        // uma seria caro demais.
        //
        // ⚠️ SÓ ENQUANTO A CHAVE ESPERA APROVAÇÃO (decisão do Felipe). Depois de aprovada ela é
        // pública: tem gente que já viu contra quem joga e já se organizou pro horário — mesma
        // razão pela qual DesfazerSorteio também para aqui.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // ⚠️ O PADRÃO É REFAZER, e isso é escolha de segurança, não inércia: o formulário SEMPRE
        // manda a opção, então este valor só vale pra quem chamar sem dizer nada — e pra esse, o
        // certo é a garantia que foi vendida (o impedimento pago). É o que as duas guardas de
        // 09/09 afirmam, e elas continuam chamando sem parâmetro de propósito.
        public async Task<IActionResult> TrocarDuplasDeGrupo(int id, int duplaA, int duplaB,
            bool refazerHorarios = true)
        {
            // Com os jogadores: a mensagem do fim diz QUEM trocou, e sem eles NomeDeExibicao cai
            // em "Dupla 11" (10/09/2026, ensaio do Er — o InMemory da suíte preenche a navegação
            // sozinho e escondia isso).
            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            // Régua de SORTEIO, não de dia de jogo: isto muda confronto, então é organizador —
            // o marcador, que pode refazer a grade, não pode remontar o grupo.
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (torneio.Status != AprovacaoDeChaves.Pendente)
            {
                TempData["Erro"] = "Só dá pra trocar duplas de grupo enquanto a chave espera aprovação.";
                return ParaAsChaves(id);
            }

            // De dentro DESTE torneio: um Id de dupla vem do formulário, e formulário não
            // escolhe em qual torneio se mexe.
            var duplasDoTorneio = torneio.Categorias.SelectMany(c => c.Duplas).ToList();
            var a = duplasDoTorneio.FirstOrDefault(d => d.Id == duplaA);
            var b = duplasDoTorneio.FirstOrDefault(d => d.Id == duplaB);

            if (TrocaDeGrupo.MotivoParaNaoTrocar(a, b) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return ParaAsChaves(id);
            }

            var jogos = await _context.Partidas.Where(p => p.TorneioId == id).OrderBy(p => p.Id).ToListAsync();
            var duplasParaAuditar = duplasDoTorneio;
            var sedesDaTroca = await SedesAsync(id);
            var antesDaTroca = Padelizou.Services.ImpactoDaTroca.Contar(torneio, jogos, duplasParaAuditar, sedesDaTroca);

            TrocaDeGrupo.Trocar(a!, b!, jogos);

            // ⚠️ E AGORA A GRADE INTEIRA É REFEITA — a segunda metade do pedido ("ao trocar,
            // verifique os horarios com impedimentos novamente, se nao vai atrapalhar algum").
            //
            // Os jogos guardaram o horário que já tinham, mas trocaram de DONO: a dupla que
            // veio do grupo que jogava no sábado herda o horário de sexta da outra — e pode ter
            // pago justamente pra não jogar na sexta. Refazendo, o encaixe reavalia todas as
            // janelas de todo mundo, e não só as das duas duplas mexidas: remanejar move o
            // horário de quem não pediu nada, então o furo pode nascer em qualquer lugar.
            //
            // Recalcular custa barato aqui e em lugar nenhum mais: neste status a chave ainda
            // não é pública, ninguém se organizou pra horário nenhum, e nada começou — então
            // AberturaDoRecalculo devolve a `AberturaDaGrade` do torneio, a mesma origem do
            // sorteio, em vez de `DateTime.Now`.
            // ⚠️ REFAZER OS HORÁRIOS VIROU ESCOLHA (10/09/2026). 🗣️ *"não obrigue a refazer os
            // horarios, questione se é para refazer os horarios ou apenas trocar as dupla sem mudar
            // os horarios"*. O recálculo automático era a segunda metade do pedido de 09/09 e
            // continua CERTO — mas ficou caro: depois de uma noite arrumando horário na mão, trocar
            // duas duplas de grupo jogava todo esse trabalho fora.
            //
            // Mantendo os horários, o risco de 09/09 é real: o jogo guardou o horário e trocou de
            // DONO, então a dupla que veio do sábado pode herdar a sexta que ela pagou pra não
            // jogar. Não se impede — CONTA-SE, com a mesma régua do Conferir grade, e se diz onde
            // resolver (o "Ajustar horários", que conserta trocando slots em vez de refazer tudo).
            int semHorario = 0;
            if (refazerHorarios)
            {
                var (remarcados, _) = await RecalcularAGradeAsync(torneio, jogos);
                semHorario = remarcados.Count(j => j.HorarioPrevisto == null);
            }

            await _context.SaveChangesAsync();

            var depoisDaTroca = Padelizou.Services.ImpactoDaTroca.Contar(torneio, jogos, duplasParaAuditar, sedesDaTroca);
            var impacto = Padelizou.Services.ImpactoDaTroca.Comparar(antesDaTroca, depoisDaTroca);

            var quem = $"{a!.NomeDeExibicao} foi pro Grupo {a.Grupo} e {b!.NomeDeExibicao} pro Grupo {b.Grupo}. ";
            var oQueAconteceu = refazerHorarios
                ? (semHorario > 0
                    ? $"⚠️ {semHorario} jogos ficaram SEM HORÁRIO: com os impedimentos desta troca não sobrou vaga no expediente."
                    : "Os horários foram recalculados respeitando os impedimentos.")
                : "Os horários ficaram como estavam — nenhuma troca sua se perdeu.";

            var oQueMudou = impacto.Grau switch
            {
                Padelizou.Services.ImpactoDaTroca.Nivel.Igual => "",
                Padelizou.Services.ImpactoDaTroca.Nivel.Melhora => $" No Conferir grade, {impacto.Texto}.",
                _ => $" ⚠️ No Conferir grade, {impacto.Texto} — o botão \"Ajustar horários\" conserta isso sem refazer a grade.",
            };

            bool ruim = semHorario > 0 || impacto.Grau == Padelizou.Services.ImpactoDaTroca.Nivel.Perigo;
            TempData[ruim ? "Erro" : "Sucesso"] = quem + oQueAconteceu + oQueMudou;

            return ParaAsChaves(id);
        }

        // A ordem da fila e as levas por posto moram em Services/LevasDaGrade — o sorteio, o
        // "Refazer grade" e o robô das próximas fases usam a MESMA régua. Duas cópias divergiriam
        // e o torneio teria duas ordens de fase, que é exatamente o defeito que ela conserta.
        private static List<Partida> OrdemDaFila(IEnumerable<Partida> jogos, int quadras) =>
            LevasDaGrade.OrdemDaFila(jogos, quadras);

        // Distribui os jogos na grade, um POSTO de fase por vez (ver Services/LevasDaGrade e
        // Services/OrdemDasFases): todos os grupos, depois todas as primeiras eliminatórias, e as
        // finais no fim do torneio.
        private static void EncaixarNasLevas(Torneio torneio, List<Partida> jogos,
            IReadOnlyDictionary<int, int[]> ocupantes,
            IReadOnlyList<string> quadras,
            DateTime? aPartirDe = null, IReadOnlyList<Partida>? jaMarcados = null,
            // A quadra preferida de cada categoria, quando o organizador escolheu alguma.
            // Ver Services/PreferenciaDeQuadra.
            IReadOnlyDictionary<int, string[]>? quadrasPorCategoria = null,
            // O impedimento de horário PAGO na inscrição. Ver Services/JanelasDeImpedimento.
            IReadOnlyDictionary<int, (DateTime, DateTime)[]>? janelas = null,
            // A concentração ("os 2 jogos na sexta") e o "sem eliminatória no sábado à noite" —
            // as duas restrições de 08/09/2026, que valem cada uma em UMA fase. Ver
            // Services/ConcentracaoDeJogos, Services/EliminatoriaNoSabado e GradeDeJogos.Encaixar.
            ConcentracaoDeJogos.Concentracoes? concentracao = null,
            IReadOnlyDictionary<int, (DateTime, DateTime)[]>? noiteDeSabado = null,
            // O torneio em mais de um clube. Nulo — o caso de quase todos — deixa tudo como era.
            // Ver Services/SedesDoTorneio.
            SedesDoTorneio? sedes = null) =>
            LevasDaGrade.Encaixar(torneio, jogos,
                aPartirDe ?? torneio.AberturaDaGrade,
                jaMarcados ?? Array.Empty<Partida>(),
                new LevasDaGrade.Restricoes(ocupantes, quadras, quadrasPorCategoria, janelas,
                    concentracao, noiteDeSabado, sedes));

        // RECALCULAR OS HORÁRIOS: os mesmos confrontos, a grade refeita a partir de agora.
        //
        // ⚠️ Isto NÃO é o que faz o torneio andar. Preencher as vagas do mata-mata quando os
        // classificados saem é automático (ProcessarMataMataAutomatico e
        // ProcessarAvancoMataMataAutomatico, disparados ao finalizar a última partida de uma
        // fase) — o organizador nunca precisa apertar nada pra isso. Este botão é o REMENDO
        // pro dia em que a realidade não cabe na grade: chuva, jogo que passou de três sets,
        // dupla que não apareceu. Aí o relógio do papel deixou de valer, e alguém precisa
        // redistribuir o que FALTA.
        //
        // O que ele preserva:
        //   • jogos FINALIZADOS e EM QUADRA não mudam de hora nem de quadra — remarcar o que
        //     já rolou é apagar história, e mexer em quem está jogando é pior;
        //   • eles continuam ocupando a quadra deles na conta (entram como `jaMarcados`), pra
        //     que o recálculo não marque nada por cima;
        //   • os confrontos não mudam. O sorteio é uma coisa, a grade é outra.
        //
        // O que ele muda: o horário e a quadra de tudo que ainda está "Agendada", começando
        // no primeiro horário livre a partir de AGORA (ou da abertura do torneio, se nada
        // começou ainda).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefazerGrade(int id, string? voltarPara = null)
        {
            // Marcador entra: remendar a grade quando o dia atrasa é trabalho da mesa. O
            // SORTEIO (GerarChaves) continua só de organizador — refazer grade não muda
            // confronto nenhum, sortear muda o torneio.
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var todos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();

            if (!todos.Any(p => p.Status == "Agendada"))
            {
                TempData["Erro"] = todos.Count == 0
                    ? "Não há jogos pra remarcar: sorteie as chaves primeiro."
                    : "Todos os jogos já foram jogados ou estão em quadra — não há horário pra recalcular.";
                return VoltarPara(voltarPara, id);
            }

            var (remarcar, intocados) = await RecalcularAGradeAsync(torneio, todos);

            await _context.SaveChangesAsync();

            var marcados = remarcar.Where(j => j.HorarioPrevisto != null).ToList();
            TempData["Sucesso"] = marcados.Count == 0
                ? "Nada foi remarcado: não sobrou horário no expediente do torneio."
                : $"Horários recalculados: {marcados.Count} jogos, de " +
                  $"{marcados.Min(j => j.HorarioPrevisto):dd/MM HH:mm} a {marcados.Max(j => j.HorarioPrevisto):HH:mm}. " +
                  (intocados.Count > 0 ? $"Os {intocados.Count} já jogados ou em quadra não mudaram. " : "") +
                  "Os confrontos não mudaram.";

            return VoltarPara(voltarPara, id);
        }

        // De onde o recálculo parte.
        //
        // Torneio que ainda não começou: a abertura da grade, como no sorteio. Torneio EM
        // ANDAMENTO: agora — é justamente porque o relógio andou mais que a grade que alguém
        // apertou o botão. Nunca antes do fim de quem está em quadra: a quadra só vaga quando
        // o jogo dela acaba.
        private static DateTime AberturaDoRecalculo(Torneio torneio, IReadOnlyList<Partida> intocados)
        {
            if (intocados.Count == 0) return torneio.AberturaDaGrade;

            var agora = DateTime.Now;
            var emQuadra = intocados
                .Where(p => p.Status == "AoVivo")
                .Select(p => p.HorarioInicioReal ?? p.HorarioPrevisto ?? agora)
                .DefaultIfEmpty(agora)
                .Max();

            var liberaQuadra = GradeDeJogos.DepoisDe(emQuadra, torneio.HoraFimDoDia,
                torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos);

            return agora > liberaQuadra ? agora : liberaQuadra;
        }

        // O RECÁLCULO DA GRADE, num lugar só: zera o horário e a quadra de tudo que ainda está
        // "Agendada" e reencaixa pelas mesmas regras do sorteio — impedimento pago,
        // concentração, noite de sábado, quadra preferida e sede.
        //
        // ⚠️ Nasceu compartilhado (09/09/2026) porque a TROCA DE DUPLA ENTRE GRUPOS precisa
        // exatamente disto depois de mexer nos confrontos, e é o mesmo motivo pelo qual
        // EncaixarNasLevas já é compartilhado entre o sorteio e o "Refazer grade": duas cópias
        // divergiriam e o torneio teria duas grades. Quem chama grava — aqui só se mexe nas
        // entidades em memória.
        //
        // Devolve (o que foi remarcado, o que ficou intocado), que é o material das duas
        // mensagens de sucesso.
        private async Task<(List<Partida> Remarcados, List<Partida> Intocados)> RecalcularAGradeAsync(
            Torneio torneio, List<Partida> todos)
        {
            // ⚠️ POR ID, e isto é a grade inteira (10/09/2026). O sorteio grava os jogos NA ORDEM DA
            // FILA (OrdemDaFila → AddRange → Ids crescentes); quem lê `Partidas.Where(...)` sem
            // ORDER BY recebe a ordem que o banco quiser — o InMemory devolvia de trás pra frente, e
            // o Postgres, depois de um UPDATE por linha, na ordem do heap. A fila chegava embaralhada
            // e a intercalação que dá o descanso trabalhava sobre outra ordem: mesmas entradas, mesmo
            // motor, grade diferente — o Refazer media PIOR que o sorteio quatro vezes seguidas.
            // GradeDoErMedidaTests.Refazer_grade_sem_nada_mudado_reproduz_a_grade_do_sorteio.
            var remarcar = todos.Where(p => p.Status == "Agendada").OrderBy(p => p.Id).ToList();
            var intocados = todos.Where(p => p.Status != "Agendada").ToList();

            foreach (var jogo in remarcar)
            {
                jogo.HorarioPrevisto = null;
                jogo.NomeQuadra = null;
            }

            // As RESERVAS de horário (Models/ReservaDeHorario) vão embora junto: a reserva é um
            // remendo por cima da grade, como a troca de dois jogos reais — que o recálculo também
            // desfaz ao remarcar tudo. O texto do botão avisa. Quem chama grava.
            _context.ReservasDeHorario.RemoveRange(await ReservasDeHorario.DoTorneio(_context, torneio.Id).ToListAsync());

            EncaixarNasLevas(torneio, remarcar,
                OcupantesPorDupla(torneio), await QuadrasEmUsoAsync(torneio.Id),
                AberturaDoRecalculo(torneio, intocados), intocados,
                await QuadrasPreferidasAsync(torneio.Id),
                JanelasDeImpedimento.PorDupla(torneio),
                ConcentracaoDeJogos.De(torneio),
                EliminatoriaNoSabado.PorCategoria(torneio),
                await SedesAsync(torneio.Id));

            // O reparo fecha o recálculo pelo mesmo motivo do sorteio: o guloso não volta atrás.
            // ⚠️ Recebe a grade INTEIRA (remarcados + intocados) porque descanso e impedimento são
            // da pessoa, não do lote — o jogo remarcado das 21h encosta no que já rolou às 20h.
            // Quem já começou não se move: `TrocaDeHorario` recusa quem não está "Agendada".
            var sedesDoRecalculo = await SedesAsync(torneio.Id);
            ReparoDaGrade.Reparar(torneio, todos,
                torneio.Categorias.SelectMany(c => c.Duplas).ToList(), sedesDoRecalculo);

            // No "por ordem", a quadra volta a ficar em aberto: quem decide onde é a Mesa,
            // conforme vaga. O horário fica — e o CLUBE também (CarimbarOClube, antes do apagar).
            OrdemDeLiberacao.CarimbarOClube(torneio, remarcar, sedesDoRecalculo);
            OrdemDeLiberacao.ApagarAsQuadras(torneio, remarcar);

            return (remarcar, intocados);
        }

        // Volta pra tela DE ONDE o organizador veio.
        //
        // A página do torneio (Details) tem as abas mãe — Inscritos, Grupos, Chaves, Jogos —,
        // e /Torneios/Jogos é só a lista. Toda ação do organizador redirecionava pra "Jogos",
        // então quem estava na página do torneio via as abas SUMIREM depois de trocar um
        // horário ou recalcular a grade, e achava que tinha perdido o caminho de volta.
        //
        // Lista fechada de destinos de propósito: `voltarPara` vem do formulário, e um campo
        // de formulário nunca pode virar redirecionamento pra qualquer lugar.
        private IActionResult VoltarPara(string? voltarPara, int id) =>
            voltarPara == "Details"
                // Com âncora: a página do torneio abre já na aba de jogos, que é de onde a
                // pessoa saiu. Sem ela, voltar pra Details jogaria o organizador na primeira
                // aba e ele teria que caçar a lista de novo.
                ? RedirectToAction("Details", "Torneios", new { id }, fragment: "jogosDoTorneio")
                : RedirectToAction("Jogos", new { id });

        // Mudar a QUADRA de um jogo sem mexer na hora. A troca de horário arrasta o slot
        // inteiro (hora + quadra) e quase nunca era o que o organizador queria: a quadra 3
        // molhou, a 1 é a coberta, a final merece a do meio. Regras em Services/TrocaDeQuadra
        // — inclusive a que importa: quadra ocupada no mesmo horário TROCA de dono em vez de
        // recusar, senão o organizador fica no mesmo beco.
        // O QUE ESTA TROCA FAZ COM O CONFERIR GRADE — perguntado pela tela ANTES do Trocar.
        //
        // 🗣️ *"veja para avisar se o jogo q eu trocar altera algo do 'conferir grade', por exemplo,
        // se vai atrapalhar o impedimento, restrição ou jogos seguidos"* (Felipe, 10/09/2026).
        //
        // ⚠️ É UM ENDPOINT, e não a lista pré-calculada, por causa da conta: o modal é UM só pro
        // torneio inteiro (o jogo A vem do botão clicado), então pré-calcular todo par seria 97×97
        // auditorias numa página de 97 jogos. Aqui é UMA, quando a pessoa escolhe.
        //
        // GET e sem antiforgery de propósito: não escreve nada. A régua de quem pode ver é a mesma
        // da tela — a auditoria mostra nome de jogador e a grade inteira.
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ImpactoDaTroca(int id, string? jogoA, string? jogoB)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var refA = ReferenciaDoJogo.Ler(jogoA);
            var refB = ReferenciaDoJogo.Ler(jogoB);
            if (refA == null || refB == null) return Json(new { grau = "impossivel", texto = "Não encontrei um dos jogos." });

            // ⚠️ A PRÉVIA FICA DE FORA, E ISSO É HONESTIDADE, NÃO PREGUIÇA: o jogo previsto não tem
            // linha no banco nem duplas definidas ("Vencedor Quartas 1"), então não há como saber
            // quem joga — e sem isso a auditoria de impedimento e de jogos seguidos não responde.
            // Dizer "nada muda" ali seria inventar.
            if (refA.EhPrevia || refB.EhPrevia)
            {
                return Json(new
                {
                    grau = "previa",
                    texto = "Jogo previsto: só dá pra conferir depois que a fase anterior terminar e "
                          + "as duplas forem conhecidas.",
                });
            }

            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var jogos = await _context.Partidas
                .Include(p => p.Categoria)
                .Where(p => p.TorneioId == id)
                .OrderBy(p => p.Id)
                .ToListAsync();

            // Qualificado: a AÇÃO se chama igual ao serviço, de propósito — a rota
            // `/Torneios/ImpactoDaTroca` é o nome certo pra quem lê o Network do navegador.
            var impacto = Padelizou.Services.ImpactoDaTroca.Avaliar(torneio, jogos,
                torneio.Categorias.SelectMany(c => c.Duplas).ToList(), await SedesAsync(id),
                jogos.FirstOrDefault(p => p.Id == refA.PartidaId),
                jogos.FirstOrDefault(p => p.Id == refB.PartidaId));

            return Json(new { grau = impacto.Grau.ToString().ToLowerInvariant(), texto = impacto.Texto });
        }

        // AJUSTAR HORÁRIOS — o irmão manso do "Recalcular horários" (10/09/2026).
        //
        // 🗣️ *"temos q pensar melhor esse botão q ele seja mais inteligente, por que hoje ele refaz
        // tudo e as vezes deixa impedimentos ainda, por que eu alterei na mao"*. O Recalcular joga a
        // grade fora e monta outra; este NÃO desmarca nada — só troca jogos de lugar enquanto isso
        // derrubar pontos do Conferir grade (Services/ReparoDaGrade). O que o organizador arrumou na
        // mão fica de pé, a menos que trocá-lo melhore o conjunto.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarHorarios(int id, string? voltarPara = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            // Por Id: a mesma ordem da fila do sorteio (ver RecalcularAGradeAsync).
            var jogos = await _context.Partidas.Where(p => p.TorneioId == id).OrderBy(p => p.Id).ToListAsync();

            var resultado = ReparoDaGrade.Reparar(torneio, jogos,
                torneio.Categorias.SelectMany(c => c.Duplas).ToList(), await SedesAsync(id));

            if (resultado.Trocas > 0) await _context.SaveChangesAsync();

            TempData[resultado.Trocas > 0 ? "Sucesso" : "Aviso"] = resultado.Trocas == 0
                ? "Não achei troca que melhorasse a grade — ela já está no melhor arranjo que as "
                  + "restrições permitem. O que sobrou no Conferir grade se resolve falando com as duplas."
                : $"Ajustei {resultado.Trocas} jogo(s) de lugar: o Conferir grade saiu de "
                  + $"{resultado.AchadosAntes} para {resultado.AchadosDepois} ponto(s). "
                  + "Nenhum horário foi refeito — só trocas.";

            return VoltarPara(voltarPara, id);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TrocarQuadra(int id, int jogoId, string quadra, string? voltarPara = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var doTorneio = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
            var jogo = doTorneio.FirstOrDefault(p => p.Id == jogoId);
            var quadras = await QuadrasEmUsoAsync(id);

            if (TrocaDeQuadra.MotivoParaNaoMudar(jogo, quadra, id, quadras) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return VoltarPara(voltarPara, id);
            }

            var ocupante = TrocaDeQuadra.QuemOcupa(jogo!, quadra, doTorneio);
            var deOnde = jogo!.NomeQuadra;

            // A quadra pode estar RESERVADA pra uma eliminatória que ainda vai nascer
            // (Models/ReservaDeHorario) — olhando só os jogos reais ela parecia livre, o jogo ia pra
            // lá e a final nascia em cima dele (revisão adversarial, 10/09/2026). Mesma regra do
            // dono real: a reserva troca de quadra com o jogo, em vez de recusar.
            string? previstoQueCedeu = null;
            if (ocupante == null)
            {
                var reservas = await ReservasDeHorario.DoTorneio(_context, id).ToListAsync();
                var reservaOcupante = ReservasDeHorario.QuemReservou(reservas,
                    doTorneio.Select(p => (p.CategoriaId, p.Fase)).ToHashSet(), jogo.HorarioPrevisto, quadra);
                if (reservaOcupante != null)
                {
                    reservaOcupante.NomeQuadra = deOnde;
                    previstoQueCedeu = ReservasDeHorario.Rotulo(reservaOcupante, await _context.Categorias
                        .Where(c => c.Id == reservaOcupante.CategoriaId).Select(c => c.Nome).FirstOrDefaultAsync());
                }
            }

            // A câmera é da QUADRA e não viaja com o jogo: o mapa sai dos jogos como estão
            // AGORA, antes da troca. Ver Services/TransmissaoDaQuadra.
            var cameras = TransmissaoDaQuadra.PorQuadra(doTorneio);
            var linkAntes = jogo.LinkTransmissao;

            TrocaDeQuadra.Mudar(jogo, quadra, ocupante, cameras);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = (ocupante != null
                ? $"Quadras trocadas: o jogo {jogo.Codigo} vai pra {quadra} e o {ocupante.Codigo} " +
                  $"assume a {deOnde ?? "quadra que estava livre"}."
                : previstoQueCedeu != null
                ? $"Quadras trocadas: o jogo {jogo.Codigo} vai pra {quadra}, e {previstoQueCedeu} (prévia), " +
                  $"que estava reservado pra ela, passa pra {deOnde ?? "a quadra que estava livre"}."
                : $"O jogo {jogo.Codigo} agora é na {quadra}.")
                + (jogo.LinkTransmissao == linkAntes ? "" :
                   string.IsNullOrEmpty(jogo.LinkTransmissao)
                       ? " A transmissão saiu junto: a nova quadra não tem câmera cadastrada."
                       : " A transmissão passou a ser a da nova quadra.");

            return VoltarPara(voltarPara, id);
        }

        // Troca de horário entre dois jogos, depois do sorteio. A grade automática acerta a
        // conta; quem conhece a vida (a dupla que só chega às 10h, o jogo que rende mais com
        // público) é o organizador — e ele troca o slot inteiro (hora + quadra) de A com B.
        // Regras em Services/TrocaDeHorario.
        //
        // ⚠️ `jogoA`/`jogoB` SÃO REFERÊNCIAS, não Ids (10/09/2026, Services/ReferenciaDoJogo): o
        // Id do jogo real, ou "previa:<categoria>:<fase>:<n>" da eliminatória que ainda não nasceu.
        // 🗣️ *"permita também trocar de horário as eliminatórias, não apenas as de chave"* — as
        // finais do Er estavam na tela com o selo "prévia", e a prévia não tinha botão.
        //
        // Com prévia no meio, o slot que a prévia recebe vira RESERVA (Models/ReservaDeHorario) e
        // o robô a transforma em jogo quando a rodada nascer. A troca é CONFERIDA antes de gravar:
        // a prévia é refeita com a reserva nova, e se a reserva não pegou (o jogo cairia antes de
        // a fase anterior da categoria terminar — ver ReservasDeHorario.Vale), a troca é recusada
        // com o motivo, em vez de gravar uma promessa que a tela ia desmentir.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> TrocarHorario(int id, string? jogoA, string? jogoB, string? voltarPara = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var refA = ReferenciaDoJogo.Ler(jogoA);
            var refB = ReferenciaDoJogo.Ler(jogoB);
            if (refA == null || refB == null)
            {
                TempData["Erro"] = "Não encontrei um dos jogos.";
                return VoltarPara(voltarPara, id);
            }

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // O "por ordem de liberação" TAMBÉM troca prévia (10/09/2026): ele tem hora desde 09/09, e
            // o robô passou a dar hora à rodada nova nele (RoboDoChaveamento.AgendarNaGradeAsync) —
            // a reserva vira jogo com a hora reservada e sem quadra, que é como o modo funciona.

            // A prévia de agora, pelo mesmo caminho da tela — é dela que saem o slot do jogo
            // previsto e a conferência de depois. Categoria e duplas vêm junto porque a projeção
            // lê o nome delas.
            var partidas = await _context.Partidas
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1)
                .Include(p => p.Dupla2)
                .Where(p => p.TorneioId == id)
                .ToListAsync();
            var reservas = await ReservasDeHorario.DoTorneio(_context, id).ToListAsync();
            var projetados = await ProjetarProximasFasesAsync(id, partidas, reservas);
            // Com as SEDES: a categoria presa em casa não vai pro slot do Radar por uma troca na mão
            // (PR #120) — vale igual quando um dos lados é prévia.
            var sedes = await SedesAsync(id);

            TrocaDeHorario.Lado Resolver(ReferenciaDoJogo referencia) => referencia.EhPrevia
                ? new TrocaDeHorario.Lado(referencia, null, projetados.FirstOrDefault(j =>
                    j.CategoriaId == referencia.CategoriaId && j.Fase == referencia.Fase && j.Numero == referencia.Numero))
                : new TrocaDeHorario.Lado(referencia, partidas.FirstOrDefault(p => p.Id == referencia.PartidaId), null);

            var ladoA = Resolver(refA);
            var ladoB = Resolver(refB);

            if (TrocaDeHorario.MotivoParaNaoTrocar(ladoA, ladoB, id, sedes) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return VoltarPara(voltarPara, id);
            }

            // O slot é o TRIO hora + quadra + clube (PR #120: "o clube é pelo horário"). O clube
            // do slot de uma prévia sai da quadra dela — e, sem quadra cadastrada, é o do torneio,
            // como CarimbarOClube faria.
            var slotDeA = (Horario: ladoA.Horario!.Value, Quadra: ladoA.Quadra, Clube: ladoA.ClubeDaVaga(sedes) ?? torneio.ClubeId);
            var slotDeB = (Horario: ladoB.Horario!.Value, Quadra: ladoB.Quadra, Clube: ladoB.ClubeDaVaga(sedes) ?? torneio.ClubeId);

            // Cada lado recebe o slot do outro: o real na própria linha, o previsto numa reserva
            // (a dele, se já tinha — a PK composta garante que é uma só).
            //
            // ⚠️ NO "POR ORDEM" A QUADRA DA PRÉVIA NÃO VAI PRO JOGO REAL (10/09/2026, ensaio do Er):
            // a projeção dá quadra a cada jogo previsto só pra contar vagas; nesse modo os jogos
            // reais não têm quadra (a Mesa chama), e três deles apareciam com "Arena 4" no meio de
            // 43 sem quadra — na lista, no ICS e na Home do jogador. Hora e clube vêm do slot;
            // quadra só quando o slot era de um jogo real (a que o balcão deu, se deu).
            void Receber(TrocaDeHorario.Lado lado, (DateTime Horario, string? Quadra, int Clube) slot, bool slotDePrevia)
            {
                if (lado.Real is Partida real)
                {
                    real.HorarioPrevisto = slot.Horario;
                    real.NomeQuadra = torneio.SemHorarioPrevisto && slotDePrevia ? null : slot.Quadra;
                    real.ClubeId = slot.Clube;
                    return;
                }

                var referencia = lado.Referencia;
                var reserva = reservas.FirstOrDefault(r =>
                    r.CategoriaId == referencia.CategoriaId && r.Fase == referencia.Fase && r.Numero == referencia.Numero);
                if (reserva == null)
                {
                    reserva = new ReservaDeHorario
                    {
                        CategoriaId = referencia.CategoriaId,
                        Fase = referencia.Fase,
                        Numero = referencia.Numero,
                    };
                    reservas.Add(reserva);
                    _context.ReservasDeHorario.Add(reserva);
                }

                reserva.Horario = slot.Horario;
                reserva.NomeQuadra = slot.Quadra;
            }

            Receber(ladoA, slotDeB, slotDePrevia: ladoB.Previsto != null);
            Receber(ladoB, slotDeA, slotDePrevia: ladoA.Previsto != null);

            // A CONFERÊNCIA: a prévia refeita com a troca tem que mostrar cada jogo previsto no
            // slot que ele recebeu. Se não mostra, a reserva não vale (o jogo cairia antes de a
            // fase anterior da categoria dele terminar) — e aí nada é gravado: o contexto é da
            // requisição, e sair sem SaveChanges deixa o banco como estava.
            var conferencia = await ProjetarProximasFasesAsync(id, partidas, reservas);
            foreach (var (lado, slot) in new[] { (ladoA, slotDeB), (ladoB, slotDeA) })
            {
                if (lado.Previsto == null) continue;

                var referencia = lado.Referencia;
                var depois = conferencia.FirstOrDefault(j =>
                    j.CategoriaId == referencia.CategoriaId && j.Fase == referencia.Fase && j.Numero == referencia.Numero);

                if (depois == null || depois.Horario != slot.Horario)
                {
                    TempData["Erro"] = $"Não dá pra pôr {lado.Rotulo} às {slot.Horario:dd/MM HH:mm}: nesse horário a fase " +
                        "anterior dessa categoria ainda não terminou. Troque com um jogo mais tarde.";
                    return VoltarPara(voltarPara, id);
                }
            }

            // ⚠️ AS OUTRAS RESERVAS QUE ESTA TROCA MATOU (revisão adversarial, 10/09/2026): mover
            // uma semifinal pra mais tarde faz a reserva da final dessa categoria deixar de valer.
            // Ela não fica no banco calada — prévia e robô a ignorariam e o organizador só
            // descobriria olhando. A troca acontece, a reserva morta some, e a mensagem diz.
            var fasesReais = partidas.Select(p => (p.CategoriaId, p.Fase)).ToHashSet();
            var mortas = ReservasDeHorario.QueNaoValemMais(reservas, fasesReais, conferencia);
            _context.ReservasDeHorario.RemoveRange(mortas);

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Horários trocados: agora {ladoA.Rotulo} é {slotDeB.Horario:dd/MM HH:mm} e " +
                $"{ladoB.Rotulo} é {slotDeA.Horario:dd/MM HH:mm}." +
                (ladoA.Previsto != null || ladoB.Previsto != null
                    ? " O jogo previsto nasce nesse horário quando a fase anterior terminar."
                    : "");

            // ⚠️ NA MESMA MENSAGEM, e não num TempData["Aviso"] à parte: a página Jogos, de onde
            // o organizador troca, só mostra Sucesso e Erro (o "Aviso" é do Details) — o aviso
            // separado era descartado calado, exatamente o que ele existe pra não ser.
            if (mortas.Count > 0)
            {
                var nomeDaCategoria = partidas
                    .GroupBy(p => p.CategoriaId)
                    .ToDictionary(g => g.Key, g => g.First().Categoria.Nome);
                TempData["Sucesso"] += " ⚠️ Com essa troca, deixou de valer e foi desfeita a reserva de: " +
                    string.Join("; ", mortas.Select(r =>
                        $"{ReservasDeHorario.Rotulo(r, nomeDaCategoria.GetValueOrDefault(r.CategoriaId))} " +
                        $"({r.Horario:dd/MM HH:mm})")) +
                    ". A fase anterior passou desse horário — o jogo volta pra grade.";
            }

            return VoltarPara(voltarPara, id);
        }

        // Definir o horário de UM jogo na mão, digitando a hora. 🗣️ *"permita tambem, trocar o
        // horario na mão, na lista de jogos, para nós organizadores"* (Felipe, 10/09/2026). A
        // troca ⇄ acima exige outro jogo pra trocar de slot; aqui só este muda. Regras em
        // Services/HorarioNaMao. `jogo` é a mesma referência da troca: Id do jogo real, ou
        // "previa:<categoria>:<fase>:<n>" — a prévia vira a mesma reserva, conferida do mesmo
        // jeito (a hora tem que ser depois de a fase anterior da categoria terminar).
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DefinirHorario(int id, string? jogo, DateTime? horario, string? voltarPara = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var referencia = ReferenciaDoJogo.Ler(jogo);
            if (referencia == null)
            {
                TempData["Erro"] = "Não encontrei o jogo.";
                return VoltarPara(voltarPara, id);
            }
            if (horario is not DateTime hora)
            {
                TempData["Erro"] = "Escolha um horário.";
                return VoltarPara(voltarPara, id);
            }

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var partidas = await _context.Partidas
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1)
                .Include(p => p.Dupla2)
                .Where(p => p.TorneioId == id)
                .ToListAsync();
            var reservas = await ReservasDeHorario.DoTorneio(_context, id).ToListAsync();
            var sedes = await SedesAsync(id);
            string rotulo;
            Partida? real = null;

            if (referencia.EhPrevia)
            {
                var projetados = await ProjetarProximasFasesAsync(id, partidas, reservas);
                var previsto = projetados.FirstOrDefault(j =>
                    j.CategoriaId == referencia.CategoriaId && j.Fase == referencia.Fase && j.Numero == referencia.Numero);
                if (previsto == null)
                {
                    TempData["Erro"] = "Não encontrei esse jogo previsto.";
                    return VoltarPara(voltarPara, id);
                }

                var reserva = reservas.FirstOrDefault(r =>
                    r.CategoriaId == referencia.CategoriaId && r.Fase == referencia.Fase && r.Numero == referencia.Numero);
                if (reserva == null)
                {
                    reserva = new ReservaDeHorario
                    {
                        CategoriaId = referencia.CategoriaId, Fase = referencia.Fase, Numero = referencia.Numero,
                    };
                    reservas.Add(reserva);
                    _context.ReservasDeHorario.Add(reserva);
                }
                // Hora digitada não traz quadra: o robô escolhe (ou o balcão, no por ordem).
                reserva.Horario = hora;
                reserva.NomeQuadra = null;
                rotulo = $"{previsto.Categoria} · {previsto.FaseNumerada}";
            }
            else
            {
                real = partidas.FirstOrDefault(p => p.Id == referencia.PartidaId);
                var motivo = HorarioNaMao.MotivoParaNaoDefinir(real, id, hora, partidas);
                if (real == null || motivo != null)
                {
                    TempData["Erro"] = motivo ?? "Não encontrei o jogo.";
                    return VoltarPara(voltarPara, id);
                }

                var quadras = await _context.Quadras.Where(q => q.TorneioId == id).ToListAsync();
                HorarioNaMao.Definir(real, hora, HorarioNaMao.ClubeParaOHorario(real, hora, torneio, quadras));
                rotulo = $"o jogo {real.Codigo}";
            }

            // A CONFERÊNCIA, a mesma da troca: a prévia refeita tem que mostrar o jogo previsto
            // na hora pedida — senão a reserva não vale (a fase anterior da categoria ainda não
            // terminou nessa hora) e nada é gravado. E as reservas que a hora nova matou (a
            // semifinal que atrasou e passou da final reservada) somem, com aviso.
            var conferencia = await ProjetarProximasFasesAsync(id, partidas, reservas);
            if (referencia.EhPrevia)
            {
                var depois = conferencia.FirstOrDefault(j =>
                    j.CategoriaId == referencia.CategoriaId && j.Fase == referencia.Fase && j.Numero == referencia.Numero);
                if (depois == null || depois.Horario != hora)
                {
                    TempData["Erro"] = $"Não dá pra pôr {rotulo} às {hora:dd/MM HH:mm}: nesse horário a fase " +
                        "anterior dessa categoria ainda não terminou. Escolha uma hora mais tarde.";
                    return VoltarPara(voltarPara, id);
                }
            }

            var fasesReais = partidas.Select(p => (p.CategoriaId, p.Fase)).ToHashSet();
            var mortas = ReservasDeHorario.QueNaoValemMais(reservas, fasesReais, conferencia);
            _context.ReservasDeHorario.RemoveRange(mortas);

            await _context.SaveChangesAsync();

            var onde = real == null ? null : LugarDoJogo.Etiqueta(sedes, real.NomeQuadra, real.CategoriaId, real.ClubeId);
            TempData["Sucesso"] = $"Agora {rotulo} é {hora:dd/MM HH:mm}" + (onde != null ? $" · {onde}" : "") + "." +
                (referencia.EhPrevia ? " O jogo previsto nasce nesse horário quando a fase anterior terminar." : "");

            if (mortas.Count > 0)
            {
                var nomeDaCategoria = partidas
                    .GroupBy(p => p.CategoriaId)
                    .ToDictionary(g => g.Key, g => g.First().Categoria.Nome);
                TempData["Sucesso"] += " ⚠️ Com essa hora, deixou de valer e foi desfeita a reserva de: " +
                    string.Join("; ", mortas.Select(r =>
                        $"{ReservasDeHorario.Rotulo(r, nomeDaCategoria.GetValueOrDefault(r.CategoriaId))} " +
                        $"({r.Horario:dd/MM HH:mm})")) +
                    ". A fase anterior passou desse horário — o jogo volta pra grade.";
            }

            return VoltarPara(voltarPara, id);
        }

        // Push de "chaves publicadas". É o momento em que o torneio deixa de ser uma lista de
        // inscritos e vira jogo com hora marcada — e até agora o jogador só descobria isso
        // abrindo o site por conta própria.
        //
        // O aviso vai personalizado com o horário do PRIMEIRO jogo de cada um: "as chaves
        // saíram" sozinho obriga a pessoa a ir procurar; "você joga sábado às 9h" resolve.
        private async Task AvisarChavesPublicadasAsync(Torneio torneio, List<Partida> jogos)
        {
            try
            {
                // Primeiro jogo de cada dupla, e daí de cada jogador.
                var primeiroPorDupla = jogos
                    .Where(p => p.HorarioPrevisto != null)
                    .SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }.Select(d => (DuplaId: d, p.HorarioPrevisto)))
                    .GroupBy(x => x.DuplaId)
                    .ToDictionary(g => g.Key, g => g.Min(x => x.HorarioPrevisto));

                // Dupla-TIME fora: o Jogador1Id dela é o organizador, e "você joga sábado
                // às 9h" no celular dele seria o jogo de um time, não o dele.
                var duplas = await _context.Duplas
                    .Where(d => primeiroPorDupla.Keys.Contains(d.Id) && d.NomeTime == null)
                    .Select(d => new { d.Id, d.Jogador1Id, d.Jogador2Id })
                    .ToListAsync();

                // Um jogador pode estar em mais de uma categoria: vale o jogo mais cedo.
                var primeiroPorJogador = new Dictionary<int, DateTime?>();
                foreach (var d in duplas)
                {
                    var quando = primeiroPorDupla.GetValueOrDefault(d.Id);
                    foreach (var jogadorId in new[] { d.Jogador1Id, d.Jogador2Id })
                    {
                        if (jogadorId == null) continue;
                        var atual = primeiroPorJogador.GetValueOrDefault(jogadorId.Value);
                        if (atual == null || (quando != null && quando < atual))
                            primeiroPorJogador[jogadorId.Value] = quando;
                    }
                }

                var url = Url.Action("Jogos", "Torneios", new { id = torneio.Id });

                foreach (var (jogadorId, quando) in primeiroPorJogador)
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId,
                        $"Chaves do {torneio.Nome} saíram!",
                        AvisosDoDiaDeJogo.CorpoDasChaves(quando),
                        // Diz a que HORAS a pessoa joga. Quem não vir isso aparece na hora
                        // errada — ou não aparece.
                        //
                        // ⚠️ FORA DO WHATSAPP desde 21/08/2026 (ver EncerramentoDaPartida): era
                        // o pior formato pro canal — 1 mensagem por jogador DE UMA VEZ, 100 num
                        // torneio cheio, todas com texto quase igual. É a rajada de texto
                        // repetido, que é a assinatura de spam que a Meta lê.
                        url, AlcanceDoAviso.SoApp);
                }
            }
            catch (Exception ex)
            {
                // Push é acessório: as chaves já estão sorteadas e gravadas. Derrubar o
                // sorteio por causa de uma notificação seria trocar o essencial pelo enfeite.
                _logger.LogWarning(ex, "Falha ao avisar chaves publicadas do torneio {TorneioId}.", torneio.Id);
            }
        }
        // Põe na tela o "não coube, e foi por isto" — a segunda metade do pedido do Felipe de
        // 09/09/2026. A régua mora em Services/PorQueNaoCoube; aqui só se escolhe o cabeçalho, que
        // depende de uma coisa que a régua não sabe: se a grade DE FATO passou do prazo.
        //
        // ⚠️ NÃO TRAVA O SORTEIO, e é escolha. Jogo sem horário é o único desfecho que o motor não
        // aceita (GradeDeJogos.Encaixar), e recusar o sorteio deixaria o organizador sem grade
        // nenhuma na véspera. Ele avisa, com a causa na mão, e quem decide é quem alugou a quadra.
        private async Task AvisarSeNaoCoubeAsync(Torneio torneio, List<Partida> jogos, SedesDoTorneio sedes)
        {
            // Sem prazo não há o que estourar; e o "por ordem de liberação" não tem relógio que
            // valha, então avisar sobre hora seria assustar com número inventado.
            if (torneio.DataFim is not DateTime prazo || torneio.SemHorarioPrevisto) return;

            var quadras = await _context.Quadras.Where(q => q.TorneioId == torneio.Id).ToListAsync();

            var ultimo = jogos.Where(j => j.HorarioPrevisto != null)
                .Select(j => j.HorarioPrevisto!.Value)
                .DefaultIfEmpty()
                .Max();

            // ⚠️ O TOTAL É O PROJETADO, não os jogos que acabaram de nascer: no sorteio só existem
            // os grupos e a primeira rodada da chave direta — o mata-mata inteiro ainda vai nascer
            // pelo robô, e ele também precisa de quadra. `MontarPrevisaoDaGrade` já faz essa conta,
            // e é a mesma que a tela de previsão mostra.
            var motivos = PorQueNaoCoube.Analisar(torneio, quadras, sedes,
                MontarPrevisaoDaGrade(torneio).TotalDeJogos,
                ultimo == default ? null : ultimo);

            if (motivos.Count == 0) return;

            bool passou = ultimo != default && ultimo.Date > prazo.Date;

            TempData["Aviso"] = (passou
                    ? $"A grade passou do fim do torneio ({prazo:dd/MM}): o último jogo ficou "
                      + $"{ultimo:dd/MM 'às' HH:mm}. "
                    : $"A grade coube até {prazo:dd/MM}, mas tem coisa pra olhar. ")
                + string.Join(" ", motivos);
        }

        // Projeta a grade inteira ANTES do sorteio: quantos jogos saem das duplas já
        // inscritas e a que horas o último termina. Cada categoria tem os próprios grupos e
        // o próprio mata-mata, mas todas dividem as mesmas quadras — por isso os jogos se
        // somam antes de virar horário.
        private static PrevisaoGradeVM MontarPrevisaoDaGrade(Torneio torneio)
        {
            int duplas = 0, grupos = 0, jogosDeGrupo = 0, jogosDeMataMata = 0;

            foreach (var categoria in torneio.Categorias)
            {
                // Times: a estrutura vem do organizador, não da regra de grupos de 3.
                if (categoria.DeTimes)
                {
                    int times = categoria.Duplas.Count(d => d.NomeTime != null && !d.EmListaDeEspera);
                    int gruposDeTimes = categoria.QuantidadeGrupos ?? 1;
                    if (times < 2 || gruposDeTimes < 1) continue;

                    duplas += times;
                    grupos += gruposDeTimes;
                    jogosDeGrupo += CategoriaDeTimes.JogosDeGrupo(times, gruposDeTimes);
                    jogosDeMataMata += CategoriaDeTimes.JogosDeMataMata(
                        gruposDeTimes, categoria.ClassificadosPorGrupo ?? 2);
                    continue;
                }

                // Chave direta não tem fase de grupos: são N-1 jogos e pronto (cada jogo
                // elimina uma dupla até sobrar o campeão). Contá-la pela régua dos grupos
                // inventava grupos que não existem e inflava a previsão — com 24 duplas, a
                // tela prometia 22 grupos e 98 jogos num torneio que tem bem menos.
                if (categoria.ChaveDireta)
                {
                    int naChave = categoria.Duplas.Count(d => !ForaDoSorteio.FicaDeFora(d));
                    if (naChave < 2) continue;

                    duplas += naChave;
                    jogosDeMataMata += naChave - 1;
                    continue;
                }

                // ⚠️ A MESMA RÉGUA DO SORTEIO, LIDA DO MESMO LUGAR (`ForaDoSorteio`), e isso é o
                // conserto de um defeito de 09/09/2026: aqui a régua estava COPIADA À MÃO
                // (`d.Jogador2Id != null`), então quando a inscrição sem parceiro passou a entrar
                // na chave o grep pelo nome da régua não alcançou esta linha. A previsão continuou
                // prometendo a grade das duplas FECHADAS enquanto o sorteio fazia a de todas —
                // menos grupos, menos jogos, menos quadra alugada, no painel que diz com todas as
                // letras que "os números são REAIS".
                //
                // E errava nos DOIS sentidos: sem o filtro de lista de espera, contava dupla
                // fechada que o sorteio deixa de fora. Num cenário com uma sozinha e uma na espera
                // os dois erros se cancelavam — foi assim que a suíte ficou verde.
                int daCategoria = categoria.Duplas.Count(d => !ForaDoSorteio.FicaDeFora(d));
                var (g, jogos) = PrevisaoDoTorneio.FaseDeGrupos(daCategoria);

                duplas += daCategoria;
                grupos += g;
                jogosDeGrupo += jogos;
                jogosDeMataMata += PrevisaoDoTorneio.MataMata(g);
            }

            int total = jogosDeGrupo + jogosDeMataMata;
            var inicio = torneio.AberturaDaGrade;

            var ultimo = PrevisaoDoTorneio.UltimoJogo(
                inicio, torneio.HoraFimDoDia, torneio.HoraInicioDiasSeguintes,
                torneio.QuantidadeQuadras, torneio.TempoPrevistoPartidaMinutos, total);

            var duracao = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
            var fim = ultimo?.AddMinutes(duracao) ?? inicio;

            return new PrevisaoGradeVM
            {
                Duplas = duplas,
                Grupos = grupos,
                JogosDeGrupo = jogosDeGrupo,
                JogosDeMataMata = jogosDeMataMata,
                TotalDeJogos = total,
                Inicio = inicio,
                FimPrevisto = fim,
                Dias = ultimo == null ? 1 : PrevisaoDoTorneio.DiasOcupados(inicio, ultimo.Value),
                SemHorarioPrevisto = torneio.SemHorarioPrevisto,
                // Comparação por DIA: o limite é "até domingo", não "até domingo às 00h".
                // Sem horário previsto não existe prazo a estourar: não há hora marcada pra
                // comparar, e avisar "passa do dia" com base numa conta que não vai valer
                // seria assustar o organizador com um número inventado.
                EstouraOPrazo = !torneio.SemHorarioPrevisto
                                && torneio.DataFim != null && ultimo != null
                                && ultimo.Value.Date > torneio.DataFim.Value.Date
            };
        }

        // ⚠️ DAQUI PRA BAIXO É TUDO ATALHO PRO MOTOR ÚNICO (Services/RoboDoChaveamento).
        //
        // O robô que monta a chave e a grade que lhe dá hora e quadra moram lá, e não aqui,
        // porque a OUTRA tela que finaliza partida (PartidasController, o Controle de Placar
        // em tela cheia) precisa exatamente das mesmas regras. Enquanto cada controller teve a
        // sua cópia, o chaveamento do torneio dependia de POR ONDE o placar foi lançado.
        private RoboDoChaveamento Robo => new(_context);

        // Quem de fato ocupa a quadra quando cada dupla joga: as DUAS pessoas dela.
        private static Dictionary<int, int[]> OcupantesPorDupla(IEnumerable<Dupla> duplas) =>
            RoboDoChaveamento.OcupantesPorDupla(duplas);

        private static Dictionary<int, int[]> OcupantesPorDupla(Torneio torneio) =>
            RoboDoChaveamento.OcupantesPorDupla(torneio);

        // Os nomes das quadras do torneio, na ordem, e as que ele está DE FATO usando.
        private Task<List<string>> QuadrasDoTorneioAsync(int torneioId) =>
            Robo.QuadrasDoTorneioAsync(torneioId);

        private Task<List<string>> QuadrasEmUsoAsync(int torneioId) =>
            Robo.QuadrasEmUsoAsync(torneioId);

        // A quadra preferida de cada categoria (Services/PreferenciaDeQuadra).
        private Task<Dictionary<int, string[]>> QuadrasPreferidasAsync(int torneioId) =>
            Robo.QuadrasPreferidasAsync(torneioId);

        // O torneio em mais de um clube (Services/SedesDoTorneio).
        private Task<SedesDoTorneio> SedesAsync(int torneioId) =>
            Robo.SedesAsync(torneioId);

        // Hora e quadra da rodada nova (Services/RoboDoChaveamento.AgendarNaGradeAsync).
        private Task AgendarNaGradeAsync(List<Partida> jogos, int? torneioId) =>
            Robo.AgendarNaGradeAsync(jogos, torneioId);

        // ROBÔ DE PROGRESSÃO: Primeira Rodada → Oitavas → Quartas → Semifinal → Final.
        private Task ProcessarAvancoMataMataAutomatico(int categoriaId, int? torneioId, string faseConcluida) =>
            Robo.AvancarFaseAsync(categoriaId, torneioId, faseConcluida);

        // ROBÔ INVISÍVEL DE CRUZAMENTO DE CHAVES: fim dos grupos → primeira rodada.
        private Task ProcessarMataMataAutomatico(int categoriaId, int? torneioId) =>
            Robo.MontarMataMataDosGruposAsync(categoriaId, torneioId);

      

    }
}
