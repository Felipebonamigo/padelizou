using Padelizou.Models;

namespace Padelizou.ViewModels;

// A home tem dois públicos com necessidades opostas. Visitante: o mapa da plataforma
// (portas de entrada, números, cadastro). Logado: "hoje no SEU padel" — próximo jogo,
// compromissos e torneios dele. O mapa ele já conhece: é o menu.
public class HomeVM
{
    public List<Torneio> Abertos { get; set; } = new();
    public List<Torneio> EmAndamento { get; set; } = new();

    public int TotalJogadores { get; set; }
    public int TorneiosRealizados { get; set; }
    public int JogosDisputados { get; set; }

    // ----- só pra quem está logado -----

    public string? PrimeiroNome { get; set; }
    public OnboardingVM? Onboarding { get; set; }

    // A informação mais valiosa no dia de torneio: hora, quadra e adversário.
    public ProximoJogoVM? ProximoJogo { get; set; }

    public List<CompromissoVM> Compromissos { get; set; } = new();
    public List<MeuTorneioVM> MeusTorneios { get; set; } = new();
}

// Partida de torneio já agendada (HorarioPrevisto definido) do jogador logado.
public class ProximoJogoVM
{
    public int TorneioId { get; set; }
    public string Torneio { get; set; } = "";
    public string Fase { get; set; } = "";
    public string Categoria { get; set; } = "";
    public DateTime Horario { get; set; }
    public string? Quadra { get; set; }
    public string Adversarios { get; set; } = "";
}

// Item da faixa "próximos compromissos" (aula, reserva de quadra, aula que vou dar).
public class CompromissoVM
{
    public DateTime Data { get; set; }
    public string Titulo { get; set; } = "";
    public string Subtitulo { get; set; } = "";
    public string Icone { get; set; } = "bi-calendar-event";
    public string Controller { get; set; } = "";
    public string Action { get; set; } = "";
    public int? RotaId { get; set; }
}

public class MeuTorneioVM
{
    public Torneio Torneio { get; set; } = null!;
    public string Categoria { get; set; } = "";
    public bool ListaDeEspera { get; set; }
}
