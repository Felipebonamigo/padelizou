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

    // ---- O cadastro dele (ver Models/CadastroDoAluno) ----
    // O celular GUARDADO, que é diferente do `Celular` lá em cima: aquele sai da última aula
    // lançada, este é o do cadastro e não muda quando o professor marca aula sem telefone.
    public string? CelularCadastrado { get; set; }

    // Quem paga por ele. Vazio = ele mesmo.
    public string? ResponsavelNome { get; set; }
    public string? ResponsavelCelular { get; set; }
    public string? ResponsavelCpf { get; set; }
    public string? Observacao { get; set; }

    // Uma conta do Padelizou com ESTE celular, pra quem ainda está como nome solto na agenda.
    // É o encontro entre o cadastro rápido do professor e a campanha de "se cadastrem
    // certinho": quando a pessoa finalmente cria conta, o professor vê o convite pra juntar
    // o histórico em vez de ficar com a mesma pessoa em dois lugares.
    public int? ContaSugeridaId { get; set; }
    public string? ContaSugeridaNome { get; set; }
}
