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
    // Tira o palpite desta pessoa neste jogo — voto e placar, que moram na mesma linha.
    // Idempotente: quem não tinha palpite recebe o resumo do jeito que ele está.
    Task<PalpiteResumoVM> RetirarPalpiteAsync(int partidaId, int jogadorId);

    // Nulo = o jogo não existe (mais). Ver a nota no PalpiteService: quem chama devolve 404.
    Task<VotantesPartidaVM?> ObterVotantesAsync(int partidaId);
}
