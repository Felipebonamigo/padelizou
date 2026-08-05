using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Um item da régua técnica DE UM PROFESSOR: "Bandeja (contenção centro)", no módulo
// Intermediário, família Bandeja.
//
// O catálogo é por professor, não do sistema: cada um recebe a lista padrão
// (Services/CatalogoDeFundamentos, tirada da planilha de um professor de verdade) na primeira
// vez que abre a tela e dali em diante edita a dele — acrescenta, renomeia, tira de vista.
// Decisão do Felipe em 05/08/2026: "coloca essa lista para todos professores, e eles poderão
// editar, adicionar e remover".
[Table("FundamentoDoProfessor")]
public class FundamentoDoProfessor
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }

    public string Modulo { get; set; } = null!;

    // Vazio quando o módulo não agrupa (Iniciante e Avançado são listas corridas).
    public string Familia { get; set; } = "";

    public string Nome { get; set; } = null!;

    // A ordem que o professor vê. Sai da planilha na semeadura e ele reordena depois.
    public int Ordem { get; set; }

    // Tirar da lista NÃO apaga: nota já dada numa ficha antiga continua existindo e precisa
    // continuar legível. Desativado some das fichas NOVAS e permanece nas antigas — apagar
    // de verdade abriria buraco no histórico do aluno, que é justamente o que a feature
    // existe pra mostrar.
    public bool Ativo { get; set; } = true;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;
}
