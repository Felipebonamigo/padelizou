using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.ViewModels;

public class PainelProfessorViewModel
{
    public int TotalAlunosAtivos { get; set; }
    public int AulasEstaSemana { get; set; }
    public int AulasPendentes { get; set; }
    public RelatorioAulasViewModel FinanceiroMesAtual { get; set; } = null!;
    public List<AlunoResumo> Alunos { get; set; } = new();
    public List<LocalAula> Locais { get; set; } = new();
    public List<Aula> ProximasAulas { get; set; } = new();
    public List<Cidade> MinhasCidades { get; set; } = new();
}

public class AlunoResumo
{
    public string Nome { get; set; } = null!;
    public string? Celular { get; set; }
    public int TotalAulas { get; set; }
    public DateTime UltimaAula { get; set; }
    public DateTime? ProximaAula { get; set; }
}
