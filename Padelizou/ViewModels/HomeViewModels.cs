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

    // Painéis por papel. A mesma pessoa pode ser jogador, professor, organizador e dono de
    // clube ao mesmo tempo — quem acumula papéis vê os painéis empilhados, na ordem do que
    // pede ação mais urgente. Null = não exerce aquele papel.
    public PainelProfessorHomeVM? Professor { get; set; }
    public PainelOrganizadorHomeVM? Organizador { get; set; }
    public PainelClubeHomeVM? Clube { get; set; }

    public bool TemAlgumPapel => Professor != null || Organizador != null || Clube != null;
}

// "Meu dia" do professor, direto na entrada.
public class PainelProfessorHomeVM
{
    public int SolicitacoesPendentes { get; set; }
    public int AulasHoje { get; set; }
    public int AulasNaSemana { get; set; }
    public decimal RecebidoNoMes { get; set; }
    public decimal AReceber { get; set; }
    public List<AulaDoDiaVM> ProximasAulas { get; set; } = new();
}

public class AulaDoDiaVM
{
    public int AulaId { get; set; }
    public DateTime DataHora { get; set; }
    public string Aluno { get; set; } = "";
    public string Local { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Preco { get; set; }
    public string? CelularAluno { get; set; }

    public bool Confirmada => Status == "Confirmada";
}

public class PainelOrganizadorHomeVM
{
    public int TorneiosAtivos { get; set; }
    public int JogosAoVivo { get; set; }
    public int InscricoesNaSemana { get; set; }
    public List<TorneioOrganizadoVM> Torneios { get; set; } = new();
}

public class TorneioOrganizadoVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Status { get; set; } = "";
    public int Inscritos { get; set; }
    public int JogosAoVivo { get; set; }
    public bool PrecisaSortear { get; set; }
}

public class PainelClubeHomeVM
{
    public int ReservasHoje { get; set; }
    public int QuadrasAtivas { get; set; }
    public List<ClubeResumoHomeVM> Clubes { get; set; } = new();
}

public class ClubeResumoHomeVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public int ReservasHoje { get; set; }
    public int Quadras { get; set; }
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
