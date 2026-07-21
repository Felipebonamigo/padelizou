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
