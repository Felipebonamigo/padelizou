using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IPalpiteService
{
    Task<Dictionary<int, PalpiteResumoVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId);
    Task<PalpiteResumoVM> RegistrarVotoAsync(int partidaId, int jogadorId, int duplaEscolhidaId);
    Task<VotantesPartidaVM> ObterVotantesAsync(int partidaId);
}
