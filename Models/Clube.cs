using System.Timers;

namespace Padelizou.Models;

public class Clube
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Endereco { get; set; }
    public string Contato { get; set; }

    // Dono do clube — atribuído só por um administrador do sistema (AdminController).
    public int? DonoId { get; set; }
    public virtual Jogador? Dono { get; set; }

    // Relacionamentos
    public ICollection<Torneio> Torneios { get; set; } = new List<Torneio>();
    public ICollection<Time> Times { get; set; } = new List<Time>();
}