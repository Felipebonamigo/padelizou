using Padelizou.Models;

namespace Padelizou.ViewModels;

// Ranking por categoria (agrupado por Categoria.Nome)
public class RankingCategoriaVM
{
    public string Categoria { get; set; } = null!;
    public List<RankingLinhaVM> Linhas { get; set; } = new();
}

public class RankingLinhaVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public int Titulos { get; set; }
    public int Torneios { get; set; }
    public int Finais { get; set; }
}

// Selo histórico de um jogador numa categoria (usado nas abas do torneio)
public class HistoricoCategoriaVM
{
    public string MelhorFase { get; set; } = "Grupos";
    public int Titulos { get; set; }

    // Estilo do selo, conforme o tier da categoria (ver EstatisticasService.TierDaCategoria).
    public string Tier { get; set; } = "Geral";
    public string TierNome { get; set; } = "Geral";
    public string IconeTier { get; set; } = "bi-trophy-fill";
    public string CorFundoTier { get; set; } = "#eef2f8";
    public string CorTextoTier { get; set; } = "#6c757d";
}

// Resumo de um adversário no histórico de confrontos
public class ConfrontoResumoVM
{
    public Jogador Oponente { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
}

// Head-to-head detalhado entre dois jogadores
public class HeadToHeadVM
{
    public Jogador Eu { get; set; } = null!;
    public Jogador Oponente { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
    public List<ConfrontoPartidaVM> Partidas { get; set; } = new();
}

public class ConfrontoPartidaVM
{
    public DateTime? Data { get; set; }
    public string Torneio { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Fase { get; set; } = "";
    public string MinhaDupla { get; set; } = "";
    public string DuplaOponente { get; set; } = "";
    public string Placar { get; set; } = "";
    public bool EuVenci { get; set; }
}

// Resumo estatístico do jogador (cards do perfil)
public class ResumoJogadorVM
{
    public int TotalTorneios { get; set; }
    public int Titulos { get; set; }
    public int Finais { get; set; }
    public int Semis { get; set; }
    public int Quartas { get; set; }
    public int CaiuNaChave { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }
}

// Parceiro de dupla mais frequente ("joga sempre com")
public class ParceiroResumoVM
{
    public Jogador Parceiro { get; set; } = null!;
    public int Jogos { get; set; }
    public int Vitorias { get; set; }
}

// Destaques de relacionamento do jogador (parceria/rivalidade), para os cards do perfil.
// Cada campo pode ser null se não houver dado suficiente ainda.
public class DestaquesJogadorVM
{
    public ParceiroResumoVM? MaisJogouJunto { get; set; }   // parceiro com mais jogos juntos
    public ConfrontoResumoVM? MaisEnfrentou { get; set; }   // adversário com mais confrontos
    public ConfrontoResumoVM? MaisTeVenceu { get; set; }    // algoz: mais derrotas suas pra ele
    public ConfrontoResumoVM? VoceMaisVenceu { get; set; }  // freguês: mais vitórias suas sobre ele
}

// Badge/conquista calculada on-the-fly (não persistida no banco)
public class ConquistaVM
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Icone { get; set; } = "";
    public bool Conquistada { get; set; }
}

// ---------------------------------------------------------------------------
// Página de Ranking consolidada ("hub"). TUDO aqui é calculado apenas a partir
// de resultados de torneio (Dupla.UltimaFase, Partida.VencedorId/sets/games e
// Categoria.Nome). Nada de jogo semanal, aula ou pontuação manual entra.
// ---------------------------------------------------------------------------
public class RankingHubVM
{
    public List<RankingCategoriaVM> PorCategoria { get; set; } = new();            // 1. ranking por categoria (pontos)
    public List<RankingCategoriaVM> TrofeusPorCategoria { get; set; } = new();     // 2. troféus por categoria (títulos)
    public List<JogadorContagemVM> VitoriasJogadores { get; set; } = new();        // 3. jogador com mais vitórias (geral)
    public List<CategoriaJogadoresVM> VitoriasJogadoresPorCategoria { get; set; } = new(); // 4. jogador com mais vitórias por categoria
    public List<InvictoJogadorVM> InvictosJogadores { get; set; } = new();         // 5. jogador com mais jogos invicto
    public List<DuplaContagemVM> VitoriasDuplas { get; set; } = new();             // 6. dupla com mais vitórias (geral)
    public List<CategoriaDuplasVM> VitoriasDuplasPorCategoria { get; set; } = new(); // 7. dupla com mais vitórias por categoria
    public List<DuplaContagemVM> InvictasDuplas { get; set; } = new();             // 8. dupla com mais tempo invicta
    public List<NivelJogadorVM> NiveisComprovados { get; set; } = new();           // 9. nível comprovado (base p/ previsão de categoria)
    public List<RankingTimeVM> Times { get; set; } = new();                        // 10. ranking de times (soma dos pontos dos jogadores)
}

// Ranking de times: soma dos pontos de torneio de todos os jogadores do time.
public class RankingTimeVM
{
    public int TimeId { get; set; }
    public string Time { get; set; } = "";
    public string? Logo { get; set; }
    public int Pontos { get; set; }
    public int Titulos { get; set; }
    public int Jogadores { get; set; }
}

// Contagem de vitórias/jogos de um jogador (usado nos leaderboards de vitórias)
public class JogadorContagemVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Vitorias { get; set; }
    public int Jogos { get; set; }
}

// Contagem de vitórias/jogos + sequência invicta de uma dupla (inscrição por torneio)
public class DuplaContagemVM
{
    public Jogador Jogador1 { get; set; } = null!;
    public Jogador Jogador2 { get; set; } = null!;
    public string Categoria { get; set; } = "";
    public string Torneio { get; set; } = "";
    public int Vitorias { get; set; }
    public int Jogos { get; set; }
    public int SequenciaInvicta { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
}

// Maior sequência de partidas de torneio consecutivas sem perder (jogador)
public class InvictoJogadorVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Sequencia { get; set; }
    public DateTime? De { get; set; }
    public DateTime? Ate { get; set; }
}

public class CategoriaJogadoresVM
{
    public string Categoria { get; set; } = "";
    public List<JogadorContagemVM> Jogadores { get; set; } = new();
}

public class CategoriaDuplasVM
{
    public string Categoria { get; set; } = "";
    public List<DuplaContagemVM> Duplas { get; set; } = new();
}

// Nível "comprovado" de um jogador: categoria mais FORTE em que ele chegou à
// final (ou foi campeão). Base da regra anti-sandbagging de inscrição.
public class NivelComprovadoVM
{
    public string Categoria { get; set; } = "";
    public int Ordem { get; set; }          // ver EstatisticasService.OrdemCategoria (maior = mais forte)
    public string? MelhorFase { get; set; } // "Final" ou "Campeao"
}

public class NivelJogadorVM
{
    public Jogador Jogador { get; set; } = null!;
    public string Categoria { get; set; } = "";
    public int Ordem { get; set; }
    public string? MelhorFase { get; set; }
}
