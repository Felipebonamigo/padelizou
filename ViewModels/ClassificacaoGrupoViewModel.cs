namespace Padelizou.ViewModels;

using Padelizou.Models;

public class ClassificacaoGrupoViewModel
{
    public Dupla Dupla { get; set; } = null!;
    public string Grupo { get; set; } = string.Empty;

    public int JogosJogados { get; set; }
    public int Vitorias { get; set; }
    public int Derrotas { get; set; }

    public int GamesPro { get; set; }
    public int GamesContra { get; set; }

    // Propriedade calculada automaticamente (Desempate)
    public int SaldoGames => GamesPro - GamesContra;
}