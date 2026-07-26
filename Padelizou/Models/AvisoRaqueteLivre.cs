using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Sessão de rodízio no clube: marca-se a hora de começar, aparece um número inexato de
// gente e todo mundo se reveza na quadra, sem dupla fixa. Cada um paga um valor fixo e
// joga enquanto a sessão durar.
[Table("AvisoRaqueteLivre")]
public partial class AvisoRaqueteLivre
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public int CriadorId { get; set; }
    public DateTime DataHoraInicio { get; set; }

    // Opcional de propósito: raquete livre costuma acabar quando o pessoal cansa, não na
    // hora marcada. Obrigar um fim fazia o clube inventar um horário que ninguém cumpria.
    public DateTime? DataHoraFim { get; set; }

    // Valor fixo por pessoa pela sessão inteira — não é por hora nem por partida.
    public decimal? Preco { get; set; }
    public string? Observacoes { get; set; }

    // Vagas máximas — null = sem limite, que é o caso normal. Mesmo padrão de Categoria.LimiteDuplas.
    public int? LimiteVagas { get; set; }
    public string Status { get; set; } = "Ativo";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("CriadorId")]
    public virtual Jogador Criador { get; set; } = null!;
}
