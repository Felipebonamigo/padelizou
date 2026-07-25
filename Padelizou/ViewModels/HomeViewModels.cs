using Padelizou.Models;

namespace Padelizou.ViewModels;

// A home deixou de ser vitrine de torneios: é o mapa da plataforma. Mostra o que está
// rolando agora, as portas de entrada e os números da comunidade.
public class HomeVM
{
    public List<Torneio> Abertos { get; set; } = new();
    public List<Torneio> EmAndamento { get; set; } = new();

    public int TotalJogadores { get; set; }
    public int TorneiosRealizados { get; set; }
    public int JogosDisputados { get; set; }
}
