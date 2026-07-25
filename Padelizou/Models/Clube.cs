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

    // Cidade do clube — usada pra bater com JogadorCidade nas notificações de "Marcar Jogo".
    public int? CidadeId { get; set; }
    public virtual Cidade? Cidade { get; set; }

    // "Marcar Jogo" — dono/admin ativa pra permitir que o Padelizou administre a agenda de
    // quadras do clube (ver QuadraClube/HorarioMarcacaoDisponivel/MarcacaoJogo).
    public bool MarcacaoHorariosAtiva { get; set; }
    public bool NotificarHorariosDiariamente { get; set; }

    // Relacionamentos
    public ICollection<Torneio> Torneios { get; set; } = new List<Torneio>();
    public ICollection<Time> Times { get; set; } = new List<Time>();
}