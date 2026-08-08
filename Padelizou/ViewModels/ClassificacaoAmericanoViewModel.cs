namespace Padelizou.ViewModels;

// Classificação individual do Torneio Americano — soma de games por jogador em todas as rodadas
// (não por dupla, já que o parceiro muda a cada rodada).
public class ClassificacaoAmericanoItemVM
{
    public Padelizou.Models.Jogador Jogador { get; set; } = null!;
    public int TotalGames { get; set; }
}

// A classificação de UMA categoria, pronta pra tela: as tabelas por grupo (Services/
// ClassificacaoDoAmericano) mais o que a tela precisa pra oferecer o desempate.
//
// Uma por categoria porque um Americano pode ter mais de uma, e cada uma é um torneio à parte.
public class ClassificacaoAmericanaDaCategoriaVM
{
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = "";

    public IReadOnlyList<Padelizou.Services.ClassificacaoDoAmericano.Tabela> Tabelas { get; set; } =
        new List<Padelizou.Services.ClassificacaoDoAmericano.Tabela>();

    // A tabela que responde "quem é o campeão" — nula enquanto a fase de grupos não fechar
    // num grupo final. É nela, e só nela, que empate na liderança vira partida de desempate.
    public Padelizou.Services.ClassificacaoDoAmericano.Tabela? QueDecide =>
        Padelizou.Services.ClassificacaoDoAmericano.QueDecideOTitulo(Tabelas);

    public bool TemDivisaoEmGrupos =>
        Padelizou.Services.ClassificacaoDoAmericano.TemDivisaoEmGrupos(Tabelas);

    // Empate na liderança do que DECIDE. Um empate no topo do grupo A não é empate de título:
    // ali ele só disputa ordem de classificação, e os dois passam do mesmo jeito.
    public bool EmpateNoTitulo => QueDecide?.Linhas.Any(l => l.Empatado) == true;

    // Quem ganhou o torneio — nula até ter ganhado de verdade. Ver ClassificacaoDoAmericano.Campea.
    public Padelizou.Services.TabelaDoAmericano.Linha? Campea =>
        Padelizou.Services.ClassificacaoDoAmericano.Campea(Tabelas);

    // "Campeã" numa categoria feminina, "Campeão" no resto. O cadastro não guarda o sexo do
    // jogador, e o nome da categoria é a única pista que o sistema tem — a mesma convenção
    // que decide a régua do Padelímetro, então as duas telas concordam.
    public string RotuloDeCampeao =>
        Padelizou.Services.FaixasDePadelimetro.EhFeminina(Categoria) ? "Campeã" : "Campeão";
}
