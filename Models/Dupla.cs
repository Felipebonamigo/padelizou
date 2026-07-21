using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Dupla")]
public partial class Dupla
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }

    public int Jogador1Id { get; set; }

    public int Jogador2Id { get; set; }

    public string? Codigo { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;

    public virtual Jogador Jogador1 { get; set; } = null!;

    public virtual Jogador Jogador2 { get; set; } = null!;

    public virtual ICollection<Partida> PartidasDupla1 { get; set; } = new List<Partida>();

    public virtual ICollection<Partida> PartidasDupla2 { get; set; } = new List<Partida>(); 
    public bool ImpedimentoSextaNoite { get; set; }
    public bool ImpedimentoSabadoManha { get; set; }
    public bool ImpedimentoSabadoTarde { get; set; }
    public int? GrupoTorneioId { get; set; }
    public virtual GrupoTorneio? GrupoTorneio { get; set; }

    [NotMapped]
    public int Jogos { get; set; }

    [NotMapped]
    public int Vitorias { get; set; }

    [NotMapped]
    public int Derrotas { get; set; }

    [NotMapped]
    public int SaldoGames { get; set; }
    public string UltimaFase { get; set; } = "Grupos";
    public string? Grupo { get; set; } // Vai receber "A", "B", etc.
}
