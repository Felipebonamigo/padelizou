using Padelizou.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("JogoAula")]
public partial class JogoAula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public int LocalAulaId { get; set; }
    public int CategoriaPadraoId { get; set; }

    // "Individual" ou "Dupla" — só descritivo, não é mecanismo de pareamento.
    public string Modalidade { get; set; } = null!;

    public DateTime DataHora { get; set; }
    public decimal? Preco { get; set; }
    public string? Observacoes { get; set; }

    // Vagas máximas — null = sem limite.
    public int? LimiteVagas { get; set; }
    public string Status { get; set; } = "Ativo";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;

    [ForeignKey("CategoriaPadraoId")]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;
}
