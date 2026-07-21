using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Jogo casual de um grupo (não é torneio) — usado pro ranking semanal/mensal interno do grupo.
[Table("JogoSemanal")]
public partial class JogoSemanal
{
    public int Id { get; set; }
    public int GrupoId { get; set; }
    public DateTime DataJogo { get; set; }

    public int Dupla1Jogador1Id { get; set; }
    public int Dupla1Jogador2Id { get; set; }
    public int Dupla2Jogador1Id { get; set; }
    public int Dupla2Jogador2Id { get; set; }

    public int GamesDupla1 { get; set; }
    public int GamesDupla2 { get; set; }

    public int RegistradoPorId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("GrupoId")]
    public virtual padelizou.Models.GrupoPrivado Grupo { get; set; } = null!;

    [ForeignKey("Dupla1Jogador1Id")]
    public virtual Jogador Dupla1Jogador1 { get; set; } = null!;

    [ForeignKey("Dupla1Jogador2Id")]
    public virtual Jogador Dupla1Jogador2 { get; set; } = null!;

    [ForeignKey("Dupla2Jogador1Id")]
    public virtual Jogador Dupla2Jogador1 { get; set; } = null!;

    [ForeignKey("Dupla2Jogador2Id")]
    public virtual Jogador Dupla2Jogador2 { get; set; } = null!;

    [ForeignKey("RegistradoPorId")]
    public virtual Jogador RegistradoPor { get; set; } = null!;

    [NotMapped]
    public int VencedorLado => GamesDupla1 == GamesDupla2 ? 0 : (GamesDupla1 > GamesDupla2 ? 1 : 2);
}
