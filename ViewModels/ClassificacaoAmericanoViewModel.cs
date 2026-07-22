namespace Padelizou.ViewModels;

// Classificação individual do Torneio Americano — soma de games por jogador em todas as rodadas
// (não por dupla, já que o parceiro muda a cada rodada).
public class ClassificacaoAmericanoItemVM
{
    public Padelizou.Models.Jogador Jogador { get; set; } = null!;
    public int TotalGames { get; set; }
}
