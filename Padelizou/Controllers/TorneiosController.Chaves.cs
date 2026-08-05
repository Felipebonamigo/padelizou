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
                torneio.Status = "Fase de Grupos";
                await _context.SaveChangesAsync();
                await AvisarChavesPublicadasAsync(torneio, jogosPraAgendar);
                return RedirectToAction("Details", new { id = torneio.Id });
            }

            // Agora sim os horários. A regra mora em EncaixarNasLevas, compartilhada com o
            // "Refazer grade" — duas cópias divergiriam e o torneio teria duas grades.
            EncaixarNasLevas(torneio, jogosPraAgendar, deChaveDireta,
                OcupantesPorDupla(torneio), await QuadrasDoTorneioAsync(torneio.Id));

            _context.Partidas.AddRange(jogosPraAgendar);

            torneio.Status = "Fase de Grupos";
            await _context.SaveChangesAsync();

            await AvisarChavesPublicadasAsync(torneio, jogosPraAgendar);

            return RedirectToAction("Details", new { id = torneio.Id });
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
            DateTime? aPartirDe = null, IReadOnlyList<Partida>? jaMarcados = null)
        {
            var abre = aPartirDe ?? torneio.AberturaDaGrade;
            var intocados = jaMarcados ?? Array.Empty<Partida>();
            bool NaoEsperaNinguem(Partida j) =>
                categoriasDeChaveDireta.Contains(j.CategoriaId) || FasesTorneio.EhFaseDeGrupos(j.Fase);

            var abertura = OrdemDaFila(jogos.Where(NaoEsperaNinguem), categoriasDeChaveDireta);
            var depoisDosGrupos = OrdemDaFila(jogos.Where(j => !NaoEsperaNinguem(j)), categoriasDeChaveDireta);

            void Agendar(List<Partida> daLeva, DateTime inicio)
            {
                if (daLeva.Count == 0) return;

                var horarios = GradeDeJogos.Horarios(
                    inicio,
                    torneio.HoraFimDoDia,
                    torneio.QuantidadeQuadras,
                    torneio.TempoPrevistoPartidaMinutos,
                    // Folga: sem vaga sobrando, o encaixe não teria como deixar uma quadra
                    // vazia pra evitar chamar a mesma pessoa duas vezes. Ver GradeDeJogos.
                    daLeva.Count + GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras)
                        + intocados.Count,
                    aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes);

                // Vaga que já tem dono sai da lista: num recálculo no meio do torneio, os
                // jogos que já rolaram e os que estão em quadra continuam ocupando as
                // quadras deles.
                var vagas = GradeDeJogos.Descontando(horarios,
                        intocados.Where(p => p.HorarioPrevisto != null).Select(p => p.HorarioPrevisto!.Value))
                    .Take(daLeva.Count + GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras))
                    .ToList();

                GradeDeJogos.Encaixar(daLeva, vagas, ocupantes, quadras, intocados);
            }

            Agendar(abertura, abre);

            // Uma rodada de folga entre as fases, não só o fim do último jogo: quem disputa o
            // último jogo do grupo é candidato a classificar, e emendar o mata-mata em cima
            // dele o poria na quadra no minuto em que saiu dela. Ver AberturaDaProximaFase.
            //
            // Conta os jogos de grupo INTOCADOS junto: num recálculo no meio do torneio a
            // maior parte dos grupos já rolou, e olhar só pros remarcados diria que a fase de
            // grupos acabou cedo — o mata-mata subiria pra cima dela.
            var fimDosGrupos = abertura.Concat(intocados)
                .Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase) && j.HorarioPrevisto != null)
                .Select(j => j.HorarioPrevisto!.Value)
                .DefaultIfEmpty()
                .Max();

            Agendar(depoisDosGrupos, fimDosGrupos != default
                ? GradeDeJogos.AberturaDaProximaFase(fimDosGrupos, torneio.HoraFimDoDia,
                                        torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos)
                : abre);
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
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

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
                AberturaDoRecalculo(torneio, intocados), intocados);

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
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

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
            TrocaDeQuadra.Mudar(jogo, quadra, ocupante);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = ocupante == null
                ? $"O jogo {jogo.Codigo} agora é na {quadra}."
                : $"Quadras trocadas: o jogo {jogo.Codigo} vai pra {quadra} e o {ocupante.Codigo} " +
                  $"assume a {deOnde ?? "quadra que estava livre"}.";

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
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

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
                        url, AlcanceDoAviso.AppEWhatsApp);
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

        // Quem de fato ocupa a quadra quando cada dupla joga: as DUAS pessoas dela.
        //
        // Sem isto a grade compara duplas, e a mesma pessoa inscrita na categoria dela E numa
        // chave direta paralela seria marcada em duas quadras no mesmo horário — duas duplas
        // de Ids diferentes, o mesmo sujeito.
        //
        // Time fica FORA do mapa de propósito (cai no Id da dupla, como sempre foi): lá o
        // Jogador1Id é o organizador em todos os times, e comparar por pessoa faria todo time
        // conflitar com todo time, empurrando a grade inteira pra frente.
        private static Dictionary<int, int[]> OcupantesPorDupla(IEnumerable<Dupla> duplas) =>
            duplas
                .Where(d => !d.EhTime && d.Jogador2Id != null)
                .ToDictionary(d => d.Id, d => new[] { d.Jogador1Id, d.Jogador2Id!.Value });

        private static Dictionary<int, int[]> OcupantesPorDupla(Torneio torneio) =>
            OcupantesPorDupla(torneio.Categorias.SelectMany(c => c.Duplas));

        // Os nomes das quadras do torneio, na ordem — é o que transforma "a definir" em
        // "Quadra C" na tela do jogador. Torneio que não cadastrou quadra devolve lista
        // vazia, e a grade segue sem nomear (ver GradeDeJogos.Encaixar).
        private async Task<List<string>> QuadrasDoTorneioAsync(int torneioId) =>
            await _context.Quadras
                .Where(q => q.TorneioId == torneioId)
                .OrderBy(q => q.Nome)
                .Select(q => q.Nome)
                .ToListAsync();

        // As quadras que o torneio está DE FATO usando: as que já estão escritas nos jogos
        // marcados. Só na falta delas vale o cadastro.
        //
        // ⚠️ Os dois podem discordar, e discordam num torneio real: no Interno Los Corneteiros
        // os jogos dizem "Quadra A".."Quadra E" (nomes do dia do sorteio) e o cadastro passou
        // a dizer "Quadra 1".."Quadra 5" — renomear depois do sorteio não reescreve os jogos.
        // Nomear as fases que ainda vêm pelo cadastro poria DOIS nomes pra mesma quadra na
        // mesma tela, e o jogador iria pra quadra errada.
        private async Task<List<string>> QuadrasEmUsoAsync(int torneioId)
        {
            var nosJogos = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.NomeQuadra != null && p.NomeQuadra != "")
                .Select(p => p.NomeQuadra!)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            return nosJogos.Count > 0 ? nosJogos : await QuadrasDoTorneioAsync(torneioId);
        }

        private async Task<Dictionary<int, int[]>> OcupantesPorDuplaAsync(int torneioId) =>
            OcupantesPorDupla(await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId)
                .ToListAsync());

        // TODO jogo do torneio nasce com horário previsto — inclusive os do mata-mata, que
        // só existem depois que a fase de grupos acaba. Sem isso o jogador via "a definir" na
        // fase que mais importa, e a Mesa de Controle não tinha ordem nenhuma pra seguir.
        //
        // A rodada nova abre uma rodada depois do fim da fase que a alimenta — a da PRÓPRIA
        // categoria — e ocupa as quadras que estiverem livres dali em diante.
        //
        // ⚠️ Antes o âncora era o último jogo marcado do TORNEIO INTEIRO, e isso enfileirava
        // as categorias uma atrás da outra: com 5 quadras e 5 categorias, cada semifinal
        // esperava a semifinal alheia acabar e quatro quadras ficavam paradas. Era o preço de
        // o encaixe não saber o que já estava marcado — agora ele sabe (`jaMarcados`), então
        // dá pra emendar em paralelo sem chamar ninguém pra duas quadras ao mesmo tempo.
        private async Task AgendarNaGradeAsync(List<Partida> jogos, int? torneioId)
        {
            if (jogos.Count == 0 || torneioId == null) return;

            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio == null) return;

            // Torneio por ordem de liberação não tem grade: o mata-mata entra na fila como
            // todo o resto, sem hora. Ver Torneio.SemHorarioPrevisto.
            if (torneio.SemHorarioPrevisto) return;

            var jaMarcados = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.HorarioPrevisto != null)
                .ToListAsync();

            // A fase que alimenta esta é a da MESMA categoria: é dela que sai quem vai jogar,
            // e é dela que a folga tem que partir. Uma rodada de folga, não o minuto em que o
            // último jogo acaba — quem joga a semifinal das 22h é quem disputa a final, e
            // colar uma fase na outra é chamar a mesma dupla de volta sem descanso.
            //
            // Vira o dia na abertura dos DIAS SEGUINTES: o mata-mata quase sempre cai no
            // domingo, que começa cedo — não às 18h da sexta em que o torneio abriu.
            int categoriaId = jogos[0].CategoriaId;
            var fimDaFaseAnterior = jaMarcados
                .Where(p => p.CategoriaId == categoriaId)
                .Select(p => p.HorarioPrevisto!.Value)
                .DefaultIfEmpty()
                .Max();

            var inicio = fimDaFaseAnterior == default
                ? torneio.AberturaDaGrade
                : GradeDeJogos.AberturaDaProximaFase(fimDaFaseAnterior, torneio.HoraFimDoDia,
                                        torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos);

            int folga = GradeDeJogos.MargemDeHorarios(torneio.QuantidadeQuadras);

            // Vaga que já tem dono sai da lista, senão a grade ofereceria cinco quadras num
            // horário em que três já estão jogando. Pede-se com sobra (`+ jaMarcados.Count`)
            // justamente porque parte vai ser descontada.
            var horarios = GradeDeJogos.Descontando(
                    GradeDeJogos.Horarios(
                        inicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                        torneio.TempoPrevistoPartidaMinutos,
                        jogos.Count + folga + jaMarcados.Count,
                        aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes),
                    jaMarcados.Select(p => p.HorarioPrevisto!.Value))
                .Take(jogos.Count + folga)
                .ToList();

            // Encaixe ciente de conflito: semifinais de chaves diferentes podem dividir o
            // horário, mas a mesma PESSOA nunca joga em duas quadras ao mesmo tempo — vale
            // pra quem chegou longe na categoria dele e na chave direta ao mesmo tempo.
            GradeDeJogos.Encaixar(jogos, horarios, await OcupantesPorDuplaAsync(torneioId.Value),
                await QuadrasEmUsoAsync(torneioId.Value), jaMarcados);
        }

        // ROBÔ DE PROGRESSÃO: Oitavas → Quartas → Semifinal → Final (motor único).
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int? torneioId, string faseConcluida)
        {
            // Fase que não encadeia (grupos, Americano, Final) para aqui.
            if (ChaveamentoMataMata.ProximaFase(faseConcluida) == null) return;

            // Vencedores da fase + quem passou direto (bye), com a fase completa conferida
            // lá dentro. Vazio = ainda tem jogo pendente. Ver Services/AvancoDaChave.
            var avancam = await AvancoDaChave.QuemAvancaAsync(_context, categoriaId, faseConcluida);
            if (avancam.Count < 2) return;

            // Com bye o quadro encolhe mais devagar: a primeira rodada de uma chave de 24
            // entrega 16 (8 vencedores + 8 byes), que são Oitavas — e não as Quartas que o
            // encadeamento por NOME sugeriria. Quem manda é quanta gente sobrou.
            var proximaFase = ChaveamentoMataMata.NomeFase(avancam.Count);

            // Nunca gera a próxima fase em duplicidade (dois finalizamentos quase simultâneos).
            if (await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase)) return;

            var novos = ChaveamentoMataMata.ParearVencedores(avancam)
                // Codigo é obrigatório no banco (NOT NULL) — sem ele o INSERT do robô falha.
                .Select(confronto => new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Fase = proximaFase,
                    Status = "Agendada",
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                })
                .ToList();

            await AgendarNaGradeAsync(novos, torneioId);

            _context.Partidas.AddRange(novos);
            await _context.SaveChangesAsync();
        }

        // ROBÔ INVISÍVEL DE CRUZAMENTO DE CHAVES
        private async Task ProcessarMataMataAutomatico(int categoriaId, int? torneioId)
        {
            var categoria = await _context.Categorias
                .Include(c => c.GruposTorneio)
                    .ThenInclude(g => g.Duplas)
                .FirstOrDefaultAsync(c => c.Id == categoriaId);

            if (categoria == null) return;

            var partidasFinalizadas = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId
                         && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
                         && p.Status == "Finalizada")
                .ToListAsync();

            // Evita gerar a chave duas vezes (ex: dois finalizamentos quase simultâneos).
            bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
                p.CategoriaId == categoriaId && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));
            if (mataMataJaGerado) return;

            var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();

            // Quantos passam de cada grupo: 2 é a regra de sempre; a categoria de TIMES
            // usa o número que o organizador definiu ao criá-la.
            int classificamPorGrupo = Math.Max(1, categoria.ClassificadosPorGrupo ?? 2);

            // 1. O ranking final de cada grupo, pela régua única (Services/ClassificacaoDeGrupos)
            //    — a mesma que o robô do Controle de Placar e a detecção de bye usam.
            var duplasDosGrupos = grupos.SelectMany(g => g.Duplas).ToList();
            var classificados = ClassificacaoDeGrupos.Calcular(
                duplasDosGrupos, partidasFinalizadas, classificamPorGrupo);

            // 2. Motor único de chaveamento: TODO classificado avança; o quadro cresce pra
            //    caber todo mundo e os MELHORES pegam bye (pulam a primeira rodada). Os byes
            //    não ganham partida aqui — é a ausência dela que o robô de avanço lê depois
            //    (Services/AvancoDaChave) pra somá-los aos vencedores.
            var (nomeFase, confrontos, _) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, classificamPorGrupo);
            if (confrontos.Count == 0) return;

            var jogosDoMataMata = confrontos
                .Select(confronto => new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    Status = "Agendada", // Nasce agendada para ir para a Mesa de Controle!
                    Fase = nomeFase,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() // NOT NULL no banco
                })
                .ToList();

            // Nasce agendada E com hora: o mata-mata emenda no fim da fase de grupos.
            await AgendarNaGradeAsync(jogosDoMataMata, torneioId);

            _context.Partidas.AddRange(jogosDoMataMata);
            await _context.SaveChangesAsync();
        }

      

    }
}
