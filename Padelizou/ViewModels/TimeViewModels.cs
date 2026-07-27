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
