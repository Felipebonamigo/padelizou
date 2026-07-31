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

            // Agora sim os horários: N quadras em paralelo, parando no fim do expediente e
            // retomando no dia seguinte. O encaixe é ciente de conflito: o mesmo inscrito
            // (dupla ou time) nunca cai em duas quadras no mesmo horário (ver GradeDeJogos).
            var horarios = GradeDeJogos.Horarios(
                torneio.AberturaDaGrade,
                torneio.HoraFimDoDia,
                torneio.QuantidadeQuadras,
                torneio.TempoPrevistoPartidaMinutos,
                jogosPraAgendar.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            GradeDeJogos.Encaixar(jogosPraAgendar, horarios);
            _context.Partidas.AddRange(jogosPraAgendar);

            torneio.Status = "Fase de Grupos";
            await _context.SaveChangesAsync();

            await AvisarChavesPublicadasAsync(torneio, jogosPraAgendar);

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // Troca de horário entre dois jogos, depois do sorteio. A grade automática acerta a
        // conta; quem conhece a vida (a dupla que só chega às 10h, o jogo que rende mais com
        // público) é o organizador — e ele troca o slot inteiro (hora + quadra) de A com B.
        // Regras em Services/TrocaDeHorario.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> TrocarHorario(int id, int jogoA, int jogoB)
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

            return RedirectToAction("Jogos", new { id });
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
                        url);
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
                // Comparação por DIA: o limite é "até domingo", não "até domingo às 00h".
                EstouraOPrazo = torneio.DataFim != null && ultimo != null
                                && ultimo.Value.Date > torneio.DataFim.Value.Date
            };
        }

        // TODO jogo do torneio nasce com horário previsto — inclusive os do mata-mata, que
        // só existem depois que a fase de grupos acaba. Sem isso o jogador via "a definir" na
        // fase que mais importa, e a Mesa de Controle não tinha ordem nenhuma pra seguir.
        //
        // A grade do mata-mata emenda no último jogo já marcado do torneio, respeitando as
        // mesmas quadras e o mesmo expediente da fase de grupos.
        private async Task AgendarNaGradeAsync(List<Partida> jogos, int? torneioId)
        {
            if (jogos.Count == 0 || torneioId == null) return;

            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio == null) return;

            var ultimoMarcado = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.HorarioPrevisto != null)
                .MaxAsync(p => p.HorarioPrevisto);

            // Vira o dia na abertura dos DIAS SEGUINTES: o mata-mata quase sempre cai no
            // domingo, que começa cedo — não às 18h da sexta em que o torneio abriu.
            var inicio = ultimoMarcado == null
                ? torneio.AberturaDaGrade
                : GradeDeJogos.DepoisDe(ultimoMarcado.Value, torneio.HoraFimDoDia,
                                        torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos);

            var horarios = GradeDeJogos.Horarios(
                inicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                torneio.TempoPrevistoPartidaMinutos, jogos.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            // Encaixe ciente de conflito: semifinais de chaves diferentes podem dividir o
            // horário, mas o mesmo classificado nunca joga em duas quadras ao mesmo tempo.
            GradeDeJogos.Encaixar(jogos, horarios);
        }

        // ROBÔ DE PROGRESSÃO: Oitavas → Quartas → Semifinal → Final (motor único).
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int? torneioId, string faseConcluida)
        {
            var proximaFase = ChaveamentoMataMata.ProximaFase(faseConcluida);
            if (proximaFase == null) return;

            var vencedores = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida && p.Status == "Finalizada")
                .OrderBy(p => p.Id)
                .Select(p => p.VencedorId!.Value)
                .ToListAsync();

            // Só avança com a fase completa, e nunca gera a próxima em duplicidade.
            if (vencedores.Count != ChaveamentoMataMata.JogosDaFase(faseConcluida)) return;
            if (await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase)) return;

            var novos = ChaveamentoMataMata.ParearVencedores(vencedores)
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

            // 1. Calcula o ranking final real de cada grupo e monta a lista de classificados.
            var classificados = new List<ChaveamentoMataMata.Classificado>();
            foreach (var grupo in grupos)
            {
                foreach (var dupla in grupo.Duplas)
                {
                    var meusJogos = partidasFinalizadas.Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id).ToList();
                    dupla.Vitorias = meusJogos.Count(p => p.VencedorId == dupla.Id);

                    int gf = meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0) +
                             meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0);
                    int gc = meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0) +
                             meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0);
                    dupla.SaldoGames = gf - gc;
                }

                var ranking = grupo.Duplas.OrderByDescending(d => d.Vitorias).ThenByDescending(d => d.SaldoGames).ToList();
                for (int pos = 0; pos < ranking.Count && pos < classificamPorGrupo; pos++)
                {
                    classificados.Add(new ChaveamentoMataMata.Classificado(
                        ranking[pos].Id, grupo.Nome, ranking[pos].Vitorias, ranking[pos].SaldoGames, pos + 1));
                }
            }

            // 2. Motor único de chaveamento: funciona pra QUALQUER nº de grupos
            //    (todos os 1ºs + melhores 2ºs completando o quadro; 1 grupo = final direta).
            var (nomeFase, confrontos) = ChaveamentoMataMata.MontarPrimeiraFase(classificados, classificamPorGrupo);
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
