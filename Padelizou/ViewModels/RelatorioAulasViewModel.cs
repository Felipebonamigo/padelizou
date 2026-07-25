namespace Padelizou.ViewModels;

public class RelatorioAulasViewModel
{
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public int TotalAulas { get; set; }
    public int TotalAlunosDiferentes { get; set; }
    public decimal TotalRecebido { get; set; }
    public decimal? TotalGasto { get; set; }
    public List<RelatorioPorLocal> PorLocal { get; set; } = new();
}

public class RelatorioPorLocal
{
    public string NomeLocal { get; set; } = null!;
    public int QuantidadeAulas { get; set; }
    public decimal Recebido { get; set; }
    public decimal? Gasto { get; set; }
}
