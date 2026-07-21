using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IEstatisticasService
{
    // Pontos de ranking por categoria conforme a fase mais longe alcançada pela dupla.
    int PontosPorFase(string? ultimaFase);

    // Leaderboards por Categoria.Nome. Se categoriaNome != null, filtra só aquela.
    Task<List<RankingCategoriaVM>> ObterRankingPorCategoriaAsync(string? categoriaNome = null);

    // Para os selos das abas do torneio: por JogadorId, melhor fase histórica + títulos nas
    // categorias informadas (opcionalmente excluindo o torneio atual).
    Task<Dictionary<int, HistoricoCategoriaVM>> ObterMelhoresColocacoesAsync(
        IEnumerable<string> categoriaNomes, int? excluirTorneioId = null);

    // Adversários do jogador agregados (jogos, vitórias, derrotas), ordenados por mais jogos.
    Task<List<ConfrontoResumoVM>> ObterConfrontosAsync(int jogadorId);

    // Head-to-head detalhado entre dois jogadores.
    Task<HeadToHeadVM> ObterHeadToHeadAsync(int jogadorId, int oponenteId);

    // Resumo estatístico do jogador (títulos, finais, semis, quartas, caiu na chave, total).
    Task<ResumoJogadorVM> ObterResumoJogadorAsync(int jogadorId);
}
