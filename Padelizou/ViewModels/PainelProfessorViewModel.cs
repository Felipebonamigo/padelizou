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

    // Como este aluno é identificado: por conta (AlunoId) ou pelo nome que o professor
    // anotou. É o que o formulário de preço combinado devolve pro servidor — ver
    // Services/PrecoDaAula.Chave, que junta os dois casos numa chave só.
    public int? AlunoId { get; set; }
    public string? NomeAvulso { get; set; }

    // O valor combinado com ele, quando existe. Nulo = paga a tabela do local.
    public decimal? PrecoCombinado { get; set; }
    public int? PrecoCombinadoId { get; set; }
}
