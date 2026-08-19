namespace Padelizou.ViewModels;

// Resumo de votos do palpitrômetro de uma partida.
public class PalpiteResumoVM
{
    public int PartidaId { get; set; }
    public int VotosDupla1 { get; set; }
    public int VotosDupla2 { get; set; }
    public int TotalVotos => VotosDupla1 + VotosDupla2;
    public double PercentualDupla1 => TotalVotos == 0 ? 0 : Math.Round(VotosDupla1 * 100.0 / TotalVotos, 1);
    public double PercentualDupla2 => TotalVotos == 0 ? 0 : Math.Round(VotosDupla2 * 100.0 / TotalVotos, 1);
    public int? MeuVotoDuplaId { get; set; }

    // O placar que EU palpitei, na orientação do jogo (lado 1 = Dupla1). Nulo nos dois = não
    // palpitei placar, que é o palpite de sempre e continua valendo.
    public int? MeuPlacarLado1 { get; set; }
    public int? MeuPlacarLado2 { get; set; }

    // A moeda do palpite de placar deste jogo: sets (2+ sets no formato) ou games. Vem daqui
    // pronta pra tela não ter que perguntar o formato de novo — e pra ela não escrever "games"
    // num jogo em que se palpita set.
    public bool PlacarEmSets { get; set; }

    public bool PalpiteiOPlacar => MeuPlacarLado1 != null && MeuPlacarLado2 != null;

    // As fichas que a tela oferece: os placares que ESTE formato admite, sempre na ordem
    // vencedor × perdedor. Vem pronta do serviço porque quem sabe o formato do jogo é ele —
    // view que pergunta formato é a segunda cópia da regra, e foi assim que o `limiteGames: 9`
    // cravado no JavaScript sobreviveu tanto tempo.
    public List<Padelizou.Services.PlacaresPossiveis.Placar> PlacaresDoFormato { get; set; } = new();

    // O placar que a galera mais crava, na orientação do jogo, e quantos palpites são.
    //
    // ⚠️ A CONTAGEM anda junto e não é enfeite: "a galera crava 6x4" com 3 de 12 palpites é
    // uma frase que promete consenso onde não há. Com o número ao lado, quem lê julga sozinho.
    public int? PlacarMaisPalpitadoLado1 { get; set; }
    public int? PlacarMaisPalpitadoLado2 { get; set; }
    public int PlacarMaisPalpitadoVotos { get; set; }
    public int PalpitesComPlacar { get; set; }

    public bool TemPlacarMaisPalpitado => PlacarMaisPalpitadoLado1 != null && PlacarMaisPalpitadoLado2 != null;

    // Quem CRAVOU o placar, depois que o jogo acabou. Vazio enquanto ele não terminou — e
    // vazio também quando ninguém acertou, que é o caso comum.
    public List<string> CravaramOPlacar { get; set; } = new();
}

// Lista de quem votou em quem, pro botão "ver quem votou" do palpitrômetro.
public class VotantesPartidaVM
{
    public List<VotanteVM> VotantesDupla1 { get; set; } = new();
    public List<VotanteVM> VotantesDupla2 { get; set; } = new();
}

public class VotanteVM
{
    public string Nome { get; set; } = null!;
    public string? FotoPerfil { get; set; }
}
