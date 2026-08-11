using Padelizou.Models;

namespace Padelizou.ViewModels;

// Linha da vitrine de times.
public class TimeResumoVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? Logo { get; set; }
    public string? Clube { get; set; }
    public int Membros { get; set; }
    public int Pontos { get; set; }
}

public class TimeDetalheVM
{
    public Time Time { get; set; } = null!;
    public List<MembroTimeVM> Membros { get; set; } = new();

    public int TotalPontos => Membros.Sum(m => m.Pontos);

    // Quem administra o time — pode ser mais de um, e pode não ser membro (um admin do
    // Padelizou consegue designar alguém antes de a pessoa escolher a camisa).
    public List<AdministradorTimeVM> Administradores { get; set; } = new();

    // Quem está vendo pode mexer na lista? (administrador do time, ou admin do Padelizou)
    public bool PossoGerenciar { get; set; }
    public bool SouAdminDoSistema { get; set; }

    // Candidatos ao cargo. Fica no ViewModel, e não num ViewBag, porque ViewBag só falha
    // em tela: o cast erra em runtime, no meio da página, pra quem tem permissão de ver.
    public List<Jogador> CandidatosAAdministrador { get; set; } = new();
}

// Linha da tela /Admin/Times — a de GESTÃO, não a vitrine. O que ela mostra é o que o admin
// precisa saber antes de apertar "Apagar": quantas pessoas perdem a camisa e quantas duplas
// de torneio perdem o escudo. Apagar um time é irreversível pela tela, e um número na frente
// do botão é a diferença entre uma decisão e um susto.
public class TimeNoAdminVM
{
    public Time Time { get; set; } = null!;

    // Só contas vivas: quem excluiu a conta pela LGPD continua com o TimeId gravado, mas não
    // é uma pessoa que "perde a camisa" — contá-la inflaria o aviso do apagar.
    public int Membros { get; set; }

    // Duplas de categoria de times que apontam pra este cadastro. Elas NÃO somem junto (o
    // vínculo é SetNull no banco): o que se perde é o escudo na tela do torneio.
    public int DuplasEmTorneios { get; set; }

    public List<AdministradorTimeVM> Administradores { get; set; } = new();
}

public class AdministradorTimeVM
{
    public Jogador Jogador { get; set; } = null!;
    public DateTime ConcedidoEm { get; set; }
    public string? ConcedidoPor { get; set; }
}

public class MembroTimeVM
{
    public Jogador Jogador { get; set; } = null!;
    public int Pontos { get; set; }
    public bool EhAdministrador { get; set; }
}
