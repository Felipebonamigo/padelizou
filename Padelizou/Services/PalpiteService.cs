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
}
