using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using System.ComponentModel.DataAnnotations.Schema;
[Table("Partida")]
public partial class Partida
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }

    public int Dupla1Id { get; set; }

    public int Dupla2Id { get; set; }

    public string Codigo { get; set; } = null!;

    public int? SetsDupla1 { get; set; }

    public int? SetsDupla2 { get; set; }

    public int? GamesDupla1 { get; set; }

    public int? GamesDupla2 { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;

    public virtual Dupla Dupla1 { get; set; } = null!;

    public virtual Dupla Dupla2 { get; set; } = null!;
    public int? TorneioId { get; set; } = null!;
    public bool SendoTransmitida { get; set; } = false;
    public string Status { get; set; } = null!;
    public int? VencedorId { get; set; }
    public string Fase { get; set; } = "Fase de Grupos";

    public DateTime? HorarioPrevisto { get; set; }
    public DateTime? HorarioInicioReal { get; set; }
    public DateTime? HorarioFimReal { get; set; }

    [NotMapped]
    public int MinutosDecorridos
    {
        get
        {
            if (HorarioInicioReal == null) return 0;
            if (HorarioFimReal != null) return (int)(HorarioFimReal.Value - HorarioInicioReal.Value).TotalMinutes;
            return (int)(DateTime.Now - HorarioInicioReal.Value).TotalMinutes;
        }
    }
    public string? NomeQuadra { get; set; } // Ex: "Quadra Central", "Quadra 1"
    public string? LinkTransmissao { get; set; } // Ex: "https://youtube.com/live/..."

}
