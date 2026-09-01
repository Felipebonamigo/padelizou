using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.ViewModels;

// Página pública do professor: é a peça de venda. Quem cai aqui ainda não é aluno.
public class ProfessorPublicoVM
{
    public Jogador Professor { get; set; } = null!;

    public List<LocalAula> Locais { get; set; } = new();
    public List<string> Cidades { get; set; } = new();

    // Padel/Tênis/Beach Tênis que o professor marcou que dá (Models/ProfessorEsporte). Vazio
    // pra quem nunca configurou — o selo só aparece depois que ele salvar pelo menos um.
    public List<string> EsportesQueEnsina { get; set; } = new();

    public decimal? MenorPreco { get; set; }
    public int AulasRealizadas { get; set; }
    public int AlunosAtendidos { get; set; }

    public double? MediaNota { get; set; }
    public int TotalAvaliacoes { get; set; }

    // Já vem filtrado: vazio quando o professor desligou a exibição de comentários.
    public List<AvaliacaoProfessor> Depoimentos { get; set; } = new();
    public bool DepoimentosHabilitados { get; set; } = true;

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

    public decimal Recebido { get; set; }        // o dinheiro que ENTROU no período (Aula.PagaEm)
    public decimal Previsto { get; set; }        // confirmadas ainda por acontecer
    public decimal AReceber { get; set; }        // gerou cobrança e ainda não foi paga
    public decimal PerdidoComFaltas { get; set; } // faltas que o professor NÃO cobrou

    public int AulasRealizadas { get; set; }
    public int AulasCanceladas { get; set; }
    public int Faltas { get; set; }

    public List<DevedorVM> Devedores { get; set; } = new();
    public List<FinanceiroPorLocalVM> PorLocal { get; set; } = new();
    public List<MesFaturamentoVM> UltimosMeses { get; set; } = new();
    public List<SemanaFaturamentoVM> UltimasSemanas { get; set; } = new();
    public List<AnoFaturamentoVM> UltimosAnos { get; set; } = new();

    public bool TemMovimento => Recebido > 0 || Previsto > 0 || AReceber > 0;
}

public class DevedorVM
{
    public string Nome { get; set; } = "";
    public string? Celular { get; set; }
    public int AulasEmAberto { get; set; }
    public decimal Valor { get; set; }
    public DateTime AulaMaisAntiga { get; set; }

    // As aulas em aberto deste aluno, pro botão "Recebi" dar baixa exatamente nelas.
    public List<int> AulaIds { get; set; } = new();

    // AS MESMAS aulas, agora com o que a cobrança detalhada do WhatsApp precisa escrever
    // (pedido do Felipe em 01/09/2026). Anda junto de `AulaIds`: uma lista para dar baixa,
    // outra para explicar ao aluno o que está sendo cobrado — se as duas divergirem, o
    // professor cobra uma coisa e baixa outra.
    public List<AulaEmAbertoVM> Aulas { get; set; } = new();

    public int DiasEmAberto => (int)(DateTime.Today - AulaMaisAntiga.Date).TotalDays;
}

// Uma linha da cobrança detalhada. Guarda o `Status` cru, e não um "é falta?" já decidido,
// porque quem escolhe como cada caso é ESCRITO pro aluno é o texto (Services/CobrancaDasAulasEmAberto)
// — a tela só entrega o fato.
public class AulaEmAbertoVM
{
    public DateTime DataHora { get; set; }
    public decimal Preco { get; set; }
    public string Status { get; set; } = "";
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

// Uma barra da visão semanal do Financeiro. A semana vai de segunda a domingo.
public class SemanaFaturamentoVM
{
    public DateTime Inicio { get; set; }
    public decimal Valor { get; set; }
    public int Aulas { get; set; }

    public DateTime Fim => Inicio.AddDays(6);
    public string Rotulo => $"{Inicio:dd/MM}–{Fim:dd/MM}";
}

// Uma barra da visão anual do Financeiro — pra quem dá aula há vários anos ver a tendência
// que nem semana nem mês mostram sozinhos.
public class AnoFaturamentoVM
{
    public int Ano { get; set; }
    public decimal Valor { get; set; }
    public int Aulas { get; set; }

    public string Rotulo => Ano.ToString();
}
