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

// Uma linha do ranking de palpiteiros — ver Services/PontosDoPalpite pra régua.
public class PalpiteiroVM
{
    public int JogadorId { get; set; }
    public string Nome { get; set; } = null!;
    public string? FotoPerfil { get; set; }
    public int Acertos { get; set; }
    public int PalpitesResolvidos { get; set; }
    public double? Aproveitamento { get; set; }
}

// O desempenho de UM jogador no Palpitrômetro — pro perfil dele, não pro ranking (por isso
// sem Nome/FotoPerfil: quem chama já sabe de quem é).
public class DesempenhoDoPalpiteiroVM
{
    public int Acertos { get; set; }
    public int PalpitesResolvidos { get; set; }
    public int PalpitesEmAberto { get; set; }
    public double? Aproveitamento { get; set; }

    // Falso só quando o jogador nunca votou em nada que conta — distingue "nunca palpitou"
    // de "palpitou e ainda não resolveu nenhum", que teriam Aproveitamento nulo do mesmo jeito.
    public bool TemHistorico { get; set; }
}
