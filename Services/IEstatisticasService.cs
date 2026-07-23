using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IEstatisticasService
{
    // Pontos de ranking por categoria conforme a fase mais longe alcançada pela dupla.
    int PontosPorFase(string? ultimaFase);

    // Leaderboards por Categoria.Nome. Se categoriaNome != null, filtra só aquela.
    Task<List<RankingCategoriaVM>> ObterRankingPorCategoriaAsync(string? categoriaNome = null);

    // Ranking de times: soma dos pontos de torneio de todos os jogadores de cada time.
    Task<List<RankingTimeVM>> ObterRankingTimesAsync();

    // Página de ranking consolidada: todas as seções (por categoria, troféus,
    // vitórias de jogador/dupla geral e por categoria, invencibilidade e nível
    // comprovado). Tudo calculado apenas a partir de resultados de torneio.
    Task<RankingHubVM> ObterRankingHubAsync();

    // Nível comprovado por jogador (categoria mais forte onde atingiu o gatilho).
    // Base da regra de elegibilidade de categoria na inscrição. O gatilho ("SaiuChave",
    // "Semifinal", "Final") é configurável pelo torneio; "Livre"/desconhecido => vazio.
    Task<Dictionary<int, NivelComprovadoVM>> ObterNiveisComprovadosAsync(string modo = "Final");

    // Para os selos das abas do torneio: por JogadorId e por tier de categoria (Diamante/Ouro/
    // Prata/Bronze/Ferro/Madeira/Plástico), melhor fase histórica + títulos (opcionalmente
    // excluindo o torneio atual).
    Task<Dictionary<int, Dictionary<string, HistoricoCategoriaVM>>> ObterMelhoresColocacoesAsync(
        IEnumerable<string> categoriaNomes, int? excluirTorneioId = null);

    // Adversários do jogador agregados (jogos, vitórias, derrotas), ordenados por mais jogos.
    Task<List<ConfrontoResumoVM>> ObterConfrontosAsync(int jogadorId);

    // Head-to-head detalhado entre dois jogadores.
    Task<HeadToHeadVM> ObterHeadToHeadAsync(int jogadorId, int oponenteId);

    // Resumo estatístico do jogador (títulos, finais, semis, quartas, caiu na chave, total).
    Task<ResumoJogadorVM> ObterResumoJogadorAsync(int jogadorId);

    // Parceiros de dupla mais frequentes ("joga sempre com"), ordenados por mais jogos juntos.
    Task<List<ParceiroResumoVM>> ObterParceirosAsync(int jogadorId);

    // Destaques de parceria/rivalidade do jogador para os cards do perfil (parceiro que mais
    // jogou junto, quem mais enfrentou, quem mais te venceu, quem você mais venceu).
    Task<DestaquesJogadorVM> ObterDestaquesAsync(int jogadorId);

    // Conquistas/badges do jogador, calculadas a partir do histórico (não persistidas).
    Task<List<ConquistaVM>> ObterConquistasAsync(int jogadorId);
}
