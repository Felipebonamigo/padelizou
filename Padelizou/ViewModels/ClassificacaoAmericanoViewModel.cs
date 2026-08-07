namespace Padelizou.ViewModels;

// Classificação individual do Torneio Americano — soma de games por jogador em todas as rodadas
// (não por dupla, já que o parceiro muda a cada rodada).
public class ClassificacaoAmericanoItemVM
{
    public Padelizou.Models.Jogador Jogador { get; set; } = null!;
    public int TotalGames { get; set; }
}

// Uma tabela: a de um grupo, a do grupo final, ou a do torneio inteiro quando não há divisão.
//
// São várias porque cada grupo tem a SUA classificação — somar o torneio todo compararia
// gente que nunca se enfrentou, e o corte de quem passa sairia dessa soma errada.
public class ClassificacaoDeGrupoVM
{
    // Null = torneio sem divisão em grupos; a tabela não precisa de título.
    public string? Titulo { get; set; }

    // Quantos desta tabela vão pro grupo final. Zero quando não há corte.
    public int PassamDaqui { get; set; }

    // É a tabela que decide o campeão do torneio.
    public bool DecideOTitulo { get; set; }

    public List<ClassificacaoAmericanoItemVM> Linhas { get; set; } = new();
}
