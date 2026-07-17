using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Categoria")]
public partial class Categoria
{
    public int Id { get; set; }

    public int TorneioId { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public virtual ICollection<Dupla> Duplas { get; set; } = new List<Dupla>();

    public virtual ICollection<Partida> Partidas { get; set; } = new List<Partida>();
    public virtual ICollection<GrupoTorneio> GruposTorneio { get; set; } = new List<GrupoTorneio>();

    public virtual Torneio Torneio { get; set; } = null!;
}
