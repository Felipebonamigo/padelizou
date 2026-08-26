using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Services;

public class PalpiteService : IPalpiteService
{
    private readonly DbPadelContext _context;

    public PalpiteService(DbPadelContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<int, PalpiteResumoVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId)
    {
        var ids = partidaIds.ToList();
        if (ids.Count == 0) return new Dictionary<int, PalpiteResumoVM>();

        var partidas = await _context.Partidas
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Dupla1Id, p.Dupla2Id })
            .ToListAsync();

        var votos = await _context.PalpitesPartida
            .Where(v => ids.Contains(v.PartidaId))
            .Select(v => new { v.PartidaId, v.JogadorId, v.DuplaEscolhidaId })
            .ToListAsync();

        var resultado = new Dictionary<int, PalpiteResumoVM>();
        foreach (var p in partidas)
        {
            var votosDaPartida = votos.Where(v => v.PartidaId == p.Id).ToList();
            resultado[p.Id] = new PalpiteResumoVM
            {
                PartidaId = p.Id,
                VotosDupla1 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla1Id),
                VotosDupla2 = votosDaPartida.Count(v => v.DuplaEscolhidaId == p.Dupla2Id),
                MeuVotoDuplaId = jogadorId.HasValue
                    ? votosDaPartida.FirstOrDefault(v => v.JogadorId == jogadorId.Value)?.DuplaEscolhidaId
                    : null
            };
        }
        return resultado;
    }

    public async Task<PalpiteResumoVM> RegistrarVotoAsync(int partidaId, int jogadorId, int duplaEscolhidaId)
    {
        var partida = await _context.Partidas.FindAsync(partidaId);
        if (partida == null) throw new InvalidOperationException("Partida não encontrada.");
        if (partida.Status != "Agendada") throw new InvalidOperationException("Esta partida já começou — não é mais possível palpitar.");
        if (duplaEscolhidaId != partida.Dupla1Id && duplaEscolhidaId != partida.Dupla2Id)
            throw new InvalidOperationException("Dupla inválida para esta partida.");

        var voto = await _context.PalpitesPartida
            .FirstOrDefaultAsync(v => v.PartidaId == partidaId && v.JogadorId == jogadorId);

        if (voto == null)
        {
            voto = new PalpitePartida { PartidaId = partidaId, JogadorId = jogadorId, DuplaEscolhidaId = duplaEscolhidaId };
            _context.PalpitesPartida.Add(voto);
        }
        else
        {
            voto.DuplaEscolhidaId = duplaEscolhidaId;
            voto.DataHora = DateTime.Now;
        }
        await _context.SaveChangesAsync();

        var resumos = await ObterResumosAsync(new[] { partidaId }, jogadorId);
        return resumos[partidaId];
    }

    public async Task<VotantesPartidaVM> ObterVotantesAsync(int partidaId)
    {
        var partida = await _context.Partidas.FindAsync(partidaId);
        if (partida == null) throw new InvalidOperationException("Partida não encontrada.");

        var votos = await _context.PalpitesPartida
            .Include(v => v.Jogador)
            .Where(v => v.PartidaId == partidaId)
            .ToListAsync();

        return new VotantesPartidaVM
        {
            VotantesDupla1 = votos.Where(v => v.DuplaEscolhidaId == partida.Dupla1Id)
                .Select(v => new VotanteVM { Nome = v.Jogador.Nome, FotoPerfil = v.Jogador.FotoPerfil }).ToList(),
            VotantesDupla2 = votos.Where(v => v.DuplaEscolhidaId == partida.Dupla2Id)
                .Select(v => new VotanteVM { Nome = v.Jogador.Nome, FotoPerfil = v.Jogador.FotoPerfil }).ToList()
        };
    }

    // ── O RANKING DE PALPITEIROS (régua completa em Services/PontosDoPalpite) ─────────────

    public async Task<List<PalpiteiroVM>> ObterRankingDoTorneioAsync(int torneioId)
    {
        var votos = await VotosPassiveisDePonto()
            .Where(v => v.Partida.TorneioId == torneioId)
            .ToListAsync();

        return await MontarRankingAsync(votos);
    }

    public async Task<List<PalpiteiroVM>> ObterRankingGeralAsync(HashSet<int>? jogadoresDoLocal = null)
    {
        var query = VotosPassiveisDePonto();
        if (jogadoresDoLocal != null)
            query = query.Where(v => jogadoresDoLocal.Contains(v.JogadorId));

        var votos = await query.ToListAsync();
        return await MontarRankingAsync(votos);
    }

    public async Task<DesempenhoDoPalpiteiroVM> ObterDesempenhoAsync(int jogadorId)
    {
        var votos = await VotosDoJogador(jogadorId).ToListAsync();
        var validos = votos.Where(VotoValePonto).ToList();
        var resolvidos = validos.Where(EhDePartidaResolvida).ToList();
        var acertos = resolvidos.Count(v => v.DuplaEscolhidaId == v.Partida.VencedorId);

        return new DesempenhoDoPalpiteiroVM
        {
            Acertos = acertos,
            PalpitesResolvidos = resolvidos.Count,
            PalpitesEmAberto = validos.Count - resolvidos.Count,
            Aproveitamento = PontosDoPalpite.Aproveitamento(acertos, resolvidos.Count),
            TemHistorico = validos.Count > 0,
        };
    }

    // Palpite em partida que já terminou — o único que pode virar ponto ou erro. Traz as
    // duplas junto: é o que diz se o jogador estava em quadra na própria partida.
    // Expressão inline, e não uma chamada a EhDePartidaResolvida: o EF Core traduz comparação
    // de propriedade pra SQL, mas não sabe traduzir uma chamada de método C# — cai como
    // "could not be translated" só na hora de rodar, o teste em memória (InMemory) não pega.
    private IQueryable<PalpitePartida> VotosPassiveisDePonto() =>
        VotosDoJogador(null).Where(v => v.Partida.Status == "Finalizada" && v.Partida.VencedorId != null);

    private IQueryable<PalpitePartida> VotosDoJogador(int? jogadorId) =>
        _context.PalpitesPartida
            .Include(v => v.Partida).ThenInclude(p => p.Dupla1)
            .Include(v => v.Partida).ThenInclude(p => p.Dupla2)
            .Where(v => jogadorId == null || v.JogadorId == jogadorId);

    private static bool EhDePartidaResolvida(PalpitePartida voto) =>
        voto.Partida.Status == "Finalizada" && voto.Partida.VencedorId != null;

    // Os quatro em quadra são os únicos que podem mudar o resultado do próprio palpite — ver
    // PontosDoPalpite.JogaAPartida. Em categoria de TIMES ninguém é excluído: o Jogador1 da
    // dupla é o organizador que cadastrou, não quem joga (Dupla.NomeTime).
    private static bool VotoValePonto(PalpitePartida voto)
    {
        var partida = voto.Partida;
        if (partida.Dupla1.NomeTime != null || partida.Dupla2.NomeTime != null) return true;

        var jogadoresEmQuadra = new[]
        {
            partida.Dupla1.Jogador1Id, partida.Dupla1.Jogador2Id,
            partida.Dupla2.Jogador1Id, partida.Dupla2.Jogador2Id,
        };
        return !PontosDoPalpite.JogaAPartida(voto.JogadorId, jogadoresEmQuadra);
    }

    // Empate de acertos desempata por aproveitamento (quem errou menos palpitando igual ou
    // menos); empate total cai em JogadorId — ordem determinística, sem depender da ordem em
    // que o banco devolveu as linhas.
    private async Task<List<PalpiteiroVM>> MontarRankingAsync(List<PalpitePartida> votosResolvidos)
    {
        var validos = votosResolvidos.Where(VotoValePonto).ToList();

        var porJogador = validos
            .GroupBy(v => v.JogadorId)
            .Select(g => new
            {
                JogadorId = g.Key,
                Acertos = g.Count(v => v.DuplaEscolhidaId == v.Partida.VencedorId),
                Resolvidos = g.Count(),
            })
            .ToList();

        var jogadorIds = porJogador.Select(p => p.JogadorId).ToList();
        var jogadores = await _context.Jogadores
            .AsNoTracking()
            .Where(j => jogadorIds.Contains(j.Id))
            .Select(j => new { j.Id, j.Nome, j.FotoPerfil })
            .ToDictionaryAsync(j => j.Id);

        return porJogador
            .Select(p =>
            {
                var jogador = jogadores[p.JogadorId];
                return new PalpiteiroVM
                {
                    JogadorId = p.JogadorId,
                    Nome = jogador.Nome,
                    FotoPerfil = jogador.FotoPerfil,
                    Acertos = p.Acertos,
                    PalpitesResolvidos = p.Resolvidos,
                    Aproveitamento = PontosDoPalpite.Aproveitamento(p.Acertos, p.Resolvidos),
                };
            })
            .OrderByDescending(l => l.Acertos)
            .ThenByDescending(l => l.Aproveitamento)
            .ThenBy(l => l.JogadorId)
            .ToList();
    }
}
