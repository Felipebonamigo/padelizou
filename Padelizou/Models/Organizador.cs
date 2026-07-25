using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using System.ComponentModel.DataAnnotations.Schema;
[Table("Organizador")]
public partial class Organizador
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string SenhaHash { get; set; } = null!;

    public virtual ICollection<Torneio> Torneios { get; set; } = new List<Torneio>();
}
