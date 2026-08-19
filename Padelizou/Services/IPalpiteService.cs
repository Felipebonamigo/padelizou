using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IPalpiteService
{
    Task<Dictionary<int, PalpiteResumoVM>> ObterResumosAsync(IEnumerable<int> partidaIds, int? jogadorId);
    // O placar é OPCIONAL: `placarLado1`/`placarLado2` nulos = palpite só de vencedor, que é
    // como o palpitrômetro sempre funcionou e continua funcionando. Os dois vêm na orientação
    // do jogo (lado 1 = Dupla1), e a moeda — games ou sets — quem decide é o formato da fase.
    Task<PalpiteResumoVM> RegistrarVotoAsync(int partidaId, int jogadorId, int duplaEscolhidaId,
        int? placarLado1 = null, int? placarLado2 = null);
    Task<VotantesPartidaVM> ObterVotantesAsync(int partidaId);
}
