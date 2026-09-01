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
    // As semanas do MÊS escolhido no card (pedido do Felipe em 01/09/2026) — antes era uma
    // janela rolante de 6 semanas, que atravessava a virada do mês e não somava mês nenhum.
    public List<SemanaFaturamentoVM> Semanas { get; set; } = new();

    // O mês que está na tela do card, sempre o primeiro dia dele.
    public DateTime MesDasSemanas { get; set; }

    // As setas do card. Elas param onde o dado para: não existe faturamento no futuro, e nem
    // antes da primeira aula — caminhar por meses vazios é como o professor conclui que o
    // sistema apagou as aulas dele.
    public bool PodeAvancarSemanas { get; set; }
    public bool PodeVoltarSemanas { get; set; }

    public string MesDasSemanasRotulo => Padelizou.Services.SemanasDoMes.Rotulo(MesDasSemanas);
    public string MesAnteriorChave => Padelizou.Services.SemanasDoMes.Chave(MesDasSemanas.AddMonths(-1));
    public string MesSeguinteChave => Padelizou.Services.SemanasDoMes.Chave(MesDasSemanas.AddMonths(1));
    public List<AnoFaturamentoVM> UltimosAnos { get; set; } = new();

    // ⚠️ O card de semanas entra nesta conta, e não só os cartões do topo. A tela corta TUDO
    // abaixo dos cartões quando não há movimento ("Nenhum movimento neste mês") — e o card
    // ganhou mês próprio em 01/09/2026. Sem esta linha, clicar na seta pra ver um mês que TEVE
    // movimento cairia na tela vazia do mês corrente, sumindo justamente com o card que o
    // clique pediu.
    public bool TemMovimento => Recebido > 0 || Previsto > 0 || AReceber > 0 || Semanas.Any(s => s.Valor > 0);
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

// Uma barra da visão semanal do Financeiro. A semana vai de segunda a domingo, RECORTADA nas
// pontas do mês (ver Services/SemanasDoMes) — por isso `Fim` é gravado e não calculado: a
// primeira e a última barra do mês quase nunca fecham no domingo.
public class SemanaFaturamentoVM
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public decimal Valor { get; set; }
    public int Aulas { get; set; }
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
