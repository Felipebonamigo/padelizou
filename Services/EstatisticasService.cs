using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public class EstatisticasService : IEstatisticasService
{
    private readonly DbPadelContext _context;

    public EstatisticasService(DbPadelContext context)
    {
        _context = context;
    }

    public int PontosPorFase(string? ultimaFase) => ultimaFase switch
    {
        "Campeao" => 100,
        "Final" => 60,
        "Semifinal" => 35,
        "Quartas de Final" => 20,
        _ => 10 // Fase de Grupos / participou
    };

    // Ordem das fases para "melhor colocação" (maior = mais longe).
    private static int RankFase(string? fase) => fase switch
    {
        "Campeao" => 5,
        "Final" => 4,
        "Semifinal" => 3,
        "Quartas de Final" => 2,
        _ => 1
    };

    public static string RotuloFase(string? fase) => fase switch
    {
        "Campeao" => "Campeão",
        "Final" => "Vice",
        "Semifinal" => "Semifinal",
        "Quartas de Final" => "Quartas",
        _ => "Fase de Grupos"
    };

    // Decide o "material" do troféu só pelo texto do nome da categoria (mesma convenção do
    // catálogo padrão em Program.cs — "2ª Categoria Masculina/Feminina", "Categoria Open ...", etc).
    public static (string Chave, string Nome, string Icone, string CorFundo, string CorTexto) TierDaCategoria(string? nomeCategoria)
    {
        string n = nomeCategoria ?? "";
        if (n.Contains("Open")) return ("Diamante", "Diamante", "bi-gem", "#e0f7fa", "#00838f");
        if (n.Contains("2ª")) return ("Ouro", "Ouro", "bi-trophy-fill", "#fff6df", "#8a6d00");
        if (n.Contains("3ª")) return ("Prata", "Prata", "bi-trophy-fill", "#f1f1f1", "#6c757d");
        if (n.Contains("4ª")) return ("Bronze", "Bronze", "bi-trophy-fill", "#fbe7d4", "#8a4b08");
        if (n.Contains("5ª")) return ("Ferro", "Ferro", "bi-trophy-fill", "#e8eaed", "#495057");
        if (n.Contains("6ª")) return ("Madeira", "Madeira", "bi-trophy-fill", "#f0e0c9", "#6b4423");
        if (n.Contains("7ª") || n.Contains("Iniciantes")) return ("Plastico", "Plástico", "bi-trophy-fill", "#eef2f8", "#6c757d");
        return ("Geral", "Geral", "bi-trophy-fill", "#eef2f8", "#6c757d");
    }

    // Ordem de FORÇA da categoria (maior número = categoria mais forte). Usado pela
    // regra anti-sandbagging: quem comprova nível numa categoria forte não pode
    // descer para categorias mais fracas (número de ordem menor). 0 = desconhecida.
    public static int OrdemCategoria(string? nomeCategoria)
    {
        string n = nomeCategoria ?? "";
        if (n.Contains("Open")) return 8;
        if (n.Contains("1ª")) return 7;
        if (n.Contains("2ª")) return 6;
        if (n.Contains("3ª")) return 5;
        if (n.Contains("4ª")) return 4;
        if (n.Contains("5ª")) return 3;
        if (n.Contains("6ª")) return 2;
        if (n.Contains("7ª") || n.Contains("Iniciantes")) return 1;
        return 0;
    }

    public async Task<List<RankingCategoriaVM>> ObterRankingPorCategoriaAsync(string? categoriaNome = null)
    {
        var duplas = await _context.Duplas
            .Include(d => d.Categoria)
            .Include(d => d.Jogador1)
            .Include(d => d.Jogador2)
            .ToListAsync();

        var porCategoria = duplas
            .Where(d => d.Categoria != null && (categoriaNome == null || d.Categoria.Nome == categoriaNome))
            .GroupBy(d => d.Categoria.Nome);

        var resultado = new List<RankingCategoriaVM>();

        foreach (var grupo in porCategoria)
        {
            var acc = new Dictionary<int, RankingLinhaVM>();

            foreach (var dupla in grupo)
            {
                foreach (var jogador in new[] { dupla.Jogador1, dupla.Jogador2 })
                {
                    if (jogador == null) continue;

                    if (!acc.TryGetValue(jogador.Id, out var linha))
                    {
                        linha = new RankingLinhaVM { Jogador = jogador };
                        acc[jogador.Id] = linha;
                    }

                    linha.Pontos += PontosPorFase(dupla.UltimaFase);
                    linha.Torneios += 1;
                    if (dupla.UltimaFase == "Campeao") linha.Titulos += 1;
                    if (dupla.UltimaFase == "Final") linha.Finais += 1;
                }
            }

            var linhas = acc.Values
                .OrderByDescending(l => l.Pontos)
                .ThenByDescending(l => l.Titulos)
                .ThenByDescending(l => l.Finais)
                .ThenByDescending(l => l.Torneios)
                .ThenBy(l => l.Jogador.Nome)
                .ToList();

            resultado.Add(new RankingCategoriaVM { Categoria = grupo.Key, Linhas = linhas });
        }

        return resultado.OrderBy(r => r.Categoria).ToList();
    }

    // Ranking de times: cada ponto de torneio que um jogador conquista (PontosPorFase da
    // fase alcançada por cada dupla dele) soma para o time ao qual ele pertence.
    public async Task<List<RankingTimeVM>> ObterRankingTimesAsync()
    {
        var jogadores = await _context.Jogadores
            .Where(j => j.TimeId != null && j.Time != null)
            .Select(j => new { j.Id, TimeId = j.TimeId!.Value, TimeNome = j.Time!.Nome, TimeLogo = j.Time!.Logo })
            .ToListAsync();

        if (jogadores.Count == 0) return new List<RankingTimeVM>();

        var idsComTime = jogadores.Select(j => j.Id).ToHashSet();

        var duplas = await _context.Duplas
            .Where(d => idsComTime.Contains(d.Jogador1Id) || idsComTime.Contains(d.Jogador2Id))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase })
            .ToListAsync();

        // Pontos/títulos por jogador (mesma pontuação por fase do ranking individual).
        var porJogador = new Dictionary<int, (int pontos, int titulos)>();
        void Somar(int jogadorId, string? fase)
        {
            if (!idsComTime.Contains(jogadorId)) return;
            var atual = porJogador.GetValueOrDefault(jogadorId);
            atual.pontos += PontosPorFase(fase);
            if (fase == "Campeao") atual.titulos += 1;
            porJogador[jogadorId] = atual;
        }
        foreach (var d in duplas)
        {
            Somar(d.Jogador1Id, d.UltimaFase);
            Somar(d.Jogador2Id, d.UltimaFase);
        }

        return jogadores
            .GroupBy(j => new { j.TimeId, j.TimeNome, j.TimeLogo })
            .Select(g =>
            {
                int pontos = 0, titulos = 0;
                foreach (var j in g)
                {
                    if (porJogador.TryGetValue(j.Id, out var p)) { pontos += p.pontos; titulos += p.titulos; }
                }
                return new RankingTimeVM
                {
                    TimeId = g.Key.TimeId,
                    Time = g.Key.TimeNome,
                    Logo = g.Key.TimeLogo,
                    Jogadores = g.Count(),
                    Pontos = pontos,
                    Titulos = titulos
                };
            })
            .OrderByDescending(t => t.Pontos).ThenByDescending(t => t.Titulos).ThenBy(t => t.Time)
            .ToList();
    }

    public async Task<RankingHubVM> ObterRankingHubAsync()
    {
        var vm = new RankingHubVM();

        // 1 e 2: ranking por categoria (pontos) e o mesmo recorte ordenado por títulos.
        var porCategoria = await ObterRankingPorCategoriaAsync();
        vm.PorCategoria = porCategoria;
        vm.TrofeusPorCategoria = porCategoria
            .Select(c => new RankingCategoriaVM
            {
                Categoria = c.Categoria,
                Linhas = c.Linhas
                    .Where(l => l.Titulos > 0 || l.Finais > 0)
                    .OrderByDescending(l => l.Titulos)
                    .ThenByDescending(l => l.Finais)
                    .ThenByDescending(l => l.Pontos)
                    .ToList()
            })
            .Where(c => c.Linhas.Count > 0)
            .ToList();

        // 3 a 8: derivados das partidas de torneio finalizadas (com vencedor definido).
        var partidas = await CarregarPartidasFinalizadasAsync(incluirTorneio: true);

        DateTime Ordem(Partida p) =>
            p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto
            ?? p.Categoria?.Torneio?.DataInicio ?? DateTime.MinValue;

        var jog = new Dictionary<int, JogadorContagemVM>();
        var jogCat = new Dictionary<(string cat, int jid), JogadorContagemVM>();
        var dup = new Dictionary<int, DuplaContagemVM>();
        var jogSeq = new Dictionary<int, List<(DateTime ord, bool venceu)>>();
        var dupSeq = new Dictionary<int, List<(DateTime ord, bool venceu)>>();

        foreach (var p in partidas.OrderBy(Ordem).ThenBy(p => p.Id))
        {
            var cat = p.Categoria?.Nome ?? "—";
            var torneio = p.Categoria?.Torneio?.Nome ?? "—";
            var ord = Ordem(p);

            foreach (var (dupla, dId) in new[] { (p.Dupla1, p.Dupla1Id), (p.Dupla2, p.Dupla2Id) })
            {
                if (dupla == null) continue;
                bool venceu = p.VencedorId == dId;

                // Dupla (inscrição por torneio)
                if (!dup.TryGetValue(dId, out var dc))
                {
                    dc = new DuplaContagemVM
                    {
                        Jogador1 = dupla.Jogador1,
                        Jogador2 = dupla.Jogador2,
                        Categoria = cat,
                        Torneio = torneio
                    };
                    dup[dId] = dc;
                }
                dc.Jogos++;
                if (venceu) dc.Vitorias++;

                if (!dupSeq.TryGetValue(dId, out var dseq)) { dseq = new(); dupSeq[dId] = dseq; }
                dseq.Add((ord, venceu));

                // Jogadores da dupla
                foreach (var jgd in new[] { dupla.Jogador1, dupla.Jogador2 })
                {
                    if (jgd == null) continue;

                    if (!jog.TryGetValue(jgd.Id, out var jc)) { jc = new JogadorContagemVM { Jogador = jgd }; jog[jgd.Id] = jc; }
                    jc.Jogos++;
                    if (venceu) jc.Vitorias++;

                    var chaveCat = (cat, jgd.Id);
                    if (!jogCat.TryGetValue(chaveCat, out var jcc)) { jcc = new JogadorContagemVM { Jogador = jgd }; jogCat[chaveCat] = jcc; }
                    jcc.Jogos++;
                    if (venceu) jcc.Vitorias++;

                    if (!jogSeq.TryGetValue(jgd.Id, out var jseq)) { jseq = new(); jogSeq[jgd.Id] = jseq; }
                    jseq.Add((ord, venceu));
                }
            }
        }

        // Maior sequência de vitórias consecutivas (sem derrota no meio). Como não há
        // empate em partida de torneio, "invicto" == vitórias seguidas.
        static (int seq, DateTime? de, DateTime? ate) MaiorSequencia(List<(DateTime ord, bool venceu)> hist)
        {
            int best = 0, cur = 0;
            DateTime? bestDe = null, bestAte = null, curDe = null;
            foreach (var h in hist.OrderBy(x => x.ord))
            {
                if (h.venceu)
                {
                    if (cur == 0) curDe = h.ord;
                    cur++;
                    if (cur > best) { best = cur; bestDe = curDe; bestAte = h.ord; }
                }
                else { cur = 0; curDe = null; }
            }
            return (best, bestDe, bestAte);
        }

        // 3. Jogador com mais vitórias (geral)
        vm.VitoriasJogadores = jog.Values
            .Where(x => x.Vitorias > 0)
            .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ThenBy(x => x.Jogador.Nome)
            .Take(50).ToList();

        // 4. Jogador com mais vitórias por categoria
        vm.VitoriasJogadoresPorCategoria = jogCat
            .GroupBy(kv => kv.Key.cat)
            .Select(g => new CategoriaJogadoresVM
            {
                Categoria = g.Key,
                Jogadores = g.Select(x => x.Value)
                    .Where(x => x.Vitorias > 0)
                    .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ThenBy(x => x.Jogador.Nome)
                    .ToList()
            })
            .Where(c => c.Jogadores.Count > 0)
            .OrderBy(c => c.Categoria).ToList();

        // 5. Jogador com mais jogos invicto
        foreach (var kv in jogSeq)
        {
            var (seq, de, ate) = MaiorSequencia(kv.Value);
            if (seq <= 0) continue;
            vm.InvictosJogadores.Add(new InvictoJogadorVM { Jogador = jog[kv.Key].Jogador, Sequencia = seq, De = de, Ate = ate });
        }
        vm.InvictosJogadores = vm.InvictosJogadores
            .OrderByDescending(x => x.Sequencia).ThenBy(x => x.Jogador.Nome)
            .Take(50).ToList();

        // 6. Dupla com mais vitórias (geral)
        vm.VitoriasDuplas = dup.Values
            .Where(x => x.Vitorias > 0)
            .OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos)
            .Take(50).ToList();

        // 7. Dupla com mais vitórias por categoria
        vm.VitoriasDuplasPorCategoria = dup.Values
            .Where(x => x.Vitorias > 0)
            .GroupBy(d => d.Categoria)
            .Select(g => new CategoriaDuplasVM
            {
                Categoria = g.Key,
                Duplas = g.OrderByDescending(x => x.Vitorias).ThenByDescending(x => x.Jogos).ToList()
            })
            .OrderBy(c => c.Categoria).ToList();

        // 8. Dupla com mais tempo invicta
        foreach (var kv in dupSeq)
        {
            var (seq, de, ate) = MaiorSequencia(kv.Value);
            var dc = dup[kv.Key];
            dc.SequenciaInvicta = seq;
            dc.De = de;
            dc.Ate = ate;
        }
        vm.InvictasDuplas = dup.Values
            .Where(x => x.SequenciaInvicta > 0)
            .OrderByDescending(x => x.SequenciaInvicta).ThenByDescending(x => x.Vitorias)
            .Take(50).ToList();

        // 9. Nível comprovado (base da previsão/elegibilidade de categoria)
        var niveis = await ObterNiveisComprovadosAsync();
        if (niveis.Count > 0)
        {
            var ids = niveis.Keys.ToList();
            var jogadores = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id);

            vm.NiveisComprovados = niveis
                .Where(kv => jogadores.ContainsKey(kv.Key))
                .Select(kv => new NivelJogadorVM
                {
                    Jogador = jogadores[kv.Key],
                    Categoria = kv.Value.Categoria,
                    Ordem = kv.Value.Ordem,
                    MelhorFase = kv.Value.MelhorFase
                })
                .OrderByDescending(x => x.Ordem).ThenBy(x => x.Jogador.Nome)
                .ToList();
        }

        // 10. Ranking de times
        vm.Times = await ObterRankingTimesAsync();

        return vm;
    }

    // Fases (UltimaFase da dupla) que "comprovam" nível, conforme o gatilho escolhido
    // pelo organizador. "Livre" => nenhuma (não trava).
    public static string[] FasesQueComprovam(string? modo) => modo switch
    {
        "SaiuChave" => new[] { "Quartas de Final", "Semifinal", "Final", "Campeao" },
        "Semifinal" => new[] { "Semifinal", "Final", "Campeao" },
        "Final" => new[] { "Final", "Campeao" },
        _ => Array.Empty<string>() // Livre ou desconhecido
    };

    // Frase curta do que o jogador fez naquela categoria (para a mensagem de bloqueio).
    public static string RotuloComprovacao(string? fase) => fase switch
    {
        "Campeao" => "foi campeão",
        "Final" => "chegou à final",
        "Semifinal" => "chegou à semifinal",
        "Quartas de Final" => "passou da fase de grupos",
        _ => "jogou"
    };

    public async Task<Dictionary<int, NivelComprovadoVM>> ObterNiveisComprovadosAsync(string modo = "Final")
    {
        var fasesComprovam = FasesQueComprovam(modo);
        if (fasesComprovam.Length == 0) return new Dictionary<int, NivelComprovadoVM>(); // Livre

        // Só resultados de torneio: o nível é comprovado pela UltimaFase da dupla.
        var duplas = await _context.Duplas
            .Include(d => d.Categoria)
            .Where(d => d.Categoria != null && fasesComprovam.Contains(d.UltimaFase))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase, CategoriaNome = d.Categoria.Nome })
            .ToListAsync();

        var mapa = new Dictionary<int, NivelComprovadoVM>();

        void Aplicar(int jogadorId, string? fase, string categoriaNome)
        {
            int ordem = OrdemCategoria(categoriaNome);
            if (ordem == 0) return; // categoria sem tier reconhecido não trava nível

            if (!mapa.TryGetValue(jogadorId, out var atual) || ordem > atual.Ordem)
            {
                mapa[jogadorId] = new NivelComprovadoVM
                {
                    Categoria = categoriaNome,
                    Ordem = ordem,
                    MelhorFase = fase
                };
            }
        }

        foreach (var d in duplas)
        {
            Aplicar(d.Jogador1Id, d.UltimaFase, d.CategoriaNome);
            Aplicar(d.Jogador2Id, d.UltimaFase, d.CategoriaNome);
        }

        return mapa;
    }

    public async Task<Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>> ObterMelhoresColocacoesAsync(
        IEnumerable<string> categoriaNomes, int? excluirTorneioId = null)
    {
        var nomes = categoriaNomes.ToHashSet();
        if (nomes.Count == 0) return new Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>();

        var registros = await _context.Duplas
            .Include(d => d.Categoria)
            .Where(d => nomes.Contains(d.Categoria.Nome)
                     && (excluirTorneioId == null || d.Categoria.TorneioId != excluirTorneioId))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase, CategoriaNome = d.Categoria.Nome })
            .ToListAsync();

        var mapa = new Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>();

        void Aplicar(int jogadorId, string? fase, string categoriaNome)
        {
            var (tierChave, tierNome, icone, corFundo, corTexto) = TierDaCategoria(categoriaNome);

            if (!mapa.TryGetValue(jogadorId, out var porTier))
            {
                porTier = new Dictionary<string, HistoricoCategoriaVM>();
                mapa[jogadorId] = porTier;
            }
            if (!porTier.TryGetValue(tierChave, out var hist))
            {
                hist = new HistoricoCategoriaVM
                {
                    MelhorFase = "Grupos",
                    Titulos = 0,
                    Tier = tierChave,
                    TierNome = tierNome,
                    IconeTier = icone,
                    CorFundoTier = corFundo,
                    CorTextoTier = corTexto
                };
                porTier[tierChave] = hist;
            }
            if (RankFase(fase) > RankFase(hist.MelhorFase)) hist.MelhorFase = fase ?? "Grupos";
            if (fase == "Campeao") hist.Titulos += 1;
        }

        foreach (var r in registros)
        {
            Aplicar(r.Jogador1Id, r.UltimaFase, r.CategoriaNome);
            Aplicar(r.Jogador2Id, r.UltimaFase, r.CategoriaNome);
        }

        return mapa;
    }

    public async Task<List<ConfrontoResumoVM>> ObterConfrontosAsync(int jogadorId)
    {
        var acc = new Dictionary<int, ConfrontoResumoVM>();

        void Somar(Jogador? oponente, bool? venci)
        {
            if (oponente == null || oponente.Id == jogadorId) return;
            if (!acc.TryGetValue(oponente.Id, out var resumo))
            {
                resumo = new ConfrontoResumoVM { Oponente = oponente };
                acc[oponente.Id] = resumo;
            }
            resumo.Jogos += 1;
            if (venci == true) resumo.Vitorias += 1;
            else if (venci == false) resumo.Derrotas += 1;
        }

        var partidas = await CarregarPartidasFinalizadasAsync();
        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            Somar(oppDupla.Jogador1, venci);
            Somar(oppDupla.Jogador2, venci);
        }

        var jogosSemanais = await CarregarJogosSemanaisAsync(jogadorId);
        foreach (var j in jogosSemanais)
        {
            var (meuLado, oponentes) = LocalizarLadoJogoSemanal(j, jogadorId);
            if (meuLado == 0) continue;

            bool? venci = j.VencedorLado == 0 ? null : (j.VencedorLado == meuLado);
            Somar(oponentes.Item1, venci);
            Somar(oponentes.Item2, venci);
        }

        return acc.Values
            .OrderByDescending(c => c.Jogos)
            .ThenByDescending(c => c.Vitorias)
            .ToList();
    }

    public async Task<List<ParceiroResumoVM>> ObterParceirosAsync(int jogadorId)
    {
        var acc = new Dictionary<int, ParceiroResumoVM>();

        void Somar(Jogador? parceiro, bool? venci)
        {
            if (parceiro == null || parceiro.Id == jogadorId) return;
            if (!acc.TryGetValue(parceiro.Id, out var resumo))
            {
                resumo = new ParceiroResumoVM { Parceiro = parceiro };
                acc[parceiro.Id] = resumo;
            }
            resumo.Jogos += 1;
            if (venci == true) resumo.Vitorias += 1;
        }

        var partidas = await CarregarPartidasFinalizadasAsync();
        foreach (var p in partidas)
        {
            var (minhaDupla, _) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            var parceiro = minhaDupla.Jogador1Id == jogadorId ? minhaDupla.Jogador2 : minhaDupla.Jogador1;
            Somar(parceiro, venci);
        }

        var jogosSemanais = await CarregarJogosSemanaisAsync(jogadorId);
        foreach (var j in jogosSemanais)
        {
            var (meuLado, _) = LocalizarLadoJogoSemanal(j, jogadorId);
            if (meuLado == 0) continue;

            bool? venci = j.VencedorLado == 0 ? null : (j.VencedorLado == meuLado);
            var parceiro = meuLado == 1
                ? (j.Dupla1Jogador1Id == jogadorId ? j.Dupla1Jogador2 : j.Dupla1Jogador1)
                : (j.Dupla2Jogador1Id == jogadorId ? j.Dupla2Jogador2 : j.Dupla2Jogador1);
            Somar(parceiro, venci);
        }

        return acc.Values
            .OrderByDescending(p => p.Jogos)
            .ThenByDescending(p => p.Vitorias)
            .ToList();
    }

    public async Task<DestaquesJogadorVM> ObterDestaquesAsync(int jogadorId)
    {
        var parceiros = await ObterParceirosAsync(jogadorId);
        var confrontos = await ObterConfrontosAsync(jogadorId);
        return MontarDestaques(parceiros, confrontos);
    }

    // Monta os destaques a partir de listas já calculadas (evita recarregar partidas quando
    // a tela já tem parceiros/confrontos em mãos, como em Jogadores/Perfil).
    public static DestaquesJogadorVM MontarDestaques(
        List<ParceiroResumoVM> parceiros, List<ConfrontoResumoVM> confrontos)
    {
        return new DestaquesJogadorVM
        {
            MaisJogouJunto = parceiros
                .OrderByDescending(p => p.Jogos).ThenByDescending(p => p.Vitorias)
                .FirstOrDefault(),
            MaisEnfrentou = confrontos
                .OrderByDescending(c => c.Jogos).ThenByDescending(c => c.Vitorias)
                .FirstOrDefault(),
            MaisTeVenceu = confrontos
                .Where(c => c.Derrotas > 0)
                .OrderByDescending(c => c.Derrotas).ThenByDescending(c => c.Jogos)
                .FirstOrDefault(),
            VoceMaisVenceu = confrontos
                .Where(c => c.Vitorias > 0)
                .OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Jogos)
                .FirstOrDefault(),
        };
    }

    public async Task<List<ConquistaVM>> ObterConquistasAsync(int jogadorId)
    {
        var jogador = await _context.Jogadores.FindAsync(jogadorId);
        if (jogador == null) return new List<ConquistaVM>();

        bool temDupla = await _context.Duplas.AnyAsync(d => d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId);
        int totalJogosSemanais = await _context.JogosSemanais.CountAsync(j =>
            j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId ||
            j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId);
        bool ehOrganizador = await _context.TorneioOrganizadores.AnyAsync(o => o.JogadorId == jogadorId);
        var resumo = await ObterResumoJogadorAsync(jogadorId);

        return new List<ConquistaVM>
        {
            new() { Codigo = "Estreia", Titulo = "Estreia", Icone = "bi-flag-fill", Conquistada = temDupla || totalJogosSemanais > 0 },
            new() { Codigo = "Mensalista", Titulo = "Mensalista", Icone = "bi-calendar2-check-fill", Conquistada = totalJogosSemanais >= 4 },
            new() { Codigo = "Organizador", Titulo = "Organizador", Icone = "bi-clipboard-check-fill", Conquistada = ehOrganizador },
            new() { Codigo = "DoTime", Titulo = "Do time", Icone = "bi-shield-fill", Conquistada = jogador.TimeId != null },
            new() { Codigo = "Campeao", Titulo = "Campeão", Icone = "bi-trophy-fill", Conquistada = resumo.Titulos > 0 },
            new() { Codigo = "Professor", Titulo = "Professor", Icone = "bi-mortarboard-fill", Conquistada = jogador.IsProfessor },
        };
    }

    public async Task<HeadToHeadVM> ObterHeadToHeadAsync(int jogadorId, int oponenteId)
    {
        var eu = await _context.Jogadores.FindAsync(jogadorId);
        var oponente = await _context.Jogadores.FindAsync(oponenteId);
        var vm = new HeadToHeadVM { Eu = eu!, Oponente = oponente! };

        var partidas = await CarregarPartidasFinalizadasAsync(incluirTorneio: true);

        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            // Só conta se o oponente específico estava na dupla adversária.
            bool oponenteNaOutraDupla = oppDupla.Jogador1Id == oponenteId || oppDupla.Jogador2Id == oponenteId;
            if (!oponenteNaOutraDupla) continue;

            bool venci = p.VencedorId == minhaDupla.Id;
            vm.Jogos += 1;
            if (venci) vm.Vitorias += 1; else vm.Derrotas += 1;

            int meusSets = minhaDupla.Id == p.Dupla1Id ? (p.SetsDupla1 ?? 0) : (p.SetsDupla2 ?? 0);
            int oppSets = minhaDupla.Id == p.Dupla1Id ? (p.SetsDupla2 ?? 0) : (p.SetsDupla1 ?? 0);
            int meusGames = minhaDupla.Id == p.Dupla1Id ? (p.GamesDupla1 ?? 0) : (p.GamesDupla2 ?? 0);
            int oppGames = minhaDupla.Id == p.Dupla1Id ? (p.GamesDupla2 ?? 0) : (p.GamesDupla1 ?? 0);

            vm.Partidas.Add(new ConfrontoPartidaVM
            {
                Data = p.HorarioFimReal ?? p.HorarioPrevisto ?? p.Categoria?.Torneio?.DataInicio,
                Torneio = p.Categoria?.Torneio?.Nome ?? "-",
                Categoria = p.Categoria?.Nome ?? "-",
                Fase = p.Fase,
                MinhaDupla = NomesDupla(minhaDupla),
                DuplaOponente = NomesDupla(oppDupla),
                Placar = (meusSets + oppSets) > 0 ? $"{meusSets} x {oppSets} ({meusGames}/{oppGames})" : $"{meusGames} x {oppGames}",
                EuVenci = venci
            });
        }

        vm.Partidas = vm.Partidas.OrderByDescending(x => x.Data).ToList();
        return vm;
    }

    public async Task<ResumoJogadorVM> ObterResumoJogadorAsync(int jogadorId)
    {
        var fases = await _context.Duplas
            .Where(d => d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
            .Select(d => d.UltimaFase)
            .ToListAsync();

        int vitorias = 0, derrotas = 0;
        var partidas = await CarregarPartidasFinalizadasAsync();
        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            if (p.VencedorId == minhaDupla.Id) vitorias++; else derrotas++;
        }

        return new ResumoJogadorVM
        {
            TotalTorneios = fases.Count,
            Titulos = fases.Count(f => f == "Campeao"),
            Finais = fases.Count(f => f == "Final"),
            Semis = fases.Count(f => f == "Semifinal"),
            Quartas = fases.Count(f => f == "Quartas de Final"),
            CaiuNaChave = fases.Count(f => f == "Quartas de Final" || f == "Semifinal" || f == "Final"),
            Vitorias = vitorias,
            Derrotas = derrotas
        };
    }

    // ---------- helpers ----------

    private async Task<List<Partida>> CarregarPartidasFinalizadasAsync(bool incluirTorneio = false)
    {
        var query = _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .Where(p => p.VencedorId != null);

        if (incluirTorneio)
        {
            query = query.Include(p => p.Categoria).ThenInclude(c => c.Torneio);
        }

        return await query.ToListAsync();
    }

    // Descobre em qual dupla o jogador está; retorna (minhaDupla, duplaAdversaria) ou (null,null).
    private static (Dupla? minha, Dupla? oponente) LocalizarDuplas(Partida p, int jogadorId)
    {
        bool naDupla1 = p.Dupla1 != null && (p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId);
        bool naDupla2 = p.Dupla2 != null && (p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId);

        if (naDupla1 && !naDupla2) return (p.Dupla1, p.Dupla2);
        if (naDupla2 && !naDupla1) return (p.Dupla2, p.Dupla1);
        return (null, null);
    }

    private async Task<List<JogoSemanal>> CarregarJogosSemanaisAsync(int jogadorId)
    {
        return await _context.JogosSemanais
            .Include(j => j.Dupla1Jogador1)
            .Include(j => j.Dupla1Jogador2)
            .Include(j => j.Dupla2Jogador1)
            .Include(j => j.Dupla2Jogador2)
            .Where(j => j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId ||
                        j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId)
            .ToListAsync();
    }

    // Descobre o lado (1 ou 2) do jogador num jogo semanal e os dois jogadores do lado adversário.
    private static (int meuLado, (Jogador?, Jogador?) oponentes) LocalizarLadoJogoSemanal(JogoSemanal j, int jogadorId)
    {
        bool naDupla1 = j.Dupla1Jogador1Id == jogadorId || j.Dupla1Jogador2Id == jogadorId;
        bool naDupla2 = j.Dupla2Jogador1Id == jogadorId || j.Dupla2Jogador2Id == jogadorId;

        if (naDupla1 && !naDupla2) return (1, (j.Dupla2Jogador1, j.Dupla2Jogador2));
        if (naDupla2 && !naDupla1) return (2, (j.Dupla1Jogador1, j.Dupla1Jogador2));
        return (0, (null, null));
    }

    private static string NomesDupla(Dupla d)
    {
        var n1 = d.Jogador1?.Nome ?? "?";
        var n2 = d.Jogador2?.Nome ?? "?";
        return $"{n1} / {n2}";
    }
}
