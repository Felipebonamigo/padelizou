using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.ViewModels;

// Página pública do professor: é a peça de venda. Quem cai aqui ainda não é aluno.
public class ProfessorPublicoVM
{
    public Jogador Professor { get; set; } = null!;

    public List<LocalAula> Locais { get; set; } = new();
    public List<string> Cidades { get; set; } = new();

    public decimal? MenorPreco { get; set; }
    public int AulasRealizadas { get; set; }
    public int AlunosAtendidos { get; set; }

    public double? MediaNota { get; set; }
    public int TotalAvaliacoes { get; set; }
    public List<AvaliacaoProfessor> Depoimentos { get; set; } = new();

    // Distribuição de notas (5 → 1), pras barrinhas.
    public Dictionary<int, int> NotasPorEstrela { get; set; } = new();

    public string PoliticaCancelamento { get; set; } = "";

    // Preenchidos só quando há alguém logado olhando.
    public bool PodeAvaliar { get; set; }
    public AvaliacaoProfessor? MinhaAvaliacao { get; set; }
    public bool EhOProprioProfessor { get; set; }
}

// Visão financeira do professor: o que entrou, o que falta entrar, quem está devendo.
public class FinanceiroProfessorVM
{
    public string Periodo { get; set; } = "mes";
    public string PeriodoRotulo { get; set; } = "";

    public decimal Recebido { get; set; }        // aulas realizadas no período
    public decimal Previsto { get; set; }        // confirmadas ainda por acontecer
    public decimal AReceber { get; set; }        // realizadas/faltas cobráveis não quitadas
    public decimal PerdidoComFaltas { get; set; } // faltas que o professor NÃO cobrou

    public int AulasRealizadas { get; set; }
    public int AulasCanceladas { get; set; }
    public int Faltas { get; set; }

    public List<DevedorVM> Devedores { get; set; } = new();
    public List<FinanceiroPorLocalVM> PorLocal { get; set; } = new();
    public List<MesFaturamentoVM> UltimosMeses { get; set; } = new();

    public bool TemMovimento => Recebido > 0 || Previsto > 0 || AReceber > 0;
}

public class DevedorVM
{
    public string Nome { get; set; } = "";
    public string? Celular { get; set; }
    public int AulasEmAberto { get; set; }
    public decimal Valor { get; set; }
    public DateTime AulaMaisAntiga { get; set; }

    public int DiasEmAberto => (int)(DateTime.Today - AulaMaisAntiga.Date).TotalDays;
}

public class FinanceiroPorLocalVM
{
    public string Local { get; set; } = "";
    public int Aulas { get; set; }
    public decimal Recebido { get; set; }
    public decimal? Custo { get; set; }

    public decimal? Liquido => Custo.HasValue ? Recebido - Custo.Value : null;
}

public class MesFaturamentoVM
{
    public DateTime Mes { get; set; }
    public decimal Valor { get; set; }
    public int Aulas { get; set; }

    public string Rotulo => Mes.ToString("MMM/yy");
}
