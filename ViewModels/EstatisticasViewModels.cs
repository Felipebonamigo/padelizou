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

// Badge/conquista calculada on-the-fly (não persistida no banco)
public class ConquistaVM
{
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Icone { get; set; } = "";
    public bool Conquistada { get; set; }
}
