using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// "Quero jogar com você": o chamado que um jogador manda pra quem está inscrito SEM PARCEIRO.
// É só o recado — quem fecha a dupla é o dono da inscrição, pelo convite por link que já
// existe. Guardado em tabela pra UM chamado por pessoa por inscrição (o índice único é o
// anti-spam) e pra medir se o mural forma dupla de verdade.
[Table("ChamadoDoMural")]
public class ChamadoDoMural
{
    public int Id { get; set; }

    public int DuplaId { get; set; }
    public int CandidatoId { get; set; }

    public DateTime CriadoEm { get; set; }

    [ForeignKey("DuplaId")]
    public virtual Dupla Dupla { get; set; } = null!;

    [ForeignKey("CandidatoId")]
    public virtual Jogador Candidato { get; set; } = null!;
}
