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
    // Formato Americano: rodadas de whist, desempate e classificação.
    public partial class TorneiosController
    {
        // Torneio Americano: sorteia as rodadas de todas as categorias do torneio, trocando os
        // parceiros a cada rodada. Heurística gulosa (não é uma escalação matematicamente perfeita
        // de round-robin) — pra cada rodada, embaralha os jogadores, agrupa de 4 em 4 e escolhe,
        // entre as 3 formas possíveis de dividir o quarteto em duplas, a que menos repete parceiros
        // já usados antes.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // `gruposEscolhidos`: em quantos grupos o organizador quer dividir (0 = deixa o sistema
        // escolher a divisão que MAIS MISTURA, que é a de menos grupos). A escolha é feita na
        // hora do sorteio e não na criação porque é agora que se sabe quantos vieram.
        public async Task<IActionResult> GerarRodadasAmericano(
            int torneioId, DateTime dataHoraInicio, int gruposEscolhidos = 0)
        {
            var torneio = await _context.Torneios.Include(t => t.Categorias).FirstOrDefaultAsync(t => t.Id == torneioId);
            if (torneio == null || torneio.Formato != "Americano") return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // Mesma trava do GerarChaves: no Americano o sorteio das rodadas É a chave.
            if (await TaxaExternoImpedeChavesAsync(torneio))
            {
                TempData["Erro"] = "As rodadas são liberadas depois do pagamento da taxa do Padelizou.";
                return RedirectToAction("TaxaPlataforma", new { id = torneioId });
            }

            var rng = new Random();
            int tempoPartida = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
            int totalPartidasGeradas = 0;
            int categoriasComGrupoFinal = 0;
            var jogosDoAmericano = new List<Partida>();

            foreach (var categoria in torneio.Categorias)
            {
                var inscritos = await _context.InscricoesAmericanas
                    .Where(i => i.CategoriaId == categoria.Id)
                    .Select(i => i.JogadorId)
                    .ToListAsync();

                if (inscritos.Count < 4) continue; // não fecha nem uma quadra

                // ⚠️ SÓ NÚMERO QUE FECHA. A regra do formato é "cada um joga com cada um, sem
                // repetir parceiro, e todos jogam a mesma quantidade" — e isso só é possível em
                // certos números (ver Services/DivisaoDoAmericano). Sortear assim mesmo criaria
                // um torneio que a própria regra proíbe, e ninguém veria até a quadra.
                var divisoes = DivisaoDoAmericano.Possiveis(inscritos.Count);
                if (divisoes.Count == 0)
                {
                    TempData["Erro"] = $"{categoria.Nome}: {DivisaoDoAmericano.PorQueNaoFecha(inscritos.Count)}";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                // A escolha do organizador; sem escolha, a que mais mistura (menos grupos).
                var divisao = divisoes.FirstOrDefault(d => d.Grupos == gruposEscolhidos) ?? divisoes[0];

                categoria.GruposAmericano = divisao.Grupos;
                categoria.PassamPorGrupo = divisao.PassamPorGrupo;

                // Reparte os inscritos nos grupos. Já vem embaralhado, então a repartição é o
                // próprio sorteio — fatiar em ordem daria grupos por ordem de inscrição.
                var embaralhados = inscritos.OrderBy(_ => rng.Next()).ToList();
                var fichas = await _context.InscricoesAmericanas
                    .Where(i => i.CategoriaId == categoria.Id)
                    .ToDictionaryAsync(i => i.JogadorId);

                for (int g = 0; g < divisao.Grupos; g++)
                {
                    // Grupo único mantém a fase SEM nome de grupo: é a mesma grafia dos
                    // torneios anteriores a esta mudança, e duas grafias pra mesma coisa é
                    // como as telas passam a discordar.
                    string? nomeDoGrupo = divisao.TemGrupoFinal ? FaseDoAmericano.NomeDoGrupo(g) : null;
                    var doGrupo = embaralhados.Skip(g * divisao.PorGrupo).Take(divisao.PorGrupo).ToList();

                    foreach (var jogadorId in doGrupo)
                    {
                        if (fichas.TryGetValue(jogadorId, out var ficha)) ficha.Grupo = nomeDoGrupo;
                    }

                    // O sorteio vive em Services/RodadasAmericano e GARANTE cada jogador
                    // fazendo dupla com cada um dos outros DO SEU GRUPO.
                    var rodadasSorteadas = RodadasAmericano.Montar(doGrupo);
                    if (rodadasSorteadas.Count == 0) continue;

                    // As duplas nascem TODAS de uma vez, e só então as partidas. Antes havia um
                    // SaveChanges por partida (era preciso pro Id): um Americano de 40 pessoas
                    // são 390 jogos, ou seja 390 idas ao banco dentro do POST do organizador.
                    var porRodada = new List<(int Rodada, Dupla A, Dupla B)>();
                    for (int rodada = 1; rodada <= rodadasSorteadas.Count; rodada++)
                    {
                        foreach (var confronto in rodadasSorteadas[rodada - 1])
                        {
                            var dupla1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.A1, Jogador2Id = confronto.A2 };
                            var dupla2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.B1, Jogador2Id = confronto.B2 };
                            _context.Duplas.AddRange(dupla1, dupla2);
                            porRodada.Add((rodada, dupla1, dupla2));
                        }
                    }
                    await _context.SaveChangesAsync();   // uma vez só por grupo: gera os Ids

                    foreach (var (rodada, a, b) in porRodada)
                    {
                        jogosDoAmericano.Add(new Partida
                        {
                            TorneioId = torneioId,
                            CategoriaId = categoria.Id,
                            Dupla1Id = a.Id,
                            Dupla2Id = b.Id,
                            Fase = nomeDoGrupo == null
                                ? FaseDoAmericano.RodadaUnica(rodada)
                                : FaseDoAmericano.RodadaDeGrupo(nomeDoGrupo, rodada),
                            Status = "Agendada",
                            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                        });
                        totalPartidasGeradas++;
                    }
                }

                if (divisao.TemGrupoFinal) categoriasComGrupoFinal++;
            }

            // O Americano também somava um jogo por vez a partir do início: com 3 quadras,
            // marcava em fila indiana e varava a noite. Mesma grade da fase de grupos.
            // (Aqui o encaixe é posicional mesmo: cada dupla do Americano nasce por jogo,
            // então o detector de conflito por dupla não teria o que detectar — e as
            // rodadas do whist já garantem jogadores distintos dentro da rodada.)
            var horariosAmericano = GradeDeJogos.Horarios(
                dataHoraInicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                tempoPartida, jogosDoAmericano.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            for (int i = 0; i < jogosDoAmericano.Count; i++)
            {
                jogosDoAmericano[i].HorarioPrevisto = horariosAmericano[i];
            }
            _context.Partidas.AddRange(jogosDoAmericano);

            torneio.Status = "Fase de Grupos"; // reaproveita o mesmo status de "torneio em andamento"
            await _context.SaveChangesAsync();

            string avisoDoGrupoFinal = categoriasComGrupoFinal > 0
                ? " O grupo final é montado sozinho quando todos os grupos terminarem."
                : "";
            TempData["Sucesso"] = $"Rodadas geradas! {totalPartidasGeradas} partidas agendadas.{avisoDoGrupoFinal}";
            return RedirectToAction("Jogos", new { id = torneioId });
        }

        // Americano: partida final entre os dois empatados na liderança. Cada um escolhe um
        // parceiro entre os outros inscritos, e sai um jogo só — quem vencer é o campeão.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> DesempateAmericano(int id, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null || torneio.Formato != "Americano") return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var (classificacao, empatados, pendentes) = await ApurarAmericanoAsync(id, categoriaId);

            ViewBag.Torneio = torneio;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.Empatados = empatados;
            ViewBag.Problema = TabelaDoAmericano.ProblemaParaDesempatar(
                torneio.DesempateAmericano, pendentes, empatados.Count);

            // Parceiro pode ser qualquer inscrito da categoria que não seja um dos empatados.
            ViewBag.PossiveisParceiros = classificacao
                .Where(l => empatados.All(e => e.Id != l.Jogador.Id))
                .Select(l => l.Jogador)
                .ToList();

            return View(classificacao);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarDesempateAmericano(
            int id, int categoriaId, int parceiro1, int parceiro2)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null || torneio.Formato != "Americano") return NotFound();
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var (_, empatados, pendentes) = await ApurarAmericanoAsync(id, categoriaId);

            // Revalida no POST: entre abrir a tela e clicar, um resultado pode ter mudado a
            // liderança — e o formulário guarda a foto antiga.
            var problema = TabelaDoAmericano.ProblemaParaDesempatar(
                torneio.DesempateAmericano, pendentes, empatados.Count);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("DesempateAmericano", new { id, categoriaId });
            }

            if (await _context.Partidas.AnyAsync(p =>
                    p.TorneioId == id && p.CategoriaId == categoriaId
                    && p.Fase == TabelaDoAmericano.FaseDesempate))
            {
                TempData["Erro"] = "O desempate desta categoria já foi criado.";
                return RedirectToAction("Jogos", new { id });
            }

            var escolhidos = new[] { parceiro1, parceiro2 };
            if (escolhidos.Distinct().Count() != 2 || escolhidos.Any(p => empatados.Any(e => e.Id == p)))
            {
                TempData["Erro"] = "Cada empatado precisa de um parceiro diferente, e o parceiro não pode ser o outro empatado.";
                return RedirectToAction("DesempateAmericano", new { id, categoriaId });
            }

            var dupla1 = new Dupla { CategoriaId = categoriaId, Jogador1Id = empatados[0].Id, Jogador2Id = parceiro1 };
            var dupla2 = new Dupla { CategoriaId = categoriaId, Jogador1Id = empatados[1].Id, Jogador2Id = parceiro2 };
            _context.Duplas.AddRange(dupla1, dupla2);
            await _context.SaveChangesAsync();   // precisa dos Ids antes de criar a Partida

            var jogo = new Partida
            {
                TorneioId = id,
                CategoriaId = categoriaId,
                Dupla1Id = dupla1.Id,
                Dupla2Id = dupla2.Id,
                Fase = TabelaDoAmericano.FaseDesempate,
                Status = "Agendada",
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()   // NOT NULL no banco
            };

            await AgendarNaGradeAsync(new List<Partida> { jogo }, id);
            _context.Partidas.Add(jogo);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Desempate criado: {empatados[0].Nome} e {empatados[1].Nome} decidem o título. " +
                                  $"Começa {jogo.HorarioPrevisto:dd/MM 'às' HH:mm}.";
            return RedirectToAction("Jogos", new { id });
        }

        // Tabela, empatados na liderança e quantos jogos ainda faltam — a mesma apuração
        // serve pra tela do desempate e pra decisão de criar a partida.
        private async Task<(List<TabelaDoAmericano.Linha> Classificacao, List<Jogador> Empatados, int Pendentes)>
            ApurarAmericanoAsync(int torneioId, int categoriaId)
        {
            var doAmericano = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == torneioId && p.CategoriaId == categoriaId
                         && p.Fase.StartsWith("Americano"))
                .ToListAsync();

            // ⚠️ Quem decide o título é o GRUPO FINAL quando ele existe. Somar o torneio
            // inteiro juntaria gente de grupos diferentes, que nunca se enfrentou — e o
            // desempate nasceria de uma liderança que não existe.
            bool temGrupoFinal = doAmericano.Any(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase));
            var queDecidem = doAmericano
                .Where(p => temGrupoFinal
                    ? FaseDoAmericano.EhDoGrupoFinal(p.Fase)
                    : FaseDoAmericano.EhDaFaseDeGrupos(p.Fase))
                .ToList();

            var classificacao = TabelaDoAmericano.Montar(queDecidem.Where(p => p.Status == "Finalizada"));

            return (classificacao,
                    TabelaDoAmericano.EmpatadosNaLideranca(classificacao),
                    // Enquanto QUALQUER partida do torneio estiver aberta a liderança pode
                    // mudar — inclusive as de grupo, que ainda alimentam o grupo final.
                    doAmericano.Count(p => p.Status != "Finalizada"));
        }

        // GET: Torneios/ClassificacaoAmericano/5?categoriaId=1 — soma de games por jogador
        // (não por dupla, já que o parceiro muda a cada rodada no formato Americano)
        public async Task<IActionResult> ClassificacaoAmericano(int id, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var partidas = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == id && p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano") && p.Status == "Finalizada")
                .ToListAsync();

            // A conta vive em Services/TabelaDoAmericano — a mesma que alimenta a aba
            // "Classificação" na tela de Jogos. Duas contas separadas divergiriam mais cedo
            // ou mais tarde, e aí o torneio teria dois campeões diferentes na mesma tela.
            //
            // ⚠️ Cada grupo tem a SUA tabela. Somar o torneio inteiro compararia gente que
            // nunca se enfrentou, e o corte de quem passa sairia dessa soma errada.
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            int passam = categoria?.PassamPorGrupo ?? 0;

            var tabelas = new List<ClassificacaoDeGrupoVM>();

            var nomesDosGrupos = partidas
                .Where(p => FaseDoAmericano.EhDaFaseDeGrupos(p.Fase))
                .Select(p => FaseDoAmericano.GrupoDe(p.Fase))
                .Distinct()
                .OrderBy(g => g, StringComparer.Ordinal)
                .ToList();

            foreach (var grupo in nomesDosGrupos)
            {
                var linhas = TabelaDoAmericano.Montar(partidas.Where(p => FaseDoAmericano.EhDoGrupo(p.Fase, grupo)));
                tabelas.Add(new ClassificacaoDeGrupoVM
                {
                    // Grupo nulo = torneio sem divisão: a tabela é a do torneio, sem título.
                    Titulo = grupo == null ? null : $"Grupo {grupo}",
                    PassamDaqui = grupo == null ? 0 : passam,
                    Linhas = linhas.Select(l => new ClassificacaoAmericanoItemVM
                    {
                        Jogador = l.Jogador,
                        TotalGames = l.TotalGames,
                    }).ToList(),
                });
            }

            // O grupo final vem por último e é o que decide o título.
            var doFinal = partidas.Where(p => FaseDoAmericano.EhDoGrupoFinal(p.Fase)).ToList();
            if (doFinal.Count > 0)
            {
                tabelas.Add(new ClassificacaoDeGrupoVM
                {
                    Titulo = "Grupo final",
                    DecideOTitulo = true,
                    Linhas = TabelaDoAmericano.Montar(doFinal).Select(l => new ClassificacaoAmericanoItemVM
                    {
                        Jogador = l.Jogador,
                        TotalGames = l.TotalGames,
                    }).ToList(),
                });
            }

            ViewBag.Torneio = torneio;
            ViewBag.CategoriaId = categoriaId;
            ViewBag.Tabelas = tabelas;

            // A lista "plana" segue no Model pra não quebrar quem já lia assim — na divisão em
            // grupos ela é a do grupo final, ou a do grupo único.
            return View(tabelas.LastOrDefault()?.Linhas ?? new List<ClassificacaoAmericanoItemVM>());
        }

        // GET: Torneios/Classificacao/5?categoriaId=1
        public async Task<IActionResult> Classificacao(int id, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // 1. Busca as duplas desta categoria que já têm um Grupo definido
            var duplas = await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.TorneioId == id && d.CategoriaId == categoriaId && d.Grupo != null)
                .ToListAsync();

            // 2. Busca todas as partidas finalizadas desta fase de grupos
            var partidas = await _context.Partidas
                .Where(p => p.TorneioId == id && p.CategoriaId == categoriaId && p.Status == "Finalizada"
                         && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
                .ToListAsync();

            var listaClassificacao = new List<ClassificacaoGrupoViewModel>();

            // 3. O Cálculo Matemático para cada dupla
            foreach (var dupla in duplas)
            {
                var stats = new ClassificacaoGrupoViewModel { Dupla = dupla, Grupo = dupla.Grupo! };

                // Pega só os jogos onde essa dupla jogou
                var jogosDaDupla = partidas.Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id).ToList();
                stats.JogosJogados = jogosDaDupla.Count;

                foreach (var jogo in jogosDaDupla)
                {
                    // Descobre se a dupla atual é a Dupla1 ou Dupla2 no registro da partida
                    bool ehDupla1 = jogo.Dupla1Id == dupla.Id;

                    int meusGames = ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                    int gamesAdversario = ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);

                    stats.GamesPro += meusGames;
                    stats.GamesContra += gamesAdversario;

                    if (meusGames > gamesAdversario) stats.Vitorias++;
                    else if (gamesAdversario > meusGames) stats.Derrotas++;
                }

                listaClassificacao.Add(stats);
            }

            // 4. O Agrupamento e a Regra de Desempate (Muito importante!)
            // Agrupamos por Letra do Grupo e ordenamos primeiro por Vitória e depois por Saldo de Games
            var classificacaoFinal = listaClassificacao
                .GroupBy(c => c.Grupo)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.SaldoGames).ToList()
                );

            ViewBag.Torneio = torneio;
            ViewBag.RegraClassificados = torneio.ClassificadosPorGrupo; // Para pintar de verde quem passa de fase

            return View(classificacaoFinal);
        }
        // GerarMataMata (manual) foi removida: o cruzamento agora é sempre automático, via
        // ProcessarMataMataAutomatico (disparado por FinalizarPartida assim que a última partida
        // da Fase de Grupos de uma categoria termina).
        
    }
}
