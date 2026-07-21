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

    public async Task<Dictionary<int, HistoricoCategoriaVM>> ObterMelhoresColocacoesAsync(
        IEnumerable<string> categoriaNomes, int? excluirTorneioId = null)
    {
        var nomes = categoriaNomes.ToHashSet();
        if (nomes.Count == 0) return new Dictionary<int, HistoricoCategoriaVM>();

        var registros = await _context.Duplas
            .Include(d => d.Categoria)
            .Where(d => nomes.Contains(d.Categoria.Nome)
                     && (excluirTorneioId == null || d.Categoria.TorneioId != excluirTorneioId))
            .Select(d => new { d.Jogador1Id, d.Jogador2Id, d.UltimaFase })
            .ToListAsync();

        var mapa = new Dictionary<int, HistoricoCategoriaVM>();

        void Aplicar(int jogadorId, string? fase)
        {
            if (!mapa.TryGetValue(jogadorId, out var hist))
            {
                hist = new HistoricoCategoriaVM { MelhorFase = "Grupos", Titulos = 0 };
                mapa[jogadorId] = hist;
            }
            if (RankFase(fase) > RankFase(hist.MelhorFase)) hist.MelhorFase = fase ?? "Grupos";
            if (fase == "Campeao") hist.Titulos += 1;
        }

        foreach (var r in registros)
        {
            Aplicar(r.Jogador1Id, r.UltimaFase);
            Aplicar(r.Jogador2Id, r.UltimaFase);
        }

        return mapa;
    }

    public async Task<List<ConfrontoResumoVM>> ObterConfrontosAsync(int jogadorId)
    {
        var partidas = await CarregarPartidasFinalizadasAsync();
        var acc = new Dictionary<int, ConfrontoResumoVM>();

        foreach (var p in partidas)
        {
            var (minhaDupla, oppDupla) = LocalizarDuplas(p, jogadorId);
            if (minhaDupla == null || oppDupla == null) continue;

            bool venci = p.VencedorId == minhaDupla.Id;

            foreach (var oponente in new[] { oppDupla.Jogador1, oppDupla.Jogador2 })
            {
                if (oponente == null || oponente.Id == jogadorId) continue;

                if (!acc.TryGetValue(oponente.Id, out var resumo))
                {
                    resumo = new ConfrontoResumoVM { Oponente = oponente };
                    acc[oponente.Id] = resumo;
                }
                resumo.Jogos += 1;
                if (venci) resumo.Vitorias += 1; else resumo.Derrotas += 1;
            }
        }

        return acc.Values
            .OrderByDescending(c => c.Jogos)
            .ThenByDescending(c => c.Vitorias)
            .ToList();
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

        return new ResumoJogadorVM
        {
            TotalTorneios = fases.Count,
            Titulos = fases.Count(f => f == "Campeao"),
            Finais = fases.Count(f => f == "Final"),
            Semis = fases.Count(f => f == "Semifinal"),
            Quartas = fases.Count(f => f == "Quartas de Final"),
            CaiuNaChave = fases.Count(f => f == "Quartas de Final" || f == "Semifinal" || f == "Final")
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

    private static string NomesDupla(Dupla d)
    {
        var n1 = d.Jogador1?.Nome ?? "?";
        var n2 = d.Jogador2?.Nome ?? "?";
        return $"{n1} / {n2}";
    }
}
