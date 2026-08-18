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

    // Só o que um admin liberou, uma a uma. O que o jogador escreve nasce invisível.
    public List<DepoimentoVM> Depoimentos { get; set; } = new();

    // ----- só pra quem está logado -----

    public string? PrimeiroNome { get; set; }
    public OnboardingVM? Onboarding { get; set; }

    // Quem se cadastrou antes de 08/08/2026 não tem o campo SEXO, que é o que decide a
    // inscrição na Mista e na Casais. Enquanto estiver vazio, a Home convida a preencher —
    // e o convite some sozinho depois, porque aviso sem fim vira paisagem.
    public bool FaltaInformarSexo { get; set; }

    // AVISOS NÃO LIDOS, na primeira tela. O sino é o caminho de sempre, mas ele mora na
    // barra — e o que chegou só aparece depois de um toque. Quem entra no app e não toca em
    // nada não descobre que a chave do torneio saiu.
    //
    // Vazio quando não há nada não lido: um card "nenhum aviso novo" em toda visita vira
    // paisagem e empurra pra baixo o que a pessoa veio ver.
    public List<AvisoDoJogador> AvisosNovos { get; set; } = new();

    // Quantos existem ao todo — a lista mostra só os primeiros.
    public int TotalAvisosNovos { get; set; }

    // A informação mais valiosa no dia de torneio: hora, quadra e adversário.
    public ProximoJogoVM? ProximoJogo { get; set; }

    public List<CompromissoVM> Compromissos { get; set; } = new();
    public List<MeuTorneioVM> MeusTorneios { get; set; } = new();

    // Jogo fixo das panelinhas dentro dos próximos 7 dias. Fica na Home porque confirmar
    // presença é a ação que tem HORA pra acontecer — quem só entra pra ver o ranking do
    // grupo descobria o jogo tarde demais.
    public List<JogoDaSemanaVM> JogosDaSemana { get; set; } = new();

    // Painéis por papel. A mesma pessoa pode ser jogador, professor, organizador e dono de
    // clube ao mesmo tempo — quem acumula papéis vê os painéis empilhados, na ordem do que
    // pede ação mais urgente. Null = não exerce aquele papel.
    public PainelProfessorHomeVM? Professor { get; set; }
    public PainelOrganizadorHomeVM? Organizador { get; set; }
    public PainelClubeHomeVM? Clube { get; set; }

    public bool TemAlgumPapel => Professor != null || Organizador != null || Clube != null;
}

// Opinião publicada na vitrine. Vai só o primeiro nome: quem escreveu topou falar do site,
// não expor o nome inteiro na página inicial.
public class DepoimentoVM
{
    public string PrimeiroNome { get; set; } = "";
    public string? Cidade { get; set; }
    public int? Nota { get; set; }
    public string Texto { get; set; } = "";

    // Quem escreveu pela enquete de fim de torneio pode ter pedido pra não assinar. ⚠️ Some
    // com o nome E com a cidade: numa base pequena, "Lucas · Santa Maria" identifica tanto
    // quanto o nome inteiro.
    public bool Anonimo { get; set; }
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

    // Texto pronto ("10 inscritos", "8 duplas", "3 times"), e não um número solto: cada
    // formato inscreve uma UNIDADE diferente, e quem sabe disso é Services/QuantosInscritos.
    // A conta caseira que morava aqui somava linhas de `Duplas` com linhas de
    // `InscricoesAmericanas` e anunciava 30 inscritos num Americano de 10.
    public string Inscritos { get; set; } = "";
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

// Jogo fixo de uma panelinha na semana corrente. A sessão é criada sob demanda (só quando
// alguém abre a tela da semana), então aqui ela pode ainda não existir — daí SessaoId nulo,
// status "Pendente" e contagem zerada: o atalho leva pra tela, que cria na hora.
public class JogoDaSemanaVM
{
    public int GrupoId { get; set; }
    public string Grupo { get; set; } = "";
    public DateTime DataHora { get; set; }
    public string? Clube { get; set; }
    public string MeuStatus { get; set; } = "Pendente"; // Pendente / Confirmado / NaoVai
    public int Confirmados { get; set; }
    public int Vagas { get; set; }
    public bool Convidado { get; set; } // não é membro da panelinha, foi chamado pra este jogo

    public bool Respondi => MeuStatus != "Pendente";
}

public class MeuTorneioVM
{
    public Torneio Torneio { get; set; } = null!;
    public string Categoria { get; set; } = "";
    public bool ListaDeEspera { get; set; }
}
