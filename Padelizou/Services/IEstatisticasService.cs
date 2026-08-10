using Padelizou.ViewModels;

namespace Padelizou.Services;

public interface IEstatisticasService
{
    // Pontos de ranking por categoria conforme a fase mais longe alcançada pela dupla.
    // `PontosPorFase` saiu daqui em 10/08/2026: a conta agora depende do TAMANHO da
    // categoria e mora em `Services/PontosDoTorneio` (estático, sem banco). Ver RANKING.md.

    // Leaderboards por Categoria.Nome. Se categoriaNome != null, filtra só aquela.
    // ate != null => considera só torneios com DataInicio até essa data (ranking "de então").
    // de != null => considera só torneios com DataInicio a partir dessa data (recorte de período).
    Task<List<RankingCategoriaVM>> ObterRankingPorCategoriaAsync(string? categoriaNome = null, DateTime? ate = null, HashSet<int>? jogadoresFiltro = null, DateTime? de = null);

    // Ranking de times: soma dos pontos de torneio de todos os jogadores de cada time.
    // ate != null => considera só torneios com DataInicio até essa data.
    Task<List<RankingTimeVM>> ObterRankingTimesAsync(DateTime? ate = null, HashSet<int>? jogadoresFiltro = null);

    // Filtro regional: ids dos jogadores de um estado + uma ou VÁRIAS cidades. null = sem filtro (país todo).
    Task<HashSet<int>?> ObterJogadoresDoLocalAsync(IEnumerable<string>? cidades, string? estado);

    // Estados/cidades existentes no cadastro de jogadores, para montar os selects do filtro —
    // já sem repetir grafia da mesma cidade. `somenteQuemJogouTorneio` é a régua do RANKING:
    // ali só faz sentido oferecer cidade que tem alguém com resultado de torneio.
    Task<(List<string> Estados, List<string> Cidades)> ObterLocaisDisponiveisAsync(
        string? estado, bool somenteQuemJogouTorneio = false);

    // Todas as grafias de uma cidade escolhida na tela ("Gravataí" também é "GRAVATAI"), pra
    // consulta feita fora deste serviço continuar achando todo mundo.
    Task<List<string>> ObterGrafiasDasCidadesAsync(IEnumerable<string> escolhidas);

    // Pontos que cada time somou dentro de UM torneio (pontos das duplas atribuídos ao
    // time que cada jogador representa). Ordenado por pontos.
    Task<List<PontosTimeTorneioVM>> ObterPontosTimesNoTorneioAsync(int torneioId);

    // Página de ranking consolidada: todas as seções (por categoria, troféus,
    // vitórias de jogador/dupla geral e por categoria, invencibilidade e nível
    // comprovado). Tudo calculado apenas a partir de resultados de torneio.
    // cidade/estado != null => ranking regional (padrão é o país todo).
    // periodo ("mes"/"ano"/"sempre") afeta só vitórias/invencibilidade/troféus.
    Task<RankingHubVM> ObterRankingHubAsync(IEnumerable<string>? cidades = null, string? estado = null, string? periodo = null);

    // Ranking de UM torneio, agregado por jogador (pontos/jogos/vitórias/títulos nesse torneio).
    Task<List<RankingTorneioLinhaVM>> ObterRankingDoTorneioAsync(int torneioId);

    // Nível comprovado por jogador (categoria mais forte onde atingiu o gatilho).
    // Base da regra de elegibilidade de categoria na inscrição. O gatilho ("SaiuChave",
    // "Semifinal", "Final") é configurável pelo torneio; "Livre"/desconhecido => vazio.
    Task<Dictionary<int, NivelComprovadoVM>> ObterNiveisComprovadosAsync(string modo = "Final");

    // Nível comprovado (categoria prevista) de um único jogador, para o perfil. Null se não comprovou.
    Task<NivelComprovadoVM?> ObterNivelComprovadoJogadorAsync(int jogadorId, string modo = "Final");

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

    // Pontos reais de ranking de vários jogadores numa consulta só. Existe porque
    // Jogador.PontuacaoGlobal é campo morto (sempre 0) — quem precisa de pontos usa isto.
    Task<Dictionary<int, int>> ObterPontosPorJogadorAsync(IEnumerable<int> jogadorIds);

    // Evolução do jogador mês a mês (pontos ganhos e acumulado) pros últimos N meses.
    Task<EvolucaoJogadorVM> ObterEvolucaoJogadorAsync(int jogadorId, int meses = 12);

    // Primeiros passos do jogador (completar perfil → seguir → se inscrever → instalar o app).
    Task<OnboardingVM> ObterOnboardingAsync(int jogadorId);
}
