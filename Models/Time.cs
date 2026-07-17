namespace Padelizou.Models;

public class Time
{
    public int Id { get; set; }
    public string Nome { get; set; } // Ex: "Nata Padel"

    public int ClubeId { get; set; }
    public Clube Clube { get; set; }

    // Lista de jogadores que compõem esse grupo/time
    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();

}