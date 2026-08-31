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
                // Só entra no sorteio quem está pronto pra jogar: dupla fechada (com os dois
                // nomes) e confirmada. Quem está na lista de espera ou ainda sem parceiro
                // continua inscrito, mas fora das chaves.
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
                var duplasOrdenadas = duplas
                    .OrderByDescending(d => pontosPorJogador.GetValueOrDefault(d.Jogador1Id)
                                          + pontosPorJogador.GetValueOrDefault(d.Jogador2Id!.Value))
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

                        // DISTRIBUIÇÃO EM ZIGUE-ZAGUE (balanceia 1 cabeça de chave forte/médio/fraco por grupo)
                        int grupoIndex = 0;
                        int direcao = 1;
                        foreach (var dupla in restantes)
                        {
                            bucket[grupoIndex].Add(dupla);

                            grupoIndex += direcao;
                            if (grupoIndex >= numGruposDeTres)
                            {
                                grupoIndex = numGruposDeTres - 1;
                                direcao = -1;
                            }
                            else if (grupoIndex < 0)
                            {
                                grupoIndex = 0;
                                direcao = 1;
                            }
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
            var deChaveDireta = torneio.Categorias.Where(c => c.ChaveDireta).Select(c => c.Id).ToHashSet();
            jogosPraAgendar = OrdemDaFila(jogosPraAgendar, deChaveDireta);

            // Torneio "por ordem de liberação": os jogos ficam SEM hora e vão pra quadra
            // conforme ela vaga, chamados pela Mesa. A ordem é a de criação — a mesma em que
            // eles entrariam na grade. Não calcular horário aqui é o ponto: horário inventado
            // que ninguém cumpre é pior que horário nenhum, porque o jogador confia nele.
            if (torneio.SemHorarioPrevisto)
            {
                _context.Partidas.AddRange(jogosPraAgendar);
                // Sorteado e gravado, mas ainda não é público — fica esperando quem aprova
                // (ver Services/AprovacaoDeChaves). O aviso "as chaves saíram" só sai daqui a
                // pouco, em AprovarChaves.
                torneio.Status = AprovacaoDeChaves.Pendente;
                await _context.SaveChangesAsync();
                return RedirectToAction("Details", new { id = torneio.Id });
            }

            // Agora sim os horários. A regra mora em EncaixarNasLevas, compartilhada com o
            // "Refazer grade" — duas cópias divergiriam e o torneio teria duas grades.
            EncaixarNasLevas(torneio, jogosPraAgendar, deChaveDireta,
                OcupantesPorDupla(torneio), await QuadrasDoTorneioAsync(torneio.Id),
                quadrasPorCategoria: await QuadrasPreferidasAsync(torneio.Id),
                janelas: JanelasDeImpedimento.PorDupla(torneio),
                sedes: await SedesAsync(torneio.Id));

            _context.Partidas.AddRange(jogosPraAgendar);

            // Sorteado e gravado, mas ainda não é público — fica esperando quem aprova (ver
            // Services/AprovacaoDeChaves). O aviso "as chaves saíram" só sai daqui a pouco, em
            // AprovarChaves.
            torneio.Status = AprovacaoDeChaves.Pendente;
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // A APROVAÇÃO das chaves: o passo entre "sorteado" e "público", pedido pelo Felipe.
        // Ninguém fora de quem organiza/administra vê nada até aqui — nem os inscritos (ver
        // TorneiosController.cs, ação Jogos, e Details.cshtml, aba Chaves e Grupos).
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AprovarChaves(int id)
        {
            var torneio = await _context.Torneios.FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();
            if (torneio.Status != AprovacaoDeChaves.Pendente)
            {
                TempData["Erro"] = "Este torneio não tem chave esperando aprovação.";
                return RedirectToAction("Details", new { id });
            }
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            torneio.Status = "Fase de Grupos";
            await _context.SaveChangesAsync();

            // O aviso "as chaves saíram" nasce AQUI, não no sorteio — é agora que elas ficam
            // de verdade visíveis pra quem joga.
            var jogos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
            await AvisarChavesPublicadasAsync(torneio, jogos);

            TempData["Sucesso"] = "Chaves aprovadas — já estão visíveis pra todo mundo.";
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

            torneio.Status = "Chaves em Sorteio";
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Sorteio desfeito — pode sortear de novo quando quiser.";
            return RedirectToAction("Details", new { id });
        }

        // A ORDEM DA FILA É O CAMINHO CRÍTICO DO TORNEIO.
        //
        // A primeira rodada de uma CHAVE DIRETA não espera resultado de ninguém: as duplas já
        // estão definidas. E é ela que tem mais fases pela frente — uma chave de 24 duplas são
        // CINCO rodadas até a final, contra as três de uma categoria que sai de grupos. Deixá-la
        // pro fim da fase de grupos empurrava essas cinco rodadas todas pra depois, e no Interno
        // isso jogou a final da chave geral pras 23h18.
        //
        // Por isso ela abre o torneio, junto com os grupos e antes deles na fila. Quem garante
        // que ninguém seja chamado pra duas quadras ao mesmo tempo é o encaixe, que compara
        // PESSOA (GradeDeJogos.Encaixar) — e é justamente essa chave que põe cada jogador em
        // duas duplas no mesmo dia.
        private static readonly string[] OrdemDasFases =
            { ChaveamentoMataMata.PrimeiraRodada, "Oitavas de Final", "Quartas de Final", "Semifinal", "Final" };

        private static List<Partida> OrdemDaFila(IEnumerable<Partida> jogos, ISet<int> categoriasDeChaveDireta) =>
            jogos
                .OrderBy(j => categoriasDeChaveDireta.Contains(j.CategoriaId) ? 0
                            : FasesTorneio.EhFaseDeGrupos(j.Fase) ? 1 : 2)
                .ThenBy(j => Array.IndexOf(OrdemDasFases, j.Fase))
                .ToList();

        // ⚠️ Em DUAS LEVAS, não numa só. Ordenar a fila não basta: quando os jogos que sobram
        // conflitam entre si num horário, o encaixe puxa o próximo livre da fila — e aí um jogo
        // de mata-mata sobe pro meio da fase de grupos. Agendando o que SAI DOS GRUPOS só depois
        // do último jogo de grupo, a ordem das fases deixa de ser preferência e vira garantia.
        //
        // A chave direta fica na PRIMEIRA leva de propósito: ela não sai de grupo nenhum, então
        // não há resultado que ela precise esperar.
        private static void EncaixarNasLevas(Torneio torneio, List<Partida> jogos,
            ISet<int> categoriasDeChaveDireta, IReadOnlyDictionary<int, int[]> ocupantes,
            IReadOnlyList<string> quadras,
            DateTime? aPartirDe = null, IReadOnlyList<Partida>? jaMarcados = null,
            // A quadra preferida de cada categoria, quando o organizador escolheu alguma.
            // Ver Services/PreferenciaDeQuadra.
            IReadOnlyDictionary<int, string[]>? quadrasPorCategoria = null,
            // O impedimento de horário PAGO na inscrição. Ver Services/JanelasDeImpedimento.
            IReadOnlyDictionary<int, (DateTime, DateTime)[]>? janelas = null,
            // O torneio em mais de um clube. Nulo — o caso de quase todos — deixa tudo como era.
            // Ver Services/SedesDoTorneio.
            SedesDoTorneio? sedes = null)
        {
            var abre = aPartirDe ?? torneio.AberturaDaGrade;
            var intocados = jaMarcados ?? Array.Empty<Partida>();
            // ⚠️ QUEM NÃO TEM O QUE ESPERAR ENTRA NA PRIMEIRA LEVA (21/08/2026).
            //
            // Antes, "não espera ninguém" era só chave direta e fase de grupos. O mata-mata de
            // uma categoria que JÁ FECHOU os grupos caía na segunda leva junto com todo o resto
            // — e a segunda leva só começa depois que a primeira acaba. Na prática: a 4ª
            // masculina terminou os grupos ontem, tem semifinal pronta pra entrar em quadra, e
            // ficava atrás de TODA a fase de grupos da 2ª. Quadra vazia com jogo esperando.
            //
            // Uma categoria sem NENHUM jogo de grupo sendo remarcado não tem resultado pendente:
            // ou é de chave direta (não tem grupo), ou os grupos dela já foram jogados. Ela
            // disputa as vagas de igual pra igual com os grupos que faltam.
            var comGrupoPendente = jogos
                .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase))
                .Select(j => j.CategoriaId)
                .ToHashSet();

            var semNadaPraEsperar = new HashSet<int>(categoriasDeChaveDireta);
            foreach (var categoriaId in jogos.Select(j => j.CategoriaId).Distinct())
                if (!comGrupoPendente.Contains(categoriaId)) semNadaPraEsperar.Add(categoriaId);

            bool NaoEsperaNinguem(Partida j) =>
                semNadaPraEsperar.Contains(j.CategoriaId) || FasesTorneio.EhFaseDeGrupos(j.Fase);

            var abertura = OrdemDaFila(jogos.Where(NaoEsperaNinguem), semNadaPraEsperar);
            var depoisDosGrupos = OrdemDaFila(jogos.Where(j => !NaoEsperaNinguem(j)), semNadaPraEsperar);

            // Tudo que já tem hora e quadra e que as levas seguintes precisam enxergar pra não
            // marcar em cima. Começa com os jogos intocados e VAI CRESCENDO a cada leva.
            //
            // ⚠️ ATÉ 21/08/2026 A SEGUNDA LEVA SÓ RECEBIA OS INTOCADOS — os jogos que a primeira
            // leva acabara de marcar ficavam invisíveis pra ela. Não dava problema por acidente:
            // a segunda leva começava depois do último jogo de grupo do torneio INTEIRO, então
            // nunca havia o que atropelar. Com a âncora por categoria (logo abaixo) isso deixa
            // de valer, e sem esta lista as duas levas marcariam duas partidas na mesma quadra
            // no mesmo horário. As duas mudanças andam JUNTAS ou nenhuma das duas anda.
            var jaEmQuadra = new List<Partida>(intocados);

            void Agendar(List<Partida> daLeva, DateTime inicio)
            {
                if (daLeva.Count == 0) return;

                // ⚠️ NENHUMA LEVA COMEÇA ANTES DA ABERTURA DA GRADE. Num "refazer grade" a
                // abertura é AGORA, e a âncora de uma leva vem do fim dos grupos da categoria —
                // que pode ser uma hora do passado quando os grupos dela já foram jogados e os
                // que faltam não couberam na grade. Sem este piso, a grade nasceria em cima de
                // horários que já passaram: jogo marcado pra ontem, que não aparece pra ninguém.
                //
                // É um piso, não o conserto de um bug observado — o caminho é de canto. Fica
                // aqui, e não na conta da âncora, porque aqui vale pra TODA leva.
                if (inicio < abre) inicio = abre;

                var vagas = VagasDaGrade.Montar(torneio, inicio, daLeva.Count, jaEmQuadra);

                GradeDeJogos.Encaixar(daLeva, vagas, VagasDaGrade.Duracao(torneio),
                    ocupantes, quadras, jaEmQuadra, quadrasPorCategoria, janelas, sedes);

                jaEmQuadra.AddRange(daLeva.Where(j => j.HorarioPrevisto != null));
            }

            Agendar(abertura, abre);

            // A hora em que o mata-mata DESTA categoria pode abrir.
            //
            // ⚠️ POR CATEGORIA, NÃO PELO TORNEIO INTEIRO (21/08/2026). Era uma âncora só: o
            // último jogo de grupo de QUALQUER categoria segurava o mata-mata de TODAS. Numa
            // categoria de 8 duplas que fecha os grupos às 15h, a semifinal ficava esperando a
            // categoria de 32 terminar às 21h — e as quadras paradas no meio, que é justamente
            // o que o organizador não pode ter ("nenhuma quadra sem jogo até o fim do torneio").
            //
            // Uma rodada de folga entre as fases, não só o fim do último jogo: quem disputa o
            // último jogo do grupo é candidato a classificar, e emendar o mata-mata em cima
            // dele o poria na quadra no minuto em que saiu dela. Ver AberturaDaProximaFase.
            //
            // Conta os jogos de grupo INTOCADOS junto: num recálculo no meio do torneio a maior
            // parte dos grupos já rolou, e olhar só pros remarcados diria que a fase de grupos
            // acabou cedo — o mata-mata subiria pra cima dela.
            DateTime AberturaDoMataMata(int categoriaId)
            {
                var fimDosGrupos = abertura.Concat(intocados)
                    .Where(j => j.CategoriaId == categoriaId
                             && FasesTorneio.EhFaseDeGrupos(j.Fase) && j.HorarioPrevisto != null)
                    .Select(j => j.HorarioPrevisto!.Value)
                    .DefaultIfEmpty()
                    .Max();

                if (fimDosGrupos == default) return abre;

                return GradeDeJogos.AberturaDaProximaFase(fimDosGrupos, torneio.HoraFimDoDia,
                    torneio.HoraInicioDiasSeguintes, VagasDaGrade.Duracao(torneio));
            }

            // Da categoria que libera primeiro pra que libera por último. O encaixe é guloso e
            // pega a primeira vaga livre: marcar fora dessa ordem daria as vagas mais cedo pra
            // quem ainda nem terminou os grupos.
            foreach (var daCategoria in depoisDosGrupos
                         .GroupBy(j => j.CategoriaId)
                         .Select(g => new { Abre = AberturaDoMataMata(g.Key), Jogos = g.ToList() })
                         .OrderBy(x => x.Abre)
                         .ToList())
            {
                Agendar(OrdemDaFila(daCategoria.Jogos, semNadaPraEsperar), daCategoria.Abre);
            }
        }

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

            if (torneio.SemHorarioPrevisto)
            {
                TempData["Erro"] = "Este torneio roda por ordem de liberação: os jogos não têm horário pra refazer.";
                return VoltarPara(voltarPara, id);
            }

            var todos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
            var remarcar = todos.Where(p => p.Status == "Agendada").ToList();
            var intocados = todos.Where(p => p.Status != "Agendada").ToList();

            if (remarcar.Count == 0)
            {
                TempData["Erro"] = todos.Count == 0
                    ? "Não há jogos pra remarcar: sorteie as chaves primeiro."
                    : "Todos os jogos já foram jogados ou estão em quadra — não há horário pra recalcular.";
                return VoltarPara(voltarPara, id);
            }

            foreach (var jogo in remarcar)
            {
                jogo.HorarioPrevisto = null;
                jogo.NomeQuadra = null;
            }

            EncaixarNasLevas(torneio, remarcar,
                torneio.Categorias.Where(c => c.ChaveDireta).Select(c => c.Id).ToHashSet(),
                OcupantesPorDupla(torneio), await QuadrasEmUsoAsync(id),
                AberturaDoRecalculo(torneio, intocados), intocados,
                await QuadrasPreferidasAsync(id),
                JanelasDeImpedimento.PorDupla(torneio),
                await SedesAsync(id));

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

            // A câmera é da QUADRA e não viaja com o jogo: o mapa sai dos jogos como estão
            // AGORA, antes da troca. Ver Services/TransmissaoDaQuadra.
            var cameras = TransmissaoDaQuadra.PorQuadra(doTorneio);
            var linkAntes = jogo.LinkTransmissao;

            TrocaDeQuadra.Mudar(jogo, quadra, ocupante, cameras);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = (ocupante == null
                ? $"O jogo {jogo.Codigo} agora é na {quadra}."
                : $"Quadras trocadas: o jogo {jogo.Codigo} vai pra {quadra} e o {ocupante.Codigo} " +
                  $"assume a {deOnde ?? "quadra que estava livre"}.")
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
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> TrocarHorario(int id, int jogoA, int jogoB, string? voltarPara = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var a = await _context.Partidas.FindAsync(jogoA);
            var b = await _context.Partidas.FindAsync(jogoB);

            if (TrocaDeHorario.MotivoParaNaoTrocar(a, b, id) is { } motivo)
            {
                TempData["Erro"] = motivo;
            }
            else
            {
                TrocaDeHorario.Trocar(a!, b!);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"Horários trocados: agora o jogo {a!.Codigo} é " +
                    $"{a.HorarioPrevisto:dd/MM HH:mm} e o {b!.Codigo} é {b.HorarioPrevisto:dd/MM HH:mm}.";
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
                    int naChave = categoria.Duplas.Count(d => d.Jogador2Id != null && !d.EmListaDeEspera);
                    if (naChave < 2) continue;

                    duplas += naChave;
                    jogosDeMataMata += naChave - 1;
                    continue;
                }

                // Dupla sem parceiro ainda não é uma dupla: não entra em grupo nenhum.
                int daCategoria = categoria.Duplas.Count(d => d.Jogador2Id != null);
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
